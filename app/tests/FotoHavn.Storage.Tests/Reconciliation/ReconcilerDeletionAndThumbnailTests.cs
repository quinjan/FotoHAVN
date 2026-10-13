using FotoHavn.Core.Storage;

namespace FotoHavn.Storage.Tests.Reconciliation;

/// <summary>Plan section 10, reconciler steps 5 (finish deletions), 6 (discarded Capture files), and 7 (orphan thumbnails).</summary>
public sealed class ReconcilerDeletionAndThumbnailTests
{
    private static long Thumbnails(SeededDataFolder folder) =>
        Directory.Exists(folder.ThumbnailsDir) ? Directory.GetFiles(folder.ThumbnailsDir).Length : 0;

    [Fact]
    public async Task Step_5_finishes_deleting_a_cycle_marked_deleting_folder_thumbnails_then_rows()
    {
        using var folder = new SeededDataFolder();
        var eventId = folder.AddEvent();
        var keep = folder.AddCycle(eventId, "completed");
        var keepCapture = folder.AddCapture(keep, 1);
        var doomed = folder.AddCycle(eventId, "completed", deleting: true);
        var doomedCapture = folder.AddCapture(doomed, 1);
        var doomedStrip = folder.AddPhotoStrip(doomed, [doomedCapture.Id]);
        folder.AddThumbnail(doomedCapture.Id, doomedCapture.Sha256);
        folder.AddThumbnail(doomed, doomedStrip.Sha256);
        var keptThumbnail = folder.AddThumbnail(keepCapture.Id, keepCapture.Sha256);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.GuestCyclesDeleted);
        Assert.Equal(0, report.EventsDeleted);
        Assert.Equal(0, report.OrphanThumbnailsDeleted);
        Assert.False(Directory.Exists(folder.CycleFolder(doomed)));
        Assert.Equal(1, folder.Count("guest_cycles"));
        Assert.Equal(1, folder.Count("captures"));
        Assert.Equal(0, folder.Count("photo_strips"));
        Assert.Equal(new[] { keptThumbnail }, Directory.GetFiles(folder.ThumbnailsDir));
        Assert.True(File.Exists(keepCapture.Path));
    }

    [Fact]
    public async Task Step_5_finishes_deleting_an_event_marked_deleting_with_all_its_rows_children_first()
    {
        using var folder = new SeededDataFolder();
        var live = folder.AddEvent();
        var liveCycle = folder.AddCycle(live, "completed");
        folder.AddCapture(liveCycle, 1);
        var doomed = folder.AddEvent(deleting: true);
        var first = folder.AddCycle(doomed, "completed");
        var second = folder.AddCycle(doomed, "started_over");
        var capture = folder.AddCapture(first, 1);
        var strip = folder.AddPhotoStrip(first, [capture.Id]);
        folder.AddCapture(second, 1);
        folder.AddThumbnail(capture.Id, capture.Sha256);
        folder.AddThumbnail(first, strip.Sha256);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.EventsDeleted);
        Assert.Equal(2, report.GuestCyclesDeleted);
        Assert.False(Directory.Exists(folder.EventFolder(doomed)));
        Assert.Equal(1, folder.Count("events"));
        Assert.Equal(1, folder.Count("event_templates"));
        Assert.Equal(1, folder.Count("event_looks"));
        Assert.Equal(1, folder.Count("event_phase_timers"));
        Assert.Equal(1, folder.Count("guest_cycles"));
        Assert.Equal(1, folder.Count("captures"));
        Assert.Equal(0, folder.Count("photo_strips"));
        Assert.Equal(0, Thumbnails(folder));
        Assert.True(Directory.Exists(folder.EventFolder(live)));
    }

    [Fact]
    public async Task Step_5_deletes_the_rows_of_an_event_whose_folder_is_already_gone()
    {
        using var folder = new SeededDataFolder();
        var doomed = folder.AddEvent(deleting: true);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.EventsDeleted);
        Assert.Equal(0, folder.Count("events"));
    }

    [Fact]
    public async Task Step_5_recovers_from_a_crash_after_the_folder_was_deleted_but_before_the_rows()
    {
        using var folder = new SeededDataFolder();
        var doomed = folder.AddEvent(deleting: true);
        var cycle = folder.AddCycle(doomed, "completed");
        folder.AddCapture(cycle, 1);
        Directory.Delete(folder.EventFolder(doomed), recursive: true);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.EventsDeleted);
        Assert.Equal(1, report.GuestCyclesDeleted);
        Assert.Equal(0, folder.Count("captures"));
    }

    [Fact]
    public async Task Step_5_removes_rows_with_ids_that_cannot_be_folder_names_without_deleting_any_folder()
    {
        using var folder = new SeededDataFolder();
        var badEvent = folder.AddEvent(deleting: true, id: "..");
        var badCycle = folder.AddCycle(badEvent, "completed", id: "not-a-guid");
        var goodEvent = folder.AddEvent();
        var survivor = folder.AddCapture(folder.AddCycle(goodEvent, "completed"), 1);
        var badCycleUnderLive = folder.AddCycle(goodEvent, "completed", deleting: true, id: "x");

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.EventsDeleted);
        Assert.Equal(2, report.GuestCyclesDeleted);
        Assert.Equal(0, folder.Scalar("SELECT COUNT(*) FROM guest_cycles WHERE id IN ($a, $b)", ("$a", badCycle), ("$b", badCycleUnderLive)));
        Assert.True(File.Exists(survivor.Path));
    }

    [Fact]
    public async Task Step_5_is_idempotent()
    {
        using var folder = new SeededDataFolder();
        folder.AddCycle(folder.AddEvent(deleting: true), "completed");

        var first = await ReconcilerKit.RunAsync(folder);
        var second = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, first.EventsDeleted);
        Assert.False(second.HasRepairs);
    }

    [Fact]
    public async Task Step_6_deletes_the_files_of_discarded_captures_and_keeps_their_rows()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var kept = folder.AddCapture(cycle, 1);
        var discarded = folder.AddCapture(cycle, 2, state: "discarded");
        var alreadyGone = folder.AddCapture(cycle, 3, state: "discarded", writeFile: false);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.DiscardedFilesDeleted);
        Assert.False(File.Exists(discarded.Path));
        Assert.False(File.Exists(alreadyGone.Path));
        Assert.True(File.Exists(kept.Path));
        Assert.Equal(3, folder.Count("captures"));
    }

    [Fact]
    public async Task Step_6_leaves_a_kept_capture_whose_file_is_missing_alone()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1, writeFile: false);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.ArtifactProblems);
        Assert.Equal(0, report.DiscardedFilesDeleted);
        Assert.Equal("kept", folder.Text("SELECT state FROM captures WHERE id = $id", ("$id", capture.Id)));
    }

    [Fact]
    public async Task Step_6_skips_a_discarded_row_with_an_unsafe_file_name()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        folder.AddCapture(cycle, 1, state: "discarded", writeFile: false);
        var outside = folder.AddFile(Path.Combine(folder.DataRoot, "outside.jpg"));
        using (var command = folder.Connection.CreateCommand())
        {
            command.CommandText = "UPDATE captures SET file_name = '..\\..\\..\\..\\outside.jpg'";
            command.ExecuteNonQuery();
        }

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(0, report.DiscardedFilesDeleted);
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task Step_6_is_idempotent()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        folder.AddCapture(cycle, 1, state: "discarded");

        var first = await ReconcilerKit.RunAsync(folder);
        var second = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, first.DiscardedFilesDeleted);
        Assert.False(second.HasRepairs);
    }

    [Fact]
    public async Task Step_7_keeps_thumbnails_whose_source_exists_with_the_same_hash()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1);
        var strip = folder.AddPhotoStrip(cycle, [capture.Id]);
        folder.AddThumbnail(capture.Id, capture.Sha256, 64);
        folder.AddThumbnail(capture.Id, capture.Sha256, 2048);
        folder.AddThumbnail(cycle, strip.Sha256, 300);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(0, report.OrphanThumbnailsDeleted);
        Assert.Equal(3, Thumbnails(folder));
    }

    [Fact]
    public async Task Step_7_deletes_thumbnails_with_no_source_a_changed_hash_a_discarded_source_or_a_name_it_cannot_parse()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1);
        var discarded = folder.AddCapture(cycle, 2, state: "discarded");
        var ghost = folder.AddThumbnail(StorageIds.NewId(), SeededDataFolder.Sha256Of([1]));
        var changed = folder.AddThumbnail(capture.Id, SeededDataFolder.Sha256Of([2]));
        var ofDiscarded = folder.AddThumbnail(discarded.Id, discarded.Sha256);
        var junk = folder.AddFile(Path.Combine(folder.ThumbnailsDir, "readme.txt"));
        var partial = folder.AddFile(Path.Combine(folder.ThumbnailsDir, capture.Id + "-" + capture.Sha256[..8] + "-256.jpg.tmp"));
        var tooSmall = folder.AddFile(Path.Combine(folder.ThumbnailsDir, $"{capture.Id}-{capture.Sha256[..8]}-10.jpg"));
        var tooBig = folder.AddFile(Path.Combine(folder.ThumbnailsDir, $"{capture.Id}-{capture.Sha256[..8]}-99999.jpg"));
        var live = folder.AddThumbnail(capture.Id, capture.Sha256);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(7, report.OrphanThumbnailsDeleted);
        Assert.All(new[] { ghost, changed, ofDiscarded, junk, partial, tooSmall, tooBig }, path => Assert.False(File.Exists(path)));
        Assert.True(File.Exists(live));
    }

    [Fact]
    public async Task Step_7_keeps_the_thumbnails_of_rows_whose_file_is_missing_because_the_rows_stay()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1, writeFile: false);
        var strip = folder.AddPhotoStrip(cycle, [capture.Id], writeFile: false);
        var captureThumbnail = folder.AddThumbnail(capture.Id, capture.Sha256);
        var stripThumbnail = folder.AddThumbnail(cycle, strip.Sha256);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(0, report.OrphanThumbnailsDeleted);
        Assert.True(File.Exists(captureThumbnail));
        Assert.True(File.Exists(stripThumbnail));
    }

    [Fact]
    public async Task Step_7_is_idempotent_and_tolerates_a_missing_thumbnail_folder()
    {
        using var folder = new SeededDataFolder();
        folder.AddThumbnail(StorageIds.NewId(), SeededDataFolder.Sha256Of([1]));

        var first = await ReconcilerKit.RunAsync(folder);
        var second = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, first.OrphanThumbnailsDeleted);
        Assert.False(second.HasRepairs);
        Directory.Delete(folder.ThumbnailsDir, recursive: true);
        Assert.False((await ReconcilerKit.RunAsync(folder)).HasRepairs);
    }
}
