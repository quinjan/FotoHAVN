using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.FileSystem;

public sealed class PhysicalStorageFileSystemTests : IDisposable
{
    private readonly ScratchFolder _folder = new();
    private readonly PhysicalStorageFileSystem _fs = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Write_creates_the_file_with_the_bytes_and_overwrites_an_existing_one()
    {
        var path = _folder.Combine("a.bin");

        _fs.WriteAllBytesAndFlush(path, [1, 2, 3]);
        _fs.WriteAllBytesAndFlush(path, [9, 8]);

        Assert.Equal([9, 8], _fs.ReadAllBytes(path));
        Assert.Equal(2, _fs.GetLength(path));
    }

    [Fact]
    public void Write_needs_the_parent_directory()
    {
        Assert.Throws<DirectoryNotFoundException>(() => _fs.WriteAllBytesAndFlush(_folder.Combine("missing", "a.bin"), [1]));
    }

    [Fact]
    public void CreateDirectory_makes_parents_and_succeeds_when_it_exists()
    {
        var path = _folder.Combine("Events", "e1", "GuestCycles", "c1");

        _fs.CreateDirectory(path);
        _fs.CreateDirectory(path);

        Assert.True(Directory.Exists(path));
        Assert.True(_fs.Exists(path));
    }

    [Fact]
    public void Move_renames_and_never_overwrites()
    {
        var source = _folder.Combine("staged.tmp");
        var destination = _folder.Combine("capture-1.jpg");
        _fs.WriteAllBytesAndFlush(source, [1]);

        _fs.Move(source, destination);

        Assert.False(_fs.Exists(source));
        Assert.Equal([1], _fs.ReadAllBytes(destination));
        _fs.WriteAllBytesAndFlush(source, [2]);
        Assert.Throws<IOException>(() => _fs.Move(source, destination));
        Assert.Equal([1], _fs.ReadAllBytes(destination));
        Assert.True(_fs.Exists(source));
    }

    [Fact]
    public void Delete_removes_a_file_and_ignores_a_missing_one()
    {
        var path = _folder.Combine("a.bin");
        _fs.WriteAllBytesAndFlush(path, [1]);

        _fs.Delete(path);
        _fs.Delete(path);

        Assert.False(_fs.Exists(path));
    }

    [Fact]
    public void DeleteDirectoryRecursive_removes_everything_and_ignores_a_missing_directory()
    {
        var root = _folder.Combine("Events", "e1");
        _fs.CreateDirectory(Path.Combine(root, "GuestCycles", "c1"));
        _fs.WriteAllBytesAndFlush(Path.Combine(root, "GuestCycles", "c1", "capture-1.jpg"), [1]);

        _fs.DeleteDirectoryRecursive(root);
        _fs.DeleteDirectoryRecursive(root);

        Assert.False(_fs.Exists(root));
    }

    [Fact]
    public void Exists_is_true_for_files_and_directories_only()
    {
        var file = _folder.Combine("a.bin");
        _fs.WriteAllBytesAndFlush(file, [1]);

        Assert.True(_fs.Exists(file));
        Assert.True(_fs.Exists(_folder.Path));
        Assert.False(_fs.Exists(_folder.Combine("nope")));
    }

    [Fact]
    public void EnumerateFiles_lists_a_snapshot_top_level_or_recursive()
    {
        var nested = _folder.Combine("sub");
        _fs.CreateDirectory(nested);
        _fs.WriteAllBytesAndFlush(_folder.Combine("a.bin"), [1]);
        _fs.WriteAllBytesAndFlush(Path.Combine(nested, "b.bin"), [1]);

        var top = _fs.EnumerateFiles(_folder.Path, recursive: false);
        var all = _fs.EnumerateFiles(_folder.Path, recursive: true);
        foreach (var file in all)
        {
            _fs.Delete(file);
        }

        Assert.Equal([_folder.Combine("a.bin")], top);
        Assert.Equal(2, all.Count);
        Assert.Empty(_fs.EnumerateFiles(_folder.Path, recursive: true));
    }

    [Fact]
    public void EnumerateFiles_of_a_missing_directory_is_empty()
    {
        Assert.Empty(_fs.EnumerateFiles(_folder.Combine("nope"), recursive: true));
    }

    [Fact]
    public void Reading_a_missing_file_throws_file_not_found()
    {
        Assert.Throws<FileNotFoundException>(() => _fs.ReadAllBytes(_folder.Combine("nope")));
        Assert.Throws<FileNotFoundException>(() => _fs.GetLength(_folder.Combine("nope")));
    }
}
