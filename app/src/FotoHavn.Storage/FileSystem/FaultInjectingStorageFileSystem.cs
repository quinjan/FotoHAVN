namespace FotoHavn.Storage.FileSystem;

/// <summary>The failures the fault-injecting file system raises (plan section 14, <c>test.injectStorageFault</c>).</summary>
public enum StorageFaultKind
{
    /// <summary>An <see cref="IOException"/> with HResult <c>0x80070070</c> (<c>ERROR_DISK_FULL</c>).</summary>
    DiskFull,

    /// <summary>An <see cref="UnauthorizedAccessException"/>.</summary>
    AccessDenied,

    /// <summary>An <see cref="IOException"/> with HResult <c>0x80070020</c> (<c>ERROR_SHARING_VIOLATION</c>).</summary>
    Locked,
}

/// <summary>How long an injected fault lasts.</summary>
public enum StorageFaultScope
{
    /// <summary>The next write operation fails, then the fault clears itself.</summary>
    NextWrite,

    /// <summary>Every write operation fails until <see cref="FaultInjectingStorageFileSystem.Clear"/>.</summary>
    AllWrites,
}

/// <summary>
/// Wraps a file system and fails its write operations (<see cref="IStorageFileSystem.CreateDirectory"/>,
/// <see cref="IStorageFileSystem.WriteAllBytesAndFlush"/>, <see cref="IStorageFileSystem.Move"/>,
/// <see cref="IStorageFileSystem.Delete"/>, <see cref="IStorageFileSystem.DeleteDirectoryRecursive"/>) with the exceptions
/// Windows raises. The failure happens before the inner call, so a failed write leaves nothing behind. Reads are never
/// faulted. One fault is active at a time; injecting again replaces it. Thread-safe.
/// </summary>
public sealed class FaultInjectingStorageFileSystem(IStorageFileSystem inner) : IStorageFileSystem
{
    /// <summary>The HResult of <c>ERROR_DISK_FULL</c> (<c>HRESULT_FROM_WIN32(112)</c>).</summary>
    public const int DiskFullHResult = unchecked((int)0x80070070);

    /// <summary>The HResult of <c>ERROR_SHARING_VIOLATION</c> (<c>HRESULT_FROM_WIN32(32)</c>).</summary>
    public const int SharingViolationHResult = unchecked((int)0x80070020);

    private readonly object _gate = new();
    private (StorageFaultKind Kind, StorageFaultScope Scope)? _fault;
    private int _raised;

    /// <summary>How many failures this file system has raised since it was created.</summary>
    public int RaisedCount
    {
        get
        {
            lock (_gate)
            {
                return _raised;
            }
        }
    }

    /// <summary>Whether a fault is waiting to fire.</summary>
    public bool HasFault
    {
        get
        {
            lock (_gate)
            {
                return _fault is not null;
            }
        }
    }

    public void Inject(StorageFaultKind kind, StorageFaultScope scope)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a fault kind.");
        }

        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a fault scope.");
        }

        lock (_gate)
        {
            _fault = (kind, scope);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _fault = null;
        }
    }

    public void CreateDirectory(string path)
    {
        ThrowIfFaulted();
        inner.CreateDirectory(path);
    }

    public void WriteAllBytesAndFlush(string path, ReadOnlySpan<byte> bytes, Action? afterWrite = null)
    {
        ThrowIfFaulted();
        inner.WriteAllBytesAndFlush(path, bytes, afterWrite);
    }

    public void Move(string source, string destination)
    {
        ThrowIfFaulted();
        inner.Move(source, destination);
    }

    public void Delete(string path)
    {
        ThrowIfFaulted();
        inner.Delete(path);
    }

    public void DeleteDirectoryRecursive(string path)
    {
        ThrowIfFaulted();
        inner.DeleteDirectoryRecursive(path);
    }

    public bool Exists(string path) => inner.Exists(path);

    public IReadOnlyList<string> EnumerateFiles(string directory, bool recursive) => inner.EnumerateFiles(directory, recursive);

    public IReadOnlyList<string> FindReparsePoints(string directory) => inner.FindReparsePoints(directory);

    public byte[] ReadAllBytes(string path) => inner.ReadAllBytes(path);

    public long GetLength(string path) => inner.GetLength(path);

    private void ThrowIfFaulted()
    {
        StorageFaultKind kind;
        lock (_gate)
        {
            if (_fault is not { } fault)
            {
                return;
            }

            kind = fault.Kind;
            if (fault.Scope == StorageFaultScope.NextWrite)
            {
                _fault = null;
            }

            _raised++;
        }

        throw kind switch
        {
            StorageFaultKind.DiskFull => new IOException("There is not enough space on the disk.", DiskFullHResult),
            StorageFaultKind.Locked => new IOException("The process cannot access the file because it is being used by another process.", SharingViolationHResult),
            _ => new UnauthorizedAccessException("Access to the path is denied."),
        };
    }
}
