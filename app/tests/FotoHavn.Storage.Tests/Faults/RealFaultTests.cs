using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Tests.Crash;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.Faults;

/// <summary>
/// Faults the operating system raises, not the decorator: a read-only file (access denied) and a file another handle holds
/// open with no sharing (locked), against the real store on real files. Disk full has no cheap real trigger, so one
/// end-to-end test drives the store's whole commit sequence through <see cref="FaultInjectingStorageFileSystem"/>.
/// </summary>
public sealed class RealFaultTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Disk_full_surfaces_from_every_commit_and_the_store_works_again_after_the_fault_clears()
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        var faults = new FaultInjectingStorageFileSystem(new PhysicalStorageFileSystem());
        await using var store = RealStore.Create(data, faults);
        await store.OpenAsync(Ct);
        var @event = Sample.Event();
        await store.CreateEventAsync(@event, Ct);
        var cycle = await store.StartGuestCycleAsync(@event.Id, Ct);

        faults.Inject(StorageFaultKind.DiskFull, StorageFaultScope.AllWrites);
        var captureFailure = await Assert.ThrowsAsync<StorageException>(() => store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct));
        Assert.Equal(StorageErrorKind.DiskFull, captureFailure.Kind);
        Assert.Equal(0L, CrashDataFolder.Count(data, "SELECT COUNT(*) FROM captures"));
        Assert.Empty(CycleFiles(data, @event.Id, cycle.Id));

        faults.Clear();
        var capture = await store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);
        Assert.Equal(["capture-1.jpg"], CycleFiles(data, @event.Id, cycle.Id));

        faults.Inject(StorageFaultKind.DiskFull, StorageFaultScope.AllWrites);
        var stripFailure = await Assert.ThrowsAsync<StorageException>(() => store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id]), Ct));
        Assert.Equal(StorageErrorKind.DiskFull, stripFailure.Kind);
        Assert.Equal(0L, CrashDataFolder.Count(data, "SELECT COUNT(*) FROM photo_strips"));
        Assert.Null(CrashDataFolder.Text(data, $"SELECT end_reason FROM guest_cycles WHERE id = '{cycle.Id}'"));

        faults.Clear();
        await store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id]), Ct);
        Assert.Equal("completed", CrashDataFolder.Text(data, $"SELECT end_reason FROM guest_cycles WHERE id = '{cycle.Id}'"));
        Assert.Equal(["capture-1.jpg", "photo-strip.png"], CycleFiles(data, @event.Id, cycle.Id));
        CrashDataFolder.AssertConsistent(data);
    }

    [Fact]
    public async Task A_read_only_file_in_the_way_is_AccessDenied_and_the_commit_works_once_the_file_is_writable()
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        await using var store = RealStore.Create(data);
        await store.OpenAsync(Ct);
        var @event = Sample.Event();
        await store.CreateEventAsync(@event, Ct);
        var cycle = await store.StartGuestCycleAsync(@event.Id, Ct);
        var blocker = Path.Combine(CrashDataFolder.CycleFolder(data, @event.Id, cycle.Id), "capture-1.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
        File.WriteAllText(blocker, "left by an earlier failed clean-up");
        File.SetAttributes(blocker, FileAttributes.ReadOnly);

        try
        {
            var failure = await Assert.ThrowsAsync<StorageException>(() => store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct));

            Assert.Equal(StorageErrorKind.AccessDenied, failure.Kind);
            Assert.Equal(0L, CrashDataFolder.Count(data, "SELECT COUNT(*) FROM captures"));
        }
        finally
        {
            File.SetAttributes(blocker, FileAttributes.Normal);
        }

        var capture = await store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);

        Assert.Equal(1L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM captures WHERE id = '{capture.Id}'"));
        Assert.Equal(["capture-1.jpg"], CycleFiles(data, @event.Id, cycle.Id));
        Assert.Empty(Directory.GetFiles(Path.Combine(CrashDataFolder.CycleFolder(data, @event.Id, cycle.Id), ".staging")));
    }

    [Fact]
    public async Task A_read_only_file_stops_a_background_deletion_and_the_next_start_finishes_it()
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        var seeded = await CrashDataFolder.SeedAsync(data);
        var protectedFile = Path.Combine(CrashDataFolder.CycleFolder(data, seeded.EventId, seeded.CompletedCycleId), "capture-1.jpg");
        File.SetAttributes(protectedFile, FileAttributes.ReadOnly);

        try
        {
            await using (var store = RealStore.Create(data))
            {
                await store.OpenAsync(Ct);
                await store.MarkGuestCycleDeletingAsync(seeded.CompletedCycleId, Ct);
                await store.DrainAsync(Ct);

                Assert.Null(await store.GetGuestCycleAsync(seeded.CompletedCycleId, Ct));
                Assert.Equal(1L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM guest_cycles WHERE id = '{seeded.CompletedCycleId}'"));
                Assert.True(File.Exists(protectedFile));
            }
        }
        finally
        {
            File.SetAttributes(protectedFile, FileAttributes.Normal);
        }

        var (reopened, opened) = await RealStore.OpenAsync(data);
        await using (reopened)
        {
            Assert.Equal(1, opened.Reconcile.GuestCyclesDeleted);
            Assert.False(Directory.Exists(CrashDataFolder.CycleFolder(data, seeded.EventId, seeded.CompletedCycleId)));
            CrashDataFolder.AssertConsistent(data);
        }
    }

    [Fact]
    public async Task A_file_another_handle_holds_is_Locked_and_the_commit_works_once_the_handle_closes()
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        await using var store = RealStore.Create(data);
        await store.OpenAsync(Ct);
        var @event = Sample.Event();
        await store.CreateEventAsync(@event, Ct);
        var cycle = await store.StartGuestCycleAsync(@event.Id, Ct);
        var blocker = Path.Combine(CrashDataFolder.CycleFolder(data, @event.Id, cycle.Id), "capture-1.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);

        await using (new FileStream(blocker, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            var failure = await Assert.ThrowsAsync<StorageException>(() => store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct));

            Assert.Equal(StorageErrorKind.Locked, failure.Kind);
            Assert.Equal(0L, CrashDataFolder.Count(data, "SELECT COUNT(*) FROM captures"));
        }

        var capture = await store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);

        Assert.Equal(1L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM captures WHERE id = '{capture.Id}'"));
        Assert.Equal(["capture-1.jpg"], CycleFiles(data, @event.Id, cycle.Id));
    }

    [Fact]
    public async Task A_locked_file_stops_a_background_deletion_and_the_next_start_finishes_it()
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        var seeded = await CrashDataFolder.SeedAsync(data);
        var heldFile = Path.Combine(CrashDataFolder.CycleFolder(data, seeded.EventId, seeded.CompletedCycleId), "photo-strip.png");

        await using (var store = RealStore.Create(data))
        {
            await store.OpenAsync(Ct);
            await using (new FileStream(heldFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await store.MarkEventDeletingAsync(seeded.EventId, Ct);
                await store.DrainAsync(Ct);

                Assert.DoesNotContain(await store.ListEventsAsync(Ct), row => row.Id == seeded.EventId);
                Assert.Equal(1L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM events WHERE id = '{seeded.EventId}'"));
                Assert.True(File.Exists(heldFile));
            }

            // The store stays usable: the next deletion, with nothing held, completes.
            await store.MarkGuestCycleDeletingAsync(seeded.OtherCycleId, Ct);
            await store.DrainAsync(Ct);
            Assert.Equal(0L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM guest_cycles WHERE id = '{seeded.OtherCycleId}'"));
        }

        var (reopened, opened) = await RealStore.OpenAsync(data);
        await using (reopened)
        {
            Assert.Equal(1, opened.Reconcile.EventsDeleted);
            Assert.False(Directory.Exists(CrashDataFolder.EventFolder(data, seeded.EventId)));
            CrashDataFolder.AssertConsistent(data);
        }
    }

    [Fact]
    public async Task A_locked_discarded_file_does_not_fail_the_cycle_end_and_the_next_start_removes_it()
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        string heldFile;
        await using (var store = RealStore.Create(data))
        {
            await store.OpenAsync(Ct);
            var @event = Sample.Event();
            await store.CreateEventAsync(@event, Ct);
            var cycle = await store.StartGuestCycleAsync(@event.Id, Ct);
            var capture = await store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);
            await store.SetCaptureStateAsync(capture.Id, CaptureState.Discarded, Ct);
            heldFile = Path.Combine(CrashDataFolder.CycleFolder(data, @event.Id, cycle.Id), "capture-1.jpg");

            await using (new FileStream(heldFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.StartedOver, Ct);

                Assert.Equal("started_over", CrashDataFolder.Text(data, $"SELECT end_reason FROM guest_cycles WHERE id = '{cycle.Id}'"));
                Assert.True(File.Exists(heldFile));
            }
        }

        var (reopened, opened) = await RealStore.OpenAsync(data);
        await using (reopened)
        {
            Assert.Equal(1, opened.Reconcile.DiscardedFilesDeleted);
            Assert.False(File.Exists(heldFile));
            CrashDataFolder.AssertConsistent(data);
        }
    }

    private static string[] CycleFiles(string data, string eventId, string cycleId)
    {
        var folder = CrashDataFolder.CycleFolder(data, eventId, cycleId);
        return Directory.Exists(folder) ? [.. Directory.GetFiles(folder).Select(file => Path.GetFileName(file)).Order(StringComparer.Ordinal)] : [];
    }
}
