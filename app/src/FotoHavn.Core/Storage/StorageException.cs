namespace FotoHavn.Core.Storage;

/// <summary>Why a storage operation failed. The brain maps a kind to operator copy; the mapping from exceptions lives in FotoHavn.Storage.</summary>
public enum StorageErrorKind
{
    /// <summary>The disk is full (<c>ERROR_DISK_FULL</c>, <c>SQLITE_FULL</c>).</summary>
    DiskFull,

    /// <summary>Windows or the file system refused access (<c>UnauthorizedAccessException</c>, <c>SQLITE_CANTOPEN</c>).</summary>
    AccessDenied,

    /// <summary>Another process holds the file or database (sharing violation, <c>SQLITE_BUSY</c>, <c>SQLITE_LOCKED</c>).</summary>
    Locked,

    /// <summary>The Data folder is on a network location (startup data-path check).</summary>
    NotLocalDisk,

    /// <summary>The Data folder is under a cloud sync root such as OneDrive (startup data-path check).</summary>
    CloudSynced,

    /// <summary>The Data folder can't be written (startup data-path check, <c>SQLITE_READONLY</c>).</summary>
    NotWritable,

    /// <summary>The SQLite integrity check failed at startup.</summary>
    IntegrityFailed,

    /// <summary>The database file is damaged (<c>SQLITE_CORRUPT</c>, <c>SQLITE_NOTADB</c>).</summary>
    Corrupt,

    /// <summary>The Event, Guest Cycle, Capture, or file asked for doesn't exist.</summary>
    NotFound,

    /// <summary>The request conflicts with stored state (duplicate id, Guest Cycle already ended, constraint violation).</summary>
    Conflict,

    /// <summary>Anything else.</summary>
    Unknown,
}

/// <summary>
/// The only exception an <see cref="IEventStore"/> throws for environment and state failures. The message is brief
/// English without personal data or file paths (it may reach logs); the cause stays in <see cref="Exception.InnerException"/>.
/// Programming errors (a malformed id, an out-of-range argument) throw <see cref="ArgumentException"/> instead.
/// </summary>
public sealed class StorageException : Exception
{
    public StorageException()
        : this(StorageErrorKind.Unknown, "Storage failed.")
    {
    }

    public StorageException(string message)
        : this(StorageErrorKind.Unknown, message)
    {
    }

    public StorageException(string message, Exception? innerException)
        : this(StorageErrorKind.Unknown, message, innerException)
    {
    }

    public StorageException(StorageErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public StorageErrorKind Kind { get; }
}
