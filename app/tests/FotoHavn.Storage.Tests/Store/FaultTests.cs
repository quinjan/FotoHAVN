using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.Store;

public sealed class FaultTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<StorageFaultKind, StorageErrorKind, string, StorageFaultScope> Cases()
    {
        var data = new TheoryData<StorageFaultKind, StorageErrorKind, string, StorageFaultScope>();
        (StorageFaultKind, StorageErrorKind)[] kinds =
        [
            (StorageFaultKind.DiskFull, StorageErrorKind.DiskFull),
            (StorageFaultKind.AccessDenied, StorageErrorKind.AccessDenied),
            (StorageFaultKind.Locked, StorageErrorKind.Locked),
        ];
        foreach (var (fault, kind) in kinds)
        {
            foreach (var stage in new[] { "CreateDirectory", "WriteAllBytesAndFlush", "Move" })
            {
                foreach (var scope in new[] { StorageFaultScope.NextWrite, StorageFaultScope.AllWrites })
                {
                    data.Add(fault, kind, stage, scope);
                }
            }
        }

        return data;
    }

    private static void Arm(StoreHarness harness, StorageFaultKind fault, string stage, StorageFaultScope scope)
    {
        var armed = false;
        harness.Fs.Before = (operation, _) =>
        {
            if (!armed && operation == stage)
            {
                armed = true;
                harness.Faults.Inject(fault, scope);
            }
        };
    }

    private static async Task<(EventRecord Event, GuestCycleRecord Cycle)> StartedAsync(StoreHarness harness)
    {
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        return (record, await harness.Store.StartGuestCycleAsync(record.Id, Ct));
    }

    private static string[] FinalFiles(StoreHarness harness) =>
        [.. StoreHarness.Files(Path.Combine(harness.DataRoot, "Events")).Where(file => !file.Contains(".staging", StringComparison.Ordinal))];

    private static string[] LeftoverFiles(StoreHarness harness, string folder) =>
        [.. StoreHarness.Files(Path.Combine(harness.DataRoot, folder)).Where(file => !file.Contains(".staging", StringComparison.Ordinal))];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_Capture_commit_fault_maps_the_kind_and_leaves_no_row_or_final_file(StorageFaultKind fault, StorageErrorKind kind, string stage, StorageFaultScope scope)
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var (_, cycle) = await StartedAsync(harness);
        Arm(harness, fault, stage, scope);

        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct));

        Assert.Equal(kind, failure.Kind);
        Assert.DoesNotContain("capture-", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, harness.Count("SELECT COUNT(*) FROM captures;"));
        Assert.Empty(FinalFiles(harness));
        Assert.DoesNotContain(CrashPoint.CaptureRenamed, harness.Crash.Steps);
        Assert.DoesNotContain(CrashPoint.CaptureInserted, harness.Crash.Steps);
        harness.Faults.Clear();
        harness.Fs.Before = null;
        var retried = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);
        Assert.Equal(1, retried.Sequence);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_Photo_Strip_commit_fault_maps_the_kind_and_leaves_no_row_final_file_or_cycle_end(StorageFaultKind fault, StorageErrorKind kind, string stage, StorageFaultScope scope)
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var (_, cycle) = await StartedAsync(harness);
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);
        var filesBefore = FinalFiles(harness);
        Arm(harness, fault, stage, scope);

        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id]), Ct));

        Assert.Equal(kind, failure.Kind);
        Assert.Equal(0, harness.Count("SELECT COUNT(*) FROM photo_strips;"));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM guest_cycles WHERE end_reason IS NULL;"));
        Assert.Equal(filesBefore, FinalFiles(harness));
        Assert.DoesNotContain(CrashPoint.StripRenamed, harness.Crash.Steps);
        harness.Faults.Clear();
        harness.Fs.Before = null;
        var retried = await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id]), Ct);
        Assert.Equal("photo-strip.png", retried.FileName);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_thumbnail_write_fault_maps_the_kind_and_leaves_no_thumbnail(StorageFaultKind fault, StorageErrorKind kind, string stage, StorageFaultScope scope)
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var (_, cycle) = await StartedAsync(harness);
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);
        Arm(harness, fault, stage, scope);

        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct));

        Assert.Equal(kind, failure.Kind);
        Assert.Empty(LeftoverFiles(harness, "Cache"));
        harness.Faults.Clear();
        harness.Fs.Before = null;
        var path = await harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct);
        Assert.True(File.Exists(harness.Absolute(path)));
    }

    [Fact]
    public async Task A_Capture_row_that_fails_to_insert_removes_the_renamed_file_and_reports_a_conflict()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var (record, cycle) = await StartedAsync(harness);
        harness.Execute("CREATE TRIGGER no_captures BEFORE INSERT ON captures BEGIN SELECT RAISE(ABORT, 'no'); END;");

        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct));

        Assert.Equal(StorageErrorKind.Conflict, failure.Kind);
        Assert.Empty(FinalFiles(harness));
        Assert.False(File.Exists(Path.Combine(harness.CycleFolder(record.Id, cycle.Id), "capture-1.jpg")));
        Assert.Contains(CrashPoint.CaptureRenamed, harness.Crash.Steps);
        Assert.DoesNotContain(CrashPoint.CaptureInserted, harness.Crash.Steps);
    }

    [Fact]
    public async Task A_Photo_Strip_whose_cycle_end_fails_rolls_the_row_back_and_removes_the_file()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var (record, cycle) = await StartedAsync(harness);
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);
        harness.Execute("CREATE TRIGGER no_completion BEFORE UPDATE ON guest_cycles WHEN NEW.end_reason = 'completed' BEGIN SELECT RAISE(ABORT, 'no'); END;");

        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id]), Ct));

        Assert.Equal(StorageErrorKind.Conflict, failure.Kind);
        Assert.Equal(0, harness.Count("SELECT COUNT(*) FROM photo_strips;"));
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM guest_cycles WHERE end_reason IS NULL;"));
        Assert.False(File.Exists(Path.Combine(harness.CycleFolder(record.Id, cycle.Id), "photo-strip.png")));
    }

    [Fact]
    public async Task A_failure_to_delete_discarded_files_after_the_cycle_ended_does_not_fail_the_call()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var (record, cycle) = await StartedAsync(harness);
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1), Ct);
        var discarded = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 2), Ct);
        await harness.Store.SetCaptureStateAsync(discarded.Id, CaptureState.Discarded, Ct);
        harness.Crash.OnReached = step =>
        {
            if (step == CrashPoint.StripInserted)
            {
                harness.Faults.Inject(StorageFaultKind.AccessDenied, StorageFaultScope.AllWrites);
            }
        };

        _ = await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id]), Ct);
        harness.Faults.Clear();

        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM photo_strips;"));
        Assert.True(File.Exists(Path.Combine(harness.CycleFolder(record.Id, cycle.Id), "capture-2.jpg")));
    }

    [Fact]
    public async Task Ending_a_cycle_survives_a_file_fault_and_leaves_the_discarded_file_for_the_reconciler()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var (record, cycle) = await StartedAsync(harness);
        var discarded = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1), Ct);
        await harness.Store.SetCaptureStateAsync(discarded.Id, CaptureState.Discarded, Ct);
        harness.Faults.Inject(StorageFaultKind.Locked, StorageFaultScope.AllWrites);

        await harness.Store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.OperatorExit, Ct);

        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM guest_cycles WHERE end_reason = 'operator_exit';"));
        Assert.True(File.Exists(Path.Combine(harness.CycleFolder(record.Id, cycle.Id), "capture-1.jpg")));
        Assert.Equal(1, harness.Faults.RaisedCount);
    }

    [Fact]
    public async Task A_database_locked_by_another_writer_reports_Locked_after_the_busy_timeout()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        using var other = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = harness.DatabasePath, Pooling = false }.ToString());
        other.Open();
        using var transaction = other.BeginTransaction();
        using (var command = other.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO events (id, name, event_date, default_template_id, default_look_id, created_at, updated_at) VALUES ('held', 'n', '2026-01-01', 't', 'l', 'now', 'now');";
            command.ExecuteNonQuery();
        }

        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.CreateEventAsync(record, Ct));

        Assert.Equal(StorageErrorKind.Locked, failure.Kind);
        transaction.Rollback();
    }
}
