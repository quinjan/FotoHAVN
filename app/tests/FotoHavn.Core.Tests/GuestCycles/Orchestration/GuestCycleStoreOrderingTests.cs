using FotoHavn.Core.Camera;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.GuestCycles.Orchestration;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>
/// The Guest Cycle's store writes happen in the order the brain decided them (a discarded Capture's file goes when the cycle ends or the
/// Photo Strip commits, and the store refuses a discard after either); a Retake candidate that is never accepted is discarded on every path
/// that drops it; and a stale preview can't disturb the final strip (M8 review fixes F3 to F5).
/// </summary>
public sealed class GuestCycleStoreOrderingTests
{
    private static readonly TimeSpan Fine = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan Coarse = TimeSpan.FromSeconds(1);

    private static GuestRigOptions OneLook => new() { Tweak = record => record with { OfferedLookIds = ["naturale-v1"] } };

    private static int StripRequests(GuestCycleRig rig) => rig.Compositor.Requests.Count(request => request.Photos.Any(photo => photo is not null));

    /// <summary>Retakes the window and waits for its candidate; the candidate's Capture is the cycle's fifth.</summary>
    private static CaptureRecord RetakeToCandidate(GuestCycleRig rig, int index)
    {
        rig.Guest("BeginRetake", $$"""{"index":{{index}}}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.RetakeCandidate is not null, TimeSpan.FromSeconds(30), Fine));
        return rig.Store.Committed.Single(capture => capture.Sequence == 5);
    }

    /// <summary>The final strip has been composed (the store write that follows is what a test holds back).</summary>
    private static void AwaitFinalComposition(GuestCycleRig rig, int before) =>
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake || (StripRequests(rig) > before && rig.Compositor.InFlight == 0), TimeSpan.FromSeconds(90), Coarse));

    private static bool ReachesKeepsake(GuestCycleRig rig, TimeSpan budget) => rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, budget, Fine);

    private static async Task AssertDiscardedAndGoneAsync(GuestCycleRig rig, string captureId)
    {
        var detail = await rig.OnlyCycleAsync();
        Assert.DoesNotContain(detail.Captures, capture => capture.Id == captureId);
        Assert.Null(rig.Memory.GetCaptureFile(captureId));
    }

    private static void StartOver(GuestCycleRig rig)
    {
        rig.Guest("StartOver").ShouldBeAccepted();
        rig.Guest("ConfirmStartOver").ShouldBeAccepted();
    }

    [Fact]
    public async Task The_review_timers_discard_of_a_candidate_reaches_the_store_before_the_Photo_Strip_commit()
    {
        using var rig = await GuestCycleRig.CreateAsync(OneLook);
        rig.ToArrange();
        var candidate = RetakeToCandidate(rig, 1);
        rig.Store.SetStateGate = new TaskCompletionSource();
        var before = StripRequests(rig);

        AwaitFinalComposition(rig, before);

        Assert.False(ReachesKeepsake(rig, TimeSpan.FromSeconds(2)), "The Photo Strip was committed before the candidate's discard reached the store.");
        rig.Store.SetStateGate.SetResult();
        rig.Store.SetStateGate = null;
        Assert.True(ReachesKeepsake(rig, TimeSpan.FromSeconds(10)));
        await AssertDiscardedAndGoneAsync(rig, candidate.Id);
        Assert.Equal(GuestSurface.Keepsake, rig.Surface);
        Assert.Equal(GuestCycleEndReason.Completed, (await rig.OnlyCycleAsync()).Cycle.EndReason);
    }

    [Fact]
    public async Task Accepting_a_Retake_and_finishing_the_strip_at_once_discards_the_original_before_the_commit()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var original = rig.Photographs.Windows[2].CaptureId!;
        RetakeToCandidate(rig, 2);
        rig.Store.SetStateGate = new TaskCompletionSource();
        rig.Guest("AcceptRetake").ShouldBeAccepted();
        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => !rig.Filters.Composing, TimeSpan.FromSeconds(10), Fine));
        var before = StripRequests(rig);
        rig.Guest("FinishStrip").ShouldBeAccepted();
        AwaitFinalComposition(rig, before);

        Assert.False(ReachesKeepsake(rig, TimeSpan.FromSeconds(2)), "The Photo Strip was committed before the original's discard reached the store.");
        rig.Store.SetStateGate.SetResult();
        rig.Store.SetStateGate = null;
        Assert.True(ReachesKeepsake(rig, TimeSpan.FromSeconds(10)));
        await AssertDiscardedAndGoneAsync(rig, original);
        Assert.NotNull((await rig.OnlyCycleAsync()).PhotoStrip);
    }

    [Fact]
    public async Task Start_over_with_a_candidate_waiting_discards_it_and_deletes_its_file()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var candidate = RetakeToCandidate(rig, 1);

        StartOver(rig);

        Assert.Equal(GuestSurface.Start, rig.Surface);
        Assert.Equal(GuestCycleEndReason.StartedOver, (await rig.OnlyCycleAsync()).Cycle.EndReason);
        await AssertDiscardedAndGoneAsync(rig, candidate.Id);
        Assert.Equal(4, (await rig.OnlyCycleAsync()).Captures.Count(capture => capture.State == CaptureState.Kept));
    }

    [Fact]
    public async Task END_EVENT_with_a_candidate_waiting_discards_it_and_deletes_its_file()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var candidate = RetakeToCandidate(rig, 0);

        rig.Operator("EndEvent").ShouldBeAccepted();
        rig.Operator("ConfirmEndEvent").ShouldBeAccepted();

        Assert.Equal(GuestCycleEndReason.OperatorExit, (await rig.OnlyCycleAsync()).Cycle.EndReason);
        await AssertDiscardedAndGoneAsync(rig, candidate.Id);
    }

    [Fact]
    public async Task The_guests_PIN_exit_with_a_candidate_waiting_discards_it()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var candidate = RetakeToCandidate(rig, 3);

        rig.Active.OpenAndCompleteExitWithPin();
        rig.Settle();

        Assert.Equal(GuestCycleEndReason.OperatorExit, (await rig.OnlyCycleAsync()).Cycle.EndReason);
        await AssertDiscardedAndGoneAsync(rig, candidate.Id);
    }

    [Fact]
    public async Task A_Retake_Capture_that_commits_after_start_over_is_discarded_before_the_cycle_ends()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.Store.CommitGate = new TaskCompletionSource();
        rig.Guest("BeginRetake", """{"index":1}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Store.InFlight > 0, TimeSpan.FromSeconds(30), Fine));

        StartOver(rig);
        rig.Store.CommitGate.SetResult();
        rig.Store.CommitGate = null;
        rig.Advance(TimeSpan.FromSeconds(1), Fine);

        Assert.Equal(GuestSurface.Start, rig.Surface);
        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(GuestCycleEndReason.StartedOver, detail.Cycle.EndReason);
        await AssertDiscardedAndGoneAsync(rig, rig.Store.Committed.Single(capture => capture.Sequence == 5).Id);
    }

    [Fact]
    public async Task A_Sequence_Capture_that_commits_after_start_over_stays_kept()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.Store.CommitGate = new TaskCompletionSource();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Store.InFlight > 0, TimeSpan.FromSeconds(30), Fine));

        StartOver(rig);
        rig.Store.CommitGate.SetResult();
        rig.Store.CommitGate = null;
        rig.Advance(TimeSpan.FromSeconds(1), Fine);

        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(GuestCycleEndReason.StartedOver, detail.Cycle.EndReason);
        Assert.Equal(CaptureState.Kept, Assert.Single(detail.Captures).State);
    }

    [Fact]
    public async Task A_Look_preview_still_composing_when_the_Filters_timer_finishes_the_strip_does_not_end_the_composing()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        var preview = new TaskCompletionSource();
        rig.Compositor.StripGate = preview;
        rig.Guest("SelectLook", """{"lookId":"monochrome-v1"}""").ShouldBeAccepted();
        Assert.True(rig.Filters.Composing);
        var final = new TaskCompletionSource();
        rig.Compositor.StripGate = final;
        Assert.True(rig.AdvanceUntil(() => rig.Compositor.InFlight == 2, TimeSpan.FromSeconds(60), Coarse));

        preview.SetResult();
        Assert.True(rig.AdvanceUntil(() => rig.Brain.OffExecutorWork == 1, TimeSpan.FromSeconds(10), Fine));

        Assert.True(rig.Filters.Composing);
        Assert.False(rig.Filters.CompositionFailed);
        Assert.False(rig.Filters.CanFinish);
        final.SetResult();
        Assert.True(ReachesKeepsake(rig, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task A_stale_preview_that_lands_after_the_Keepsake_leaves_no_preview_in_the_cache()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        var preview = new TaskCompletionSource();
        rig.Compositor.StripGate = preview;
        rig.Guest("SelectLook", """{"lookId":"monochrome-v1"}""").ShouldBeAccepted();
        var final = new TaskCompletionSource();
        rig.Compositor.StripGate = final;
        Assert.True(rig.AdvanceUntil(() => rig.Compositor.InFlight == 2, TimeSpan.FromSeconds(60), Coarse));
        final.SetResult();
        Assert.True(ReachesKeepsake(rig, TimeSpan.FromSeconds(10)));
        var cached = rig.Active.Previews.Count;

        preview.SetResult();
        Assert.True(rig.AdvanceUntil(() => rig.Brain.OffExecutorWork == 0, TimeSpan.FromSeconds(10), Fine));

        Assert.Equal(cached, rig.Active.Previews.Count);
        Assert.Equal(GuestSurface.Keepsake, rig.Surface);
    }

    [Fact]
    public async Task A_Look_thumbnail_that_lands_after_the_Keepsake_leaves_no_preview_in_the_cache()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var thumbnails = new TaskCompletionSource();
        rig.Compositor.ThumbnailGate = thumbnails;
        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => !rig.Filters.Composing, TimeSpan.FromSeconds(10), Fine));
        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(ReachesKeepsake(rig, TimeSpan.FromSeconds(10)));
        var cached = rig.Active.Previews.Count;

        thumbnails.SetResult();
        Assert.True(rig.AdvanceUntil(() => rig.Brain.OffExecutorWork == 0, TimeSpan.FromSeconds(10), Fine));

        Assert.Equal(cached, rig.Active.Previews.Count);
    }

    [Fact]
    public async Task The_rig_counts_a_frame_being_encoded_as_work_the_brain_has_not_yet_handled()
    {
        using var encoder = new BlockingEncoder();
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { Encoder = encoder });
        rig.Begin();
        rig.ToPhotographs();
        rig.Guest("TakePhotographs").ShouldBeAccepted();

        // No Settle here: with the encoder held, Settle would rightly wait for it.
        for (var step = 0; step < 2000 && !encoder.Entered.IsSet; step++)
        {
            rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
            rig.Active.Executor.RunPending();
            encoder.Entered.Wait(5, TestContext.Current.CancellationToken);
        }

        Assert.True(encoder.Entered.IsSet, "The encoder was never reached.");
        Assert.True(rig.Brain.OffExecutorWork > 0);
        encoder.Release.Set();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Sequence.Pausing, TimeSpan.FromSeconds(30), Fine));
        rig.Settle();
        Assert.Equal(0, rig.Brain.OffExecutorWork);
    }

    private sealed class BlockingEncoder : ICaptureEncoder, IDisposable
    {
        private readonly FakeCaptureEncoder _inner = new();

        public ManualResetEventSlim Entered { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public byte[] EncodeJpeg(CapturedFrame frame)
        {
            Entered.Set();
            Release.Wait(TestContext.Current.CancellationToken);
            return _inner.EncodeJpeg(frame);
        }

        public void Dispose()
        {
            Entered.Dispose();
            Release.Dispose();
        }
    }
}
