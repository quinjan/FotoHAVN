using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.FileSystem;

public sealed class FaultInjectingStorageFileSystemTests : IDisposable
{
    private readonly ScratchFolder _folder = new();
    private readonly FaultInjectingStorageFileSystem _fs;

    public FaultInjectingStorageFileSystemTests()
    {
        _fs = new FaultInjectingStorageFileSystem(new PhysicalStorageFileSystem());
    }

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Without_a_fault_it_passes_every_call_through()
    {
        var source = _folder.Combine("a.tmp");
        var destination = _folder.Combine("a.bin");
        var directory = _folder.Combine("d");

        _fs.CreateDirectory(directory);
        _fs.WriteAllBytesAndFlush(source, [1, 2]);
        _fs.Move(source, destination);

        Assert.True(_fs.Exists(destination));
        Assert.Equal([1, 2], _fs.ReadAllBytes(destination));
        Assert.Equal(2, _fs.GetLength(destination));
        Assert.Equal([destination], _fs.EnumerateFiles(_folder.Path, recursive: false));
        _fs.Delete(destination);
        _fs.DeleteDirectoryRecursive(directory);
        Assert.False(_fs.Exists(destination));
        Assert.False(_fs.HasFault);
        Assert.Equal(0, _fs.RaisedCount);
    }

    [Fact]
    public void Disk_full_raises_the_windows_io_exception_and_leaves_no_file()
    {
        var path = _folder.Combine("a.bin");
        _fs.Inject(StorageFaultKind.DiskFull, StorageFaultScope.NextWrite);

        var exception = Assert.Throws<IOException>(() => _fs.WriteAllBytesAndFlush(path, [1]));

        Assert.Equal(unchecked((int)0x80070070), exception.HResult);
        Assert.Equal(FaultInjectingStorageFileSystem.DiskFullHResult, exception.HResult);
        Assert.False(_fs.Exists(path));
        Assert.Equal(StorageErrorKind.DiskFull, StorageErrorMapper.Map(exception));
    }

    [Fact]
    public void Locked_raises_a_sharing_violation()
    {
        _fs.Inject(StorageFaultKind.Locked, StorageFaultScope.NextWrite);

        var exception = Assert.Throws<IOException>(() => _fs.Delete(_folder.Combine("a.bin")));

        Assert.Equal(unchecked((int)0x80070020), exception.HResult);
        Assert.Equal(FaultInjectingStorageFileSystem.SharingViolationHResult, exception.HResult);
        Assert.Equal(StorageErrorKind.Locked, StorageErrorMapper.Map(exception));
    }

    [Fact]
    public void Access_denied_raises_unauthorized_access()
    {
        _fs.Inject(StorageFaultKind.AccessDenied, StorageFaultScope.NextWrite);

        var exception = Assert.Throws<UnauthorizedAccessException>(() => _fs.CreateDirectory(_folder.Combine("d")));

        Assert.Equal(StorageErrorKind.AccessDenied, StorageErrorMapper.Map(exception));
    }

    [Fact]
    public void A_next_write_fault_fires_once_then_clears_itself()
    {
        _fs.Inject(StorageFaultKind.DiskFull, StorageFaultScope.NextWrite);
        Assert.True(_fs.HasFault);

        Assert.Throws<IOException>(() => _fs.WriteAllBytesAndFlush(_folder.Combine("a.bin"), [1]));
        _fs.WriteAllBytesAndFlush(_folder.Combine("a.bin"), [1]);

        Assert.False(_fs.HasFault);
        Assert.Equal(1, _fs.RaisedCount);
    }

    [Fact]
    public void An_all_writes_fault_fires_on_every_write_kind_until_cleared()
    {
        var file = _folder.Combine("a.bin");
        _fs.WriteAllBytesAndFlush(file, [1]);
        _fs.Inject(StorageFaultKind.DiskFull, StorageFaultScope.AllWrites);

        Assert.Throws<IOException>(() => _fs.CreateDirectory(_folder.Combine("d")));
        Assert.Throws<IOException>(() => _fs.WriteAllBytesAndFlush(_folder.Combine("b.bin"), [1]));
        Assert.Throws<IOException>(() => _fs.Move(file, _folder.Combine("c.bin")));
        Assert.Throws<IOException>(() => _fs.Delete(file));
        Assert.Throws<IOException>(() => _fs.DeleteDirectoryRecursive(_folder.Path));
        Assert.True(_fs.HasFault);
        Assert.Equal(5, _fs.RaisedCount);

        _fs.Clear();
        _fs.Delete(file);

        Assert.False(_fs.HasFault);
        Assert.False(_fs.Exists(file));
    }

    [Fact]
    public void Reads_are_never_faulted()
    {
        var file = _folder.Combine("a.bin");
        _fs.WriteAllBytesAndFlush(file, [1, 2, 3]);
        _fs.Inject(StorageFaultKind.AccessDenied, StorageFaultScope.AllWrites);

        Assert.True(_fs.Exists(file));
        Assert.Equal([1, 2, 3], _fs.ReadAllBytes(file));
        Assert.Equal(3, _fs.GetLength(file));
        Assert.Single(_fs.EnumerateFiles(_folder.Path, recursive: true));
        Assert.Equal(0, _fs.RaisedCount);
    }

    [Fact]
    public void Injecting_again_replaces_the_fault()
    {
        _fs.Inject(StorageFaultKind.DiskFull, StorageFaultScope.AllWrites);
        _fs.Inject(StorageFaultKind.AccessDenied, StorageFaultScope.NextWrite);

        Assert.Throws<UnauthorizedAccessException>(() => _fs.CreateDirectory(_folder.Combine("d")));
        _fs.CreateDirectory(_folder.Combine("d"));
    }

    [Fact]
    public void Inject_refuses_unknown_kinds_and_scopes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _fs.Inject((StorageFaultKind)9, StorageFaultScope.NextWrite));
        Assert.Throws<ArgumentOutOfRangeException>(() => _fs.Inject(StorageFaultKind.DiskFull, (StorageFaultScope)9));
    }
}
