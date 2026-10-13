using System.Security.Cryptography;
using FotoHavn.Core.Storage;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.Store;

public sealed class GuestCycleTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(StoreHarness Harness, EventRecord Event, GuestCycleRecord Cycle)> StartedAsync()
    {
        var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        return (harness, record, cycle);
    }

    [Fact]
    public async Task A_started_cycle_is_open_with_a_new_id_and_the_clock_time()
    {
        var (harness, record, cycle) = await StartedAsync();
        await using var lifetime = harness;

        Assert.True(StorageIds.IsValid(cycle.Id));
        Assert.Equal(record.Id, cycle.EventId);
        Assert.Equal(harness.Clock.UtcNow, cycle.StartedAt);
        Assert.Null(cycle.EndedAt);
        Assert.Null(cycle.EndReason);
        var detail = await harness.Store.GetGuestCycleAsync(cycle.Id, Ct);
        Assert.Equal(cycle, detail!.Cycle);
        Assert.Empty(detail.Captures);
        Assert.Null(detail.PhotoStrip);
    }

    [Fact]
    public async Task Starting_a_cycle_on_a_missing_Event_is_NotFound()
    {
        await using var harness = await StoreHarness.OpenedAsync();

        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.StartGuestCycleAsync(StorageIds.NewId(), Ct));

        Assert.Equal(StorageErrorKind.NotFound, failure.Kind);
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.StartGuestCycleAsync("x", Ct));
    }

    [Theory]
    [InlineData(GuestCycleEndReason.StartedOver)]
    [InlineData(GuestCycleEndReason.OperatorExit)]
    [InlineData(GuestCycleEndReason.AppClosed)]
    [InlineData(GuestCycleEndReason.Unrecoverable)]
    public async Task Ending_a_cycle_records_the_time_and_reason_and_a_second_end_conflicts(GuestCycleEndReason reason)
    {
        var (harness, _, cycle) = await StartedAsync();
        await using var lifetime = harness;
        harness.Clock.Advance(TimeSpan.FromMinutes(3));

        await harness.Store.EndGuestCycleAsync(cycle.Id, reason, Ct);
        var detail = await harness.Store.GetGuestCycleAsync(cycle.Id, Ct);
        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.EndGuestCycleAsync(cycle.Id, reason, Ct));

        Assert.Equal(reason, detail!.Cycle.EndReason);
        Assert.Equal(harness.Clock.UtcNow, detail.Cycle.EndedAt);
        Assert.Equal(StorageErrorKind.Conflict, failure.Kind);
    }

    [Fact]
    public async Task End_refuses_Completed_a_bad_reason_a_bad_id_and_a_missing_cycle()
    {
        var (harness, _, cycle) = await StartedAsync();
        await using var lifetime = harness;

        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.Completed, Ct));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => harness.Store.EndGuestCycleAsync(cycle.Id, (GuestCycleEndReason)99, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.EndGuestCycleAsync("nope", GuestCycleEndReason.OperatorExit, Ct));
        var missing = await Assert.ThrowsAsync<StorageException>(() => harness.Store.EndGuestCycleAsync(StorageIds.NewId(), GuestCycleEndReason.OperatorExit, Ct));
        Assert.Equal(StorageErrorKind.NotFound, missing.Kind);
    }

    [Fact]
    public async Task A_Capture_is_staged_flushed_renamed_and_then_has_a_row_with_its_hash_and_length()
    {
        var (harness, record, cycle) = await StartedAsync();
        await using var lifetime = harness;
        var jpeg = Sample.Jpeg();
        var commit = Sample.Capture(cycle.Id, 2, jpeg);

        var capture = await harness.Store.CommitCaptureAsync(commit, Ct);

        Assert.True(StorageIds.IsValid(capture.Id));
        Assert.Equal("capture-2.jpg", capture.FileName);
        Assert.Equal(jpeg.Length, capture.ByteLength);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(jpeg)), capture.Sha256);
        Assert.Equal(CaptureState.Kept, capture.State);
        Assert.Equal((cycle.Id, 2, 80, 60, true, commit.TakenAt), (capture.GuestCycleId, capture.Sequence, capture.Width, capture.Height, capture.Mirrored, capture.TakenAt));
        var folder = harness.CycleFolder(record.Id, cycle.Id);
        Assert.Equal(jpeg, await File.ReadAllBytesAsync(Path.Combine(folder, "capture-2.jpg"), Ct));
        Assert.Empty(Directory.GetFiles(Path.Combine(folder, ".staging")));
        var detail = await harness.Store.GetGuestCycleAsync(cycle.Id, Ct);
        Assert.Equal([capture], detail!.Captures);
    }

    [Fact]
    public async Task Captures_come_back_by_sequence_and_discarded_ones_are_left_out()
    {
        var (harness, _, cycle) = await StartedAsync();
        await using var lifetime = harness;
        var third = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 3), Ct);
        var first = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1), Ct);
        var second = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 2), Ct);

        await harness.Store.SetCaptureStateAsync(second.Id, CaptureState.Discarded, Ct);
        var detail = await harness.Store.GetGuestCycleAsync(cycle.Id, Ct);

        Assert.Equal([first.Id, third.Id], detail!.Captures.Select(capture => capture.Id));
        await harness.Store.SetCaptureStateAsync(second.Id, CaptureState.Kept, Ct);
        Assert.Equal(3, (await harness.Store.GetGuestCycleAsync(cycle.Id, Ct))!.Captures.Count);
    }

    [Fact]
    public async Task A_duplicate_sequence_conflicts_and_a_sequence_below_one_is_refused_before_any_io()
    {
        var (harness, _, cycle) = await StartedAsync();
        await using var lifetime = harness;
        _ = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1), Ct);
        var callsBefore = harness.Fs.Calls.Count;

        var duplicate = await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1), Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 0), Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, -1), Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 2, []), Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id) with { Width = 0 }, Ct));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => harness.Store.CommitCaptureAsync(null!, Ct));

        Assert.Equal(StorageErrorKind.Conflict, duplicate.Kind);
        Assert.Equal(callsBefore, harness.Fs.Calls.Count);
        Assert.Equal(1, harness.Count("SELECT COUNT(*) FROM captures;"));
    }

    [Fact]
    public async Task The_schema_rejects_a_capture_sequence_below_one_and_a_duplicate_sequence()
    {
        var (harness, _, cycle) = await StartedAsync();
        await using var lifetime = harness;
        var insert = (string id, int sequence) => harness.Execute(
            $"INSERT INTO captures (id, guest_cycle_id, sequence, file_name, byte_length, sha256, width, height, mirrored, taken_at, state) VALUES ('{id}', '{cycle.Id}', {sequence}, 'f', 1, 'h', 1, 1, 0, 'now', 'kept');");
        insert("a", 1);

        Assert.Contains("CHECK", Assert.ThrowsAny<Exception>(() => insert("b", 0)).Message, StringComparison.Ordinal);
        Assert.Contains("UNIQUE", Assert.ThrowsAny<Exception>(() => insert("c", 1)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Capture_for_an_ended_missing_or_deleting_cycle_is_refused_and_leaves_no_file()
    {
        var (harness, record, cycle) = await StartedAsync();
        await using var lifetime = harness;
        var ended = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        await harness.Store.EndGuestCycleAsync(ended.Id, GuestCycleEndReason.OperatorExit, Ct);
        await harness.Store.MarkGuestCycleDeletingAsync(cycle.Id, Ct);

        Assert.Equal(StorageErrorKind.Conflict, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(ended.Id), Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(StorageIds.NewId()), Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct))).Kind);
        Assert.Empty(StoreHarness.Files(Path.Combine(harness.DataRoot, "Events")));
        await harness.Store.DrainAsync(Ct);
    }

    [Fact]
    public async Task A_stale_file_under_the_final_name_with_no_row_is_replaced()
    {
        var (harness, record, cycle) = await StartedAsync();
        await using var lifetime = harness;
        var folder = harness.CycleFolder(record.Id, cycle.Id);
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, "capture-1.jpg"), [1, 2, 3], Ct);
        var jpeg = Sample.Jpeg();

        _ = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1, jpeg), Ct);

        Assert.Equal(jpeg, await File.ReadAllBytesAsync(Path.Combine(folder, "capture-1.jpg"), Ct));
    }

    [Fact]
    public async Task Capture_state_changes_are_refused_for_a_missing_Capture_and_an_ended_cycle()
    {
        var (harness, _, cycle) = await StartedAsync();
        await using var lifetime = harness;
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct);

        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.SetCaptureStateAsync(StorageIds.NewId(), CaptureState.Discarded, Ct))).Kind);
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.SetCaptureStateAsync("x", CaptureState.Discarded, Ct));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => harness.Store.SetCaptureStateAsync(capture.Id, (CaptureState)9, Ct));
        await harness.Store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.OperatorExit, Ct);
        Assert.Equal(StorageErrorKind.Conflict, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.SetCaptureStateAsync(capture.Id, CaptureState.Discarded, Ct))).Kind);
    }

    [Fact]
    public async Task A_discarded_file_stays_until_the_cycle_ends_and_then_goes_with_its_row_kept()
    {
        var (harness, record, cycle) = await StartedAsync();
        await using var lifetime = harness;
        var kept = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1), Ct);
        var discarded = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 2), Ct);
        var folder = harness.CycleFolder(record.Id, cycle.Id);

        await harness.Store.SetCaptureStateAsync(discarded.Id, CaptureState.Discarded, Ct);
        Assert.True(File.Exists(Path.Combine(folder, "capture-2.jpg")));
        await harness.Store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.StartedOver, Ct);

        Assert.False(File.Exists(Path.Combine(folder, "capture-2.jpg")));
        Assert.True(File.Exists(Path.Combine(folder, "capture-1.jpg")));
        Assert.Equal("discarded", harness.Query($"SELECT state FROM captures WHERE id = '{discarded.Id}';", reader => reader.GetString(0))[0]);
        Assert.Equal([kept.Id], (await harness.Store.GetGuestCycleAsync(cycle.Id, Ct))!.Captures.Select(capture => capture.Id));
    }

    [Fact]
    public async Task A_Photo_Strip_commit_writes_the_file_inserts_the_row_and_completes_the_cycle_together()
    {
        var (harness, record, cycle) = await StartedAsync();
        await using var lifetime = harness;
        var first = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1), Ct);
        var second = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 2), Ct);
        var discarded = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 3), Ct);
        await harness.Store.SetCaptureStateAsync(discarded.Id, CaptureState.Discarded, Ct);
        harness.Clock.Advance(TimeSpan.FromMinutes(2));
        var png = Sample.Png();

        var strip = await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [second.Id, first.Id], png), Ct);

        Assert.Equal("photo-strip.png", strip.FileName);
        Assert.Equal([second.Id, first.Id], strip.Arrangement);
        Assert.Equal(png.Length, strip.ByteLength);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(png)), strip.Sha256);
        Assert.Equal(harness.Clock.UtcNow, strip.CommittedAt);
        var folder = harness.CycleFolder(record.Id, cycle.Id);
        Assert.Equal(png, await File.ReadAllBytesAsync(Path.Combine(folder, "photo-strip.png"), Ct));
        Assert.False(File.Exists(Path.Combine(folder, "capture-3.jpg")));
        var detail = await harness.Store.GetGuestCycleAsync(cycle.Id, Ct);
        Assert.Equal(GuestCycleEndReason.Completed, detail!.Cycle.EndReason);
        Assert.Equal(strip.CommittedAt, detail.Cycle.EndedAt);
        Assert.Equal(strip with { Arrangement = [] }, detail.PhotoStrip! with { Arrangement = [] });
        Assert.Equal(strip.Arrangement, detail.PhotoStrip!.Arrangement);
        Assert.Equal(["[\"" + second.Id + "\",\"" + first.Id + "\"]"], harness.Query("SELECT arrangement FROM photo_strips;", reader => reader.GetString(0)));
    }

    [Fact]
    public async Task A_Photo_Strip_commit_is_refused_for_a_bad_arrangement_a_second_strip_and_an_ended_cycle()
    {
        var (harness, record, cycle) = await StartedAsync();
        await using var lifetime = harness;
        var kept = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1), Ct);
        var discarded = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 2), Ct);
        await harness.Store.SetCaptureStateAsync(discarded.Id, CaptureState.Discarded, Ct);
        var otherCycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var foreign = await harness.Store.CommitCaptureAsync(Sample.Capture(otherCycle.Id, 1), Ct);

        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, []), Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [kept.Id, kept.Id]), Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [kept.Id, kept.Id, kept.Id, kept.Id, kept.Id]), Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, ["not-an-id"]), Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [kept.Id], []), Ct));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => harness.Store.CommitPhotoStripAsync(null!, Ct));
        foreach (var arrangement in new string[][] { [discarded.Id], [foreign.Id], [StorageIds.NewId()], [kept.Id, discarded.Id] })
        {
            Assert.Equal(StorageErrorKind.Conflict, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, arrangement), Ct))).Kind);
        }

        Assert.False(File.Exists(Path.Combine(harness.CycleFolder(record.Id, cycle.Id), "photo-strip.png")));
        Assert.Equal(0, harness.Count("SELECT COUNT(*) FROM photo_strips;"));
        _ = await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [kept.Id]), Ct);
        Assert.Equal(StorageErrorKind.Conflict, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [kept.Id]), Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitPhotoStripAsync(Sample.Strip(StorageIds.NewId(), [kept.Id]), Ct))).Kind);
        var ended = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        await harness.Store.EndGuestCycleAsync(ended.Id, GuestCycleEndReason.OperatorExit, Ct);
        Assert.Equal(StorageErrorKind.Conflict, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitPhotoStripAsync(Sample.Strip(ended.Id, [kept.Id]), Ct))).Kind);
    }

    [Fact]
    public async Task A_stale_strip_file_with_no_row_is_replaced_and_a_cycle_with_one_to_four_Captures_completes()
    {
        var (harness, record, cycle) = await StartedAsync();
        await using var lifetime = harness;
        var ids = new List<string>();
        for (var sequence = 1; sequence <= 4; sequence++)
        {
            ids.Add((await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, sequence), Ct)).Id);
        }

        await File.WriteAllBytesAsync(Path.Combine(harness.CycleFolder(record.Id, cycle.Id), "photo-strip.png"), [9], Ct);
        var strip = await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, ids), Ct);

        Assert.Equal(4, strip.Arrangement.Count);
        Assert.Equal(strip.ByteLength, new FileInfo(Path.Combine(harness.CycleFolder(record.Id, cycle.Id), "photo-strip.png")).Length);
    }

    [Fact]
    public async Task Get_returns_null_for_a_missing_or_deleting_cycle_and_refuses_a_bad_id()
    {
        var (harness, _, cycle) = await StartedAsync();
        await using var lifetime = harness;

        Assert.Null(await harness.Store.GetGuestCycleAsync(StorageIds.NewId(), Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.GetGuestCycleAsync("x", Ct));
        await harness.Store.MarkGuestCycleDeletingAsync(cycle.Id, Ct);
        Assert.Null(await harness.Store.GetGuestCycleAsync(cycle.Id, Ct));
        await harness.Store.DrainAsync(Ct);
    }

    [Fact]
    public async Task Concurrent_calls_are_serialized_and_all_succeed()
    {
        var (harness, _, cycle) = await StartedAsync();
        await using var lifetime = harness;

        var captures = await Task.WhenAll(Enumerable.Range(1, 24).Select(sequence => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, sequence), Ct)));
        var reads = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => harness.Store.ListEventsAsync(Ct)));

        Assert.Equal(24, captures.Select(capture => capture.Id).Distinct().Count());
        Assert.All(reads, read => Assert.Single(read));
        Assert.Equal(24, (await harness.Store.GetGuestCycleAsync(cycle.Id, Ct))!.Captures.Count);
    }
}
