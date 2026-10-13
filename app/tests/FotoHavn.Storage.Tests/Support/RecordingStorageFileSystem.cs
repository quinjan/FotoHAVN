using FotoHavn.Storage.FileSystem;

namespace FotoHavn.Storage.Tests.Support;

/// <summary>One call a <see cref="RecordingStorageFileSystem"/> saw.</summary>
public sealed record FileSystemCall(string Operation, string Path);

/// <summary>
/// Records every call and forwards it. <see cref="Before"/> runs just ahead of the forwarded call, so a test can arm a fault
/// on the next write at an exact point (the fault-injecting file system fails the call that follows).
/// </summary>
public sealed class RecordingStorageFileSystem(IStorageFileSystem inner) : IStorageFileSystem
{
    private readonly object _gate = new();
    private readonly List<FileSystemCall> _calls = [];

    /// <summary>Runs before each forwarded call with the operation name (<c>Move</c>, <c>WriteAllBytesAndFlush</c>, ...) and the path.</summary>
    public Action<string, string>? Before { get; set; }

    /// <summary>
    /// Runs inside <c>WriteAllBytesAndFlush</c> with <c>"written"</c> (bytes written, before the flush, ahead of the caller's
    /// <c>afterWrite</c>) and then <c>"flushed"</c> (the call is about to return), and the path.
    /// </summary>
    public Action<string, string>? WriteStage { get; set; }

    public IReadOnlyList<FileSystemCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public void CreateDirectory(string path) => Forward(nameof(CreateDirectory), path, () => inner.CreateDirectory(path));

    public void WriteAllBytesAndFlush(string path, ReadOnlySpan<byte> bytes, Action? afterWrite = null)
    {
        Record(nameof(WriteAllBytesAndFlush), path);
        inner.WriteAllBytesAndFlush(
            path,
            bytes,
            () =>
            {
                WriteStage?.Invoke("written", path);
                afterWrite?.Invoke();
            });
        WriteStage?.Invoke("flushed", path);
    }

    public void Move(string source, string destination) => Forward(nameof(Move), source, () => inner.Move(source, destination));

    public void Delete(string path) => Forward(nameof(Delete), path, () => inner.Delete(path));

    public void DeleteDirectoryRecursive(string path) => Forward(nameof(DeleteDirectoryRecursive), path, () => inner.DeleteDirectoryRecursive(path));

    public bool Exists(string path) => inner.Exists(path);

    public IReadOnlyList<string> EnumerateFiles(string directory, bool recursive) => inner.EnumerateFiles(directory, recursive);

    public IReadOnlyList<string> FindReparsePoints(string directory) => inner.FindReparsePoints(directory);

    public byte[] ReadAllBytes(string path) => inner.ReadAllBytes(path);

    public long GetLength(string path) => inner.GetLength(path);

    private void Forward(string operation, string path, Action call)
    {
        Record(operation, path);
        call();
    }

    private void Record(string operation, string path)
    {
        lock (_gate)
        {
            _calls.Add(new FileSystemCall(operation, path));
        }

        Before?.Invoke(operation, path);
    }
}
