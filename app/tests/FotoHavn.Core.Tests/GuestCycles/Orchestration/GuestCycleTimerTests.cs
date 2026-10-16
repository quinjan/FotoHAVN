using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>The Phase Timers (plan section 7, Phase timers, D18): each phase's outcome, the warning and MORE TIME, pausing, restarting, and the timer's absence.</summary>
public sealed class GuestCycleTimerTests
{
    private static readonly TimeSpan Fine = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Coarse = TimeSpan.FromSeconds(1);

    private static GuestRigOptions With(int template = 30, int capture = 30, int review = 45, int filters = 30, int keepsake = 12) =>
        new() { Tweak = GuestCycleRig.Timers(template, capture, review, filters, keepsake) };

    private static bool Reaches(GuestCycleRig rig, GuestSurface surface, int seconds, TimeSpan? step = null) =>
        rig.AdvanceUntil(() => rig.Surface == surface, TimeSpan.FromSeconds(seconds), step ?? Coarse);

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
    public async Task A_timer_the_operator_turned_off_waits_at_its_phase()
    {
        using var rig = await GuestCycleRig.CreateAsync(With(template: 0, filters: 0));
        rig.Begin();
        var template = rig.CycleDiagnostics().Timer;
        Assert.Null(template.Phase);
        Assert.Null(template.RemainingMs);

        rig.Advance(TimeSpan.FromMinutes(10), Coarse);
        Assert.Equal(GuestSurface.Template, rig.Surface);
        Assert.Null(rig.View.Overlays.PhaseTimerWarning);

        rig.ToPhotographs();
        rig.TakeAll();
        rig.ToFilters();
        rig.Advance(TimeSpan.FromMinutes(10), Coarse);
        Assert.Equal(GuestSurface.Filters, rig.Surface);
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
    public async Task The_warning_shows_in_the_last_20_seconds_counting_down_once_a_second()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();

        rig.Advance(TimeSpan.FromSeconds(9), Fine);
        Assert.Null(rig.View.Overlays.PhaseTimerWarning);
        Assert.False(rig.CycleDiagnostics().Timer.Warning);

        var shown = new List<int>();
        Assert.True(rig.AdvanceUntil(
            () =>
            {
                if (rig.View.Overlays.PhaseTimerWarning is { } warning)
                {
                    Assert.Equal(PhaseTimer.Template, warning.Phase);
                    shown.Add(warning.RemainingSeconds);
                }

                return rig.Surface != GuestSurface.Template;
            },
            TimeSpan.FromSeconds(30),
            Fine));

        Assert.Equal(Enumerable.Range(1, 20).Reverse(), shown.Distinct());
        Assert.Null(rig.View.Overlays.PhaseTimerWarning);
    }

    [Fact]
    public async Task The_warning_is_in_the_diagnostics()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();

        rig.Advance(TimeSpan.FromSeconds(12), Fine);

        var timer = rig.CycleDiagnostics().Timer;
        Assert.True(timer.Warning);
        Assert.False(timer.Paused);
        Assert.InRange(timer.RemainingMs!.Value, 17_000, 18_100);
    }

    [Fact]
    public async Task MORE_TIME_restarts_the_phases_full_time_and_clears_the_warning()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.Guest("MoreTime").ShouldBeRejected();
        rig.Advance(TimeSpan.FromSeconds(15), Fine);
        Assert.NotNull(rig.View.Overlays.PhaseTimerWarning);

        rig.Guest("MoreTime").ShouldBeAccepted();

        Assert.Null(rig.View.Overlays.PhaseTimerWarning);
        Assert.Equal(30_000, rig.CycleDiagnostics().Timer.RemainingMs);
        rig.Advance(TimeSpan.FromSeconds(25), Fine);
        Assert.Equal(GuestSurface.Template, rig.Surface);
        Assert.NotNull(rig.View.Overlays.PhaseTimerWarning);
    }

    [Fact]
    public async Task A_phase_of_exactly_20_seconds_warns_from_the_start()
    {
        using var rig = await GuestCycleRig.CreateAsync(With(template: 20));

        rig.Begin();

        Assert.Equal(20, rig.View.Overlays.PhaseTimerWarning!.RemainingSeconds);
    }

    [Fact]
    public async Task The_capture_timer_starts_the_sequence_for_every_empty_window()
    {
        using var rig = await GuestCycleRig.CreateAsync(With(capture: 20));
        rig.Begin();
        rig.ToPhotographs();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Windows[0].CaptureId is not null && rig.Photographs.Sequence.CountdownRemaining == 4, TimeSpan.FromSeconds(30), Fine));
        rig.Guest("StopCapture").ShouldBeAccepted();
        Assert.Equal(20_000, rig.CycleDiagnostics().Timer.RemainingMs);

        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Sequence.Running, TimeSpan.FromSeconds(25), Fine));

        Assert.Equal(1, rig.Photographs.Sequence.TargetWindow);
        Assert.Equal(3, rig.Photographs.Sequence.TotalInSequence);
        Assert.True(rig.AdvanceUntil(() => !rig.Photographs.Sequence.Running, TimeSpan.FromMinutes(1), Fine));
        Assert.All(rig.Photographs.Windows, window => Assert.NotNull(window.CaptureId));
        Assert.Equal(PhaseTimer.PhotographsReview, rig.CycleDiagnostics().Timer.Phase);
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
    public async Task The_timer_stays_paused_across_a_whole_capture_sequence()
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
    }

    [Fact]
    public async Task Showing_the_Camera_tab_going_back_and_entering_a_phase_again_restart_the_timer()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.Advance(TimeSpan.FromSeconds(10), Coarse);
        rig.Guest("ShowCameraTab").ShouldBeAccepted();
        Assert.Equal(45_000, rig.CycleDiagnostics().Timer.RemainingMs);

        rig.Guest("GoToStep", """{"step":"template"}""").ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(10), Coarse);
        Assert.InRange(rig.CycleDiagnostics().Timer.RemainingMs!.Value, 19_000, 20_100);
        rig.Guest("GoToStep", """{"step":"photographs"}""").ShouldBeAccepted();
        Assert.Equal(PhaseTimer.PhotographsReview, rig.CycleDiagnostics().Timer.Phase);
        Assert.Equal(45_000, rig.CycleDiagnostics().Timer.RemainingMs);
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
        Assert.Equal(4, detail.Captures.Count);
        rig.Guest("GoToStep", """{"step":"photographs"}""").ShouldBeAccepted();
        Assert.Equal(ids, rig.Photographs.Windows.Select(window => window.CaptureId));
    }

    [Fact]
    public async Task The_review_timer_cancels_a_pending_Move()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var ids = rig.Photographs.Windows.Select(window => window.CaptureId).ToArray();
        rig.Guest("BeginMove", """{"index":0}""").ShouldBeAccepted();

        Assert.True(Reaches(rig, GuestSurface.Filters, 60));

        rig.Guest("GoToStep", """{"step":"photographs"}""").ShouldBeAccepted();
        Assert.Equal(ids, rig.Photographs.Windows.Select(window => window.CaptureId));
        Assert.NotEqual(PhotographSelectionMode.Moving, rig.Photographs.Selection.Mode);
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
    public async Task The_Keepsake_timer_returns_to_Start_with_the_cycle_still_completed_and_never_warns()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(Reaches(rig, GuestSurface.Keepsake, 10, Fine));
        Assert.Equal(PhaseTimer.Keepsake, rig.CycleDiagnostics().Timer.Phase);

        var warned = false;
        Assert.True(rig.AdvanceUntil(
            () =>
            {
                warned |= rig.View.Overlays.PhaseTimerWarning is not null;
                return rig.Surface == GuestSurface.Start;
            },
            TimeSpan.FromSeconds(20),
            Fine));

        Assert.False(warned);
        Assert.Null(rig.Diagnostics().GuestCycle);
        Assert.Equal(1, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
        Assert.Equal(GuestCycleEndReason.Completed, (await rig.OnlyCycleAsync()).Cycle.EndReason);
        Assert.Empty(rig.Store.Ended);
        rig.Guest("BeginGuestCycle").ShouldBeAccepted();
    }

    [Fact]
    public async Task A_timer_that_runs_out_while_the_Start_over_question_is_open_closes_it_and_the_cycle_continues()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.Advance(TimeSpan.FromSeconds(5), Fine);
        rig.Guest("StartOver").ShouldBeAccepted();
        Assert.NotNull(rig.View.Overlays.StartOverConfirmation);

        Assert.True(Reaches(rig, GuestSurface.Photographs, 40));

        Assert.Null(rig.View.Overlays.StartOverConfirmation);
        Assert.Empty(rig.Store.Ended);
        Assert.NotNull(rig.Diagnostics().GuestCycle);
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
