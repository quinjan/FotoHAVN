namespace FotoHavn.Storage.FileSystem;

/// <summary>
/// The file operations the Event store and the reconciler need, and nothing else, so tests can inject faults and the
/// crash harness can stop between steps. Paths are absolute. Members are synchronous and thread-safe; callers run them
/// off the brain's executor. Failures surface as the exceptions Windows raises (<see cref="IOException"/> with its
/// HResult, <see cref="UnauthorizedAccessException"/>); <see cref="StorageErrorMapper"/> turns them into storage errors.
/// </summary>
public interface IStorageFileSystem
{
    /// <summary>Creates the directory and any missing parents. Succeeds when it exists.</summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Writes <paramref name="bytes"/> to a new or existing file and forces them to disk before returning (a
    /// <see cref="FileStream"/> opened with write-through, then <c>Flush(true)</c>). The parent directory must exist.
    /// <paramref name="afterWrite"/> runs once the bytes are written and before the flush, so a caller can observe the
    /// point where the file is staged but not yet flushed; it doesn't run when the write fails.
    /// </summary>
    void WriteAllBytesAndFlush(string path, ReadOnlySpan<byte> bytes, Action? afterWrite = null);

    /// <summary>
    /// Renames a file: atomic on one volume, and never overwrites. Throws <see cref="IOException"/> when
    /// <paramref name="destination"/> exists.
    /// </summary>
    void Move(string source, string destination);

    /// <summary>
    /// Deletes a file. Succeeds when it doesn't exist. Throws <see cref="ReparsePointException"/> when the file, or a folder
    /// above it inside the guarded root, is a junction or symbolic link, and deletes nothing.
    /// </summary>
    void Delete(string path);

    /// <summary>
    /// Deletes a directory with everything in it. Succeeds when it doesn't exist. Never follows a junction or symbolic link:
    /// when <paramref name="path"/> itself is one, only the link is removed; when a folder above it inside the guarded root is
    /// one, it throws <see cref="ReparsePointException"/> and deletes nothing.
    /// </summary>
    void DeleteDirectoryRecursive(string path);

    /// <summary>Whether a file or a directory exists at <paramref name="path"/>.</summary>
    bool Exists(string path);

    /// <summary>
    /// The full paths of the files in <paramref name="directory"/> (and below it when <paramref name="recursive"/>), as a
    /// snapshot, so the caller can delete while it walks. Empty when the directory doesn't exist. Junctions and symbolic
    /// links (files and folders) are skipped, never followed.
    /// </summary>
    IReadOnlyList<string> EnumerateFiles(string directory, bool recursive);

    /// <summary>
    /// The full paths of the junctions and symbolic links (files or folders) at or below <paramref name="directory"/>, found
    /// without following them. Empty when the directory doesn't exist.
    /// </summary>
    IReadOnlyList<string> FindReparsePoints(string directory);

    /// <summary>Reads a whole file. Throws <see cref="FileNotFoundException"/> when it doesn't exist.</summary>
    byte[] ReadAllBytes(string path);

    /// <summary>A file's size in bytes. Throws <see cref="FileNotFoundException"/> when it doesn't exist.</summary>
    long GetLength(string path);
}
