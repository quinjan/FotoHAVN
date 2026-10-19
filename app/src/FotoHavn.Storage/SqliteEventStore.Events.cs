using FotoHavn.Core.Storage;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage;

public sealed partial class SqliteEventStore
{
    private const string EventColumns =
        "id, name, event_date, venue, customer_name, notes, camera_device_id, camera_name, guest_display_id, guest_display_name, " +
        "printer, mirror, countdown_seconds, default_template_id, default_look_id, guest_gallery_enabled, guest_sounds_enabled, created_at, updated_at, pin_hash IS NOT NULL";

    private static readonly string[] ChildTables = ["event_templates", "event_looks", "event_phase_timers"];

    public Task<IReadOnlyList<EventSummary>> ListEventsAsync(CancellationToken ct) =>
        RunAsync<IReadOnlyList<EventSummary>>(ct, db => Db.Query(
            db,
            null,
            """
            SELECT e.id, e.name, e.event_date, e.venue,
                   (SELECT COUNT(*) FROM guest_cycles g WHERE g.event_id = e.id AND g.deleting_at IS NULL AND g.end_reason = 'completed'),
                   (SELECT MAX(g.started_at) FROM guest_cycles g WHERE g.event_id = e.id AND g.deleting_at IS NULL)
            FROM events e
            WHERE e.deleting_at IS NULL
            ORDER BY e.event_date DESC, e.created_at DESC, e.rowid DESC;
            """,
            reader => new EventSummary(
                reader.GetString(0),
                reader.GetString(1),
                StorageTimestamp.ParseDate(reader.GetString(2)),
                reader.NullableString(3),
                reader.GetInt32(4),
                reader.IsDBNull(5) ? null : StorageTimestamp.Parse(reader.GetString(5)))));

    public Task<EventRecord?> GetEventAsync(string eventId, CancellationToken ct) =>
        RunAsync<EventRecord?>(ct, db =>
        {
            StorageIds.ThrowIfInvalid(eventId, nameof(eventId));
            return ReadEvent(db, eventId);
        });

    public Task CreateEventAsync(EventRecord record, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageValidation.ValidateEvent(record);
            var now = StorageTimestamp.Format(_clock.UtcNow);
            using var transaction = db.BeginTransaction();
            Db.Exec(
                db,
                transaction,
                """
                INSERT INTO events (id, name, event_date, venue, customer_name, notes, camera_device_id, camera_name, guest_display_id, guest_display_name,
                                    printer, mirror, countdown_seconds, default_template_id, default_look_id, guest_gallery_enabled, guest_sounds_enabled, created_at, updated_at)
                VALUES (@id, @name, @date, @venue, @customer, @notes, @cameraId, @cameraName, @displayId, @displayName,
                        @printer, @mirror, @countdown, @template, @look, @gallery, @sounds, @now, @now);
                """,
                [.. EventParameters(record), ("@now", now)]);
            InsertChildren(db, transaction, record);
            transaction.Commit();
        });

    public Task UpdateEventAsync(EventRecord record, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageValidation.ValidateEvent(record);
            using var transaction = db.BeginTransaction();
            var changed = Db.Exec(
                db,
                transaction,
                """
                UPDATE events SET name = @name, event_date = @date, venue = @venue, customer_name = @customer, notes = @notes,
                       camera_device_id = @cameraId, camera_name = @cameraName, guest_display_id = @displayId, guest_display_name = @displayName,
                       printer = @printer, mirror = @mirror, countdown_seconds = @countdown, default_template_id = @template,
                       default_look_id = @look, guest_gallery_enabled = @gallery, guest_sounds_enabled = @sounds, updated_at = @now
                WHERE id = @id AND deleting_at IS NULL;
                """,
                [.. EventParameters(record), ("@now", StorageTimestamp.Format(_clock.UtcNow))]);
            if (changed == 0)
            {
                throw NotFound("The Event doesn't exist.");
            }

            foreach (var table in ChildTables)
            {
                Db.Exec(db, transaction, $"DELETE FROM {table} WHERE event_id = @id;", ("@id", record.Id));
            }

            InsertChildren(db, transaction, record);
            transaction.Commit();
        });

    public Task SetPinAsync(string eventId, string pin, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageValidation.ValidatePin(eventId, pin);
            RequireEvent(db, eventId);
            var credential = _hasher.Create(pin);
            Db.Exec(
                db,
                null,
                "UPDATE events SET pin_hash = @hash, pin_salt = @salt, pin_iterations = @iterations WHERE id = @id;",
                ("@hash", credential.Hash),
                ("@salt", credential.Salt),
                ("@iterations", credential.Iterations),
                ("@id", eventId));
        });

    public Task<bool> VerifyPinAsync(string eventId, string pin, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageValidation.ValidatePin(eventId, pin);
            var rows = Db.Query(
                db,
                null,
                "SELECT pin_hash, pin_salt, pin_iterations FROM events WHERE id = @id AND deleting_at IS NULL;",
                reader => reader.IsDBNull(0)
                    ? null
                    : new PinCredential(reader.GetFieldValue<byte[]>(0), reader.GetFieldValue<byte[]>(1), reader.GetInt32(2)),
                ("@id", eventId));
            return rows.Count == 0
                ? throw NotFound("The Event doesn't exist.")
                : rows[0] is { } credential && _hasher.Verify(pin, credential);
        });

    private static (string Name, object? Value)[] EventParameters(EventRecord record) =>
    [
        ("@id", record.Id),
        ("@name", record.Name),
        ("@date", StorageTimestamp.FormatDate(record.EventDate)),
        ("@venue", record.Venue),
        ("@customer", record.CustomerName),
        ("@notes", record.Notes),
        ("@cameraId", record.CameraDeviceId),
        ("@cameraName", record.CameraName),
        ("@displayId", record.GuestDisplayId),
        ("@displayName", record.GuestDisplayName),
        ("@printer", record.Printer),
        ("@mirror", record.Mirror),
        ("@countdown", record.CountdownSeconds),
        ("@template", record.DefaultTemplateId),
        ("@look", record.DefaultLookId),
        ("@gallery", record.GuestGalleryEnabled),
        ("@sounds", record.GuestSoundsEnabled),
    ];

    private static void InsertChildren(SqliteConnection db, SqliteTransaction transaction, EventRecord record)
    {
        foreach (var templateId in record.OfferedTemplateIds)
        {
            Db.Exec(db, transaction, "INSERT INTO event_templates (event_id, template_id) VALUES (@id, @value);", ("@id", record.Id), ("@value", templateId));
        }

        foreach (var lookId in record.OfferedLookIds)
        {
            Db.Exec(db, transaction, "INSERT INTO event_looks (event_id, look_id) VALUES (@id, @value);", ("@id", record.Id), ("@value", lookId));
        }

        foreach (var timer in record.PhaseTimers)
        {
            Db.Exec(
                db,
                transaction,
                "INSERT INTO event_phase_timers (event_id, phase, seconds) VALUES (@id, @phase, @seconds);",
                ("@id", record.Id),
                ("@phase", StorageSqlNames.ToSql(timer.Phase)),
                ("@seconds", timer.Seconds));
        }
    }

    private static EventRecord? ReadEvent(SqliteConnection db, string eventId)
    {
        var rows = Db.Query(
            db,
            null,
            $"SELECT {EventColumns} FROM events WHERE id = @id AND deleting_at IS NULL;",
            reader => new EventRecord(
                reader.GetString(0),
                reader.GetString(1),
                StorageTimestamp.ParseDate(reader.GetString(2)),
                reader.NullableString(3),
                reader.NullableString(4),
                reader.NullableString(5),
                reader.NullableString(6),
                reader.NullableString(7),
                reader.NullableString(8),
                reader.NullableString(9),
                reader.GetString(10),
                reader.GetBoolean(11),
                reader.GetInt32(12),
                reader.GetString(13),
                reader.GetString(14),
                reader.GetBoolean(15),
                reader.GetBoolean(16),
                [],
                [],
                [])
            {
                CreatedAt = StorageTimestamp.Parse(reader.GetString(17)),
                UpdatedAt = StorageTimestamp.Parse(reader.GetString(18)),
                HasPin = reader.GetBoolean(19),
            },
            ("@id", eventId));
        if (rows.Count == 0)
        {
            return null;
        }

        return rows[0] with
        {
            OfferedTemplateIds = Db.Query(db, null, "SELECT template_id FROM event_templates WHERE event_id = @id ORDER BY rowid;", reader => reader.GetString(0), ("@id", eventId)),
            OfferedLookIds = Db.Query(db, null, "SELECT look_id FROM event_looks WHERE event_id = @id ORDER BY rowid;", reader => reader.GetString(0), ("@id", eventId)),
            PhaseTimers = Db.Query(
                db,
                null,
                "SELECT phase, seconds FROM event_phase_timers WHERE event_id = @id ORDER BY rowid;",
                reader => new PhaseTimerSetting(StorageSqlNames.PhaseFromSql(reader.GetString(0)), reader.GetInt32(1)),
                ("@id", eventId)),
        };
    }

    /// <summary>Throws NotFound unless the Event exists and isn't deleting.</summary>
    private static void RequireEvent(SqliteConnection db, string eventId)
    {
        if (Db.Scalar(db, null, "SELECT 1 FROM events WHERE id = @id AND deleting_at IS NULL;", ("@id", eventId)) is null)
        {
            throw NotFound("The Event doesn't exist.");
        }
    }
}
