namespace FotoHavn.Storage.FileSystem;

/// <summary>
/// The real file system. It never follows a junction or symbolic link: listings skip them, and deletes refuse a path that
/// goes through one, because the operator may have linked a folder under <c>Events/</c> to somewhere that must not be touched.
/// </summary>
/// <param name="guardedRoot">
/// The Data folder. A delete checks every folder between this root and the path (the root itself may be a link, for
/// example a Data folder the operator mapped to another drive). Null checks only the path itself.
/// </param>
public sealed class PhysicalStorageFileSystem(string? guardedRoot = null) : IStorageFileSystem
{
    private readonly string? _rootPrefix = guardedRoot is null
        ? null
        : Path.EndsInDirectorySeparator(Path.GetFullPath(guardedRoot)) ? Path.GetFullPath(guardedRoot) : Path.GetFullPath(guardedRoot) + Path.DirectorySeparatorChar;

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void WriteAllBytesAndFlush(string path, ReadOnlySpan<byte> bytes, Action? afterWrite = null)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        afterWrite?.Invoke();
        stream.Flush(flushToDisk: true);
    }

    public void Move(string source, string destination) => File.Move(source, destination, overwrite: false);

    public void Delete(string path)
    {
        ThrowIfLinked(path, includeSelf: true);
        File.Delete(path);
    }

    public void DeleteDirectoryRecursive(string path)
    {
        ThrowIfLinked(path, includeSelf: false);
        if (IsReparsePoint(path))
        {
            // A non-recursive delete of a link removes the link and leaves its target alone.
            Directory.Delete(path, recursive: false);
        }
        else if (Directory.Exists(path))
        {
            DeleteTree(new DirectoryInfo(path));
        }
    }

    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public IReadOnlyList<string> EnumerateFiles(string directory, bool recursive) =>
        Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, "*", new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
            })]
            : [];

    public IReadOnlyList<string> FindReparsePoints(string directory)
    {
        var found = new List<string>();
        if (Directory.Exists(directory))
        {
            Collect(new DirectoryInfo(directory), found);
        }

        return found;
    }

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public long GetLength(string path) => new FileInfo(path) is { Exists: true } info ? info.Length : throw new FileNotFoundException("The file doesn't exist.", path);

    // Directory.Delete(recursive) fails on Windows when a junction is inside, so the tree is walked here: a link (file or
    // folder) is removed on its own and never entered, everything else is deleted children first.
    private static void DeleteTree(DirectoryInfo directory)
    {
        var options = new EnumerationOptions { IgnoreInaccessible = false, ReturnSpecialDirectories = false, AttributesToSkip = 0 };
        foreach (var entry in directory.EnumerateFileSystemInfos("*", options).ToList())
        {
            if (entry is DirectoryInfo child && !child.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                DeleteTree(child);
            }
            else
            {
                entry.Delete();
            }
        }

        directory.Delete();
    }

    private static void Collect(DirectoryInfo directory, List<string> found)
    {
        var options = new EnumerationOptions { IgnoreInaccessible = true, ReturnSpecialDirectories = false, AttributesToSkip = 0 };
        foreach (var entry in directory.EnumerateFileSystemInfos("*", options))
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                found.Add(entry.FullName);
            }
            else if (entry is DirectoryInfo child)
            {
                Collect(child, found);
            }
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    // Walks from the path up to (not including) the guarded root; a path outside the root is checked on its own.
    private void ThrowIfLinked(string path, bool includeSelf)
    {
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (includeSelf && IsReparsePoint(current))
        {
            throw new ReparsePointException();
        }

        if (_rootPrefix is null || !current.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        for (var parent = Path.GetDirectoryName(current); parent is not null && parent.Length >= _rootPrefix.Length; parent = Path.GetDirectoryName(parent))
        {
            if (IsReparsePoint(parent))
            {
                throw new ReparsePointException();
            }
        }
    }
}
