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

    [Fact]
    public void Write_reports_afterWrite_once_after_the_bytes_and_not_when_the_write_fails()
    {
        var path = _folder.Combine("a.bin");
        var calls = 0;

        _fs.WriteAllBytesAndFlush(path, [1, 2, 3], () => calls++);
        Assert.Throws<DirectoryNotFoundException>(() => _fs.WriteAllBytesAndFlush(_folder.Combine("missing", "a.bin"), [1], () => calls++));

        Assert.Equal(1, calls);
        Assert.Equal([1, 2, 3], _fs.ReadAllBytes(path));
    }

    [Fact]
    public void Write_does_not_flush_when_afterWrite_throws()
    {
        var path = _folder.Combine("a.bin");

        Assert.Throws<InvalidOperationException>(() => _fs.WriteAllBytesAndFlush(path, [1], () => throw new InvalidOperationException()));

        Assert.True(_fs.Exists(path));
    }

    private (PhysicalStorageFileSystem Fs, string Root, string Outside, string Precious) Linked()
    {
        var root = _folder.Combine("Data");
        var outside = _folder.Combine("outside");
        _fs.CreateDirectory(Path.Combine(root, "Events", "e1"));
        _fs.CreateDirectory(outside);
        var precious = Path.Combine(outside, "precious.txt");
        _fs.WriteAllBytesAndFlush(precious, [7]);
        return (new PhysicalStorageFileSystem(root), root, outside, precious);
    }

    [Fact]
    public void EnumerateFiles_never_follows_a_junction()
    {
        var (fs, root, outside, precious) = Linked();
        var own = Path.Combine(root, "Events", "e1", "own.bin");
        fs.WriteAllBytesAndFlush(own, [1]);
        Links.CreateJunction(Path.Combine(root, "Events", "e1", "link"), outside);

        var found = fs.EnumerateFiles(Path.Combine(root, "Events"), recursive: true);

        Assert.Equal([own], found);
        Assert.True(File.Exists(precious));
    }

    [Fact]
    public void EnumerateFiles_skips_a_file_symbolic_link()
    {
        var (fs, root, _, precious) = Linked();
        var link = Path.Combine(root, "Events", "e1", "link.bin");
        if (!Links.TryCreateFileSymbolicLink(link, precious))
        {
            Assert.Skip("This account can't create symbolic links (needs Developer Mode or elevation); the junction tests cover the same rule.");
        }

        Assert.Empty(fs.EnumerateFiles(Path.Combine(root, "Events"), recursive: true));
        Assert.Throws<ReparsePointException>(() => fs.Delete(link));
        Assert.True(File.Exists(precious));
        Assert.Equal([link], fs.FindReparsePoints(Path.Combine(root, "Events")));
    }

    [Fact]
    public void Delete_refuses_a_file_below_a_junction_and_a_junction_itself()
    {
        var (fs, root, outside, precious) = Linked();
        var link = Links.CreateJunction(Path.Combine(root, "Events", "e1", "link"), outside);

        Assert.Throws<ReparsePointException>(() => fs.Delete(Path.Combine(link, "precious.txt")));
        Assert.Throws<ReparsePointException>(() => fs.Delete(link));

        Assert.True(File.Exists(precious));
        Assert.True(Directory.Exists(link));
    }

    [Fact]
    public void Delete_refuses_through_a_junction_that_is_deeper_than_one_level()
    {
        var (fs, root, outside, precious) = Linked();
        var link = Links.CreateJunction(Path.Combine(root, "Events", "e1", "link"), outside);
        var deep = Path.Combine(outside, "a", "b");
        Directory.CreateDirectory(deep);
        File.WriteAllBytes(Path.Combine(deep, "deep.txt"), [1]);

        Assert.Throws<ReparsePointException>(() => fs.Delete(Path.Combine(link, "a", "b", "deep.txt")));
        Assert.Throws<ReparsePointException>(() => fs.DeleteDirectoryRecursive(Path.Combine(link, "a")));

        Assert.True(File.Exists(Path.Combine(deep, "deep.txt")));
        Assert.True(File.Exists(precious));
    }

    [Fact]
    public void DeleteDirectoryRecursive_removes_only_a_junction_and_never_its_target()
    {
        var (fs, root, outside, precious) = Linked();
        var link = Links.CreateJunction(Path.Combine(root, "Events", "e1", "link"), outside);

        fs.DeleteDirectoryRecursive(link);

        Assert.False(Directory.Exists(link));
        Assert.True(File.Exists(precious));
    }

    [Fact]
    public void DeleteDirectoryRecursive_of_a_folder_that_holds_a_junction_leaves_the_target_alone()
    {
        var (fs, root, outside, precious) = Linked();
        var own = Path.Combine(root, "Events", "e1", "own.bin");
        fs.WriteAllBytesAndFlush(own, [1]);
        Links.CreateJunction(Path.Combine(root, "Events", "e1", "link"), outside);

        fs.DeleteDirectoryRecursive(Path.Combine(root, "Events", "e1"));

        Assert.False(Directory.Exists(Path.Combine(root, "Events", "e1")));
        Assert.True(File.Exists(precious));
    }

    [Fact]
    public void A_delete_works_through_a_guarded_root_that_is_itself_a_junction_and_outside_the_root()
    {
        var real = _folder.Combine("real");
        _fs.CreateDirectory(Path.Combine(real, "Events", "e1"));
        var root = Links.CreateJunction(_folder.Combine("mapped"), real);
        var inside = Path.Combine(root, "Events", "e1", "a.bin");
        var elsewhere = _folder.Combine("elsewhere.bin");
        _fs.WriteAllBytesAndFlush(inside, [1]);
        _fs.WriteAllBytesAndFlush(elsewhere, [1]);
        var fs = new PhysicalStorageFileSystem(root + Path.DirectorySeparatorChar);

        fs.Delete(inside);
        fs.Delete(elsewhere);
        fs.DeleteDirectoryRecursive(Path.Combine(root, "Events", "e1"));

        Assert.False(File.Exists(inside));
        Assert.False(File.Exists(elsewhere));
        Assert.False(Directory.Exists(Path.Combine(real, "Events", "e1")));
    }

    [Fact]
    public void FindReparsePoints_lists_junctions_without_following_them()
    {
        var (fs, root, outside, _) = Linked();
        var first = Links.CreateJunction(Path.Combine(root, "Events", "e1", "link"), outside);
        var second = Links.CreateJunction(Path.Combine(root, "Events", "top"), outside);
        Directory.CreateDirectory(Path.Combine(outside, "inner"));
        Links.CreateJunction(Path.Combine(outside, "inner", "deeper"), outside);

        var found = fs.FindReparsePoints(Path.Combine(root, "Events"));

        Assert.Equal([first, second], found.Order(StringComparer.Ordinal));
        Assert.Empty(fs.FindReparsePoints(Path.Combine(root, "Cache")));
    }
}
