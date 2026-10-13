using System.Globalization;
using FotoHavn.Core.Storage;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage.Migrations;

/// <summary>
/// Brings a database to the newest schema: every script above <c>PRAGMA user_version</c> runs in version order, each in its
/// own transaction that also sets <c>user_version</c>, so a failed script leaves the database at the last good version.
/// </summary>
public static class MigrationRunner
{
    /// <summary>Applies <see cref="MigrationCatalog.Embedded"/>.</summary>
    public static int Run(SqliteConnection connection) => Run(connection, MigrationCatalog.Embedded);

    /// <summary>
    /// Applies <paramref name="scripts"/> (versions 1, 2, 3 without gaps) above the database's version and returns the version it
    /// ends at. A database already above the newest script is refused with <see cref="StorageErrorKind.Unknown"/>: downgrades
    /// aren't supported.
    /// </summary>
    /// <exception cref="StorageException">The database is newer than the scripts, or a script failed (its changes are rolled back).</exception>
    public static int Run(SqliteConnection connection, IReadOnlyList<MigrationScript> scripts)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var ordered = MigrationCatalog.Order(scripts);
        var latest = ordered[^1].Version;
        var current = ReadVersion(connection);
        if (current > latest)
        {
            throw new StorageException(StorageErrorKind.Unknown, "The database was made by a newer version of the app.");
        }

        foreach (var script in ordered.Where(script => script.Version > current))
        {
            Apply(connection, script);
        }

        return latest;
    }

    /// <summary>The database's <c>PRAGMA user_version</c>.</summary>
    public static int ReadVersion(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        try
        {
            return Convert.ToInt32(Db.Scalar(connection, null, "PRAGMA user_version;"), CultureInfo.InvariantCulture);
        }
        catch (SqliteException exception)
        {
            throw StorageErrorMapper.ToStorageException(exception);
        }
    }

    private static void Apply(SqliteConnection connection, MigrationScript script)
    {
        try
        {
            using var transaction = connection.BeginTransaction();
            Db.Exec(connection, transaction, script.Sql);
            Db.Exec(connection, transaction, "PRAGMA user_version = " + script.Version.ToString(CultureInfo.InvariantCulture) + ";");
            transaction.Commit();
        }
        catch (SqliteException exception)
        {
            throw StorageErrorMapper.ToStorageException(exception);
        }
    }
}
