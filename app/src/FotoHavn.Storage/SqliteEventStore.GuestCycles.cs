using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FotoHavn.Core.Storage;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage;

public sealed partial class SqliteEventStore
{
    private const string CycleColumns = "g.id, g.event_id, g.started_at, g.ended_at, g.end_reason";

    private const string CaptureColumns = "id, guest_cycle_id, sequence, file_name, byte_length, sha256, width, height, mirrored, taken_at, state";

    private const string StripColumns = "p.guest_cycle_id, p.template_id, p.look_id, p.arrangement, p.file_name, p.byte_length, p.sha256, p.width, p.height, p.committed_at";

    private const string LiveCycle = "g.deleting_at IS NULL AND e.deleting_at IS NULL";

    public Task<GuestCycleRecord> StartGuestCycleAsync(string eventId, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageIds.ThrowIfInvalid(eventId, nameof(eventId));
            RequireEvent(db, eventId);
            var cycle = new GuestCycleRecord(StorageIds.NewId(), eventId, _clock.UtcNow, null, null);
            Db.Exec(
                db,
                null,
                "INSERT INTO guest_cycles (id, event_id, started_at) VALUES (@id, @event, @started);",
                ("@id", cycle.Id),
                ("@event", eventId),
                ("@started", StorageTimestamp.Format(cycle.StartedAt)));
            return cycle;
        });

    public Task EndGuestCycleAsync(string guestCycleId, GuestCycleEndReason reason, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageIds.ThrowIfInvalid(guestCycleId, nameof(guestCycleId));
            StorageValidation.ValidateEndReason(reason);
            var cycle = RequireOpenCycle(db, guestCycleId);
            var discarded = DiscardedFiles(db, cycle);
            Db.Exec(
                db,
                null,
                "UPDATE guest_cycles SET ended_at = @ended, end_reason = @reason WHERE id = @id;",
                ("@ended", StorageTimestamp.Format(_clock.UtcNow)),
                ("@reason", StorageSqlNames.ToSql(reason)),
                ("@id", guestCycleId));
            DeleteQuietly(discarded);
        });

    public Task<CaptureRecord> CommitCaptureAsync(CaptureCommit commit, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageValidation.ValidateCaptureCommit(commit);
            var cycle = RequireOpenCycle(db, commit.GuestCycleId);
            if (Db.Scalar(db, null, "SELECT 1 FROM captures WHERE guest_cycle_id = @cycle AND sequence = @sequence;", ("@cycle", cycle.Id), ("@sequence", commit.Sequence)) is not null)
            {
                throw Conflict("The Capture sequence is already used.");
            }

            var record = new CaptureRecord(
                StorageIds.NewId(),
                cycle.Id,
                commit.Sequence,
                "capture-" + commit.Sequence.ToString(CultureInfo.InvariantCulture) + ".jpg",
                commit.Jpeg.Length,
                Sha256(commit.Jpeg),
                commit.Width,
                commit.Height,
                commit.Mirrored,
                commit.TakenAt,
                CaptureState.Kept);
            CommitArtifact(
                CycleDirectory(cycle.EventId, cycle.Id),
                record.FileName,
                commit.Jpeg,
                (CrashPoint.CaptureStaged, CrashPoint.CaptureFlushed, CrashPoint.CaptureRenamed, CrashPoint.CaptureInserted),
                () => Db.Exec(
                    db,
                    null,
                    $"INSERT INTO captures ({CaptureColumns}) VALUES (@id, @cycle, @sequence, @file, @length, @sha, @width, @height, @mirrored, @taken, 'kept');",
                    ("@id", record.Id),
                    ("@cycle", record.GuestCycleId),
                    ("@sequence", record.Sequence),
                    ("@file", record.FileName),
                    ("@length", record.ByteLength),
                    ("@sha", record.Sha256),
                    ("@width", record.Width),
                    ("@height", record.Height),
                    ("@mirrored", record.Mirrored),
                    ("@taken", StorageTimestamp.Format(record.TakenAt))));
            return record;
        });

    public Task SetCaptureStateAsync(string captureId, CaptureState state, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageIds.ThrowIfInvalid(captureId, nameof(captureId));
            var sql = StorageSqlNames.ToSql(state);
            var endReasons = Db.Query(
                db,
                null,
                $"SELECT g.end_reason FROM captures c JOIN guest_cycles g ON g.id = c.guest_cycle_id JOIN events e ON e.id = g.event_id WHERE c.id = @id AND {LiveCycle};",
                reader => reader.NullableString(0),
                ("@id", captureId));
            if (endReasons.Count == 0)
            {
                throw NotFound("The Capture doesn't exist.");
            }

            if (endReasons[0] is not null)
            {
                throw Conflict("The Guest Cycle has ended.");
            }

            Db.Exec(db, null, "UPDATE captures SET state = @state WHERE id = @id;", ("@state", sql), ("@id", captureId));
        });

    public Task<PhotoStripRecord> CommitPhotoStripAsync(PhotoStripCommit commit, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageValidation.ValidatePhotoStripCommit(commit);
            var cycle = RequireOpenCycle(db, commit.GuestCycleId);
            if (Db.Scalar(db, null, "SELECT 1 FROM photo_strips WHERE guest_cycle_id = @cycle;", ("@cycle", cycle.Id)) is not null)
            {
                throw Conflict("The Guest Cycle already has a Photo Strip.");
            }

            foreach (var captureId in commit.Arrangement)
            {
                if (Db.Scalar(db, null, "SELECT 1 FROM captures WHERE id = @id AND guest_cycle_id = @cycle AND state = 'kept';", ("@id", captureId), ("@cycle", cycle.Id)) is null)
                {
                    throw Conflict("The arrangement names a Capture that isn't a kept Capture of this Guest Cycle.");
                }
            }

            var committedAt = _clock.UtcNow;
            var record = new PhotoStripRecord(
                cycle.Id,
                commit.TemplateId,
                commit.LookId,
                [.. commit.Arrangement],
                "photo-strip.png",
                commit.Png.Length,
                Sha256(commit.Png),
                commit.Width,
                commit.Height,
                committedAt);
            var discarded = DiscardedFiles(db, cycle);
            CommitArtifact(
                CycleDirectory(cycle.EventId, cycle.Id),
                record.FileName,
                commit.Png,
                (CrashPoint.StripStaged, CrashPoint.StripFlushed, CrashPoint.StripRenamed, CrashPoint.StripInserted),
                () =>
                {
                    using var transaction = db.BeginTransaction();
                    Db.Exec(
                        db,
                        transaction,
                        $"INSERT INTO photo_strips (guest_cycle_id, template_id, look_id, arrangement, file_name, byte_length, sha256, width, height, committed_at) VALUES (@cycle, @template, @look, @arrangement, @file, @length, @sha, @width, @height, @committed);",
                        ("@cycle", record.GuestCycleId),
                        ("@template", record.TemplateId),
                        ("@look", record.LookId),
                        ("@arrangement", JsonSerializer.Serialize(record.Arrangement)),
                        ("@file", record.FileName),
                        ("@length", record.ByteLength),
                        ("@sha", record.Sha256),
                        ("@width", record.Width),
                        ("@height", record.Height),
                        ("@committed", StorageTimestamp.Format(committedAt)));
                    Db.Exec(
                        db,
                        transaction,
                        "UPDATE guest_cycles SET ended_at = @ended, end_reason = 'completed' WHERE id = @cycle;",
                        ("@ended", StorageTimestamp.Format(committedAt)),
                        ("@cycle", cycle.Id));
                    transaction.Commit();
                });
            DeleteQuietly(discarded);
            return record;
        });

    public Task<GuestCyclePage> ListGuestCyclesAsync(string eventId, GuestCycleFilter filter, string? cursor, int pageSize, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageValidation.ValidatePaging(eventId, filter, cursor, pageSize);
            RequireEvent(db, eventId);
            var parameters = new List<(string Name, object? Value)> { ("@event", eventId), ("@limit", pageSize + 1) };
            var where = "g.event_id = @event AND g.deleting_at IS NULL";
            if (filter == GuestCycleFilter.CompletedOnly)
            {
                where += " AND g.end_reason = 'completed' AND p.guest_cycle_id IS NOT NULL";
            }

            if (cursor is not null)
            {
                _ = GuestCycleCursor.TryDecode(cursor, out var startedAt, out var id);
                where += " AND (g.started_at < @started OR (g.started_at = @started AND g.id < @id))";
                parameters.Add(("@started", StorageTimestamp.Format(startedAt)));
                parameters.Add(("@id", id));
            }

            var rows = Db.Query(
                db,
                null,
                $"""
                SELECT {CycleColumns},
                       (SELECT COUNT(*) FROM captures c WHERE c.guest_cycle_id = g.id AND c.state = 'kept'),
                       {StripColumns}
                FROM guest_cycles g LEFT JOIN photo_strips p ON p.guest_cycle_id = g.id
                WHERE {where}
                ORDER BY g.started_at DESC, g.id DESC
                LIMIT @limit;
                """,
                reader => new GuestCycleSummary(ReadCycle(reader), reader.GetInt32(5), reader.IsDBNull(6) ? null : ReadStrip(reader, 6)),
                [.. parameters]);
            var items = rows.Take(pageSize).ToList();
            var next = rows.Count > pageSize ? GuestCycleCursor.Encode(items[^1].Cycle.StartedAt, items[^1].Cycle.Id) : null;
            return new GuestCyclePage(items, next);
        });

    public Task<GuestCycleDetail?> GetGuestCycleAsync(string guestCycleId, CancellationToken ct) =>
        RunAsync<GuestCycleDetail?>(ct, db =>
        {
            StorageIds.ThrowIfInvalid(guestCycleId, nameof(guestCycleId));
            var cycle = FindCycle(db, guestCycleId);
            if (cycle is null)
            {
                return null;
            }

            var captures = Db.Query(
                db,
                null,
                $"SELECT {CaptureColumns} FROM captures WHERE guest_cycle_id = @cycle AND state = 'kept' ORDER BY sequence;",
                reader => new CaptureRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    reader.GetInt64(4),
                    reader.GetString(5),
                    reader.GetInt32(6),
                    reader.GetInt32(7),
                    reader.GetBoolean(8),
                    StorageTimestamp.Parse(reader.GetString(9)),
                    StorageSqlNames.CaptureStateFromSql(reader.GetString(10))),
                ("@cycle", guestCycleId));
            var strips = Db.Query(db, null, $"SELECT {StripColumns} FROM photo_strips p WHERE p.guest_cycle_id = @cycle;", reader => ReadStrip(reader, 0), ("@cycle", guestCycleId));
            return new GuestCycleDetail(cycle, captures, strips.Count == 0 ? null : strips[0]);
        });

    private static GuestCycleRecord ReadCycle(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        StorageTimestamp.Parse(reader.GetString(2)),
        reader.IsDBNull(3) ? null : StorageTimestamp.Parse(reader.GetString(3)),
        reader.IsDBNull(4) ? null : StorageSqlNames.EndReasonFromSql(reader.GetString(4)));

    private static PhotoStripRecord ReadStrip(SqliteDataReader reader, int first) => new(
        reader.GetString(first),
        reader.GetString(first + 1),
        reader.GetString(first + 2),
        JsonSerializer.Deserialize<string[]>(reader.GetString(first + 3))!,
        reader.GetString(first + 4),
        reader.GetInt64(first + 5),
        reader.GetString(first + 6),
        reader.GetInt32(first + 7),
        reader.GetInt32(first + 8),
        StorageTimestamp.Parse(reader.GetString(first + 9)));

    /// <summary>The Guest Cycle, or null when it is missing or it (or its Event) is deleting.</summary>
    private static GuestCycleRecord? FindCycle(SqliteConnection db, string guestCycleId)
    {
        var rows = Db.Query(
            db,
            null,
            $"SELECT {CycleColumns} FROM guest_cycles g JOIN events e ON e.id = g.event_id WHERE g.id = @id AND {LiveCycle};",
            ReadCycle,
            ("@id", guestCycleId));
        return rows.Count == 0 ? null : rows[0];
    }

    private static GuestCycleRecord RequireOpenCycle(SqliteConnection db, string guestCycleId)
    {
        var cycle = FindCycle(db, guestCycleId) ?? throw NotFound("The Guest Cycle doesn't exist.");
        return cycle.EndReason is null ? cycle : throw Conflict("The Guest Cycle has ended.");
    }

    private static string Sha256(ReadOnlyMemory<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes.Span));

    /// <summary>The paths of the Guest Cycle's discarded Capture files (D23: they go when the cycle ends).</summary>
    private List<string> DiscardedFiles(SqliteConnection db, GuestCycleRecord cycle)
    {
        var directory = CycleDirectory(cycle.EventId, cycle.Id);
        return Db.Query(
            db,
            null,
            "SELECT file_name FROM captures WHERE guest_cycle_id = @cycle AND state = 'discarded';",
            reader => Path.Combine(directory, reader.GetString(0)),
            ("@cycle", cycle.Id));
    }

    /// <summary>
    /// Writes <paramref name="bytes"/> as <paramref name="fileName"/> in the Guest Cycle folder: staged under <c>.staging</c>
    /// (reported from inside the file system's write, after the bytes are written and before they are flushed), flushed,
    /// renamed, then <paramref name="insert"/> commits the row. A failure before the row exists leaves nothing under the final
    /// name (a leftover staging file is the reconciler's). The crash points after the write run outside the clean-up.
    /// </summary>
    private void CommitArtifact(
        string cycleDirectory,
        string fileName,
        ReadOnlyMemory<byte> bytes,
        (string Staged, string Flushed, string Renamed, string Inserted) steps,
        Action insert)
    {
        var stagingDirectory = Path.Combine(cycleDirectory, ".staging");
        var stagingPath = Path.Combine(stagingDirectory, StorageIds.NewId() + ".tmp");
        var finalPath = Path.Combine(cycleDirectory, fileName);
        var inHook = false;
        try
        {
            _fs.CreateDirectory(stagingDirectory);
            _fs.WriteAllBytesAndFlush(
                stagingPath,
                bytes.Span,
                () =>
                {
                    inHook = true;
                    _crash.Reached(steps.Staged);
                    inHook = false;
                });
        }
        catch (Exception) when (!inHook)
        {
            DeleteQuietly([stagingPath]);
            throw;
        }

        _crash.Reached(steps.Flushed);
        try
        {
            if (_fs.Exists(finalPath))
            {
                _fs.Delete(finalPath);
            }

            _fs.Move(stagingPath, finalPath);
        }
        catch
        {
            DeleteQuietly([stagingPath]);
            throw;
        }

        _crash.Reached(steps.Renamed);
        try
        {
            insert();
        }
        catch
        {
            DeleteQuietly([finalPath]);
            throw;
        }

        _crash.Reached(steps.Inserted);
    }

    /// <summary>Deletes files best effort: a file that stays is an orphan or a discarded file the reconciler removes at the next start.</summary>
    private void DeleteQuietly(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                _fs.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Left for the startup reconciler.
            }
        }
    }
}
