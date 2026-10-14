using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Storage;

public sealed class InMemoryEventStoreTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

    private async Task<InMemoryEventStore> OpenStoreAsync()
    {
        var store = new InMemoryEventStore(_clock);
        await store.OpenAsync(Ct);
        return store;
    }

    private static async Task<StorageException> FailureAsync(Task task)
    {
        var exception = await Assert.ThrowsAsync<StorageException>(() => task);
        return exception;
    }

    private async Task<(InMemoryEventStore Store, EventRecord Event)> StoreWithEventAsync()
    {
        var store = await OpenStoreAsync();
        var record = StorageSamples.Event(StorageIds.NewId());
        await store.CreateEventAsync(record, Ct);
        return (store, record);
    }

    [Fact]
    public async Task Open_reports_the_schema_version_and_reconcile_report()
    {
        var report = ReconcileReport.Empty with { StagingFilesDeleted = 2 };
        var store = new InMemoryEventStore(_clock) { ReconcileOnOpen = report, SchemaVersion = 3, DataRoot = @"D:\Booth\Data" };

        var result = await store.OpenAsync(Ct);

        Assert.Equal(3, result.SchemaVersion);
        Assert.Same(report, result.Reconcile);
        Assert.Equal(@"D:\Booth\Data", store.DataRoot);
        Assert.Equal(1, store.OpenCalls);
    }

    [Fact]
    public async Task Open_can_fail_until_cleared_and_then_succeeds_on_retry()
    {
        var store = new InMemoryEventStore(_clock);
        store.FailOpen(StorageErrorKind.CloudSynced);

        var first = await FailureAsync(store.OpenAsync(Ct));
        var second = await FailureAsync(store.OpenAsync(Ct));
        store.ClearFailures();
        var result = await store.OpenAsync(Ct);

        Assert.Equal(StorageErrorKind.CloudSynced, first.Kind);
        Assert.Equal(StorageErrorKind.CloudSynced, second.Kind);
        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal(3, store.OpenCalls);
    }

    [Fact]
    public async Task A_cancelled_open_is_cancelled_and_does_not_open_the_store()
    {
        var store = new InMemoryEventStore(_clock);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.OpenAsync(cancelled.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ListEventsAsync(Ct));
    }

    [Fact]
    public async Task Every_member_but_open_refuses_until_the_store_is_open()
    {
        var store = new InMemoryEventStore(_clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ListEventsAsync(Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetEventAsync(StorageIds.NewId(), Ct));
    }

    [Fact]
    public async Task A_cancelled_call_is_cancelled()
    {
        var store = await OpenStoreAsync();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ListEventsAsync(cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DrainAsync(cancelled.Token));
    }

    [Fact]
    public async Task FailNext_fails_one_call_then_clears_itself()
    {
        var store = await OpenStoreAsync();
        store.FailNext(StorageErrorKind.DiskFull);

        var failure = await FailureAsync(store.ListEventsAsync(Ct));
        var next = await store.ListEventsAsync(Ct);

        Assert.Equal(StorageErrorKind.DiskFull, failure.Kind);
        Assert.Empty(next);
    }

    [Fact]
    public async Task ClearFailures_clears_a_pending_fail_next()
    {
        var store = await OpenStoreAsync();
        store.FailNext(StorageErrorKind.Locked);

        store.ClearFailures();

        Assert.Empty(await store.ListEventsAsync(Ct));
    }

    [Fact]
    public async Task A_saved_event_reads_back_with_store_assigned_times_and_copies_of_its_lists()
    {
        var store = await OpenStoreAsync();
        var offered = new List<string> { "signature-strip" };
        var record = StorageSamples.Event(StorageIds.NewId()) with { OfferedTemplateIds = offered };

        await store.CreateEventAsync(record with { CreatedAt = DateTimeOffset.UnixEpoch }, Ct);
        offered.Add("changed-afterwards");
        var read = await store.GetEventAsync(record.Id, Ct);

        Assert.NotNull(read);
        Assert.Equal(["signature-strip"], read.OfferedTemplateIds);
        Assert.Equal(_clock.UtcNow, read.CreatedAt);
        Assert.Equal(_clock.UtcNow, read.UpdatedAt);
        Assert.Equal(record.Name, read.Name);
        Assert.Equal(record.PhaseTimers, read.PhaseTimers);
        Assert.False(read.HasPin);
    }

    [Fact]
    public async Task Creating_an_existing_event_conflicts()
    {
        var (store, record) = await StoreWithEventAsync();

        Assert.Equal(StorageErrorKind.Conflict, (await FailureAsync(store.CreateEventAsync(record, Ct))).Kind);
    }

    [Fact]
    public async Task An_invalid_event_is_an_argument_error()
    {
        var store = await OpenStoreAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateEventAsync(StorageSamples.Event(StorageIds.NewId()) with { CountdownSeconds = 4 }, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => store.GetEventAsync("event-1", Ct));
    }

    [Fact]
    public async Task Update_replaces_the_event_keeps_created_at_and_sets_updated_at()
    {
        var (store, record) = await StoreWithEventAsync();
        var created = _clock.UtcNow;
        _clock.Advance(TimeSpan.FromMinutes(5));

        await store.UpdateEventAsync(record with { Name = "Renamed", OfferedLookIds = ["monochrome-v1"], PhaseTimers = [] }, Ct);
        var read = await store.GetEventAsync(record.Id, Ct);

        Assert.Equal("Renamed", read!.Name);
        Assert.Equal(["monochrome-v1"], read.OfferedLookIds);
        Assert.Empty(read.PhaseTimers);
        Assert.Equal(created, read.CreatedAt);
        Assert.Equal(created.AddMinutes(5), read.UpdatedAt);
    }

    [Fact]
    public async Task Updating_a_missing_event_is_not_found()
    {
        var store = await OpenStoreAsync();

        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.UpdateEventAsync(StorageSamples.Event(StorageIds.NewId()), Ct))).Kind);
    }

    [Fact]
    public async Task Getting_a_missing_event_returns_null()
    {
        var store = await OpenStoreAsync();

        Assert.Null(await store.GetEventAsync(StorageIds.NewId(), Ct));
    }

    [Fact]
    public async Task The_event_list_is_sorted_by_date_then_creation_newest_first_with_counts_and_last_used()
    {
        var store = await OpenStoreAsync();
        var oldest = StorageSamples.Event(StorageIds.NewId(), "Old", new DateOnly(2026, 1, 1));
        var sameDayFirst = StorageSamples.Event(StorageIds.NewId(), "Same day first", new DateOnly(2026, 6, 1));
        var sameDaySecond = StorageSamples.Event(StorageIds.NewId(), "Same day second", new DateOnly(2026, 6, 1), venue: null);
        var newest = StorageSamples.Event(StorageIds.NewId(), "Newest", new DateOnly(2026, 12, 1));
        await store.CreateEventAsync(sameDayFirst, Ct);
        _clock.Advance(TimeSpan.FromSeconds(1));
        await store.CreateEventAsync(oldest, Ct);
        await store.CreateEventAsync(sameDaySecond, Ct);
        await store.CreateEventAsync(newest, Ct);
        var completed = await store.StartGuestCycleAsync(newest.Id, Ct);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(completed.Id, 1), Ct);
        await store.CommitPhotoStripAsync(StorageSamples.StripCommit(completed.Id, [capture.Id]), Ct);
        _clock.Advance(TimeSpan.FromMinutes(1));
        var abandoned = await store.StartGuestCycleAsync(newest.Id, Ct);
        await store.EndGuestCycleAsync(abandoned.Id, GuestCycleEndReason.StartedOver, Ct);

        var list = await store.ListEventsAsync(Ct);

        Assert.Equal(["Newest", "Same day second", "Same day first", "Old"], list.Select(summary => summary.Name));
        var summary = list[0];
        Assert.Equal(newest.Id, summary.Id);
        Assert.Equal(1, summary.CompletedGuestCycles);
        Assert.Equal(abandoned.StartedAt, summary.LastUsedAt);
        Assert.Equal("Harbour Hall", summary.Venue);
        Assert.Null(list[1].Venue);
        Assert.Null(list[1].LastUsedAt);
        Assert.Equal(0, list[1].CompletedGuestCycles);
    }

    [Fact]
    public async Task The_pin_is_set_verified_and_reported_through_has_pin()
    {
        var (store, record) = await StoreWithEventAsync();

        Assert.False(await store.VerifyPinAsync(record.Id, "4826", Ct));
        await store.SetPinAsync(record.Id, "4826", Ct);

        Assert.True(await store.VerifyPinAsync(record.Id, "4826", Ct));
        Assert.False(await store.VerifyPinAsync(record.Id, "0000", Ct));
        Assert.True((await store.GetEventAsync(record.Id, Ct))!.HasPin);
    }

    [Fact]
    public async Task Updating_an_event_keeps_its_pin()
    {
        var (store, record) = await StoreWithEventAsync();
        await store.SetPinAsync(record.Id, "4826", Ct);

        await store.UpdateEventAsync(record with { Name = "Renamed", HasPin = false }, Ct);

        Assert.True(await store.VerifyPinAsync(record.Id, "4826", Ct));
        Assert.True((await store.GetEventAsync(record.Id, Ct))!.HasPin);
    }

    [Fact]
    public async Task A_pin_works_with_an_injected_hasher()
    {
        var store = new InMemoryEventStore(_clock, new Pbkdf2PinHasher());
        await store.OpenAsync(Ct);
        var record = StorageSamples.Event(StorageIds.NewId());
        await store.CreateEventAsync(record, Ct);

        await store.SetPinAsync(record.Id, "4826", Ct);

        Assert.True(await store.VerifyPinAsync(record.Id, "4826", Ct));
    }

    [Fact]
    public async Task Pin_calls_on_a_missing_event_are_not_found_and_an_empty_pin_is_refused()
    {
        var (store, record) = await StoreWithEventAsync();
        var missing = StorageIds.NewId();

        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.SetPinAsync(missing, "4826", Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.VerifyPinAsync(missing, "4826", Ct))).Kind);
        await Assert.ThrowsAsync<ArgumentException>(() => store.SetPinAsync(record.Id, string.Empty, Ct));
    }

    [Fact]
    public async Task A_deleting_event_vanishes_at_once_and_drain_removes_it_with_its_cycles()
    {
        var (store, record) = await StoreWithEventAsync();
        var keep = StorageSamples.Event(StorageIds.NewId(), "Keep");
        await store.CreateEventAsync(keep, Ct);
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);

        await store.MarkEventDeletingAsync(record.Id, Ct);

        Assert.Equal([keep.Id], (await store.ListEventsAsync(Ct)).Select(summary => summary.Id));
        Assert.Null(await store.GetEventAsync(record.Id, Ct));
        Assert.Null(await store.GetGuestCycleAsync(cycle.Id, Ct));
        Assert.Equal(1, store.PendingDeletionCount);
        Assert.NotNull(store.GetCaptureFile(capture.Id));

        await store.DrainAsync(Ct);

        Assert.Equal(0, store.PendingDeletionCount);
        Assert.Null(store.GetCaptureFile(capture.Id));
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.StartGuestCycleAsync(record.Id, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.MarkEventDeletingAsync(record.Id, Ct))).Kind);
    }

    [Fact]
    public async Task A_deleting_event_can_not_be_changed()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        await store.MarkEventDeletingAsync(record.Id, Ct);

        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.UpdateEventAsync(record, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.OperatorExit, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, null, 10, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.GetThumbnailPathAsync(cycle.Id, 320, Ct))).Kind);
    }

    [Fact]
    public async Task Starting_a_cycle_needs_an_existing_event()
    {
        var store = await OpenStoreAsync();

        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.StartGuestCycleAsync(StorageIds.NewId(), Ct))).Kind);
    }

    [Fact]
    public async Task A_started_cycle_is_open_and_ending_it_records_the_reason_and_time()
    {
        var (store, record) = await StoreWithEventAsync();

        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        _clock.Advance(TimeSpan.FromMinutes(2));
        await store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.StartedOver, Ct);
        var detail = await store.GetGuestCycleAsync(cycle.Id, Ct);

        Assert.True(StorageIds.IsValid(cycle.Id));
        Assert.Equal(record.Id, cycle.EventId);
        Assert.Null(cycle.EndReason);
        Assert.Null(cycle.EndedAt);
        Assert.Equal(GuestCycleEndReason.StartedOver, detail!.Cycle.EndReason);
        Assert.Equal(_clock.UtcNow, detail.Cycle.EndedAt);
    }

    [Fact]
    public async Task Ending_rules_completed_is_refused_missing_is_not_found_and_twice_conflicts()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);

        await Assert.ThrowsAsync<ArgumentException>(() => store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.Completed, Ct));
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.EndGuestCycleAsync(StorageIds.NewId(), GuestCycleEndReason.AppClosed, Ct))).Kind);
        await store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.AppClosed, Ct);
        Assert.Equal(StorageErrorKind.Conflict, (await FailureAsync(store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.OperatorExit, Ct))).Kind);
    }

    [Fact]
    public async Task A_capture_commit_computes_hash_length_and_names_the_file_by_sequence()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var commit = StorageSamples.CaptureCommit(cycle.Id, 3);

        var capture = await store.CommitCaptureAsync(commit, Ct);

        Assert.Equal("capture-3.jpg", capture.FileName);
        Assert.Equal(64, capture.ByteLength);
        Assert.Equal(System.Security.Cryptography.SHA256.HashData(commit.Jpeg.Span).Length * 2, capture.Sha256.Length);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(commit.Jpeg.Span)), capture.Sha256);
        Assert.Equal(CaptureState.Kept, capture.State);
        Assert.Equal((cycle.Id, 3, 1280, 720, true, commit.TakenAt), (capture.GuestCycleId, capture.Sequence, capture.Width, capture.Height, capture.Mirrored, capture.TakenAt));
        Assert.Equal(commit.Jpeg.ToArray(), store.GetCaptureFile(capture.Id)!.Value.ToArray());
    }

    [Fact]
    public async Task Capture_commit_rules_duplicate_sequence_ended_cycle_and_missing_cycle()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);

        Assert.Equal(StorageErrorKind.Conflict, (await FailureAsync(store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct))).Kind);
        await Assert.ThrowsAsync<ArgumentException>(() => store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 0), Ct));
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.CommitCaptureAsync(StorageSamples.CaptureCommit(StorageIds.NewId(), 1), Ct))).Kind);
        await store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.OperatorExit, Ct);
        Assert.Equal(StorageErrorKind.Conflict, (await FailureAsync(store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 2), Ct))).Kind);
    }

    [Fact]
    public async Task A_discarded_capture_leaves_the_detail_and_its_file_goes_when_the_cycle_ends()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var kept = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);
        var discarded = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 2), Ct);

        await store.SetCaptureStateAsync(discarded.Id, CaptureState.Discarded, Ct);
        var before = await store.GetGuestCycleAsync(cycle.Id, Ct);
        Assert.NotNull(store.GetCaptureFile(discarded.Id));
        await store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.StartedOver, Ct);

        Assert.Equal([kept.Id], before!.Captures.Select(capture => capture.Id));
        Assert.Null(store.GetCaptureFile(discarded.Id));
        Assert.NotNull(store.GetCaptureFile(kept.Id));
    }

    [Fact]
    public async Task A_capture_can_be_kept_again_before_the_cycle_ends()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);

        await store.SetCaptureStateAsync(capture.Id, CaptureState.Discarded, Ct);
        await store.SetCaptureStateAsync(capture.Id, CaptureState.Kept, Ct);

        Assert.Single((await store.GetGuestCycleAsync(cycle.Id, Ct))!.Captures);
    }

    [Fact]
    public async Task Capture_state_rules_missing_ended_and_out_of_range()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);

        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.SetCaptureStateAsync(StorageIds.NewId(), CaptureState.Discarded, Ct))).Kind);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.SetCaptureStateAsync(capture.Id, (CaptureState)9, Ct));
        await store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.OperatorExit, Ct);
        Assert.Equal(StorageErrorKind.Conflict, (await FailureAsync(store.SetCaptureStateAsync(capture.Id, CaptureState.Discarded, Ct))).Kind);
    }

    [Fact]
    public async Task A_capture_of_a_deleting_cycle_is_not_found()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);
        await store.MarkGuestCycleDeletingAsync(cycle.Id, Ct);

        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.SetCaptureStateAsync(capture.Id, CaptureState.Discarded, Ct))).Kind);
    }

    [Fact]
    public async Task Committing_a_photo_strip_ends_the_cycle_as_completed_and_deletes_discarded_files()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var first = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);
        var second = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 2), Ct);
        var replaced = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 3), Ct);
        await store.SetCaptureStateAsync(replaced.Id, CaptureState.Discarded, Ct);
        var commit = StorageSamples.StripCommit(cycle.Id, [second.Id, first.Id]);
        _clock.Advance(TimeSpan.FromMinutes(1));

        var strip = await store.CommitPhotoStripAsync(commit, Ct);
        var detail = await store.GetGuestCycleAsync(cycle.Id, Ct);

        Assert.Equal("photo-strip.png", strip.FileName);
        Assert.Equal([second.Id, first.Id], strip.Arrangement);
        Assert.Equal(128, strip.ByteLength);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(commit.Png.Span)), strip.Sha256);
        Assert.Equal((cycle.Id, "signature-strip", "naturale-v1", 600, 1800, _clock.UtcNow), (strip.GuestCycleId, strip.TemplateId, strip.LookId, strip.Width, strip.Height, strip.CommittedAt));
        Assert.Equal(GuestCycleEndReason.Completed, detail!.Cycle.EndReason);
        Assert.Equal(strip.CommittedAt, detail.Cycle.EndedAt);
        Assert.Same(strip, detail.PhotoStrip);
        Assert.Equal(commit.Png.ToArray(), store.GetPhotoStripFile(cycle.Id)!.Value.ToArray());
        Assert.Null(store.GetCaptureFile(replaced.Id));
        Assert.Equal([1, 2], detail.Captures.Select(capture => capture.Sequence));
    }

    [Fact]
    public async Task Photo_strip_commit_rules()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var other = await store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);
        var foreign = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(other.Id, 1), Ct);
        var discarded = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 2), Ct);
        await store.SetCaptureStateAsync(discarded.Id, CaptureState.Discarded, Ct);

        await Assert.ThrowsAsync<ArgumentException>(() => store.CommitPhotoStripAsync(StorageSamples.StripCommit(cycle.Id, []), Ct));
        Assert.Equal(StorageErrorKind.Conflict, (await FailureAsync(store.CommitPhotoStripAsync(StorageSamples.StripCommit(cycle.Id, [foreign.Id]), Ct))).Kind);
        Assert.Equal(StorageErrorKind.Conflict, (await FailureAsync(store.CommitPhotoStripAsync(StorageSamples.StripCommit(cycle.Id, [discarded.Id]), Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.CommitPhotoStripAsync(StorageSamples.StripCommit(StorageIds.NewId(), [capture.Id]), Ct))).Kind);
        await store.CommitPhotoStripAsync(StorageSamples.StripCommit(cycle.Id, [capture.Id]), Ct);
        Assert.Equal(StorageErrorKind.Conflict, (await FailureAsync(store.CommitPhotoStripAsync(StorageSamples.StripCommit(cycle.Id, [capture.Id]), Ct))).Kind);
        await store.EndGuestCycleAsync(other.Id, GuestCycleEndReason.OperatorExit, Ct);
        Assert.Equal(StorageErrorKind.Conflict, (await FailureAsync(store.CommitPhotoStripAsync(StorageSamples.StripCommit(other.Id, [foreign.Id]), Ct))).Kind);
    }

    [Fact]
    public async Task Paging_returns_newest_first_with_a_stable_cursor_and_no_gaps()
    {
        var (store, record) = await StoreWithEventAsync();
        var ids = new List<string>();
        for (var index = 0; index < 7; index++)
        {
            ids.Add((await store.StartGuestCycleAsync(record.Id, Ct)).Id);
            _clock.Advance(TimeSpan.FromSeconds(index % 3 == 0 ? 0 : 1));
        }

        var seen = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, cursor, 3, Ct);
            seen.AddRange(page.Items.Select(item => item.Cycle.Id));
            cursor = page.NextCursor;
            pages++;
        }
        while (cursor is not null);

        Assert.Equal(3, pages);
        Assert.Equal(ids.Count, seen.Distinct().Count());
        Assert.Equal(seen, seen.Select(id => ids.IndexOf(id)).OrderDescending().Select(index => ids[index]));
    }

    [Fact]
    public async Task Paging_is_stable_when_newer_cycles_arrive_between_pages()
    {
        var (store, record) = await StoreWithEventAsync();
        for (var index = 0; index < 4; index++)
        {
            await store.StartGuestCycleAsync(record.Id, Ct);
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        var first = await store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, null, 2, Ct);
        await store.StartGuestCycleAsync(record.Id, Ct);
        var second = await store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, first.NextCursor, 2, Ct);

        Assert.Equal(2, second.Items.Count);
        Assert.Empty(second.Items.Select(item => item.Cycle.Id).Intersect(first.Items.Select(item => item.Cycle.Id)));
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task An_exact_page_has_no_next_cursor()
    {
        var (store, record) = await StoreWithEventAsync();
        await store.StartGuestCycleAsync(record.Id, Ct);
        await store.StartGuestCycleAsync(record.Id, Ct);

        var page = await store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, null, 2, Ct);

        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task The_guest_view_lists_only_completed_cycles_with_a_strip_and_counts_kept_captures()
    {
        var (store, record) = await StoreWithEventAsync();
        var open = await store.StartGuestCycleAsync(record.Id, Ct);
        var started = await store.StartGuestCycleAsync(record.Id, Ct);
        await store.EndGuestCycleAsync(started.Id, GuestCycleEndReason.StartedOver, Ct);
        var done = await store.StartGuestCycleAsync(record.Id, Ct);
        var one = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(done.Id, 1), Ct);
        var two = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(done.Id, 2), Ct);
        await store.SetCaptureStateAsync(two.Id, CaptureState.Discarded, Ct);
        await store.CommitPhotoStripAsync(StorageSamples.StripCommit(done.Id, [one.Id]), Ct);
        var deleting = await store.StartGuestCycleAsync(record.Id, Ct);
        await store.MarkGuestCycleDeletingAsync(deleting.Id, Ct);

        var all = await store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, null, 10, Ct);
        var completed = await store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.CompletedOnly, null, 10, Ct);

        Assert.Equal(3, all.Items.Count);
        Assert.DoesNotContain(all.Items, item => item.Cycle.Id == deleting.Id);
        Assert.Contains(all.Items, item => item.Cycle.Id == open.Id && item.KeptCaptureCount == 0 && item.PhotoStrip is null);
        var only = Assert.Single(completed.Items);
        Assert.Equal(done.Id, only.Cycle.Id);
        Assert.Equal(1, only.KeptCaptureCount);
        Assert.NotNull(only.PhotoStrip);
    }

    [Fact]
    public async Task Paging_arguments_are_checked_and_the_event_must_exist()
    {
        var (store, record) = await StoreWithEventAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, "garbage", 5, Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, null, 0, Ct));
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.ListGuestCyclesAsync(StorageIds.NewId(), GuestCycleFilter.All, null, 5, Ct))).Kind);
    }

    [Fact]
    public async Task A_deleting_cycle_vanishes_from_both_views_and_drain_removes_it()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);
        await store.CommitPhotoStripAsync(StorageSamples.StripCommit(cycle.Id, [capture.Id]), Ct);

        await store.MarkGuestCycleDeletingAsync(cycle.Id, Ct);

        Assert.Empty((await store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, null, 10, Ct)).Items);
        Assert.Empty((await store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.CompletedOnly, null, 10, Ct)).Items);
        Assert.Null(await store.GetGuestCycleAsync(cycle.Id, Ct));
        Assert.Equal(1, store.PendingDeletionCount);
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.MarkGuestCycleDeletingAsync(cycle.Id, Ct))).Kind);

        await store.DrainAsync(Ct);

        Assert.Equal(0, store.PendingDeletionCount);
        Assert.Null(store.GetCaptureFile(capture.Id));
        Assert.Null(store.GetPhotoStripFile(cycle.Id));
        Assert.NotNull(await store.GetEventAsync(record.Id, Ct));
    }

    [Fact]
    public async Task Files_of_unknown_ids_are_null()
    {
        var store = await OpenStoreAsync();

        Assert.Null(store.GetCaptureFile(StorageIds.NewId()));
        Assert.Null(store.GetPhotoStripFile(StorageIds.NewId()));
        Assert.Null(await store.GetGuestCycleAsync(StorageIds.NewId(), Ct));
    }

    [Fact]
    public async Task Thumbnail_paths_use_the_source_id_hash_prefix_and_width()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);
        var strip = await store.CommitPhotoStripAsync(StorageSamples.StripCommit(cycle.Id, [capture.Id]), Ct);

        var capturePath = await store.GetThumbnailPathAsync(capture.Id, 320, Ct);
        var stripPath = await store.GetThumbnailPathAsync(cycle.Id, 640, Ct);

        Assert.Equal($"Cache/thumbnails/{capture.Id}-{capture.Sha256[..8]}-320.jpg", capturePath);
        Assert.Equal($"Cache/thumbnails/{cycle.Id}-{strip.Sha256[..8]}-640.jpg", stripPath);
    }

    [Fact]
    public async Task Thumbnail_rules_unknown_source_no_strip_discarded_file_and_width()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);
        await store.SetCaptureStateAsync(capture.Id, CaptureState.Discarded, Ct);
        await store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.OperatorExit, Ct);

        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.GetThumbnailPathAsync(StorageIds.NewId(), 320, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.GetThumbnailPathAsync(cycle.Id, 320, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.GetThumbnailPathAsync(capture.Id, 320, Ct))).Kind);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetThumbnailPathAsync(capture.Id, 10, Ct));
    }

    [Fact]
    public async Task A_discarded_Capture_has_no_thumbnail_even_while_its_cycle_is_open()
    {
        var (store, record) = await StoreWithEventAsync();
        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), Ct);
        await store.GetThumbnailPathAsync(capture.Id, 320, Ct);

        await store.SetCaptureStateAsync(capture.Id, CaptureState.Discarded, Ct);

        Assert.Equal(StorageErrorKind.NotFound, (await FailureAsync(store.GetThumbnailPathAsync(capture.Id, 320, Ct))).Kind);
        await store.SetCaptureStateAsync(capture.Id, CaptureState.Kept, Ct);
        Assert.StartsWith("Cache/thumbnails/", await store.GetThumbnailPathAsync(capture.Id, 320, Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Supplied_id_generators_and_default_ids_are_used()
    {
        var counter = 0;
        var store = new InMemoryEventStore(_clock, newId: () => $"00000000-0000-0000-0000-{++counter:D12}");
        await store.OpenAsync(Ct);
        var record = StorageSamples.Event(StorageIds.NewId());
        await store.CreateEventAsync(record, Ct);

        var cycle = await store.StartGuestCycleAsync(record.Id, Ct);
        var defaults = await OpenStoreAsync();
        await defaults.CreateEventAsync(record, Ct);

        Assert.Equal("00000000-0000-0000-0000-000000000001", cycle.Id);
        Assert.Equal("00000000-0000-0000-0000-000000000001", (await defaults.StartGuestCycleAsync(record.Id, Ct)).Id);
        Assert.Equal("00000000-0000-0000-0000-000000000002", (await defaults.StartGuestCycleAsync(record.Id, Ct)).Id);
    }

    [Fact]
    public async Task The_default_clock_does_not_move_and_disposal_completes()
    {
        var store = new InMemoryEventStore();
        await store.OpenAsync(Ct);
        var record = StorageSamples.Event(StorageIds.NewId());
        await store.CreateEventAsync(record, Ct);

        var first = await store.StartGuestCycleAsync(record.Id, Ct);
        var second = await store.StartGuestCycleAsync(record.Id, Ct);
        await store.DisposeAsync();

        Assert.Equal(first.StartedAt, second.StartedAt);
    }
}
