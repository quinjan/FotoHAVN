namespace FotoHavn.Storage.FileSystem;

/// <summary>The real file system.</summary>
public sealed class PhysicalStorageFileSystem : IStorageFileSystem
{
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void WriteAllBytesAndFlush(string path, ReadOnlySpan<byte> bytes)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    public void Move(string source, string destination) => File.Move(source, destination, overwrite: false);

    public void Delete(string path) => File.Delete(path);

    public void DeleteDirectoryRecursive(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public IReadOnlyList<string> EnumerateFiles(string directory, bool recursive) =>
        Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)]
            : [];

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public long GetLength(string path) => new FileInfo(path) is { Exists: true } info ? info.Length : throw new FileNotFoundException("The file doesn't exist.", path);
}
