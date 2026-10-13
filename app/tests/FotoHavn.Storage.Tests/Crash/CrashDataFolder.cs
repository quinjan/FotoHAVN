using System.Security.Cryptography;
using FotoHavn.Core;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Storage.DataPath;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Reconciliation;
using FotoHavn.Storage.Tests.Support;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage.Tests.Crash;

/// <summary>A store built the way the hosts build it: the real file system, probe, and reconciler.</summary>
public static class RealStore
{
    public static SqliteEventStore Create(string dataRoot, IStorageFileSystem? fileSystem = null, ICrashPoints? crashPoints = null) =>
        new(
            dataRoot,
            SystemClock.Instance,
            fileSystem ?? new PhysicalStorageFileSystem(),
            new WindowsDataPathProbe(),
            new StartupReconciler(SystemClock.Instance),
            new FastPinHasher(),
            crashPoints);

    public static async Task<(SqliteEventStore Store, StorageOpenResult Opened)> OpenAsync(string dataRoot, IStorageFileSystem? fileSystem = null)
    {
        var store = Create(dataRoot, fileSystem);
        var opened = await store.OpenAsync(CancellationToken.None);
        return (store, opened);
    }
}

/// <summary>The ids of the data <see cref="CrashDataFolder.SeedAsync"/> writes.</summary>
/// <param name="EventId">The Event the scenarios work on.</param>
/// <param name="CompletedCycleId">A completed cycle of that Event: two Captures, a Photo Strip, and thumbnails of both.</param>
/// <param name="EndedCycleId">A cycle of that Event that ended "started over", with one Capture.</param>
/// <param name="OtherEventId">A second Event with one completed cycle that no scenario touches.</param>
/// <param name="OtherCycleId">That cycle.</param>
/// <param name="OtherCaptureId">Its Capture.</param>
public sealed record SeededIds(string EventId, string CompletedCycleId, IReadOnlyList<string> CompletedCaptureIds, string EndedCycleId, string OtherEventId, string OtherCycleId, string OtherCaptureId);

/// <summary>Seeds a Data folder through the real store and checks the invariants the reconciler guarantees after any crash.</summary>
public static class CrashDataFolder
{
    public static async Task<SeededIds> SeedAsync(string dataRoot)
    {
        await using var store = RealStore.Create(dataRoot);
        await store.OpenAsync(CancellationToken.None);

        var @event = Sample.Event(name: "Crash wedding");
        var other = Sample.Event(name: "Other party");
        await store.CreateEventAsync(@event, CancellationToken.None);
        await store.CreateEventAsync(other, CancellationToken.None);

        var completed = await store.StartGuestCycleAsync(@event.Id, CancellationToken.None);
        var first = await store.CommitCaptureAsync(Sample.Capture(completed.Id, 1, Sample.Jpeg(color: SkiaSharp.SKColors.Coral)), CancellationToken.None);
        var second = await store.CommitCaptureAsync(Sample.Capture(completed.Id, 2, Sample.Jpeg(color: SkiaSharp.SKColors.Teal)), CancellationToken.None);
        await store.CommitPhotoStripAsync(Sample.Strip(completed.Id, [first.Id, second.Id]), CancellationToken.None);
        await store.GetThumbnailPathAsync(first.Id, 256, CancellationToken.None);
        await store.GetThumbnailPathAsync(completed.Id, 256, CancellationToken.None);

        var ended = await store.StartGuestCycleAsync(@event.Id, CancellationToken.None);
        await store.CommitCaptureAsync(Sample.Capture(ended.Id, 1), CancellationToken.None);
        await store.EndGuestCycleAsync(ended.Id, GuestCycleEndReason.StartedOver, CancellationToken.None);

        var otherCycle = await store.StartGuestCycleAsync(other.Id, CancellationToken.None);
        var otherCapture = await store.CommitCaptureAsync(Sample.Capture(otherCycle.Id, 1), CancellationToken.None);
        await store.CommitPhotoStripAsync(Sample.Strip(otherCycle.Id, [otherCapture.Id]), CancellationToken.None);
        await store.GetThumbnailPathAsync(otherCapture.Id, 256, CancellationToken.None);
        await store.GetThumbnailPathAsync(otherCycle.Id, 256, CancellationToken.None);

        return new SeededIds(@event.Id, completed.Id, [first.Id, second.Id], ended.Id, other.Id, otherCycle.Id, otherCapture.Id);
    }

    public static string EventFolder(string dataRoot, string eventId) => Path.Combine(dataRoot, "Events", eventId);

    public static string CycleFolder(string dataRoot, string eventId, string cycleId) => Path.Combine(EventFolder(dataRoot, eventId), "GuestCycles", cycleId);

    public static IReadOnlyList<string> ThumbnailsOf(string dataRoot, params string[] sourceIds) =>
        Directory.Exists(Path.Combine(dataRoot, "Cache", "thumbnails"))
            ? [.. Directory.GetFiles(Path.Combine(dataRoot, "Cache", "thumbnails")).Where(file => sourceIds.Any(id => Path.GetFileName(file).StartsWith(id, StringComparison.Ordinal)))]
            : [];

    public static long Count(string dataRoot, string sql)
    {
        using var connection = Open(dataRoot);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string? Text(string dataRoot, string sql)
    {
        using var connection = Open(dataRoot);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() as string;
    }

    /// <summary>
    /// The invariants after a reopen: the database is intact; nothing is deleting or unfinished; every kept Capture and
    /// Photo Strip row has its file with the recorded size and hash; no file lacks a row; no staging file or orphan
    /// thumbnail remains; a Photo Strip's cycle is completed.
    /// </summary>
    public static void AssertConsistent(string dataRoot)
    {
        using var connection = Open(dataRoot);
        Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
        Assert.Equal(0L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM events WHERE deleting_at IS NOT NULL"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(0L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM guest_cycles WHERE deleting_at IS NOT NULL"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(0L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM guest_cycles WHERE ended_at IS NULL OR end_reason IS NULL"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(0L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM photo_strips p JOIN guest_cycles c ON c.id = p.guest_cycle_id WHERE c.end_reason <> 'completed'"), System.Globalization.CultureInfo.InvariantCulture));

        var expected = new Dictionary<string, (long Length, string Sha256)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (relative, length, sha) in Rows(
            connection,
            "SELECT c.event_id || '/GuestCycles/' || k.guest_cycle_id || '/' || k.file_name, k.byte_length, k.sha256 FROM captures k JOIN guest_cycles c ON c.id = k.guest_cycle_id WHERE k.state = 'kept' " +
            "UNION ALL SELECT c.event_id || '/GuestCycles/' || p.guest_cycle_id || '/' || p.file_name, p.byte_length, p.sha256 FROM photo_strips p JOIN guest_cycles c ON c.id = p.guest_cycle_id"))
        {
            expected[relative] = (length, sha);
        }

        var eventsRoot = Path.Combine(dataRoot, "Events");
        var found = Directory.Exists(eventsRoot)
            ? Directory.GetFiles(eventsRoot, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(eventsRoot, file).Replace('\\', '/')).ToList()
            : [];
        Assert.DoesNotContain(found, file => file.Contains("/.staging/", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), found.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (relative, (length, sha)) in expected)
        {
            var path = Path.Combine(eventsRoot, relative.Replace('/', '\\'));
            Assert.Equal(length, new FileInfo(path).Length);
            Assert.Equal(sha, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
        }

        var thumbnails = Path.Combine(dataRoot, "Cache", "thumbnails");
        Assert.False(Directory.Exists(Path.Combine(thumbnails, ".staging")) && Directory.EnumerateFiles(Path.Combine(thumbnails, ".staging"), "*", SearchOption.AllDirectories).Any(), "Thumbnail staging files remain.");
        var sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, _, _) in Rows(connection, "SELECT id, 0, '' FROM captures WHERE state = 'kept' UNION ALL SELECT guest_cycle_id, 0, '' FROM photo_strips"))
        {
            sources.Add(id);
        }

        if (Directory.Exists(thumbnails))
        {
            foreach (var file in Directory.GetFiles(thumbnails))
            {
                Assert.True(ThumbnailName.TryParse(Path.GetFileName(file), out var sourceId, out _, out _) && sources.Contains(sourceId), $"Orphan thumbnail {Path.GetFileName(file)}.");
            }
        }
    }

    private static SqliteConnection Open(string dataRoot)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataRoot, SqliteEventStore.DatabaseFileName),
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static List<(string, long, string)> Rows(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<(string, long, string)>();
        while (reader.Read())
        {
            rows.Add((reader.GetString(0), reader.GetInt64(1), reader.GetString(2)));
        }

        return rows;
    }
}
