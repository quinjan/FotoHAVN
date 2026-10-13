using FotoHavn.Core.Storage;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.Reconciliation;

/// <summary>
/// The reconciler never follows or removes a junction or symbolic link: files reachable only through one survive every step,
/// and the link itself is left in place and reported. Every test makes a REAL junction (<c>mklink /J</c>) to a folder outside
/// the Data folder that holds a file the reconciler would delete if it followed the link.
/// </summary>
public sealed class ReconcilerLinkTests : IDisposable
{
    private readonly SeededDataFolder _folder = new();
    private readonly ScratchFolder _elsewhere = new();

    public void Dispose()
    {
        _folder.Dispose();
        _elsewhere.Dispose();
    }

    private string Precious(string name = "precious.txt")
    {
        var path = Path.Combine(_elsewhere.Path, name);
        File.WriteAllText(path, "keep me");
        return path;
    }

    [Fact]
    public async Task A_junction_in_an_Event_folder_is_not_followed_by_steps_1_and_2_and_is_reported()
    {
        var eventId = _folder.AddEvent();
        var cycle = _folder.AddCycle(eventId, "completed");
        var capture = _folder.AddCapture(cycle, 1);
        var orphan = _folder.AddFile(Path.Combine(_folder.CycleFolder(cycle), "capture-9.jpg"));
        var precious = Precious();
        var staged = Precious("leftover.tmp");
        Links.CreateJunction(Path.Combine(_folder.EventFolder(eventId), "link"), _elsewhere.Path);
        Links.CreateJunction(Path.Combine(_folder.CycleFolder(cycle), ".staging"), _elsewhere.Path);

        var report = await ReconcilerKit.RunAsync(_folder);

        Assert.True(File.Exists(precious));
        Assert.True(File.Exists(staged));
        Assert.True(File.Exists(capture.Path));
        Assert.False(File.Exists(orphan));
        Assert.Equal(1, report.OrphanFilesDeleted);
        Assert.Equal(0, report.StagingFilesDeleted);
        Assert.Equal(2, report.Problems.Count(problem => problem.Contains("junction or symbolic link", StringComparison.Ordinal) && problem.Contains(eventId, StringComparison.Ordinal)));
        Assert.True(Directory.Exists(Path.Combine(_folder.EventFolder(eventId), "link")));
    }

    [Fact]
    public async Task An_orphan_Event_folder_that_is_a_junction_is_left_in_place_with_its_target()
    {
        var precious = Precious();
        var link = Links.CreateJunction(Path.Combine(_folder.EventsDir, StorageIds.NewId()), _elsewhere.Path);

        var report = await ReconcilerKit.RunAsync(_folder);

        Assert.True(File.Exists(precious));
        Assert.True(Directory.Exists(link));
        Assert.Equal(0, report.OrphanFilesDeleted);
        Assert.Contains(report.Problems, problem => problem.Contains("directly under Events", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deleting_an_Event_removes_a_junction_inside_it_but_never_its_target()
    {
        var eventId = _folder.AddEvent(deleting: true);
        var cycle = _folder.AddCycle(eventId, "completed");
        _folder.AddCapture(cycle, 1);
        var precious = Precious();
        Links.CreateJunction(Path.Combine(_folder.EventFolder(eventId), "link"), _elsewhere.Path);

        var report = await ReconcilerKit.RunAsync(_folder);

        Assert.Equal(1, report.EventsDeleted);
        Assert.False(Directory.Exists(_folder.EventFolder(eventId)));
        Assert.True(File.Exists(precious));
    }

    [Fact]
    public async Task Deleting_an_Event_whose_folder_is_a_junction_removes_the_link_but_never_its_target()
    {
        var eventId = _folder.AddEvent(deleting: true);
        var precious = Precious();
        var link = Links.CreateJunction(_folder.EventFolder(eventId), _elsewhere.Path);

        var report = await ReconcilerKit.RunAsync(_folder);

        Assert.Equal(1, report.EventsDeleted);
        Assert.False(Directory.Exists(link));
        Assert.True(File.Exists(precious));
    }

    [Fact]
    public async Task Deleting_a_Guest_Cycle_removes_a_junction_inside_it_but_never_its_target()
    {
        var eventId = _folder.AddEvent();
        var cycle = _folder.AddCycle(eventId, "completed", deleting: true);
        _folder.AddCapture(cycle, 1);
        var precious = Precious();
        Links.CreateJunction(Path.Combine(_folder.CycleFolder(cycle), "link"), _elsewhere.Path);

        var report = await ReconcilerKit.RunAsync(_folder);

        Assert.Equal(1, report.GuestCyclesDeleted);
        Assert.False(Directory.Exists(_folder.CycleFolder(cycle)));
        Assert.True(File.Exists(precious));
    }

    [Fact]
    public async Task An_Events_folder_that_is_itself_a_junction_is_never_deleted_through_and_deletions_wait()
    {
        var eventId = StorageIds.NewId();
        var cycleId = StorageIds.NewId();
        var outsideCycle = Path.Combine(_elsewhere.Path, eventId, "GuestCycles", cycleId);
        Directory.CreateDirectory(outsideCycle);
        var stray = Path.Combine(outsideCycle, "capture-1.jpg");
        File.WriteAllText(stray, "stray");
        var staged = Path.Combine(outsideCycle, ".staging", "x.tmp");
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.WriteAllText(staged, "staged");
        var other = _folder.AddEvent(deleting: true);
        Directory.CreateDirectory(Path.Combine(_elsewhere.Path, other));
        var inOther = Path.Combine(_elsewhere.Path, other, "x.bin");
        File.WriteAllText(inOther, "x");
        Links.CreateJunction(_folder.EventsDir, _elsewhere.Path);

        var report = await ReconcilerKit.RunAsync(_folder);

        Assert.True(File.Exists(stray));
        Assert.True(File.Exists(staged));
        Assert.True(File.Exists(inOther));
        Assert.Equal(0, report.StagingFilesDeleted);
        Assert.Equal(0, report.OrphanFilesDeleted);
        Assert.Equal(0, report.EventsDeleted);
        Assert.Equal(1, _folder.Scalar("SELECT COUNT(*) FROM events WHERE id = $id AND deleting_at IS NOT NULL", ("$id", other)));
        Assert.Contains(report.Problems, problem => problem.Contains("folder reached through a junction", StringComparison.Ordinal));
        Assert.Contains(report.Problems, problem => problem.Contains("file reached through a junction", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cycle_folder_behind_a_junction_keeps_its_rows_marked_deleting()
    {
        var real = Path.Combine(_elsewhere.Path, "real-events");
        var eventId = _folder.AddEvent();
        var cycle = _folder.AddCycle(eventId, "completed", deleting: true);
        var capture = _folder.AddCapture(cycle, 1);
        var moved = Path.Combine(real, eventId);
        Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
        Directory.Move(_folder.EventFolder(eventId), moved);
        Directory.Delete(_folder.EventsDir, recursive: true);
        Links.CreateJunction(_folder.EventsDir, real);

        var report = await ReconcilerKit.RunAsync(_folder);

        Assert.Equal(0, report.GuestCyclesDeleted);
        Assert.True(File.Exists(Path.Combine(moved, "GuestCycles", cycle, capture.FileName)));
        Assert.Equal(1, _folder.Scalar("SELECT COUNT(*) FROM guest_cycles WHERE id = $id AND deleting_at IS NOT NULL", ("$id", cycle)));
    }

    [Fact]
    public async Task A_thumbnail_cache_that_is_a_junction_is_never_deleted_through()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_folder.ThumbnailsDir)!);
        var stale = Path.Combine(_elsewhere.Path, ThumbnailName.Format(StorageIds.NewId(), SeededDataFolder.Sha256Of([1]), 256));
        File.WriteAllText(stale, "thumb");
        var junk = Precious("notes.txt");
        var staging = Path.Combine(_elsewhere.Path, ".staging", "t.tmp");
        Directory.CreateDirectory(Path.GetDirectoryName(staging)!);
        File.WriteAllText(staging, "tmp");
        Links.CreateJunction(_folder.ThumbnailsDir, _elsewhere.Path);

        var report = await ReconcilerKit.RunAsync(_folder);

        Assert.True(File.Exists(stale));
        Assert.True(File.Exists(junk));
        Assert.True(File.Exists(staging));
        Assert.Equal(0, report.OrphanThumbnailsDeleted);
        Assert.Equal(0, report.StagingFilesDeleted);
        Assert.Contains(report.Problems, problem => problem.Contains("file reached through a junction", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_junction_inside_the_thumbnail_cache_is_reported_and_left_alone()
    {
        var precious = Precious();
        Links.CreateJunction(Path.Combine(_folder.ThumbnailsDir, "link"), _elsewhere.Path);

        var report = await ReconcilerKit.RunAsync(_folder);

        Assert.True(File.Exists(precious));
        Assert.Contains(report.Problems, problem => problem.Contains("thumbnail cache", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_discarded_capture_behind_a_junction_is_not_deleted_through_it()
    {
        var eventId = _folder.AddEvent();
        var cycle = _folder.AddCycle(eventId, "completed");
        var discarded = _folder.AddCapture(cycle, 1, state: "discarded", writeFile: false);
        var targetFolder = Path.Combine(_elsewhere.Path, "cycle");
        Directory.CreateDirectory(targetFolder);
        var precious = Path.Combine(targetFolder, discarded.FileName);
        File.WriteAllText(precious, "keep me");
        Links.CreateJunction(_folder.CycleFolder(cycle), targetFolder);

        var report = await ReconcilerKit.RunAsync(_folder);

        Assert.True(File.Exists(precious));
        Assert.Equal(0, report.DiscardedFilesDeleted);
        Assert.Contains(report.Problems, problem => problem.Contains("file reached through a junction", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_file_symbolic_link_in_a_cycle_folder_is_not_followed_and_is_reported()
    {
        var eventId = _folder.AddEvent();
        var cycle = _folder.AddCycle(eventId, "completed");
        var precious = Precious();
        if (!Links.TryCreateFileSymbolicLink(Path.Combine(_folder.CycleFolder(cycle), "capture-9.jpg"), precious))
        {
            Assert.Skip("This account can't create symbolic links (needs Developer Mode or elevation); the junction tests cover the same rule.");
        }

        var report = await ReconcilerKit.RunAsync(_folder);

        Assert.True(File.Exists(precious));
        Assert.Equal(0, report.OrphanFilesDeleted);
        Assert.Contains(report.Problems, problem => problem.Contains(eventId, StringComparison.Ordinal));
    }
}
