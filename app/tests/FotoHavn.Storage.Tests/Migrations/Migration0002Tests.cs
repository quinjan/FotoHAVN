using FotoHavn.Storage.Migrations;
using FotoHavn.Storage.Tests.Support;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage.Tests.Migrations;

/// <summary>Migration 0002 (the Guest sounds column) on a database that 0001 made: saved Events get sounds on, and the column keeps its constraint.</summary>
public sealed class Migration0002Tests : IDisposable
{
    private readonly ScratchFolder _scratch = new();
    private readonly SqliteConnection _connection;

    public Migration0002Tests()
    {
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _scratch.Combine("test.db"), Pooling = false }.ToString());
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
        _scratch.Dispose();
    }

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

    private void InsertEvent(string id, string? column = null, string? value = null) =>
        Execute($"INSERT INTO events (id, name, event_date, default_template_id, default_look_id, created_at, updated_at{(column is null ? string.Empty : ", " + column)}) " +
            $"VALUES ('{id}', 'Name', '2026-10-24', 't', 'l', 'now', 'now'{(value is null ? string.Empty : ", " + value)});");

    [Fact]
    public void It_is_the_second_migration_and_is_named_for_the_setting()
    {
        var script = MigrationCatalog.Embedded[1];

        Assert.Equal((2, "guest_sounds"), (script.Version, script.Name));
    }

    [Fact]
    public void An_Event_saved_before_the_setting_existed_gets_sounds_on()
    {
        _ = MigrationRunner.Run(_connection, [MigrationCatalog.Embedded[0]]);
        InsertEvent("old-1");
        InsertEvent("old-2");

        _ = MigrationRunner.Run(_connection);

        Assert.Equal(MigrationCatalog.LatestVersion, MigrationRunner.ReadVersion(_connection));
        Assert.Equal(2, Scalar("SELECT COUNT(*) FROM events WHERE guest_sounds_enabled = 1;"));
    }

    [Fact]
    public void A_new_Event_defaults_to_sounds_on_and_can_turn_them_off()
    {
        _ = MigrationRunner.Run(_connection);

        InsertEvent("new-1");
        InsertEvent("new-2", "guest_sounds_enabled", "0");

        Assert.Equal(1, Scalar("SELECT guest_sounds_enabled FROM events WHERE id = 'new-1';"));
        Assert.Equal(0, Scalar("SELECT guest_sounds_enabled FROM events WHERE id = 'new-2';"));
    }

    [Fact]
    public void The_column_is_never_null()
    {
        _ = MigrationRunner.Run(_connection);

        Assert.Throws<SqliteException>(() => InsertEvent("bad", "guest_sounds_enabled", "NULL"));
    }
}
