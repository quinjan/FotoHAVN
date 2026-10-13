using FotoHavn.Core.Storage;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage;

/// <summary>
/// Maps what the file system and SQLite throw to <see cref="StorageErrorKind"/> (plan section 10), and wraps the cause in
/// a <see cref="StorageException"/> whose message names the kind but never a path or a name.
/// </summary>
public static class StorageErrorMapper
{
    // Win32 error codes in HRESULT form (0x8007xxxx), as IOException.HResult carries them on Windows.
    private const int Win32FileNotFound = unchecked((int)0x80070002);
    private const int Win32PathNotFound = unchecked((int)0x80070003);
    private const int Win32AccessDenied = unchecked((int)0x80070005);
    private const int Win32SharingViolation = unchecked((int)0x80070020);
    private const int Win32LockViolation = unchecked((int)0x80070021);
    private const int Win32HandleDiskFull = unchecked((int)0x80070027);
    private const int Win32DiskFull = unchecked((int)0x80070070);

    // SQLite primary result codes (https://www.sqlite.org/rescode.html).
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;
    private const int SqliteReadOnly = 8;
    private const int SqliteCorrupt = 11;
    private const int SqliteFull = 13;
    private const int SqliteCantOpen = 14;
    private const int SqliteConstraint = 19;
    private const int SqliteNotADatabase = 26;

    /// <summary>The kind for <paramref name="exception"/>; <see cref="StorageErrorKind.Unknown"/> when nothing matches.</summary>
    public static StorageErrorKind Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            StorageException storage => storage.Kind,
            UnauthorizedAccessException => StorageErrorKind.AccessDenied,
            SqliteException sqlite => MapSqlite(sqlite.SqliteErrorCode),
            FileNotFoundException or DirectoryNotFoundException => StorageErrorKind.NotFound,
            IOException io => MapHResult(io.HResult),
            _ => StorageErrorKind.Unknown,
        };
    }

    /// <summary>Returns <paramref name="exception"/> itself when it is already a <see cref="StorageException"/>, otherwise wraps it.</summary>
    public static StorageException ToStorageException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception as StorageException ?? new StorageException(Map(exception), Describe(Map(exception)), exception);
    }

    /// <summary>The brief, non-personal message for a kind.</summary>
    public static string Describe(StorageErrorKind kind) => kind switch
    {
        StorageErrorKind.DiskFull => "The disk is full.",
        StorageErrorKind.AccessDenied => "Access to the Data folder was denied.",
        StorageErrorKind.Locked => "Another program is using the Data folder or its database.",
        StorageErrorKind.NotLocalDisk => "The Data folder isn't on a local disk.",
        StorageErrorKind.CloudSynced => "The Data folder is in a cloud-synced folder.",
        StorageErrorKind.NotWritable => "The Data folder can't be written.",
        StorageErrorKind.IntegrityFailed => "The database failed its integrity check.",
        StorageErrorKind.Corrupt => "The database is damaged.",
        StorageErrorKind.NotFound => "Something storage needs doesn't exist.",
        StorageErrorKind.Conflict => "The request conflicts with what is stored.",
        _ => "Storage failed.",
    };

    private static StorageErrorKind MapHResult(int hResult) => hResult switch
    {
        Win32DiskFull or Win32HandleDiskFull => StorageErrorKind.DiskFull,
        Win32SharingViolation or Win32LockViolation => StorageErrorKind.Locked,
        Win32AccessDenied => StorageErrorKind.AccessDenied,
        Win32FileNotFound or Win32PathNotFound => StorageErrorKind.NotFound,
        _ => StorageErrorKind.Unknown,
    };

    // Microsoft.Data.Sqlite may report an extended code; its low byte is the primary code.
    private static StorageErrorKind MapSqlite(int errorCode) => (errorCode & 0xFF) switch
    {
        SqliteFull => StorageErrorKind.DiskFull,
        SqliteBusy or SqliteLocked => StorageErrorKind.Locked,
        SqliteCorrupt or SqliteNotADatabase => StorageErrorKind.Corrupt,
        SqliteCantOpen => StorageErrorKind.AccessDenied,
        SqliteReadOnly => StorageErrorKind.NotWritable,
        SqliteConstraint => StorageErrorKind.Conflict,
        _ => StorageErrorKind.Unknown,
    };
}
