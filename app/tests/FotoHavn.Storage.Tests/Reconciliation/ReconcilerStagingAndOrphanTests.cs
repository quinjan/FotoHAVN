using FotoHavn.Core.Storage;

namespace FotoHavn.Storage.Tests.Reconciliation;

/// <summary>Plan section 10, reconciler step 1 (staging files) and step 2 (files and folders no row refers to).</summary>
public sealed class ReconcilerStagingAndOrphanTests
{
    private static string Cycle(SeededDataFolder folder, out string eventId)
    {
        eventId = folder.AddEvent();
        return folder.AddCycle(eventId, "completed");
    }

    [Fact]
    public async Task It_has_nothing_to_do_on_a_fresh_database_with_no_folders()
    {
        using var folder = new SeededDataFolder();

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.False(report.HasRepairs);
        Assert.Equal(ReconcileReport.Empty.StagingFilesDeleted, report.StagingFilesDeleted);
        Assert.Empty(report.Problems);
    }

    [Fact]
    public async Task It_has_nothing_to_do_on_healthy_data()
    {
        using var folder = new SeededDataFolder();
        var cycle = Cycle(folder, out _);
        var capture = folder.AddCapture(cycle, 1);
        var strip = folder.AddPhotoStrip(cycle, [capture.Id]);
        folder.AddThumbnail(capture.Id, capture.Sha256);
        var before = folder.FilesUnderData();

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.False(report.HasRepairs);
        Assert.Equal(before, folder.FilesUnderData());
        Assert.True(File.Exists(capture.Path));
        Assert.True(File.Exists(strip.Path));
    }

    [Fact]
    public async Task Step_1_deletes_every_staging_file_in_every_cycle_and_keeps_the_final_files()
    {
        using var folder = new SeededDataFolder();
        var first = Cycle(folder, out var eventId);
        var second = folder.AddCycle(eventId, "completed");
        var capture = folder.AddCapture(first, 1);
        var staged = new[]
        {
            folder.AddStagingFile(first, "a.tmp"),
            folder.AddStagingFile(first, "b.tmp"),
            folder.AddStagingFile(second, "c.tmp"),
            folder.AddFile(Path.Combine(folder.CycleFolder(second), ".STAGING", "d.tmp")),
        };

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(4, report.StagingFilesDeleted);
        Assert.Equal(0, report.OrphanFilesDeleted);
        Assert.All(staged, path => Assert.False(File.Exists(path)));
        Assert.True(File.Exists(capture.Path));
    }

    [Fact]
    public async Task Step_1_deletes_the_thumbnail_staging_files_too_and_keeps_finished_thumbnails()
    {
        using var folder = new SeededDataFolder();
        var cycle = Cycle(folder, out _);
        var capture = folder.AddCapture(cycle, 1);
        var thumbnail = folder.AddThumbnail(capture.Id, capture.Sha256);
        var staged = new[]
        {
            folder.AddFile(Path.Combine(folder.ThumbnailsDir, ".staging", "half-written.tmp")),
            folder.AddFile(Path.Combine(folder.ThumbnailsDir, ".staging", "nested", "other.tmp")),
        };

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(2, report.StagingFilesDeleted);
        Assert.Equal(0, report.OrphanThumbnailsDeleted);
        Assert.All(staged, path => Assert.False(File.Exists(path)));
        Assert.True(File.Exists(thumbnail));
    }

    [Fact]
    public async Task Step_1_is_idempotent()
    {
        using var folder = new SeededDataFolder();
        var cycle = Cycle(folder, out _);
        folder.AddStagingFile(cycle);

        var first = await ReconcilerKit.RunAsync(folder);
        var second = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, first.StagingFilesDeleted);
        Assert.False(second.HasRepairs);
    }

    [Fact]
    public async Task Step_2_deletes_files_with_no_row_and_keeps_files_with_one()
    {
        using var folder = new SeededDataFolder();
        var cycle = Cycle(folder, out _);
        var capture = folder.AddCapture(cycle, 1);
        var strip = folder.AddPhotoStrip(cycle, [capture.Id]);
        var noCaptureRow = folder.AddFile(Path.Combine(folder.CycleFolder(cycle), "capture-2.jpg"));
        var oddName = folder.AddFile(Path.Combine(folder.CycleFolder(cycle), "notes.txt"));

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(2, report.OrphanFilesDeleted);
        Assert.False(File.Exists(noCaptureRow));
        Assert.False(File.Exists(oddName));
        Assert.True(File.Exists(capture.Path));
        Assert.True(File.Exists(strip.Path));
        Assert.Equal(1, folder.Count("captures"));
    }

    [Fact]
    public async Task Step_2_deletes_a_photo_strip_file_that_has_no_row()
    {
        using var folder = new SeededDataFolder();
        var cycle = Cycle(folder, out _);
        var path = folder.AddFile(Path.Combine(folder.CycleFolder(cycle), "photo-strip.png"));

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.OrphanFilesDeleted);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Step_2_deletes_files_that_sit_outside_the_cycle_folder_layout()
    {
        using var folder = new SeededDataFolder();
        var eventId = folder.AddEvent();
        var cycle = folder.AddCycle(eventId, "completed");
        var capture = folder.AddCapture(cycle, 1);
        var loose = folder.AddFile(Path.Combine(folder.EventsDir, "loose.txt"));
        var inEvent = folder.AddFile(Path.Combine(folder.EventFolder(eventId), "extra.txt"));
        var wrongFolder = folder.AddFile(Path.Combine(folder.EventFolder(eventId), "Other", cycle, "capture-1.jpg"));
        var tooDeep = folder.AddFile(Path.Combine(folder.CycleFolder(cycle), "nested", "capture-1.jpg"));

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(4, report.OrphanFilesDeleted);
        Assert.All(new[] { loose, inEvent, wrongFolder, tooDeep }, path => Assert.False(File.Exists(path)));
        Assert.True(File.Exists(capture.Path));
    }

    [Fact]
    public async Task Step_2_deletes_the_folder_of_an_event_with_no_row_even_its_empty_subfolders()
    {
        using var folder = new SeededDataFolder();
        var ghostEvent = StorageIds.NewId();
        var ghostCycle = StorageIds.NewId();
        var file = folder.AddFile(Path.Combine(folder.EventFolder(ghostEvent), "GuestCycles", ghostCycle, "capture-1.jpg"));
        Directory.CreateDirectory(Path.Combine(folder.EventFolder(ghostEvent), "GuestCycles", ghostCycle, ".staging"));

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.OrphanFilesDeleted);
        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(folder.EventFolder(ghostEvent)));
    }

    [Fact]
    public async Task Step_2_deletes_the_folder_of_a_cycle_with_no_row_under_a_live_event()
    {
        using var folder = new SeededDataFolder();
        var eventId = folder.AddEvent();
        var keptCycle = folder.AddCycle(eventId, "completed");
        var kept = folder.AddCapture(keptCycle, 1);
        var ghostCycle = StorageIds.NewId();
        var ghostFile = folder.AddFile(Path.Combine(folder.EventFolder(eventId), "GuestCycles", ghostCycle, "capture-1.jpg"));
        Directory.CreateDirectory(Path.Combine(folder.EventFolder(eventId), "GuestCycles", ghostCycle, ".staging"));

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.OrphanFilesDeleted);
        Assert.False(File.Exists(ghostFile));
        Assert.False(Directory.Exists(Path.GetDirectoryName(ghostFile)));
        Assert.True(File.Exists(kept.Path));
    }

    [Fact]
    public async Task Step_2_treats_a_cycle_folder_under_the_wrong_event_as_an_orphan()
    {
        using var folder = new SeededDataFolder();
        var owner = folder.AddEvent();
        var other = folder.AddEvent();
        var cycle = folder.AddCycle(owner, "completed");
        var capture = folder.AddCapture(cycle, 1);
        var misplaced = folder.AddFile(Path.Combine(folder.EventFolder(other), "GuestCycles", cycle, "capture-1.jpg"));

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.OrphanFilesDeleted);
        Assert.False(File.Exists(misplaced));
        Assert.False(Directory.Exists(Path.GetDirectoryName(misplaced)));
        Assert.True(File.Exists(capture.Path));
    }

    [Fact]
    public async Task Step_2_does_not_count_files_of_events_and_cycles_marked_deleting_as_orphans()
    {
        using var folder = new SeededDataFolder();
        var eventId = folder.AddEvent(deleting: true);
        var cycle = folder.AddCycle(eventId, "completed");
        folder.AddCapture(cycle, 1);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(0, report.OrphanFilesDeleted);
        Assert.Equal(1, report.EventsDeleted);
    }

    [Fact]
    public async Task Step_2_is_idempotent()
    {
        using var folder = new SeededDataFolder();
        var cycle = Cycle(folder, out _);
        folder.AddFile(Path.Combine(folder.CycleFolder(cycle), "capture-7.jpg"));

        var first = await ReconcilerKit.RunAsync(folder);
        var second = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, first.OrphanFilesDeleted);
        Assert.False(second.HasRepairs);
    }
}
