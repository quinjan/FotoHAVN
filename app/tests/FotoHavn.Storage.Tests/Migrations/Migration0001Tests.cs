using FotoHavn.Storage.Migrations;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage.Tests.Migrations;

/// <summary>Migration 0001 against a real in-memory SQLite database: it applies, and each constraint of the plan's schema holds.</summary>
public sealed class Migration0001Tests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public Migration0001Tests()
    {
        _connection.Open();
        Execute("PRAGMA foreign_keys = ON;");
        Execute(MigrationCatalog.Embedded[0].Sql);
    }

    public void Dispose() => _connection.Dispose();

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private long Scalar(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    private void InsertEvent(string id = "e1", string extra = "")
    {
        Execute($"INSERT INTO events (id, name, event_date, default_template_id, default_look_id, created_at, updated_at{(extra.Length > 0 ? ", " + extra.Split('=')[0] : string.Empty)}) " +
            $"VALUES ('{id}', 'Name', '2026-10-24', 't', 'l', 'now', 'now'{(extra.Length > 0 ? ", " + extra.Split('=')[1] : string.Empty)});");
    }

    private void InsertCycle(string id = "c1", string eventId = "e1") =>
        Execute($"INSERT INTO guest_cycles (id, event_id, started_at) VALUES ('{id}', '{eventId}', 'now');");

    private void InsertCapture(string id, string cycleId, int sequence, string state = "kept") =>
        Execute($"INSERT INTO captures (id, guest_cycle_id, sequence, file_name, byte_length, sha256, width, height, mirrored, taken_at, state) VALUES ('{id}', '{cycleId}', {sequence}, 'f', 1, 'h', 1, 1, 0, 'now', '{state}');");

    [Fact]
    public void It_creates_the_plan_tables_and_the_query_indexes()
    {
        string[] tables = ["events", "event_templates", "event_looks", "event_phase_timers", "guest_cycles", "captures", "photo_strips"];

        foreach (var table in tables)
        {
            Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}';"));
        }

        foreach (var index in new[] { "ix_events_list", "ix_guest_cycles_paging", "ix_captures_guest_cycle" })
        {
            Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = '{index}';"));
        }
    }

    [Fact]
    public void An_event_gets_the_documented_defaults()
    {
        InsertEvent();

        Assert.Equal(1, Scalar("SELECT COUNT(*) FROM events WHERE printer = 'none' AND mirror = 1 AND countdown_seconds = 5 AND guest_gallery_enabled = 0 AND deleting_at IS NULL;"));
    }

    [Theory]
    [InlineData("printer='dnp-ds620'")]
    [InlineData("countdown_seconds=4")]
    [InlineData("countdown_seconds=0")]
    public void The_printer_and_countdown_checks_hold(string extra)
    {
        Assert.Throws<SqliteException>(() => InsertEvent(extra: extra));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void The_allowed_countdowns_are_accepted(int seconds)
    {
        InsertEvent(extra: $"countdown_seconds={seconds}");

        Assert.Equal(1, Scalar($"SELECT COUNT(*) FROM events WHERE countdown_seconds = {seconds};"));
    }

    [Fact]
    public void A_capture_sequence_below_one_is_refused()
    {
        InsertEvent();
        InsertCycle();

        Assert.Throws<SqliteException>(() => InsertCapture("k0", "c1", 0));
        Assert.Throws<SqliteException>(() => InsertCapture("k-1", "c1", -1));
        InsertCapture("k1", "c1", 1);
    }

    [Fact]
    public void A_capture_sequence_is_unique_within_its_cycle_only()
    {
        InsertEvent();
        InsertCycle("c1");
        InsertCycle("c2");
        InsertCapture("k1", "c1", 1);

        Assert.Throws<SqliteException>(() => InsertCapture("k2", "c1", 1));
        InsertCapture("k3", "c2", 1);
    }

    [Fact]
    public void Capture_state_and_end_reason_checks_hold()
    {
        InsertEvent();
        InsertCycle();

        Assert.Throws<SqliteException>(() => InsertCapture("k1", "c1", 1, "lost"));
        Assert.Throws<SqliteException>(() => Execute("UPDATE guest_cycles SET end_reason = 'finished' WHERE id = 'c1';"));
        foreach (var reason in new[] { "completed", "started_over", "operator_exit", "app_closed", "unrecoverable" })
        {
            Execute($"UPDATE guest_cycles SET end_reason = '{reason}' WHERE id = 'c1';");
        }
    }

    [Fact]
    public void Phase_timer_checks_hold()
    {
        InsertEvent();

        Assert.Throws<SqliteException>(() => Execute("INSERT INTO event_phase_timers VALUES ('e1', 'review', 10);"));
        Assert.Throws<SqliteException>(() => Execute("INSERT INTO event_phase_timers VALUES ('e1', 'filters', -1);"));
        foreach (var phase in new[] { "template", "photographs_capture", "photographs_review", "filters", "keepsake", "guest_gallery" })
        {
            Execute($"INSERT INTO event_phase_timers VALUES ('e1', '{phase}', 0);");
        }

        Assert.Throws<SqliteException>(() => Execute("INSERT INTO event_phase_timers VALUES ('e1', 'filters', 5);"));
    }

    [Fact]
    public void Foreign_keys_are_enforced()
    {
        Assert.Throws<SqliteException>(() => InsertCycle("c1", "missing"));
        Assert.Throws<SqliteException>(() => Execute("INSERT INTO event_templates VALUES ('missing', 't');"));
        Assert.Throws<SqliteException>(() => Execute("INSERT INTO event_looks VALUES ('missing', 'l');"));
        InsertEvent();
        InsertCycle();
        Assert.Throws<SqliteException>(() => InsertCapture("k1", "missing", 1));
        Assert.Throws<SqliteException>(() => Execute("INSERT INTO photo_strips VALUES ('missing', 't', 'l', '[]', 'photo-strip.png', 1, 'h', 1, 1, 'now');"));
        Execute("INSERT INTO photo_strips VALUES ('c1', 't', 'l', '[]', 'photo-strip.png', 1, 'h', 1, 1, 'now');");
        Assert.Throws<SqliteException>(() => Execute("INSERT INTO photo_strips VALUES ('c1', 't', 'l', '[]', 'photo-strip.png', 1, 'h', 1, 1, 'now');"));
    }

    [Fact]
    public void Offered_templates_and_looks_are_unique_per_event()
    {
        InsertEvent();
        Execute("INSERT INTO event_templates VALUES ('e1', 't');");
        Execute("INSERT INTO event_looks VALUES ('e1', 'l');");

        Assert.Throws<SqliteException>(() => Execute("INSERT INTO event_templates VALUES ('e1', 't');"));
        Assert.Throws<SqliteException>(() => Execute("INSERT INTO event_looks VALUES ('e1', 'l');"));
    }

    [Fact]
    public void Required_event_columns_are_not_null()
    {
        Assert.Throws<SqliteException>(() => Execute("INSERT INTO events (id, name, event_date, default_template_id, default_look_id, created_at, updated_at) VALUES ('e1', NULL, 'd', 't', 'l', 'n', 'n');"));
    }
}
