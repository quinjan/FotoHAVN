using FotoHavn.Storage.FileSystem;

namespace FotoHavn.Storage.Tests.Reconciliation;

/// <summary>The crash the tests simulate: raised before the next write reaches the disk.</summary>
public sealed class SimulatedCrashException() : Exception("Simulated crash.");

/// <summary>
/// Wraps a file system, records every call, and can "crash" (throw <see cref="SimulatedCrashException"/>) at the next write
/// after <see cref="CrashAfterWrites"/> writes went through. A write is <c>CreateDirectory</c>, <c>Delete</c>,
/// <c>DeleteDirectoryRecursive</c>, <c>Move</c>, or <c>WriteAllBytesAndFlush</c>.
/// </summary>
public sealed class ScriptedFileSystem(IStorageFileSystem inner) : IStorageFileSystem
{
    private readonly List<(string Operation, string Path)> _operations = [];

    /// <summary>When set, the write after this many successful writes throws.</summary>
    public int? CrashAfterWrites { get; set; }

    public int WriteCount { get; private set; }

    public IReadOnlyList<(string Operation, string Path)> Operations => _operations;

    /// <summary>Called after each recorded operation (for example to cancel a token mid-run).</summary>
    public Action<string, string>? OnOperation { get; set; }

    public void CreateDirectory(string path)
    {
        Write(nameof(CreateDirectory), path);
        inner.CreateDirectory(path);
    }

    public void WriteAllBytesAndFlush(string path, ReadOnlySpan<byte> bytes)
    {
        Write(nameof(WriteAllBytesAndFlush), path);
        inner.WriteAllBytesAndFlush(path, bytes);
    }

    public void Move(string source, string destination)
    {
        Write(nameof(Move), source);
        inner.Move(source, destination);
    }

    public void Delete(string path)
    {
        Write(nameof(Delete), path);
        inner.Delete(path);
    }

    public void DeleteDirectoryRecursive(string path)
    {
        Write(nameof(DeleteDirectoryRecursive), path);
        inner.DeleteDirectoryRecursive(path);
    }

    public bool Exists(string path)
    {
        Record(nameof(Exists), path);
        return inner.Exists(path);
    }

    public IReadOnlyList<string> EnumerateFiles(string directory, bool recursive)
    {
        Record(nameof(EnumerateFiles), directory);
        return inner.EnumerateFiles(directory, recursive);
    }

    public byte[] ReadAllBytes(string path)
    {
        Record(nameof(ReadAllBytes), path);
        return inner.ReadAllBytes(path);
    }

    public long GetLength(string path)
    {
        Record(nameof(GetLength), path);
        return inner.GetLength(path);
    }

    private void Write(string operation, string path)
    {
        if (CrashAfterWrites is { } limit && WriteCount >= limit)
        {
            throw new SimulatedCrashException();
        }

        WriteCount++;
        Record(operation, path);
    }

    private void Record(string operation, string path)
    {
        _operations.Add((operation, path));
        OnOperation?.Invoke(operation, path);
    }
}
