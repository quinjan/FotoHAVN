using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Migrations;
using FotoHavn.Storage.Tests.Support;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage.Tests.Reconciliation;

/// <summary>A Capture seeded by <see cref="SeededDataFolder"/>.</summary>
public sealed record SeededCapture(string Id, string CycleId, string EventId, int Sequence, string FileName, string Path, byte[] Bytes, string Sha256);

/// <summary>A Photo Strip seeded by <see cref="SeededDataFolder"/>.</summary>
public sealed record SeededStrip(string CycleId, string EventId, string FileName, string Path, byte[] Bytes, string Sha256);

/// <summary>
/// The ids and paths <see cref="SeededDataFolder.SeedReconcileScenario"/> creates: one folder with every kind of damage the
/// reconciler repairs. The e2e <c>reconcile-seeded-data</c> test mirrors this layout.
/// </summary>
public sealed record ReconcileScenario(
    string LiveEventId,
    string CompletedCycleId,
    string OpenCycleId,
    string DeletingCycleId,
    string DeletingEventId,
    string DeletingEventCycleId,
    string OrphanEventFolder,
    string StagingFile,
    string OrphanFile,
    string OrphanFolderFile,
    string DiscardedCaptureFile,
    IReadOnlyList<string> StaleThumbnails,
    IReadOnlyList<string> LiveThumbnails,
    IReadOnlyList<string> ProtectedFiles);

/// <summary>
/// A real Data folder under a <see cref="ScratchFolder"/>: <c>fotohavn.db</c> with the embedded migrations applied, an open
/// connection (foreign keys on), and builders for Events, Guest Cycles, Captures, Photo Strips, thumbnails, and stray files.
/// Wave C's integration tests reuse it.
/// </summary>
public sealed class SeededDataFolder : IDisposable
{
    private readonly ScratchFolder _scratch = new();
    private readonly Dictionary<string, string> _cycleEvents = new(StringComparer.Ordinal);
    private int _counter;

    public SeededDataFolder()
    {
        DataRoot = _scratch.Combine("Data");
        Fs = new PhysicalStorageFileSystem(DataRoot);
        Directory.CreateDirectory(DataRoot);
        DatabasePath = Path.Combine(DataRoot, "fotohavn.db");
        Connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False;Default Timeout=1");
        Connection.Open();
        Execute("PRAGMA journal_mode = WAL;");
        Execute("PRAGMA foreign_keys = ON;");
        foreach (var script in MigrationCatalog.Embedded)
        {
            Execute(script.Sql);
            Execute($"PRAGMA user_version = {script.Version};");
        }
    }

    public string DataRoot { get; }

    public string DatabasePath { get; }

    public string EventsDir => Path.Combine(DataRoot, "Events");

    public string ThumbnailsDir => Path.Combine(DataRoot, "Cache", "thumbnails");

    public SqliteConnection Connection { get; }

    public PhysicalStorageFileSystem Fs { get; }

    public void Dispose()
    {
        Connection.Dispose();
        SqliteConnection.ClearAllPools();
        _scratch.Dispose();
    }

    public string CycleFolder(string cycleId) => Path.Combine(EventsDir, _cycleEvents[cycleId], "GuestCycles", cycleId);

    public string EventFolder(string eventId) => Path.Combine(EventsDir, eventId);

    public string AddEvent(bool deleting = false, string? id = null)
    {
        id ??= StorageIds.NewId();
        Execute(
            "INSERT INTO events (id, name, event_date, default_template_id, default_look_id, created_at, updated_at, deleting_at) " +
            "VALUES ($id, 'Seeded', '2026-10-24', 'classic-strip', 'natural', $now, $now, $deleting)",
            ("$id", id),
            ("$now", StorageTimestamp.Format(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero))),
            ("$deleting", deleting ? StorageTimestamp.Format(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero)) : DBNull.Value));
        Execute("INSERT INTO event_templates (event_id, template_id) VALUES ($id, 'classic-strip')", ("$id", id));
        Execute("INSERT INTO event_looks (event_id, look_id) VALUES ($id, 'natural')", ("$id", id));
        Execute("INSERT INTO event_phase_timers (event_id, phase, seconds) VALUES ($id, 'template', 30)", ("$id", id));
        return id;
    }

    /// <summary>Adds a Guest Cycle. A non-null <paramref name="endReason"/> (the SQL string) ends it.</summary>
    public string AddCycle(string eventId, string? endReason = null, bool deleting = false, string? id = null)
    {
        id ??= StorageIds.NewId();
        var started = new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero).AddMinutes(++_counter);
        Execute(
            "INSERT INTO guest_cycles (id, event_id, started_at, ended_at, end_reason, deleting_at) VALUES ($id, $event, $started, $ended, $reason, $deleting)",
            ("$id", id),
            ("$event", eventId),
            ("$started", StorageTimestamp.Format(started)),
            ("$ended", endReason is null ? DBNull.Value : StorageTimestamp.Format(started.AddMinutes(1))),
            ("$reason", endReason is null ? DBNull.Value : endReason),
            ("$deleting", deleting ? StorageTimestamp.Format(started.AddMinutes(2)) : DBNull.Value));
        _cycleEvents[id] = eventId;
        return id;
    }

    /// <summary>Adds a Capture row and, unless <paramref name="writeFile"/> is false, its file.</summary>
    public SeededCapture AddCapture(string cycleId, int sequence, string state = "kept", bool writeFile = true, byte[]? bytes = null)
    {
        bytes ??= Encoding.UTF8.GetBytes($"jpeg bytes {cycleId} {sequence}");
        var id = StorageIds.NewId();
        var fileName = $"capture-{sequence}.jpg";
        var path = Path.Combine(CycleFolder(cycleId), fileName);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Execute(
            "INSERT INTO captures (id, guest_cycle_id, sequence, file_name, byte_length, sha256, width, height, mirrored, taken_at, state) " +
            "VALUES ($id, $cycle, $sequence, $file, $length, $sha, 1920, 1080, 1, $taken, $state)",
            ("$id", id),
            ("$cycle", cycleId),
            ("$sequence", sequence),
            ("$file", fileName),
            ("$length", (long)bytes.Length),
            ("$sha", sha),
            ("$taken", StorageTimestamp.Format(new DateTimeOffset(2026, 10, 24, 10, 5, 0, TimeSpan.Zero))),
            ("$state", state));
        if (writeFile)
        {
            WriteFile(path, bytes);
        }

        return new SeededCapture(id, cycleId, _cycleEvents[cycleId], sequence, fileName, path, bytes, sha);
    }

    /// <summary>Adds a Photo Strip row (arrangement = the given Capture ids) and, unless <paramref name="writeFile"/> is false, its file.</summary>
    public SeededStrip AddPhotoStrip(string cycleId, IReadOnlyList<string> arrangement, bool writeFile = true, byte[]? bytes = null)
    {
        bytes ??= Encoding.UTF8.GetBytes($"png bytes {cycleId}");
        var path = Path.Combine(CycleFolder(cycleId), "photo-strip.png");
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Execute(
            "INSERT INTO photo_strips (guest_cycle_id, template_id, look_id, arrangement, file_name, byte_length, sha256, width, height, committed_at) " +
            "VALUES ($cycle, 'classic-strip', 'natural', $arrangement, 'photo-strip.png', $length, $sha, 600, 1800, $committed)",
            ("$cycle", cycleId),
            ("$arrangement", JsonSerializer.Serialize(arrangement)),
            ("$length", (long)bytes.Length),
            ("$sha", sha),
            ("$committed", StorageTimestamp.Format(new DateTimeOffset(2026, 10, 24, 10, 6, 0, TimeSpan.Zero))));
        if (writeFile)
        {
            WriteFile(path, bytes);
        }

        return new SeededStrip(cycleId, _cycleEvents[cycleId], "photo-strip.png", path, bytes, sha);
    }

    /// <summary>Writes a file the database doesn't know about (a staging leftover, an orphan, anything) and returns its path.</summary>
    public string AddFile(string path, string content = "stray")
    {
        if (!path.StartsWith(DataRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The file must be inside the Data folder.", nameof(path));
        }

        WriteFile(path, Encoding.UTF8.GetBytes(content));
        return path;
    }

    public string AddStagingFile(string cycleId, string name = "leftover.tmp") =>
        AddFile(Path.Combine(CycleFolder(cycleId), ".staging", name));

    /// <summary>Writes a thumbnail named for <paramref name="sourceId"/> and <paramref name="sha256"/>.</summary>
    public string AddThumbnail(string sourceId, string sha256, int width = 256) =>
        AddFile(Path.Combine(ThumbnailsDir, ThumbnailName.Format(sourceId, sha256, width)), "thumbnail");

    /// <summary>Writes the files of the "never touch" paths (<c>fotohavn.db</c> already exists) and returns them.</summary>
    public IReadOnlyList<string> AddProtectedFiles() =>
    [
        DatabasePath,
        AddFile(Path.Combine(DataRoot, "logs", "app.log"), "log"),
        AddFile(Path.Combine(DataRoot, "webview2-user-data", "Default", "Preferences"), "prefs"),
    ];

    public long Scalar(string sql, params (string Name, object Value)[] parameters)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public string? Text(string sql, params (string Name, object Value)[] parameters)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command.ExecuteScalar() is string text ? text : null;
    }

    public long Count(string table) => Scalar($"SELECT COUNT(*) FROM {table}");

    public static string Sha256Of(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Every file under the Data folder except the database files, as paths relative to it (sorted).</summary>
    public IReadOnlyList<string> FilesUnderData() =>
        Directory.EnumerateFiles(DataRoot, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(DataRoot, file))
            .Where(file => !file.StartsWith("fotohavn.db", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Seeds one of each kind of damage: a staging leftover, an orphan file in a live cycle, an orphan Event folder, an open
    /// Guest Cycle, an Event and a Guest Cycle marked deleting (with files and thumbnails), a discarded Capture with a file,
    /// and a stale, a wrong-hash, and a junk thumbnail, next to healthy data (a completed cycle with a Photo Strip and thumbnails).
    /// </summary>
    public ReconcileScenario SeedReconcileScenario()
    {
        var liveEvent = AddEvent();
        var completed = AddCycle(liveEvent, "completed");
        var first = AddCapture(completed, 1);
        var second = AddCapture(completed, 2);
        var discarded = AddCapture(completed, 3, state: "discarded");
        var strip = AddPhotoStrip(completed, [first.Id, second.Id]);
        var orphanFile = AddFile(Path.Combine(CycleFolder(completed), "capture-9.jpg"));

        var open = AddCycle(liveEvent);
        AddCapture(open, 1);
        var staging = AddStagingFile(open);

        var deletingCycle = AddCycle(liveEvent, "completed", deleting: true);
        var deletingCapture = AddCapture(deletingCycle, 1);
        var deletingStrip = AddPhotoStrip(deletingCycle, [deletingCapture.Id]);

        var deletingEvent = AddEvent(deleting: true);
        var deletingEventCycle = AddCycle(deletingEvent, "completed");
        var eventCapture = AddCapture(deletingEventCycle, 1);

        var orphanFolderFile = AddFile(Path.Combine(EventsDir, StorageIds.NewId(), "GuestCycles", StorageIds.NewId(), "capture-1.jpg"));

        string[] live =
        [
            AddThumbnail(first.Id, first.Sha256, 256),
            AddThumbnail(completed, strip.Sha256, 512),
        ];
        string[] stale =
        [
            AddThumbnail(StorageIds.NewId(), Sha256Of([1]), 256),
            AddThumbnail(second.Id, Sha256Of([9, 9]), 256),
            AddThumbnail(discarded.Id, discarded.Sha256, 256),
            AddFile(Path.Combine(ThumbnailsDir, "notes.txt")),
        ];
        _ = AddThumbnail(deletingCapture.Id, deletingCapture.Sha256);
        _ = AddThumbnail(deletingCycle, deletingStrip.Sha256);
        _ = AddThumbnail(eventCapture.Id, eventCapture.Sha256);

        return new ReconcileScenario(
            liveEvent,
            completed,
            open,
            deletingCycle,
            deletingEvent,
            deletingEventCycle,
            Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(orphanFolderFile)!)!)!,
            staging,
            orphanFile,
            orphanFolderFile,
            discarded.Path,
            stale,
            live,
            AddProtectedFiles());
    }

    private static void WriteFile(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private void Execute(string sql, params (string Name, object Value)[] parameters)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }
}
