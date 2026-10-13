using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.Store;

public sealed class DeletionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Completed(EventRecord Event, GuestCycleRecord Cycle, CaptureRecord Capture, PhotoStripRecord Strip, string CaptureThumbnail, string StripThumbnail);

    private static async Task<Completed> CompletedCycleAsync(StoreHarness harness, EventRecord? record = null)
    {
        record ??= Sample.Event();
        if (await harness.Store.GetEventAsync(record.Id, Ct) is null)
        {
            await harness.Store.CreateEventAsync(record, Ct);
        }

        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);
        var strip = await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id]), Ct);
        var captureThumbnail = await harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct);
        var stripThumbnail = await harness.Store.GetThumbnailPathAsync(cycle.Id, 64, Ct);
        return new Completed(record, cycle, capture, strip, captureThumbnail, stripThumbnail);
    }

    private static long Rows(StoreHarness harness) => harness.Count(
        "SELECT (SELECT COUNT(*) FROM events) + (SELECT COUNT(*) FROM guest_cycles) + (SELECT COUNT(*) FROM captures) + (SELECT COUNT(*) FROM photo_strips) " +
        "+ (SELECT COUNT(*) FROM event_templates) + (SELECT COUNT(*) FROM event_looks) + (SELECT COUNT(*) FROM event_phase_timers);");

    [Fact]
    public async Task Deleting_an_Event_marks_it_then_removes_folder_and_thumbnails_then_rows_in_order()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var doomed = await CompletedCycleAsync(harness);
        var second = await harness.Store.StartGuestCycleAsync(doomed.Event.Id, Ct);
        var survivor = await CompletedCycleAsync(harness);
        var observations = new Dictionary<string, (bool Folder, long Marked, long Rows)>();
        harness.Crash.OnReached = step =>
        {
            if (step.StartsWith("event.", StringComparison.Ordinal))
            {
                observations[step] = (
                    Directory.Exists(Path.Combine(harness.DataRoot, "Events", doomed.Event.Id)),
                    harness.Count($"SELECT COUNT(*) FROM events WHERE id = '{doomed.Event.Id}' AND deleting_at IS NOT NULL;"),
                    harness.Count($"SELECT COUNT(*) FROM events WHERE id = '{doomed.Event.Id}';"));
            }
        };

        await harness.Store.MarkEventDeletingAsync(doomed.Event.Id, Ct);
        await harness.Store.DrainAsync(Ct);

        Assert.Equal(["event.markedDeleting", "event.folderDeleted", "event.rowsDeleted"], harness.Crash.Steps.Where(step => step.StartsWith("event.", StringComparison.Ordinal)));
        Assert.Equal((true, 1L, 1L), observations["event.markedDeleting"]);
        Assert.Equal((false, 1L, 1L), observations["event.folderDeleted"]);
        Assert.Equal((false, 0L, 0L), observations["event.rowsDeleted"]);
        Assert.False(File.Exists(harness.Absolute(doomed.CaptureThumbnail)));
        Assert.False(File.Exists(harness.Absolute(doomed.StripThumbnail)));
        Assert.True(File.Exists(harness.Absolute(survivor.CaptureThumbnail)));
        Assert.True(Directory.Exists(Path.Combine(harness.DataRoot, "Events", survivor.Event.Id)));
        Assert.Single(await harness.Store.ListEventsAsync(Ct));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM events;"));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM guest_cycles;"));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM captures;"));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM photo_strips;"));
        Assert.Equal(2, harness.Count("SELECT COUNT(*) FROM event_templates;"));
        Assert.NotEqual(second.Id, survivor.Cycle.Id);
    }

    [Fact]
    public async Task Deleting_a_Guest_Cycle_removes_only_its_folder_thumbnails_and_rows()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var doomed = await CompletedCycleAsync(harness);
        var sibling = await CompletedCycleAsync(harness, doomed.Event);
        await File.WriteAllTextAsync(Path.Combine(harness.DataRoot, "Cache", "thumbnails", "notes.txt"), "keep", Ct);
        var observations = new Dictionary<string, (bool Folder, long Marked, long Rows)>();
        harness.Crash.OnReached = step =>
        {
            if (step.StartsWith("cycle.", StringComparison.Ordinal))
            {
                observations[step] = (
                    Directory.Exists(harness.CycleFolder(doomed.Event.Id, doomed.Cycle.Id)),
                    harness.Count($"SELECT COUNT(*) FROM guest_cycles WHERE id = '{doomed.Cycle.Id}' AND deleting_at IS NOT NULL;"),
                    harness.Count($"SELECT COUNT(*) FROM guest_cycles WHERE id = '{doomed.Cycle.Id}';"));
            }
        };

        await harness.Store.MarkGuestCycleDeletingAsync(doomed.Cycle.Id, Ct);
        await harness.Store.DrainAsync(Ct);

        Assert.Equal(["cycle.markedDeleting", "cycle.folderDeleted", "cycle.rowsDeleted"], harness.Crash.Steps.Where(step => step.StartsWith("cycle.", StringComparison.Ordinal)));
        Assert.Equal((true, 1L, 1L), observations["cycle.markedDeleting"]);
        Assert.Equal((false, 1L, 1L), observations["cycle.folderDeleted"]);
        Assert.Equal((false, 0L, 0L), observations["cycle.rowsDeleted"]);
        Assert.False(File.Exists(harness.Absolute(doomed.CaptureThumbnail)));
        Assert.False(File.Exists(harness.Absolute(doomed.StripThumbnail)));
        Assert.True(File.Exists(harness.Absolute(sibling.CaptureThumbnail)));
        Assert.True(File.Exists(harness.Absolute(sibling.StripThumbnail)));
        Assert.True(File.Exists(Path.Combine(harness.DataRoot, "Cache", "thumbnails", "notes.txt")));
        Assert.True(Directory.Exists(harness.CycleFolder(sibling.Event.Id, sibling.Cycle.Id)));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM guest_cycles;"));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM captures;"));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM photo_strips;"));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM events;"));
    }

    [Fact]
    public async Task An_open_cycle_can_be_deleted_too_and_vanishes_at_once()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        _ = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);

        await harness.Store.MarkGuestCycleDeletingAsync(cycle.Id, Ct);

        Assert.Null(await harness.Store.GetGuestCycleAsync(cycle.Id, Ct));
        Assert.Empty((await harness.Store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, null, 10, Ct)).Items);
        await harness.Store.DrainAsync(Ct);
        Assert.Equal(0, harness.Count("SELECT COUNT(*) FROM guest_cycles;"));
        Assert.Empty(StoreHarness.Files(Path.Combine(harness.DataRoot, "Events")));
    }

    [Fact]
    public async Task Marking_a_missing_or_already_deleting_target_is_NotFound_and_ids_are_checked()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var done = await CompletedCycleAsync(harness);
        await harness.Store.MarkGuestCycleDeletingAsync(done.Cycle.Id, Ct);
        await harness.Store.MarkEventDeletingAsync(done.Event.Id, Ct);

        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.MarkGuestCycleDeletingAsync(done.Cycle.Id, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.MarkGuestCycleDeletingAsync(StorageIds.NewId(), Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.MarkEventDeletingAsync(done.Event.Id, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.MarkEventDeletingAsync(StorageIds.NewId(), Ct))).Kind);
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.MarkEventDeletingAsync("..", Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.MarkGuestCycleDeletingAsync("..", Ct));
        await harness.Store.DrainAsync(Ct);
        Assert.Equal(0, Rows(harness));
    }

    [Fact]
    public async Task Drain_waits_for_every_queued_deletion_and_is_immediate_when_idle()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        await harness.Store.DrainAsync(Ct);
        var cycles = new List<Completed>();
        var record = Sample.Event();
        for (var index = 0; index < 5; index++)
        {
            cycles.Add(await CompletedCycleAsync(harness, record));
        }

        foreach (var cycle in cycles)
        {
            await harness.Store.MarkGuestCycleDeletingAsync(cycle.Cycle.Id, Ct);
        }

        await harness.Store.DrainAsync(Ct);

        Assert.Equal(0, harness.Count("SELECT COUNT(*) FROM guest_cycles;"));
        Assert.Empty(StoreHarness.Files(Path.Combine(harness.DataRoot, "Events")));
        Assert.Empty(Directory.GetFiles(Path.Combine(harness.DataRoot, "Cache", "thumbnails")));
    }

    [Fact]
    public async Task A_deletion_queued_while_Drain_is_waiting_is_waited_for_too()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var first = await CompletedCycleAsync(harness);
        var second = await CompletedCycleAsync(harness, first.Event);
        Task? queued = null;
        harness.Crash.OnReached = step =>
        {
            if (step == CrashPoint.CycleRowsDeleted && queued is null)
            {
                queued = Task.Run(() => harness.Store.MarkGuestCycleDeletingAsync(second.Cycle.Id, Ct), Ct);
                Thread.Sleep(100);
            }
        };

        await harness.Store.MarkGuestCycleDeletingAsync(first.Cycle.Id, Ct);
        await harness.Store.DrainAsync(Ct);
        await queued!;
        await harness.Store.DrainAsync(Ct);

        Assert.Equal(0, harness.Count("SELECT COUNT(*) FROM guest_cycles;"));
    }

    [Fact]
    public async Task A_job_whose_row_is_no_longer_deleting_does_nothing()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var event1 = await CompletedCycleAsync(harness);
        var event2 = await CompletedCycleAsync(harness);
        harness.Crash.OnReached = step =>
        {
            if (step == CrashPoint.EventMarkedDeleting)
            {
                harness.Execute("UPDATE events SET deleting_at = NULL;");
            }
            else if (step == CrashPoint.CycleMarkedDeleting)
            {
                harness.Execute("UPDATE guest_cycles SET deleting_at = NULL;");
            }
        };

        await harness.Store.MarkEventDeletingAsync(event1.Event.Id, Ct);
        await harness.Store.MarkGuestCycleDeletingAsync(event2.Cycle.Id, Ct);
        await harness.Store.DrainAsync(Ct);

        Assert.Equal(2, harness.Count("SELECT COUNT(*) FROM events;"));
        Assert.Equal(2, harness.Count("SELECT COUNT(*) FROM guest_cycles;"));
        Assert.True(Directory.Exists(Path.Combine(harness.DataRoot, "Events", event1.Event.Id)));
        Assert.True(Directory.Exists(harness.CycleFolder(event2.Event.Id, event2.Cycle.Id)));
        Assert.DoesNotContain(CrashPoint.EventFolderDeleted, harness.Crash.Steps);
        Assert.DoesNotContain(CrashPoint.CycleFolderDeleted, harness.Crash.Steps);
    }

    [Theory]
    [InlineData(StorageFaultKind.DiskFull)]
    [InlineData(StorageFaultKind.AccessDenied)]
    [InlineData(StorageFaultKind.Locked)]
    public async Task A_file_fault_stops_the_job_keeps_the_rows_marked_and_Drain_still_returns(StorageFaultKind fault)
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var done = await CompletedCycleAsync(harness);
        var other = await CompletedCycleAsync(harness);
        harness.Crash.OnReached = step =>
        {
            if (step is CrashPoint.EventMarkedDeleting or CrashPoint.CycleMarkedDeleting)
            {
                harness.Faults.Inject(fault, StorageFaultScope.AllWrites);
            }
        };

        await harness.Store.MarkEventDeletingAsync(done.Event.Id, Ct);
        await harness.Store.MarkGuestCycleDeletingAsync(other.Cycle.Id, Ct);
        await harness.Store.DrainAsync(Ct);
        harness.Faults.Clear();

        Assert.Equal(2, harness.Count("SELECT COUNT(*) FROM guest_cycles;"));
        Assert.Equal(2, harness.Count("SELECT COUNT(*) FROM events;"));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM events WHERE deleting_at IS NOT NULL;"));
        Assert.True(Directory.Exists(Path.Combine(harness.DataRoot, "Events", done.Event.Id)));
        Assert.Equal(other.Event.Id, Assert.Single(await harness.Store.ListEventsAsync(Ct)).Id);
    }

    [Fact]
    public async Task A_database_failure_while_removing_rows_leaves_them_marked_for_the_reconciler()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var done = await CompletedCycleAsync(harness);
        var other = await CompletedCycleAsync(harness);
        harness.Execute("CREATE TRIGGER keep_cycles BEFORE DELETE ON guest_cycles BEGIN SELECT RAISE(ABORT, 'no'); END;");

        await harness.Store.MarkGuestCycleDeletingAsync(other.Cycle.Id, Ct);
        await harness.Store.MarkEventDeletingAsync(done.Event.Id, Ct);
        await harness.Store.DrainAsync(Ct);

        Assert.False(Directory.Exists(harness.CycleFolder(other.Event.Id, other.Cycle.Id)));
        Assert.Equal(2, harness.Count("SELECT COUNT(*) FROM guest_cycles;"));
        Assert.Equal(2, harness.Count("SELECT COUNT(*) FROM events;"));
        Assert.DoesNotContain(CrashPoint.CycleRowsDeleted, harness.Crash.Steps);
        Assert.DoesNotContain(CrashPoint.EventRowsDeleted, harness.Crash.Steps);
    }

    [Fact]
    public async Task A_job_that_finds_the_store_closed_leaves_everything_for_the_reconciler()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var done = await CompletedCycleAsync(harness);
        Task? reopening = null;
        harness.Crash.OnReached = step =>
        {
            if (step == CrashPoint.CycleMarkedDeleting)
            {
                harness.Probe.Facts = harness.Probe.Facts with { CanWrite = false };
                reopening = Task.Run(() => harness.Store.OpenAsync(Ct), Ct);
                Thread.Sleep(300);
            }
        };

        await harness.Store.MarkGuestCycleDeletingAsync(done.Cycle.Id, Ct);
        await harness.Store.DrainAsync(Ct);
        var failure = await Assert.ThrowsAsync<StorageException>(() => reopening!);

        Assert.Equal(StorageErrorKind.NotWritable, failure.Kind);
        Assert.True(Directory.Exists(harness.CycleFolder(done.Event.Id, done.Cycle.Id)));
        harness.Probe.Facts = harness.Probe.Facts with { CanWrite = true };
        _ = await harness.Store.OpenAsync(Ct);
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM guest_cycles WHERE deleting_at IS NOT NULL;"));
    }

    [Fact]
    public async Task Disposing_waits_for_a_pending_deletion()
    {
        var harness = await StoreHarness.OpenedAsync();
        var done = await CompletedCycleAsync(harness);
        await harness.Store.MarkEventDeletingAsync(done.Event.Id, Ct);

        await harness.Store.DisposeAsync();

        Assert.Equal(0, Rows(harness));
        await harness.DisposeAsync();
    }

    private static string Precious(ScratchFolder outside)
    {
        var path = outside.Combine("precious.txt");
        File.WriteAllText(path, "keep me");
        return path;
    }

    [Fact]
    public async Task Deleting_an_Event_never_follows_a_junction_in_its_folder_and_leaves_the_target_alone()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        using var outside = new ScratchFolder();
        var done = await CompletedCycleAsync(harness);
        var precious = Precious(outside);
        Links.CreateJunction(Path.Combine(harness.CycleFolder(done.Event.Id, done.Cycle.Id), "link"), outside.Path);

        await harness.Store.MarkEventDeletingAsync(done.Event.Id, Ct);
        await harness.Store.DrainAsync(Ct);

        Assert.True(File.Exists(precious));
        Assert.Equal(0, Rows(harness));
        Assert.False(Directory.Exists(Path.Combine(harness.DataRoot, "Events", done.Event.Id)));
    }

    [Fact]
    public async Task Deleting_an_Event_whose_folder_is_a_junction_removes_only_the_link()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        using var outside = new ScratchFolder();
        var done = await CompletedCycleAsync(harness);
        var precious = Precious(outside);
        var eventFolder = Path.Combine(harness.DataRoot, "Events", done.Event.Id);
        Directory.Delete(eventFolder, recursive: true);
        Links.CreateJunction(eventFolder, outside.Path);

        await harness.Store.MarkEventDeletingAsync(done.Event.Id, Ct);
        await harness.Store.DrainAsync(Ct);

        Assert.True(File.Exists(precious));
        Assert.False(Directory.Exists(eventFolder));
        Assert.Equal(0, Rows(harness));
    }

    [Fact]
    public async Task Deleting_a_Guest_Cycle_never_follows_a_junction_in_its_folder_and_leaves_the_target_alone()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        using var outside = new ScratchFolder();
        var done = await CompletedCycleAsync(harness);
        var precious = Precious(outside);
        Links.CreateJunction(Path.Combine(harness.CycleFolder(done.Event.Id, done.Cycle.Id), "link"), outside.Path);

        await harness.Store.MarkGuestCycleDeletingAsync(done.Cycle.Id, Ct);
        await harness.Store.DrainAsync(Ct);

        Assert.True(File.Exists(precious));
        Assert.Equal(0, harness.Count("SELECT COUNT(*) FROM guest_cycles;"));
        Assert.False(Directory.Exists(harness.CycleFolder(done.Event.Id, done.Cycle.Id)));
    }

    [Fact]
    public async Task A_deletion_behind_a_junction_above_the_folder_deletes_nothing_and_stays_marked_for_the_reconciler()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        using var outside = new ScratchFolder();
        var done = await CompletedCycleAsync(harness);
        var moved = Path.Combine(outside.Path, "Events");
        var events = Path.Combine(harness.DataRoot, "Events");
        Directory.Move(events, moved);
        Links.CreateJunction(events, moved);
        var capture = Path.Combine(harness.CycleFolder(done.Event.Id, done.Cycle.Id), "capture-1.jpg");

        await harness.Store.MarkEventDeletingAsync(done.Event.Id, Ct);
        await harness.Store.DrainAsync(Ct);

        Assert.True(File.Exists(capture));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM events WHERE deleting_at IS NOT NULL;"));
        Assert.DoesNotContain(CrashPoint.EventRowsDeleted, harness.Crash.Steps);
    }
}
