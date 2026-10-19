using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>The Phase Timers (plan section 7, Phase timers, D18): each phase's outcome, the always-visible timer with no MORE TIME, pausing, restarting, and the timer's absence.</summary>
public sealed class GuestCycleTimerTests
{
    private static readonly TimeSpan Fine = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Coarse = TimeSpan.FromSeconds(1);

    private static GuestRigOptions With(int template = 30, int capture = 30, int review = 45, int filters = 30, int keepsake = 12) =>
        new() { Tweak = GuestCycleRig.Timers(template, capture, review, filters, keepsake) };

    private static bool Reaches(GuestCycleRig rig, GuestSurface surface, int seconds, TimeSpan? step = null) =>
        rig.AdvanceUntil(() => rig.Surface == surface, TimeSpan.FromSeconds(seconds), step ?? Coarse);

    /// <summary>
    /// Leaves the cycle on the Camera tab with window 0 filled and the rest empty and no sequence running. Guests can't stop a sequence, so only
    /// Operator Assistance interrupts one: the camera fails during the second countdown, and RETRY brings the cycle back.
    /// </summary>
    private static void InterruptAfterFirstCapture(GuestCycleRig rig)
    {
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Windows[0].CaptureId is not null && rig.Photographs.Sequence.CountdownRemaining == 4, TimeSpan.FromSeconds(30), Fine));
        rig.Active.Camera.Fail(CameraFailureKind.Failed);
        rig.Settle();
        Assert.Equal(GuestSurface.OperatorAssistance, rig.Surface);
        rig.Active.Camera.Recover();
        rig.Guest("RetryAssistance").ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(1), Fine);
        Assert.Equal(GuestSurface.Photographs, rig.Surface);
        Assert.False(rig.Photographs.Sequence.Running);
    }

    [Fact]
    public async Task The_Template_timer_continues_with_the_selected_Template()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.Guest("SelectTemplate", """{"templateId":"panorama-four"}""").ShouldBeAccepted();

        Assert.True(Reaches(rig, GuestSurface.Photographs, 40));

        Assert.Equal("panorama-four", rig.Photographs.Template.Id);
        Assert.Equal(PhaseTimer.PhotographsCapture, rig.CycleDiagnostics().Timer.Phase);
        Assert.Equal(PhotographsTabName.Camera, rig.Photographs.Tab);
    }

    [Fact]
    public async Task Each_phase_runs_the_Events_seconds()
    {
        using var rig = await GuestCycleRig.CreateAsync(With(template: 25, capture: 40, review: 50));
        rig.Begin();
        Assert.Equal(25_000, rig.CycleDiagnostics().Timer.RemainingMs);
        rig.ToPhotographs();
        Assert.Equal(40_000, rig.CycleDiagnostics().Timer.RemainingMs);
        rig.TakeAll();
        Assert.Equal(PhaseTimer.PhotographsReview, rig.CycleDiagnostics().Timer.Phase);
        Assert.Equal(50_000, rig.CycleDiagnostics().Timer.RemainingMs);
    }

    [Fact]
    public async Task A_timer_the_operator_turned_off_waits_at_its_phase_and_shows_no_timer()
    {
        using var rig = await GuestCycleRig.CreateAsync(With(template: 0, filters: 0));
        rig.Begin();
        var template = rig.CycleDiagnostics().Timer;
        Assert.Null(template.Phase);
        Assert.Null(template.RemainingMs);

        rig.Advance(TimeSpan.FromMinutes(10), Coarse);
        Assert.Equal(GuestSurface.Template, rig.Surface);
        Assert.Null(rig.View.Overlays.PhaseTimer);

        rig.ToPhotographs();
        Assert.Equal(PhaseTimer.PhotographsCapture, rig.View.Overlays.PhaseTimer!.Phase);
        rig.TakeAll();
        rig.ToFilters();
        rig.Advance(TimeSpan.FromMinutes(10), Coarse);
        Assert.Equal(GuestSurface.Filters, rig.Surface);
        Assert.Null(rig.View.Overlays.PhaseTimer);
    }

    [Fact]
    public async Task A_cycle_abandoned_at_a_phase_with_its_timer_off_is_cleared_only_by_ending_the_Event()
    {
        using var rig = await GuestCycleRig.CreateAsync(With(template: 0));
        rig.Begin();
        rig.Advance(TimeSpan.FromMinutes(30), Coarse);
        Assert.Equal(GuestSurface.Template, rig.Surface);

        rig.Operator("EndEvent").ShouldBeAccepted();
        rig.Operator("ConfirmEndEvent").ShouldBeAccepted();
        rig.Settle();

        var ended = Assert.Single(rig.Store.Ended);
        Assert.Equal(GuestCycleEndReason.OperatorExit, ended.Reason);
    }

    [Fact]
    public async Task The_capture_and_review_timers_can_be_off_too()
    {
        using var rig = await GuestCycleRig.CreateAsync(With(capture: 0, review: 0));
        rig.Begin();
        rig.ToPhotographs();

        rig.Advance(TimeSpan.FromMinutes(1), Coarse);
        Assert.Equal(PhotographsTabName.Camera, rig.Photographs.Tab);
        Assert.False(rig.Photographs.Sequence.Running);

        rig.TakeAll();
        rig.Advance(TimeSpan.FromMinutes(2), Coarse);
        Assert.Equal(GuestSurface.Photographs, rig.Surface);
    }

    [Fact]
    public async Task The_timer_shows_for_the_whole_phase_counting_down_once_a_second()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();

        var shown = new List<int>();
        Assert.True(rig.AdvanceUntil(
            () =>
            {
                if (rig.View.Overlays.PhaseTimer is { Phase: PhaseTimer.Template } timer)
                {
                    Assert.False(timer.Paused);
                    shown.Add(timer.RemainingSeconds);
                }

                return rig.Surface != GuestSurface.Template;
            },
            TimeSpan.FromSeconds(35),
            Fine));

        Assert.Equal(Enumerable.Range(1, 30).Reverse(), shown.Distinct());
        Assert.Equal(new PhaseTimerOverlay(PhaseTimer.PhotographsCapture, 30, false), rig.View.Overlays.PhaseTimer);
    }

    [Fact]
    public async Task The_timer_is_pushed_each_time_the_whole_second_it_shows_changes()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        var previous = rig.View.Overlays.PhaseTimer!.RemainingSeconds;

        for (var step = 0; step < 50; step++)
        {
            rig.Advance(TimeSpan.FromMilliseconds(100));
            var now = rig.View.Overlays.PhaseTimer!.RemainingSeconds;
            Assert.InRange(previous - now, 0, 1);
            previous = now;
        }

        Assert.Equal(25, previous);
    }

    [Fact]
    public async Task The_diagnostics_carry_the_time_left()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();

        rig.Advance(TimeSpan.FromSeconds(12), Fine);

        var timer = rig.CycleDiagnostics().Timer;
        Assert.False(timer.Paused);
        Assert.InRange(timer.RemainingMs!.Value, 17_000, 18_100);
        Assert.Equal(18, rig.View.Overlays.PhaseTimer!.RemainingSeconds);
    }

    [Fact]
    public async Task Guests_cannot_add_time_MORE_TIME_is_unknown_and_the_phase_ends_on_time()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.Advance(TimeSpan.FromSeconds(25), Fine);

        rig.Guest("MoreTime").ShouldBeRejected(RejectReason.UnknownCommand);

        Assert.InRange(rig.CycleDiagnostics().Timer.RemainingMs!.Value, 4_900, 5_000);
        Assert.True(Reaches(rig, GuestSurface.Photographs, 6, Fine));
    }

    [Fact]
    public async Task The_timer_shows_the_phases_full_time_as_it_starts()
    {
        using var rig = await GuestCycleRig.CreateAsync(With(template: 20));

        rig.Begin();

        Assert.Equal(new PhaseTimerOverlay(PhaseTimer.Template, 20, false), rig.View.Overlays.PhaseTimer);
    }

    [Fact]
    public async Task The_capture_timer_starts_the_sequence_for_every_empty_window()
    {
        using var rig = await GuestCycleRig.CreateAsync(With(capture: 20));
        rig.Begin();
        rig.ToPhotographs();
        Assert.Equal(20_000, rig.CycleDiagnostics().Timer.RemainingMs);

        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Sequence.Running, TimeSpan.FromSeconds(25), Fine));

        Assert.Equal(0, rig.Photographs.Sequence.TargetWindow);
        Assert.Equal(4, rig.Photographs.Sequence.TotalInSequence);
        Assert.True(rig.AdvanceUntil(() => !rig.Photographs.Sequence.Running, TimeSpan.FromMinutes(1), Fine));
        Assert.All(rig.Photographs.Windows, window => Assert.NotNull(window.CaptureId));
        Assert.Equal(PhaseTimer.PhotographsReview, rig.CycleDiagnostics().Timer.Phase);
    }

    [Fact]
    public async Task The_capture_timer_keeps_the_original_of_a_Retake_left_waiting_and_takes_the_empty_windows()
    {
        using var rig = await GuestCycleRig.CreateAsync(With(capture: 20));
        rig.Begin();
        rig.ToPhotographs();
        InterruptAfterFirstCapture(rig);
        var original = rig.Photographs.Windows[0].CaptureId;
        rig.Guest("BeginRetake", """{"index":0}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.RetakeCandidate is not null, TimeSpan.FromSeconds(30), Fine));

        // Nobody answers the Retake: the capture timer ends it as KEEP ORIGINAL, then fills the empty windows, rather than restarting forever.
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Sequence.Running, TimeSpan.FromSeconds(25), Fine));

        Assert.Null(rig.Photographs.RetakeCandidate);
        Assert.Equal(original, rig.Photographs.Windows[0].CaptureId);
        Assert.Equal(1, rig.Photographs.Sequence.TargetWindow);
        Assert.True(rig.AdvanceUntil(() => !rig.Photographs.Sequence.Running, TimeSpan.FromMinutes(1), Fine));
        Assert.All(rig.Photographs.Windows, window => Assert.NotNull(window.CaptureId));
        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(4, detail.Captures.Count);
    }

    [Fact]
    public async Task The_timer_pauses_while_a_Retake_counts_down_and_resumes_at_the_candidate()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.Advance(TimeSpan.FromSeconds(10), Coarse);
        var before = rig.CycleDiagnostics().Timer.RemainingMs!.Value;
        Assert.InRange(before, 34_000, 35_100);

        rig.Guest("BeginRetake", """{"index":1}""").ShouldBeAccepted();
        Assert.True(rig.CycleDiagnostics().Timer.Paused);
        rig.Advance(TimeSpan.FromSeconds(3), Coarse);
        Assert.Equal(before, rig.CycleDiagnostics().Timer.RemainingMs);

        Assert.True(rig.AdvanceUntil(() => rig.Photographs.RetakeCandidate is not null, TimeSpan.FromSeconds(30), Fine));
        var resumed = rig.CycleDiagnostics().Timer;
        Assert.False(resumed.Paused);
        Assert.InRange(resumed.RemainingMs!.Value, before - 1_500, before);
    }

    [Fact]
    public async Task The_timer_stays_paused_and_visible_across_a_whole_capture_sequence()
    {
        using var rig = await GuestCycleRig.CreateAsync(With(capture: 60));
        rig.Begin();
        rig.ToPhotographs();
        rig.Advance(TimeSpan.FromSeconds(5), Coarse);
        var before = rig.CycleDiagnostics().Timer.RemainingMs;

        rig.Guest("TakePhotographs").ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(12), Coarse);

        var during = rig.CycleDiagnostics().Timer;
        Assert.True(during.Paused);
        Assert.Equal(before, during.RemainingMs);
        Assert.Equal(new PhaseTimerOverlay(PhaseTimer.PhotographsCapture, 55, true), rig.View.Overlays.PhaseTimer);
    }

    [Fact]
    public async Task Showing_the_Camera_tab_and_entering_a_phase_restart_the_timer()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        Assert.Equal(PhaseTimer.PhotographsReview, rig.CycleDiagnostics().Timer.Phase);
        Assert.Equal(45_000, rig.CycleDiagnostics().Timer.RemainingMs);
        rig.Advance(TimeSpan.FromSeconds(10), Coarse);
        rig.Guest("ShowCameraTab").ShouldBeAccepted();
        Assert.Equal(45_000, rig.CycleDiagnostics().Timer.RemainingMs);

        rig.ToFilters();
        Assert.Equal(PhaseTimer.Filters, rig.CycleDiagnostics().Timer.Phase);
        Assert.Equal(30_000, rig.CycleDiagnostics().Timer.RemainingMs);
    }

    [Fact]
    public async Task The_review_timer_discards_a_pending_candidate_cancels_a_Move_and_continues_to_Filters()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var ids = rig.Photographs.Windows.Select(window => window.CaptureId).ToArray();
        rig.Guest("BeginRetake", """{"index":1}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.RetakeCandidate is not null, TimeSpan.FromSeconds(30), Fine));

        Assert.True(Reaches(rig, GuestSurface.Filters, 60));

        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(4, detail.Captures.Count(capture => capture.State == CaptureState.Kept));
        Assert.Equal(ids, await StripArrangementAsync(rig));
    }

    /// <summary>The Photo Strip's Arrangement once the Filters timer has finished it: guests can't go back to look at the Photographs step.</summary>
    private static async Task<string?[]> StripArrangementAsync(GuestCycleRig rig)
    {
        Assert.True(Reaches(rig, GuestSurface.Keepsake, 60));
        return [.. (await rig.OnlyCycleAsync()).PhotoStrip!.Arrangement];
    }

    [Fact]
    public async Task The_review_timer_cancels_a_pending_Move()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var ids = rig.Photographs.Windows.Select(window => window.CaptureId).ToArray();
        rig.Guest("BeginMove", """{"index":0}""").ShouldBeAccepted();

        Assert.True(Reaches(rig, GuestSurface.Filters, 60));

        Assert.Equal(ids, await StripArrangementAsync(rig));
    }

    [Fact]
    public async Task With_one_Look_the_review_timer_finishes_the_strip_directly()
    {
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { Tweak = record => record with { OfferedLookIds = ["naturale-v1"] } });
        rig.ToArrange();

        Assert.True(Reaches(rig, GuestSurface.Keepsake, 80));

        Assert.Equal(GuestCycleEndReason.Completed, (await rig.OnlyCycleAsync()).Cycle.EndReason);
    }

    [Fact]
    public async Task The_Filters_timer_finishes_with_the_selected_Look()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        rig.Guest("SelectLook", """{"lookId":"monochrome-v1"}""").ShouldBeAccepted();

        Assert.True(Reaches(rig, GuestSurface.Keepsake, 60));

        Assert.Equal("monochrome-v1", (await rig.OnlyCycleAsync()).PhotoStrip!.LookId);
    }

    [Fact]
    public async Task The_Filters_timer_finishes_even_when_a_preview_failed()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Compositor.FailStrips = 1;
        rig.ToArrange();
        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Filters.CompositionFailed, TimeSpan.FromSeconds(5), Fine));

        Assert.True(Reaches(rig, GuestSurface.Keepsake, 60));
    }

    [Fact]
    public async Task The_Keepsake_timer_shows_and_returns_to_Start_with_the_cycle_still_completed()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(Reaches(rig, GuestSurface.Keepsake, 10, Fine));
        Assert.Equal(PhaseTimer.Keepsake, rig.CycleDiagnostics().Timer.Phase);

        var shown = new List<int>();
        Assert.True(rig.AdvanceUntil(
            () =>
            {
                if (rig.View.Overlays.PhaseTimer is { } timer)
                {
                    Assert.Equal(PhaseTimer.Keepsake, timer.Phase);
                    shown.Add(timer.RemainingSeconds);
                }

                return rig.Surface == GuestSurface.Start;
            },
            TimeSpan.FromSeconds(20),
            Fine));

        Assert.Equal(Enumerable.Range(1, 12).Reverse(), shown.Distinct());
        Assert.Null(rig.View.Overlays.PhaseTimer);
        Assert.Null(rig.Diagnostics().GuestCycle);
        Assert.Equal(1, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
        Assert.Equal(GuestCycleEndReason.Completed, (await rig.OnlyCycleAsync()).Cycle.EndReason);
        Assert.Empty(rig.Store.Ended);
        rig.Guest("BeginGuestCycle").ShouldBeAccepted();
    }

    [Fact]
    public async Task The_timer_keeps_running_whatever_the_guest_touches()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.Advance(TimeSpan.FromSeconds(10), Coarse);

        rig.Guest("SelectTemplate", """{"templateId":"panorama-four"}""").ShouldBeAccepted();
        rig.Guest("SelectTemplate", """{"templateId":"signature-strip"}""").ShouldBeAccepted();

        Assert.InRange(rig.CycleDiagnostics().Timer.RemainingMs!.Value, 19_000, 20_100);
    }
}
