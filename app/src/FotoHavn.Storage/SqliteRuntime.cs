using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage;

/// <summary>
/// Placeholder until M4 brings the Event store. Proves the bundled SQLite engine loads.
/// </summary>
public static class SqliteRuntime
{
    public static string GetVersion()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select sqlite_version();";
        return (string)command.ExecuteScalar()!;
    }
}
