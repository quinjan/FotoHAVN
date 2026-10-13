using System.Security.Cryptography;
using FotoHavn.Core;
using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage.Reconciliation;

/// <summary>
/// The startup reconciler (plan section 10, "Startup reconciler"). Runs steps 1 to 7 in order, only through the injected
/// <see cref="IStorageFileSystem"/>, and only under <c>Events/</c> and <c>Cache/thumbnails/</c>: never <c>fotohavn.db</c>,
/// <c>logs/</c>, or <c>webview2-user-data/</c>.
/// <para>
/// Every step is safe to re-run after a crash at any point: it works from what the database and the folder say now, deletes
/// files before the rows that describe them, and changes rows in transactions.
/// </para>
/// <para>
/// Step 3 is log and keep, on the rule of ADR 0005: a row whose file is missing, or whose size or hash differs, is reported in
/// <see cref="ReconcileReport.Problems"/> by id only, and nothing about it changes: the Capture keeps its state, the Photo Strip
/// keeps its row, the cycle keeps its end, and a file that reappears is adopted as it is (step 2 sees its row, so it is never an
/// orphan). Only an unfinished cycle with such a problem is ended, as <c>unrecoverable</c>; its other files stay. The gallery
/// shows such an item as unavailable (M9).
/// </para>
/// <para>
/// The reconciler never follows a junction or symbolic link: listings skip them, a delete through one is refused and
/// reported, and a link found under <c>Events/</c> or <c>Cache/thumbnails/</c> is left in place and reported, because the
/// operator made it on purpose.
/// </para>
/// <para>
/// Step 2 deletes unreferenced files, then the Event and cycle folders that have no row. It sees folders only through the
/// files in them (the file-system seam lists files), so an empty folder with no row is left behind: it is harmless and is
/// removed with its Event.
/// </para>
/// </summary>
public sealed class StartupReconciler(IClock clock) : IStartupReconciler
{
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public Task<ReconcileReport> RunAsync(SqliteConnection connection, IStorageFileSystem fs, string dataRoot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);

        try
        {
            return Task.FromResult(new Run(connection, fs, dataRoot, _clock.UtcNow, ct).Execute());
        }
        catch (OperationCanceledException)
        {
            return Task.FromCanceled<ReconcileReport>(ct);
        }
        catch (Exception ex)
        {
            return Task.FromException<ReconcileReport>(StorageErrorMapper.ToStorageException(ex));
        }
    }

    private sealed record CycleRow(string Id, string EventId);

    private sealed record CaptureRow(string Id, string CycleId, string EventId, string FileName, long ByteLength, string Sha256);

    private sealed record StripRow(string CycleId, string EventId, string FileName, long ByteLength, string Sha256);

    private enum FileCheck
    {
        Good,
        Missing,
        Differs,
    }

    private sealed class Run(SqliteConnection connection, IStorageFileSystem fs, string dataRoot, DateTimeOffset now, CancellationToken ct)
    {
        private const string LiveCycles =
            "FROM guest_cycles c JOIN events e ON e.id = c.event_id WHERE c.deleting_at IS NULL AND e.deleting_at IS NULL";

        private readonly string _eventsDir = Path.Combine(dataRoot, "Events");
        private readonly string _thumbnailsDir = Path.Combine(dataRoot, ThumbnailName.Folder.Replace('/', Path.DirectorySeparatorChar));
        private readonly List<string> _problems = [];

        public ReconcileReport Execute()
        {
            var staging = DeleteStagingFiles();
            var orphans = DeleteOrphanFiles();
            var (artifactProblems, unrecoverable) = CheckArtifacts();
            var appClosed = CloseUnfinishedCycles();
            var (cycles, events) = FinishDeletions();
            var discarded = DeleteDiscardedFiles();
            var thumbnails = DeleteOrphanThumbnails();
            ReportLinks();

            return new ReconcileReport(staging, orphans, artifactProblems, unrecoverable, appClosed, cycles, events, discarded, thumbnails, _problems.ToArray());
        }

        // Junctions and symbolic links are never followed or removed: the operator made them on purpose, so they are only reported.
        private void ReportLinks()
        {
            foreach (var link in fs.FindReparsePoints(_eventsDir))
            {
                ct.ThrowIfCancellationRequested();
                var segments = Segments(link);
                _problems.Add(segments.Length > 1
                    ? $"A junction or symbolic link in the folder of Event {segments[0]} was left alone."
                    : "A junction or symbolic link directly under Events was left alone.");
            }

            foreach (var _ in fs.FindReparsePoints(_thumbnailsDir))
            {
                ct.ThrowIfCancellationRequested();
                _problems.Add("A junction or symbolic link in the thumbnail cache was left alone.");
            }
        }

        // A refused delete (the path goes through a link) is reported and skipped, never a crash.
        private bool TryDelete(string file)
        {
            try
            {
                fs.Delete(file);
                return true;
            }
            catch (ReparsePointException)
            {
                _problems.Add("A file reached through a junction or symbolic link was left alone.");
                return false;
            }
        }

        private bool TryDeleteDirectory(string directory)
        {
            try
            {
                fs.DeleteDirectoryRecursive(directory);
                return true;
            }
            catch (ReparsePointException)
            {
                _problems.Add("A folder reached through a junction or symbolic link was left alone.");
                return false;
            }
        }

        // Step 1: every .staging file, under Events/ and the thumbnail staging folder Cache/thumbnails/.staging.
        private int DeleteStagingFiles()
        {
            var deleted = 0;
            foreach (var file in fs.EnumerateFiles(_eventsDir, recursive: true))
            {
                ct.ThrowIfCancellationRequested();
                var segments = Segments(file);
                if (segments.Length > 1 && segments[..^1].Any(segment => segment.Equals(".staging", StringComparison.OrdinalIgnoreCase)) && TryDelete(file))
                {
                    deleted++;
                }
            }

            foreach (var file in fs.EnumerateFiles(Path.Combine(_thumbnailsDir, ".staging"), recursive: true))
            {
                ct.ThrowIfCancellationRequested();
                if (TryDelete(file))
                {
                    deleted++;
                }
            }

            return deleted;
        }

        // Step 2: files (then Event and cycle folders) that no row refers to.
        private int DeleteOrphanFiles()
        {
            var events = Query("SELECT id FROM events", r => r.GetString(0)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var cycleEvents = Query("SELECT id, event_id FROM guest_cycles", r => (Id: r.GetString(0), EventId: r.GetString(1)))
                .ToDictionary(row => row.Id, row => row.EventId, StringComparer.OrdinalIgnoreCase);
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (cycleId, fileName) in Query("SELECT guest_cycle_id, file_name FROM captures", r => (r.GetString(0), r.GetString(1))))
            {
                known.Add(cycleId + "/" + fileName);
            }

            foreach (var (cycleId, fileName) in Query("SELECT guest_cycle_id, file_name FROM photo_strips", r => (r.GetString(0), r.GetString(1))))
            {
                known.Add(cycleId + "/" + fileName);
            }

            var deleted = 0;
            var foldersToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in fs.EnumerateFiles(_eventsDir, recursive: true))
            {
                ct.ThrowIfCancellationRequested();
                var segments = Segments(file);
                var referenced = segments.Length == 4
                    && segments[1] == "GuestCycles"
                    && cycleEvents.TryGetValue(segments[2], out var eventId)
                    && string.Equals(eventId, segments[0], StringComparison.OrdinalIgnoreCase)
                    && known.Contains(segments[2] + "/" + segments[3]);
                if (referenced)
                {
                    continue;
                }

                if (!TryDelete(file))
                {
                    continue;
                }

                deleted++;

                if (segments.Length >= 2 && !events.Contains(segments[0]))
                {
                    foldersToRemove.Add(Path.Combine(_eventsDir, segments[0]));
                }
                else if (segments.Length >= 4 && segments[1] == "GuestCycles"
                    && !(cycleEvents.TryGetValue(segments[2], out var owner) && string.Equals(owner, segments[0], StringComparison.OrdinalIgnoreCase)))
                {
                    foldersToRemove.Add(Path.Combine(_eventsDir, segments[0], "GuestCycles", segments[2]));
                }
            }

            foreach (var folder in foldersToRemove)
            {
                ct.ThrowIfCancellationRequested();
                _ = TryDeleteDirectory(folder);
            }

            return deleted;
        }

        // Step 3: rows whose file is missing or differs. Log and keep: nothing about the row changes (an unfinished cycle ends
        // as unrecoverable); a file that reappears is adopted because its row is still there.
        private (int Problems, int Unrecoverable) CheckArtifacts()
        {
            var openCycles = Query($"SELECT c.id {LiveCycles} AND c.end_reason IS NULL", r => r.GetString(0)).ToHashSet(StringComparer.Ordinal);
            var captures = Query(
                "SELECT k.id, k.guest_cycle_id, c.event_id, k.file_name, k.byte_length, k.sha256 FROM captures k JOIN guest_cycles c ON c.id = k.guest_cycle_id JOIN events e ON e.id = c.event_id " +
                "WHERE k.state = 'kept' AND c.deleting_at IS NULL AND e.deleting_at IS NULL ORDER BY k.guest_cycle_id, k.sequence",
                r => new CaptureRow(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt64(4), r.GetString(5)));
            var strips = Query(
                "SELECT s.guest_cycle_id, c.event_id, s.file_name, s.byte_length, s.sha256 FROM photo_strips s JOIN guest_cycles c ON c.id = s.guest_cycle_id JOIN events e ON e.id = c.event_id " +
                "WHERE c.deleting_at IS NULL AND e.deleting_at IS NULL ORDER BY s.guest_cycle_id",
                r => new StripRow(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt64(3), r.GetString(4)));

            var affectedCycles = new HashSet<string>(StringComparer.Ordinal);
            var problemCount = 0;

            foreach (var capture in captures)
            {
                ct.ThrowIfCancellationRequested();
                var check = CheckFile(capture.EventId, capture.CycleId, capture.FileName, capture.ByteLength, capture.Sha256);
                if (check == FileCheck.Good)
                {
                    continue;
                }

                problemCount++;
                affectedCycles.Add(capture.CycleId);
                _problems.Add(check == FileCheck.Missing
                    ? $"Capture {capture.Id} of Guest Cycle {capture.CycleId}: its file is missing; the Capture was kept."
                    : $"Capture {capture.Id} of Guest Cycle {capture.CycleId}: its file doesn't match the recorded size or hash; the Capture was kept.");
            }

            foreach (var strip in strips)
            {
                ct.ThrowIfCancellationRequested();
                var check = CheckFile(strip.EventId, strip.CycleId, strip.FileName, strip.ByteLength, strip.Sha256);
                if (check == FileCheck.Good)
                {
                    continue;
                }

                problemCount++;
                affectedCycles.Add(strip.CycleId);
                _problems.Add(check == FileCheck.Missing
                    ? $"Photo Strip of Guest Cycle {strip.CycleId}: its file is missing; the Photo Strip was kept."
                    : $"Photo Strip of Guest Cycle {strip.CycleId}: its file doesn't match the recorded size or hash; the Photo Strip was kept.");
            }

            var unrecoverable = affectedCycles.Where(openCycles.Contains).OrderBy(id => id, StringComparer.Ordinal).ToList();
            if (unrecoverable.Count > 0)
            {
                using var transaction = connection.BeginTransaction();
                foreach (var id in unrecoverable)
                {
                    Execute(
                        transaction,
                        "UPDATE guest_cycles SET ended_at = $now, end_reason = 'unrecoverable' WHERE id = $id AND end_reason IS NULL",
                        ("$now", StorageTimestamp.Format(now)),
                        ("$id", id));
                    _problems.Add($"Guest Cycle {id}: it was unfinished and has damaged files, so it was ended as unrecoverable.");
                }

                transaction.Commit();
            }

            return (problemCount, unrecoverable.Count);
        }

        // Step 4: still-open cycles end as app_closed.
        private int CloseUnfinishedCycles()
        {
            using var transaction = connection.BeginTransaction();
            var closed = Execute(
                transaction,
                "UPDATE guest_cycles SET ended_at = $now, end_reason = 'app_closed' WHERE end_reason IS NULL AND deleting_at IS NULL " +
                "AND event_id NOT IN (SELECT id FROM events WHERE deleting_at IS NOT NULL)",
                ("$now", StorageTimestamp.Format(now)));
            transaction.Commit();
            return closed;
        }

        // Step 5: finish deleting what was marked deleting_at (folders and thumbnails first, then rows, children first).
        private (int Cycles, int Events) FinishDeletions()
        {
            var deletingEvents = Query("SELECT id FROM events WHERE deleting_at IS NOT NULL ORDER BY id", r => r.GetString(0));
            var deletingCycles = Query(
                "SELECT c.id, c.event_id FROM guest_cycles c JOIN events e ON e.id = c.event_id WHERE c.deleting_at IS NOT NULL AND e.deleting_at IS NULL ORDER BY c.id",
                r => new CycleRow(r.GetString(0), r.GetString(1)));
            var cyclesOfDeletingEvents = Query(
                "SELECT c.id, c.event_id FROM guest_cycles c JOIN events e ON e.id = c.event_id WHERE e.deleting_at IS NOT NULL ORDER BY c.id",
                r => new CycleRow(r.GetString(0), r.GetString(1)));
            if (deletingEvents.Count + deletingCycles.Count == 0)
            {
                return (0, 0);
            }

            var doomedCycleIds = deletingCycles.Concat(cyclesOfDeletingEvents).Select(cycle => cycle.Id).ToHashSet(StringComparer.Ordinal);
            var sourceIds = new HashSet<string>(doomedCycleIds, StringComparer.Ordinal);
            foreach (var (captureId, cycleId) in Query("SELECT id, guest_cycle_id FROM captures", r => (r.GetString(0), r.GetString(1))))
            {
                if (doomedCycleIds.Contains(cycleId))
                {
                    sourceIds.Add(captureId);
                }
            }

            foreach (var file in fs.EnumerateFiles(_thumbnailsDir, recursive: false))
            {
                ct.ThrowIfCancellationRequested();
                if (ThumbnailName.TryParse(Path.GetFileName(file), out var sourceId, out _, out _) && sourceIds.Contains(sourceId))
                {
                    _ = TryDelete(file);
                }
            }

            var cyclesDeleted = 0;
            var eventsDeleted = 0;
            foreach (var eventId in deletingEvents)
            {
                ct.ThrowIfCancellationRequested();
                if (StorageIds.IsValid(eventId) && !TryDeleteDirectory(Path.Combine(_eventsDir, eventId)))
                {
                    // The folder is behind a link: its rows stay marked deleting (hidden) and the problem is reported at every start.
                    continue;
                }

                using var transaction = connection.BeginTransaction();
                const string OfEvent = "(SELECT id FROM guest_cycles WHERE event_id = $id)";
                Execute(transaction, $"DELETE FROM captures WHERE guest_cycle_id IN {OfEvent}", ("$id", eventId));
                Execute(transaction, $"DELETE FROM photo_strips WHERE guest_cycle_id IN {OfEvent}", ("$id", eventId));
                cyclesDeleted += Execute(transaction, "DELETE FROM guest_cycles WHERE event_id = $id", ("$id", eventId));
                Execute(transaction, "DELETE FROM event_templates WHERE event_id = $id", ("$id", eventId));
                Execute(transaction, "DELETE FROM event_looks WHERE event_id = $id", ("$id", eventId));
                Execute(transaction, "DELETE FROM event_phase_timers WHERE event_id = $id", ("$id", eventId));
                eventsDeleted += Execute(transaction, "DELETE FROM events WHERE id = $id", ("$id", eventId));
                transaction.Commit();
            }

            foreach (var cycle in deletingCycles)
            {
                ct.ThrowIfCancellationRequested();
                if (StorageIds.IsValid(cycle.EventId) && StorageIds.IsValid(cycle.Id) && !TryDeleteDirectory(CycleFolder(cycle.EventId, cycle.Id)))
                {
                    continue;
                }

                using var transaction = connection.BeginTransaction();
                Execute(transaction, "DELETE FROM captures WHERE guest_cycle_id = $id", ("$id", cycle.Id));
                Execute(transaction, "DELETE FROM photo_strips WHERE guest_cycle_id = $id", ("$id", cycle.Id));
                cyclesDeleted += Execute(transaction, "DELETE FROM guest_cycles WHERE id = $id", ("$id", cycle.Id));
                transaction.Commit();
            }

            return (cyclesDeleted, eventsDeleted);
        }

        // Step 6: the files of discarded Captures.
        private int DeleteDiscardedFiles()
        {
            var discarded = Query(
                "SELECT k.id, k.guest_cycle_id, c.event_id, k.file_name, k.byte_length, k.sha256 FROM captures k JOIN guest_cycles c ON c.id = k.guest_cycle_id WHERE k.state = 'discarded' ORDER BY k.id",
                r => new CaptureRow(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt64(4), r.GetString(5)));

            var deleted = 0;
            foreach (var capture in discarded)
            {
                ct.ThrowIfCancellationRequested();
                var path = FilePath(capture.EventId, capture.CycleId, capture.FileName);
                if (path is not null && fs.Exists(path) && TryDelete(path))
                {
                    deleted++;
                }
            }

            return deleted;
        }

        // Step 7: thumbnails whose source row is gone or whose recorded hash changed, and files that aren't thumbnails. A row
        // whose file is missing still counts as a source, so its thumbnail stays (log and keep, as in step 3); the row says what
        // the thumbnail shows, and the thumbnail is the only picture left of it.
        private int DeleteOrphanThumbnails()
        {
            var sources = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (id, sha) in Query("SELECT id, sha256 FROM captures WHERE state = 'kept'", r => (r.GetString(0), r.GetString(1))))
            {
                sources[id] = sha;
            }

            foreach (var (id, sha) in Query("SELECT guest_cycle_id, sha256 FROM photo_strips", r => (r.GetString(0), r.GetString(1))))
            {
                sources[id] = sha;
            }

            var deleted = 0;
            foreach (var file in fs.EnumerateFiles(_thumbnailsDir, recursive: false))
            {
                ct.ThrowIfCancellationRequested();
                var current = ThumbnailName.TryParse(Path.GetFileName(file), out var sourceId, out var sha8, out var width)
                    && width is >= ThumbnailName.MinimumWidth and <= ThumbnailName.MaximumWidth
                    && sources.TryGetValue(sourceId, out var sha)
                    && sha.StartsWith(sha8, StringComparison.OrdinalIgnoreCase);
                if (!current && TryDelete(file))
                {
                    deleted++;
                }
            }

            return deleted;
        }

        private FileCheck CheckFile(string eventId, string cycleId, string fileName, long byteLength, string sha256)
        {
            var path = FilePath(eventId, cycleId, fileName);
            if (path is null || !fs.Exists(path))
            {
                return FileCheck.Missing;
            }

            if (fs.GetLength(path) != byteLength)
            {
                return FileCheck.Differs;
            }

            var actual = Convert.ToHexStringLower(SHA256.HashData(fs.ReadAllBytes(path)));
            return string.Equals(actual, sha256, StringComparison.OrdinalIgnoreCase) ? FileCheck.Good : FileCheck.Differs;
        }

        private string CycleFolder(string eventId, string cycleId) => Path.Combine(_eventsDir, eventId, "GuestCycles", cycleId);

        // Row values become path parts, so a row whose ids or file name aren't plain names has no usable file.
        private string? FilePath(string eventId, string cycleId, string fileName) =>
            StorageIds.IsValid(eventId) && StorageIds.IsValid(cycleId) && fileName.Length > 0 && Path.GetFileName(fileName) == fileName && fileName is not ("." or "..")
                ? Path.Combine(CycleFolder(eventId, cycleId), fileName)
                : null;

        private string[] Segments(string file) =>
            Path.GetRelativePath(_eventsDir, file).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        private List<T> Query<T>(string sql, Func<SqliteDataReader, T> map)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var rows = new List<T>();
            while (reader.Read())
            {
                rows.Add(map(reader));
            }

            return rows;
        }

        private int Execute(SqliteTransaction transaction, string sql, params (string Name, object Value)[] parameters)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            return command.ExecuteNonQuery();
        }
    }
}
