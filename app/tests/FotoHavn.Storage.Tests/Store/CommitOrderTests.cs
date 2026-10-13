using FotoHavn.Core.Storage;
using FotoHavn.Storage.Tests.Reconciliation;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.Store;

/// <summary>A file is never referenced before its row exists: the order of file-system calls, crash points, and database state.</summary>
public sealed class CommitOrderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static List<string> Record(StoreHarness harness, Func<string, string> observe)
    {
        var log = new List<string>();
        harness.Fs.Before = (operation, path) => log.Add($"fs:{operation}:{Path.GetFileName(path)}");
        harness.Crash.OnReached = step => log.Add($"crash:{step}:{observe(step)}");
        return log;
    }

    [Fact]
    public async Task A_Capture_is_staged_flushed_and_renamed_before_its_row_and_the_steps_run_in_the_contract_order()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var finalFile = Path.Combine(harness.CycleFolder(record.Id, cycle.Id), "capture-1.jpg");
        var log = Record(harness, _ => $"rows={harness.Count("SELECT COUNT(*) FROM captures;")},file={File.Exists(finalFile)}");

        _ = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);

        Assert.Equal(
            [
                "fs:CreateDirectory:.staging",
                "fs:WriteAllBytesAndFlush:" + log[1][(log[1].LastIndexOf(':') + 1)..],
                "crash:capture.staged:rows=0,file=False",
                "crash:capture.flushed:rows=0,file=False",
                "fs:Move:" + log[1][(log[1].LastIndexOf(':') + 1)..],
                "crash:capture.renamed:rows=0,file=True",
                "crash:capture.inserted:rows=1,file=True",
            ],
            log);
        Assert.EndsWith(".tmp", log[1], StringComparison.Ordinal);
        Assert.Equal(CrashPoint.All.Take(4), harness.Crash.Steps);
    }

    [Fact]
    public async Task A_Photo_Strip_row_and_the_cycle_end_arrive_together_after_the_file_is_renamed()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);
        var finalFile = Path.Combine(harness.CycleFolder(record.Id, cycle.Id), "photo-strip.png");
        var log = Record(harness, _ => $"strips={harness.Count("SELECT COUNT(*) FROM photo_strips;")},completed={harness.Count("SELECT COUNT(*) FROM guest_cycles WHERE end_reason = 'completed';")},file={File.Exists(finalFile)}");

        _ = await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id]), Ct);

        var staged = log[1][(log[1].LastIndexOf(':') + 1)..];
        Assert.Equal(
            [
                "fs:CreateDirectory:.staging",
                "fs:WriteAllBytesAndFlush:" + staged,
                "crash:strip.staged:strips=0,completed=0,file=False",
                "crash:strip.flushed:strips=0,completed=0,file=False",
                "fs:Move:" + staged,
                "crash:strip.renamed:strips=0,completed=0,file=True",
                "crash:strip.inserted:strips=1,completed=1,file=True",
            ],
            log);
        Assert.Equal(CrashPoint.All.Skip(4).Take(4), harness.Crash.Steps.Skip(4));
    }

    [Fact]
    public async Task The_staged_point_comes_after_the_bytes_are_written_and_before_the_flush_for_a_Capture_and_a_Photo_Strip()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var log = new List<string>();
        harness.Fs.WriteStage = (stage, _) => log.Add("fs:" + stage);
        harness.Crash.OnReached = step => log.Add("crash:" + step);

        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);
        _ = await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id]), Ct);

        Assert.Equal(
            [
                "fs:written", "crash:capture.staged", "fs:flushed", "crash:capture.flushed", "crash:capture.renamed", "crash:capture.inserted",
                "fs:written", "crash:strip.staged", "fs:flushed", "crash:strip.flushed", "crash:strip.renamed", "crash:strip.inserted",
            ],
            log);
    }

    [Fact]
    public async Task A_hook_that_throws_at_the_staged_point_leaves_the_staging_file_as_it_was_at_that_step()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var staging = Path.Combine(harness.CycleFolder(record.Id, cycle.Id), ".staging");
        harness.Crash.OnReached = step =>
        {
            if (step == CrashPoint.CaptureStaged)
            {
                throw new SimulatedCrashException();
            }
        };

        _ = await Assert.ThrowsAnyAsync<Exception>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct));

        Assert.Single(StoreHarness.Files(staging));
        Assert.Equal(0L, harness.Count("SELECT COUNT(*) FROM captures;"));
    }

    [Fact]
    public async Task A_failed_flush_after_the_staged_point_still_cleans_up_the_staging_file()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var staging = Path.Combine(harness.CycleFolder(record.Id, cycle.Id), ".staging");
        harness.Fs.WriteStage = (stage, _) =>
        {
            if (stage == "written")
            {
                throw new IOException("The flush failed.");
            }
        };

        _ = await Assert.ThrowsAnyAsync<Exception>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct));

        Assert.Empty(StoreHarness.Files(staging));
        Assert.DoesNotContain(CrashPoint.CaptureStaged, harness.Crash.Steps);
    }

    [Fact]
    public async Task The_store_never_hands_out_a_Capture_whose_row_is_not_committed()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        harness.Execute("CREATE TRIGGER no_captures BEFORE INSERT ON captures BEGIN SELECT RAISE(ABORT, 'no'); END;");
        var seen = new List<int>();
        harness.Crash.OnReached = step =>
        {
            if (step == CrashPoint.CaptureRenamed)
            {
                seen.Add(StoreHarness.Files(Path.Combine(harness.DataRoot, "Events")).Count(file => file.EndsWith("capture-1.jpg", StringComparison.Ordinal)));
            }
        };

        _ = await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct));

        Assert.Equal([1], seen);
        Assert.Empty((await harness.Store.GetGuestCycleAsync(cycle.Id, Ct))!.Captures);
        Assert.DoesNotContain(StoreHarness.Files(Path.Combine(harness.DataRoot, "Events")), file => file.EndsWith("capture-1.jpg", StringComparison.Ordinal));
    }
}
