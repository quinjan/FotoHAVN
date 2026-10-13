using FotoHavn.Core.Storage;
using FotoHavn.Storage.Migrations;
using FotoHavn.Storage.Tests.Support;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage.Tests.Migrations;

public sealed class MigrationRunnerTests : IDisposable
{
    private readonly ScratchFolder _scratch = new();
    private readonly List<SqliteConnection> _connections = [];

    public void Dispose()
    {
        foreach (var connection in _connections)
        {
            connection.Dispose();
        }

        _scratch.Dispose();
    }

    private SqliteConnection Open(string name = "test.db")
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _scratch.Combine(name), Pooling = false }.ToString());
        connection.Open();
        _connections.Add(connection);
        return connection;
    }

    private static T Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static List<string> Schema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT type || ':' || name || ':' || COALESCE(sql, '') FROM sqlite_master ORDER BY type, name;";
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private static MigrationScript Script(int version, string sql) => new(version, "step" + version, sql);

    [Fact]
    public void An_empty_database_is_brought_to_the_latest_version()
    {
        var connection = Open();

        var version = MigrationRunner.Run(connection);

        Assert.Equal(MigrationCatalog.LatestVersion, version);
        Assert.Equal(MigrationCatalog.LatestVersion, MigrationRunner.ReadVersion(connection));
        Assert.Equal(1, Scalar<int>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'events';"));
    }

    [Fact]
    public void Every_earlier_version_is_brought_to_the_same_schema_as_a_fresh_database()
    {
        var fresh = Open("fresh.db");
        _ = MigrationRunner.Run(fresh);
        var expected = Schema(fresh);

        for (var from = 0; from < MigrationCatalog.LatestVersion; from++)
        {
            var connection = Open($"from-{from}.db");
            if (from > 0)
            {
                _ = MigrationRunner.Run(connection, [.. MigrationCatalog.Embedded.Take(from)]);
                Assert.Equal(from, MigrationRunner.ReadVersion(connection));
            }

            var version = MigrationRunner.Run(connection);

            Assert.Equal(MigrationCatalog.LatestVersion, version);
            Assert.Equal(MigrationCatalog.LatestVersion, MigrationRunner.ReadVersion(connection));
            Assert.Equal(expected, Schema(connection));
        }
    }

    [Fact]
    public void Running_again_changes_nothing()
    {
        var connection = Open();
        _ = MigrationRunner.Run(connection);
        var schema = Schema(connection);

        var version = MigrationRunner.Run(connection);

        Assert.Equal(MigrationCatalog.LatestVersion, version);
        Assert.Equal(schema, Schema(connection));
    }

    [Fact]
    public void A_database_newer_than_the_app_is_refused_and_left_alone()
    {
        var connection = Open();
        _ = MigrationRunner.Run(connection);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"PRAGMA user_version = {MigrationCatalog.LatestVersion + 1};";
            command.ExecuteNonQuery();
        }

        var failure = Assert.Throws<StorageException>(() => MigrationRunner.Run(connection));

        Assert.Equal(StorageErrorKind.Unknown, failure.Kind);
        Assert.Equal(MigrationCatalog.LatestVersion + 1, MigrationRunner.ReadVersion(connection));
    }

    [Fact]
    public void Each_script_applies_in_order_and_sets_its_own_version()
    {
        var connection = Open();
        MigrationScript[] scripts =
        [
            Script(1, "CREATE TABLE a (x INTEGER);"),
            Script(2, "CREATE TABLE b (x INTEGER); INSERT INTO a VALUES (1);"),
            Script(3, "ALTER TABLE b ADD COLUMN y INTEGER;"),
        ];

        Assert.Equal(3, MigrationRunner.Run(connection, scripts));

        Assert.Equal(3, MigrationRunner.ReadVersion(connection));
        Assert.Equal(1, Scalar<int>(connection, "SELECT COUNT(*) FROM a;"));
    }

    [Fact]
    public void A_failing_script_rolls_back_and_leaves_the_version_unchanged()
    {
        var connection = Open();
        MigrationScript[] good = [Script(1, "CREATE TABLE a (x INTEGER);")];
        _ = MigrationRunner.Run(connection, good);
        MigrationScript[] withBad = [good[0], Script(2, "CREATE TABLE b (x INTEGER); CREATE TABLE a (x INTEGER);"), Script(3, "CREATE TABLE c (x INTEGER);")];

        var failure = Assert.Throws<StorageException>(() => MigrationRunner.Run(connection, withBad));

        Assert.Equal(StorageErrorKind.Unknown, failure.Kind);
        Assert.IsType<SqliteException>(failure.InnerException);
        Assert.Equal(1, MigrationRunner.ReadVersion(connection));
        Assert.Equal(0, Scalar<int>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('b', 'c');"));
    }

    [Fact]
    public void A_script_that_fails_leaves_earlier_scripts_applied()
    {
        var connection = Open();
        MigrationScript[] scripts = [Script(1, "CREATE TABLE a (x INTEGER);"), Script(2, "NOT SQL;")];

        _ = Assert.Throws<StorageException>(() => MigrationRunner.Run(connection, scripts));

        Assert.Equal(1, MigrationRunner.ReadVersion(connection));
        Assert.Equal(1, Scalar<int>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'a';"));
    }

    [Fact]
    public void Scripts_with_a_gap_are_refused()
    {
        var connection = Open();

        _ = Assert.Throws<InvalidOperationException>(() => MigrationRunner.Run(connection, [Script(1, "SELECT 1;"), Script(3, "SELECT 1;")]));
    }

    [Fact]
    public void Arguments_are_required()
    {
        _ = Assert.Throws<ArgumentNullException>(() => MigrationRunner.Run(null!));
        _ = Assert.Throws<ArgumentNullException>(() => MigrationRunner.ReadVersion(null!));
    }

    [Fact]
    public void A_file_that_is_not_a_database_reports_corrupt()
    {
        var path = _scratch.Combine("garbage.db");
        File.WriteAllText(path, new string('x', 4096));
        var connection = Open("garbage.db");

        var failure = Assert.Throws<StorageException>(() => MigrationRunner.ReadVersion(connection));

        Assert.Equal(StorageErrorKind.Corrupt, failure.Kind);
    }
}
