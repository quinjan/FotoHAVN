using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Migrations;
using Microsoft.Data.Sqlite;
using SkiaSharp;

namespace FotoHavn.Storage.Tests.Support;

/// <summary>An <see cref="ICrashPoints"/> that records the steps reached and can run code at one.</summary>
public sealed class RecordingCrashPoints : ICrashPoints
{
    private readonly object _gate = new();
    private readonly List<string> _steps = [];

    public Action<string>? OnReached { get; set; }

    public IReadOnlyList<string> Steps
    {
        get
        {
            lock (_gate)
            {
                return [.. _steps];
            }
        }
    }

    public void Reached(string stepName)
    {
        lock (_gate)
        {
            _steps.Add(stepName);
        }

        OnReached?.Invoke(stepName);
    }
}

public sealed class StubProbe : IDataPathProbe
{
    public DataPathFacts Facts { get; set; } = new(DriveType.Fixed, false, false, null, false, true);

    public DataPathFacts Probe(string dataRoot) => Facts;
}

public sealed class StubReconciler : IStartupReconciler
{
    public int Runs { get; private set; }

    public ReconcileReport Report { get; set; } = ReconcileReport.Empty;

    public Exception? Failure { get; set; }

    public Func<SqliteConnection, Task>? OnRun { get; set; }

    public async Task<ReconcileReport> RunAsync(SqliteConnection connection, IStorageFileSystem fs, string dataRoot, CancellationToken ct)
    {
        Runs++;
        if (OnRun is not null)
        {
            await OnRun(connection);
        }

        return Failure is null ? Report : throw Failure;
    }
}

/// <summary>The pieces of one store under test, over a real SQLite file and real files in a scratch folder.</summary>
public sealed class StoreHarness : IAsyncDisposable
{
    private readonly ScratchFolder _scratch = new();

    public StoreHarness(IReadOnlyList<MigrationScript>? migrations = null, IPinHasher? hasher = null)
    {
        Faults = new FaultInjectingStorageFileSystem(new PhysicalStorageFileSystem(DataRoot));
        Fs = new RecordingStorageFileSystem(Faults);
        Store = new SqliteEventStore(DataRoot, Clock, Fs, Probe, Reconciler, hasher ?? new FastPinHasher(), Crash, Classify, migrations);
    }

    public string DataRoot => _scratch.Combine("Data");

    public string DatabasePath => Path.Combine(DataRoot, SqliteEventStore.DatabaseFileName);

    public FakeClock Clock { get; } = new();

    public FaultInjectingStorageFileSystem Faults { get; }

    public RecordingStorageFileSystem Fs { get; }

    public RecordingCrashPoints Crash { get; } = new();

    public StubProbe Probe { get; } = new();

    public StubReconciler Reconciler { get; } = new();

    public SqliteEventStore Store { get; }

    public static async Task<StoreHarness> OpenedAsync(IReadOnlyList<MigrationScript>? migrations = null, IPinHasher? hasher = null)
    {
        var harness = new StoreHarness(migrations, hasher);
        _ = await harness.Store.OpenAsync(CancellationToken.None);
        return harness;
    }

    public static DataPathVerdict Classify(DataPathFacts facts) => facts switch
    {
        { IsUnc: true } or { DriveType: DriveType.Network } => new DataPathVerdict(DataPathStatus.NotLocalDisk, "Network."),
        { IsUnderKnownCloudSyncRoot: true } => new DataPathVerdict(DataPathStatus.CloudSynced, "Cloud."),
        { CanWrite: false } => new DataPathVerdict(DataPathStatus.NotWritable, "Read-only."),
        _ => new DataPathVerdict(DataPathStatus.Ok, "Fine."),
    };

    /// <summary>The folder of a Guest Cycle's files.</summary>
    public string CycleFolder(string eventId, string guestCycleId) => Path.Combine(DataRoot, "Events", eventId, "GuestCycles", guestCycleId);

    public string Absolute(string relativeToDataRoot) => Path.Combine(DataRoot, relativeToDataRoot.Replace('/', Path.DirectorySeparatorChar));

    public static string[] Files(string folder) => Directory.Exists(folder) ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories) : [];

    /// <summary>Reads the database through a second connection, as another process would.</summary>
    public List<T> Query<T>(string sql, Func<SqliteDataReader, T> read)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    public long Count(string sql) => Query(sql, reader => reader.GetInt64(0))[0];

    public void Execute(string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public async ValueTask DisposeAsync()
    {
        await Store.DisposeAsync();
        _scratch.Dispose();
    }
}

public static class Sample
{
    public static EventRecord Event(string? id = null, string name = "Wedding", DateOnly? date = null) => new(
        id ?? StorageIds.NewId(),
        name,
        date ?? new DateOnly(2026, 10, 24),
        "Town hall",
        "Alex",
        "Notes",
        "camera-1",
        "Front camera",
        "display-1",
        "Guest display",
        EventRecord.NoPrinter,
        true,
        5,
        "signature-strip",
        "naturale-v1",
        true,
        true,
        ["signature-strip", "panorama-four"],
        ["naturale-v1", "sepia-v1", "classic-v1"],
        [new PhaseTimerSetting(PhaseTimerPhase.Template, 30), new PhaseTimerSetting(PhaseTimerPhase.Filters, 20)]);

    public static byte[] Jpeg(int width = 80, int height = 60, SKColor? color = null) => Encode(SKEncodedImageFormat.Jpeg, width, height, color ?? SKColors.Coral);

    public static byte[] Png(int width = 60, int height = 180, SKColor? color = null) => Encode(SKEncodedImageFormat.Png, width, height, color ?? SKColors.SteelBlue);

    private static byte[] Encode(SKEncodedImageFormat format, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(color);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    public static CaptureCommit Capture(string guestCycleId, int sequence = 1, byte[]? jpeg = null) =>
        new(guestCycleId, sequence, jpeg ?? Jpeg(), 80, 60, true, new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));

    public static PhotoStripCommit Strip(string guestCycleId, IReadOnlyList<string> arrangement, byte[]? png = null) =>
        new(guestCycleId, "signature-strip", "naturale-v1", arrangement, png ?? Png(), 60, 180);
}
