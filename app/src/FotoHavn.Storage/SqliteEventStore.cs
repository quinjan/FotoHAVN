using FotoHavn.Core;
using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Migrations;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage;

/// <summary>
/// The Event store: SQLite for metadata (<c>fotohavn.db</c>) and files for artifacts, under one Data folder (ADR 0005, plan
/// section 10). Every operation takes one internal lock, so calls never interleave, and does its I/O on a pool thread, so the
/// brain's executor only awaits. Environment failures surface as <see cref="StorageException"/>; contract violations as
/// <see cref="ArgumentException"/> before any I/O. Files reach their final name before their row exists and are never read
/// before the row commits; the startup reconciler repairs what a crash leaves.
/// </summary>
public sealed partial class SqliteEventStore : IEventStore
{
    public const string DatabaseFileName = "fotohavn.db";
    private const string ClosedMessage = "The store isn't open.";

    private readonly IClock _clock;
    private readonly IStorageFileSystem _fs;
    private readonly IDataPathProbe _probe;
    private readonly IStartupReconciler _reconciler;
    private readonly IPinHasher _hasher;
    private readonly ICrashPoints _crash;
    private readonly Func<DataPathFacts, DataPathVerdict> _classify;
    private readonly IReadOnlyList<MigrationScript> _migrations;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteConnection? _db;

    /// <param name="dataRoot">The Data folder (made absolute).</param>
    /// <param name="clock">Source of "now" for every timestamp.</param>
    /// <param name="fileSystem">Every file operation goes through it.</param>
    /// <param name="probe">Observes where the Data folder lives.</param>
    /// <param name="reconciler">Runs the startup repair after migrations.</param>
    /// <param name="hasher">PIN hasher; defaults to <see cref="Pbkdf2PinHasher"/>.</param>
    /// <param name="crashPoints">Called after each step of a multi-step change; defaults to <see cref="NoCrashPoints.Instance"/>.</param>
    /// <param name="classify">Turns the probe's facts into a verdict; defaults to <see cref="DataPathClassifier.Classify"/>.</param>
    /// <param name="migrations">The schema migrations; defaults to <see cref="MigrationCatalog.Embedded"/>.</param>
    public SqliteEventStore(
        string dataRoot,
        IClock clock,
        IStorageFileSystem fileSystem,
        IDataPathProbe probe,
        IStartupReconciler reconciler,
        IPinHasher? hasher = null,
        ICrashPoints? crashPoints = null,
        Func<DataPathFacts, DataPathVerdict>? classify = null,
        IReadOnlyList<MigrationScript>? migrations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(reconciler);
        DataRoot = Path.GetFullPath(dataRoot);
        _clock = clock;
        _fs = fileSystem;
        _probe = probe;
        _reconciler = reconciler;
        _hasher = hasher ?? new Pbkdf2PinHasher();
        _crash = crashPoints ?? NoCrashPoints.Instance;
        _classify = classify ?? DataPathClassifier.Classify;
        _migrations = migrations ?? MigrationCatalog.Embedded;
    }

    public string DataRoot { get; }

    public Task ProbeWritableAsync(CancellationToken ct) =>
        RunAsync(ct, _ =>
        {
            var stagingDirectory = Path.Combine(DataRoot, ".staging");
            var probePath = Path.Combine(stagingDirectory, "write-probe.tmp");
            try
            {
                _fs.CreateDirectory(stagingDirectory);
                _fs.WriteAllBytesAndFlush(probePath, "FOTOHAVN"u8);
                _fs.Delete(probePath);
            }
            catch
            {
                DeleteQuietly([probePath]);
                throw;
            }
        });

    public async Task<StorageOpenResult> OpenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            CloseConnection();
            SqliteConnection? connection = null;
            try
            {
                connection = await Task.Run(OpenDatabase, CancellationToken.None).ConfigureAwait(false);
                var version = await Task.Run(() => PrepareDatabase(connection), CancellationToken.None).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                var report = await _reconciler.RunAsync(connection, _fs, DataRoot, ct).ConfigureAwait(false);
                _db = connection;
                return new StorageOpenResult(version, report);
            }
            catch (Exception exception)
            {
                connection?.Dispose();
                if (exception is OperationCanceledException)
                {
                    throw;
                }

                var translated = StorageErrorMapper.ToStorageException(exception);
                if (ReferenceEquals(translated, exception))
                {
                    throw;
                }

                throw translated;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DrainAsync(CancellationToken.None).ConfigureAwait(false);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            CloseConnection();
        }
        finally
        {
            _gate.Release();
        }
    }

    private SqliteConnection OpenDatabase()
    {
        _fs.CreateDirectory(DataRoot);
        var verdict = _classify(_probe.Probe(DataRoot));
        if (!verdict.IsOk)
        {
            throw new StorageException(verdict.ToErrorKind(), verdict.Reason);
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(DataRoot, DatabaseFileName),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        try
        {
            connection.Open();
            _ = Db.Scalar(connection, null, "PRAGMA journal_mode = WAL;");
            Db.Exec(connection, null, "PRAGMA synchronous = FULL;");
            Db.Exec(connection, null, "PRAGMA foreign_keys = ON;");
            Db.Exec(connection, null, "PRAGMA busy_timeout = 5000;");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private int PrepareDatabase(SqliteConnection connection)
    {
        var version = MigrationRunner.Run(connection, _migrations);
        var integrity = Db.Query(connection, null, "PRAGMA integrity_check;", reader => reader.GetString(0));
        if (integrity is not ["ok"])
        {
            throw new StorageException(StorageErrorKind.IntegrityFailed, StorageErrorMapper.Describe(StorageErrorKind.IntegrityFailed));
        }

        return version;
    }

    private void CloseConnection()
    {
        _db?.Dispose();
        _db = null;
    }

#pragma warning disable CA1068 // The token leads so each member reads as "run this action under this token".
    /// <summary>Runs <paramref name="work"/> on a pool thread under the store lock, with the open connection.</summary>
    private async Task<T> RunAsync<T>(CancellationToken ct, Func<SqliteConnection, T> work)
    {
        ct.ThrowIfCancellationRequested();
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Guarded(work), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task<bool> RunAsync(CancellationToken ct, Action<SqliteConnection> work) => RunAsync(ct, db =>
    {
        work(db);
        return true;
    });
#pragma warning restore CA1068

    private T Guarded<T>(Func<SqliteConnection, T> work)
    {
        var db = _db ?? throw new InvalidOperationException(ClosedMessage);
        try
        {
            return work(db);
        }
        catch (Exception exception) when (exception is not (StorageException or ArgumentException))
        {
            throw StorageErrorMapper.ToStorageException(exception);
        }
    }

    private static StorageException NotFound(string message) => new(StorageErrorKind.NotFound, message);

    private static StorageException Conflict(string message) => new(StorageErrorKind.Conflict, message);

    private string EventDirectory(string eventId) => Path.Combine(DataRoot, "Events", eventId);

    private string CycleDirectory(string eventId, string guestCycleId) => Path.Combine(EventDirectory(eventId), "GuestCycles", guestCycleId);

    private string ThumbnailDirectory => Path.Combine(DataRoot, "Cache", "thumbnails");
}
