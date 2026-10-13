using FotoHavn.Core.Storage;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage;

public sealed partial class SqliteEventStore
{
    private readonly object _jobGate = new();
    private Task _jobs = Task.CompletedTask;

    public Task MarkEventDeletingAsync(string eventId, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageIds.ThrowIfInvalid(eventId, nameof(eventId));
            var changed = Db.Exec(
                db,
                null,
                "UPDATE events SET deleting_at = @now WHERE id = @id AND deleting_at IS NULL;",
                ("@now", StorageTimestamp.Format(_clock.UtcNow)),
                ("@id", eventId));
            if (changed == 0)
            {
                throw NotFound("The Event doesn't exist.");
            }

            _crash.Reached(CrashPoint.EventMarkedDeleting);
            Enqueue(() => DeleteEventAsync(eventId));
        });

    public Task MarkGuestCycleDeletingAsync(string guestCycleId, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageIds.ThrowIfInvalid(guestCycleId, nameof(guestCycleId));
            _ = FindCycle(db, guestCycleId) ?? throw NotFound("The Guest Cycle doesn't exist.");
            Db.Exec(
                db,
                null,
                "UPDATE guest_cycles SET deleting_at = @now WHERE id = @id;",
                ("@now", StorageTimestamp.Format(_clock.UtcNow)),
                ("@id", guestCycleId));
            _crash.Reached(CrashPoint.CycleMarkedDeleting);
            Enqueue(() => DeleteGuestCycleAsync(guestCycleId));
        });

    public async Task DrainAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        while (true)
        {
            Task current;
            lock (_jobGate)
            {
                current = _jobs;
            }

            await current.WaitAsync(ct).ConfigureAwait(false);
            lock (_jobGate)
            {
                if (ReferenceEquals(current, _jobs))
                {
                    return;
                }
            }
        }
    }

    private void Enqueue(Func<Task> job)
    {
        lock (_jobGate)
        {
            var previous = _jobs;
            _jobs = Task.Run(async () =>
            {
                await previous.ConfigureAwait(false);
                await job().ConfigureAwait(false);
            });
        }
    }

    private async Task DeleteEventAsync(string eventId)
    {
        var proceed = false;
        await StepAsync(db =>
        {
            if (Db.Scalar(db, null, "SELECT 1 FROM events WHERE id = @id AND deleting_at IS NOT NULL;", ("@id", eventId)) is null)
            {
                return;
            }

            var sources = Db.Query(db, null, "SELECT id FROM guest_cycles WHERE event_id = @id;", reader => reader.GetString(0), ("@id", eventId));
            sources.AddRange(Db.Query(
                db,
                null,
                "SELECT c.id FROM captures c JOIN guest_cycles g ON g.id = c.guest_cycle_id WHERE g.event_id = @id;",
                reader => reader.GetString(0),
                ("@id", eventId)));
            _fs.DeleteDirectoryRecursive(EventDirectory(eventId));
            DeleteThumbnails(sources);
            _crash.Reached(CrashPoint.EventFolderDeleted);
            proceed = true;
        }).ConfigureAwait(false);
        if (!proceed)
        {
            return;
        }

        await StepAsync(db =>
        {
            using var transaction = db.BeginTransaction();
            var scope = ("@id", (object?)eventId);
            Db.Exec(db, transaction, "DELETE FROM photo_strips WHERE guest_cycle_id IN (SELECT id FROM guest_cycles WHERE event_id = @id);", scope);
            Db.Exec(db, transaction, "DELETE FROM captures WHERE guest_cycle_id IN (SELECT id FROM guest_cycles WHERE event_id = @id);", scope);
            Db.Exec(db, transaction, "DELETE FROM guest_cycles WHERE event_id = @id;", scope);
            Db.Exec(db, transaction, "DELETE FROM event_templates WHERE event_id = @id;", scope);
            Db.Exec(db, transaction, "DELETE FROM event_looks WHERE event_id = @id;", scope);
            Db.Exec(db, transaction, "DELETE FROM event_phase_timers WHERE event_id = @id;", scope);
            Db.Exec(db, transaction, "DELETE FROM events WHERE id = @id;", scope);
            transaction.Commit();
            _crash.Reached(CrashPoint.EventRowsDeleted);
        }).ConfigureAwait(false);
    }

    private async Task DeleteGuestCycleAsync(string guestCycleId)
    {
        var proceed = false;
        await StepAsync(db =>
        {
            var owners = Db.Query(db, null, "SELECT event_id FROM guest_cycles WHERE id = @id AND deleting_at IS NOT NULL;", reader => reader.GetString(0), ("@id", guestCycleId));
            if (owners.Count == 0)
            {
                return;
            }

            var sources = Db.Query(db, null, "SELECT id FROM captures WHERE guest_cycle_id = @id;", reader => reader.GetString(0), ("@id", guestCycleId));
            sources.Add(guestCycleId);
            _fs.DeleteDirectoryRecursive(CycleDirectory(owners[0], guestCycleId));
            DeleteThumbnails(sources);
            _crash.Reached(CrashPoint.CycleFolderDeleted);
            proceed = true;
        }).ConfigureAwait(false);
        if (!proceed)
        {
            return;
        }

        await StepAsync(db =>
        {
            using var transaction = db.BeginTransaction();
            var scope = ("@id", (object?)guestCycleId);
            Db.Exec(db, transaction, "DELETE FROM photo_strips WHERE guest_cycle_id = @id;", scope);
            Db.Exec(db, transaction, "DELETE FROM captures WHERE guest_cycle_id = @id;", scope);
            Db.Exec(db, transaction, "DELETE FROM guest_cycles WHERE id = @id;", scope);
            transaction.Commit();
            _crash.Reached(CrashPoint.CycleRowsDeleted);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// One locked step of a background deletion. A failure ends the job quietly: the rows stay marked deleting, invisible to every
    /// read, and the startup reconciler (plan section 10, step 5) finishes the removal at the next start.
    /// </summary>
    private async Task StepAsync(Action<SqliteConnection> step)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_db is { } db)
            {
                step(db);
            }
        }
#pragma warning disable CA1031 // A background job has no caller; whatever stops it is left for the reconciler.
        catch (Exception)
#pragma warning restore CA1031
        {
            // The rows stay marked deleting.
        }
        finally
        {
            _gate.Release();
        }
    }

    private void DeleteThumbnails(IEnumerable<string> sourceIds)
    {
        var sources = sourceIds.ToHashSet(StringComparer.Ordinal);
        foreach (var file in _fs.EnumerateFiles(ThumbnailDirectory, recursive: false))
        {
            if (ThumbnailName.TryParse(Path.GetFileName(file), out var sourceId, out _, out _) && sources.Contains(sourceId))
            {
                _fs.Delete(file);
            }
        }
    }
}
