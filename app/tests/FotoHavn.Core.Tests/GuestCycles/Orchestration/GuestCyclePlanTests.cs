using FotoHavn.Core.GuestCycles;
using FotoHavn.Core.GuestCycles.Orchestration;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

public sealed class GuestCyclePlanTests
{
    private static GuestCycleModel Complete() => GuestCycleKit.Loaded();

    [Fact]
    public void The_timer_phase_follows_the_step_and_whether_every_window_is_filled()
    {
        Assert.Equal(PhaseTimerPhase.Template, GuestCyclePlan.PhaseFor(GuestCycleKit.Fresh(), false));
        Assert.Equal(PhaseTimerPhase.PhotographsCapture, GuestCyclePlan.PhaseFor(GuestCycleKit.OnPhotographs(), false));
        Assert.Equal(PhaseTimerPhase.PhotographsCapture, GuestCyclePlan.PhaseFor(GuestCycleKit.Loaded("A", "B"), false));
        Assert.Equal(PhaseTimerPhase.PhotographsReview, GuestCyclePlan.PhaseFor(Complete(), false));
        Assert.Equal(PhaseTimerPhase.Filters, GuestCyclePlan.PhaseFor(Complete().Do(new GoToStepAction(GuestCycleStep.Filters)), false));
        Assert.Equal(PhaseTimerPhase.Keepsake, GuestCyclePlan.PhaseFor(Complete().Do(new GoToStepAction(GuestCycleStep.Filters)).Do(new GoToStepAction(GuestCycleStep.Keepsake)), false));
    }

    [Fact]
    public void No_phase_is_timed_while_the_final_Photo_Strip_is_being_made()
    {
        Assert.Null(GuestCyclePlan.PhaseFor(Complete(), finishing: true));
    }

    [Fact]
    public void A_Retake_whose_candidate_waits_shows_the_Arrange_tab_and_every_other_state_shows_the_models_tab()
    {
        var retaking = Complete().Do(new BeginRetakeAction(5, 1));
        Assert.Equal(PhotographsTab.Camera, GuestCyclePlan.EffectiveTab(retaking));

        var candidate = retaking.Do(new CaptureCommittedAction(5, 1, GuestCycleKit.Capture("N", 5)));
        Assert.Equal(PhotographsTab.Arrange, GuestCyclePlan.EffectiveTab(candidate));
        Assert.Equal(PhotographsTab.Camera, GuestCyclePlan.EffectiveTab(candidate.Do(new TryAgainAction())));
        Assert.Equal(PhotographsTab.Arrange, GuestCyclePlan.EffectiveTab(Complete()));
    }

    [Fact]
    public void The_step_progress_lists_all_four_steps_with_where_the_guest_is()
    {
        var onPhotographs = GuestCyclePlan.Steps(GuestCycleStep.Photographs, templateSkipped: false, filtersSkipped: false);

        Assert.Equal(
            [
                new GuestCycleStepEntry(GuestCycleStepName.Template, GuestStepState.Done),
                new GuestCycleStepEntry(GuestCycleStepName.Photographs, GuestStepState.Current),
                new GuestCycleStepEntry(GuestCycleStepName.Filters, GuestStepState.Upcoming),
                new GuestCycleStepEntry(GuestCycleStepName.Keepsake, GuestStepState.Upcoming),
            ],
            onPhotographs);
    }

    [Fact]
    public void A_skipped_step_stays_skipped()
    {
        var steps = GuestCyclePlan.Steps(GuestCycleStep.Filters, templateSkipped: true, filtersSkipped: false);

        Assert.Equal(new GuestCycleStepEntry(GuestCycleStepName.Template, GuestStepState.Skipped), steps[0]);
        Assert.Equal(new GuestCycleStepEntry(GuestCycleStepName.Photographs, GuestStepState.Done), steps[1]);

        var filtersSkipped = GuestCyclePlan.Steps(GuestCycleStep.Photographs, templateSkipped: false, filtersSkipped: true);
        Assert.Equal(new GuestCycleStepEntry(GuestCycleStepName.Filters, GuestStepState.Skipped), filtersSkipped[2]);
    }

    [Fact]
    public void On_the_Keepsake_every_earlier_step_is_done()
    {
        var steps = GuestCyclePlan.Steps(GuestCycleStep.Keepsake, templateSkipped: false, filtersSkipped: false);

        Assert.Equal(
            [GuestStepState.Done, GuestStepState.Done, GuestStepState.Done, GuestStepState.Current],
            steps.Select(entry => entry.State));
    }

    [Theory]
    [InlineData(GuestCycleStep.Template, GuestCycleStepName.Template, RunningEventStep.Template)]
    [InlineData(GuestCycleStep.Photographs, GuestCycleStepName.Photographs, RunningEventStep.Photographs)]
    [InlineData(GuestCycleStep.Filters, GuestCycleStepName.Filters, RunningEventStep.Filters)]
    [InlineData(GuestCycleStep.Keepsake, GuestCycleStepName.Keepsake, RunningEventStep.Keepsake)]
    public void Each_step_has_its_wire_name_and_its_panel_name(GuestCycleStep step, GuestCycleStepName name, RunningEventStep panel)
    {
        Assert.Equal(name, GuestCyclePlan.ToName(step));
        Assert.Equal(panel, GuestCyclePlan.ToRunning(step));
    }

    [Theory]
    [InlineData(300.0, 300)]
    [InlineData(20.0, 20)]
    [InlineData(19.001, 20)]
    [InlineData(18.5, 19)]
    [InlineData(0.2, 1)]
    [InlineData(0.0, 0)]
    public void The_timer_shows_the_time_left_rounded_up(double seconds, int expected) =>
        Assert.Equal(expected, GuestCyclePlan.WholeSecondsLeft(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void A_running_timer_wakes_as_the_displayed_second_changes_the_last_wake_being_the_expiry()
    {
        Assert.Equal(TimeSpan.FromSeconds(0.25), GuestCyclePlan.WakeDelay(TimeSpan.FromSeconds(7.25)));
        Assert.Equal(TimeSpan.FromSeconds(1), GuestCyclePlan.WakeDelay(TimeSpan.FromSeconds(7)));
        Assert.Equal(TimeSpan.FromSeconds(1), GuestCyclePlan.WakeDelay(TimeSpan.FromSeconds(60)));
        Assert.Equal(TimeSpan.FromSeconds(0.5), GuestCyclePlan.WakeDelay(TimeSpan.FromSeconds(0.5)));
        Assert.Equal(TimeSpan.FromTicks(1), GuestCyclePlan.WakeDelay(TimeSpan.FromSeconds(3) + TimeSpan.FromTicks(1)));
    }

    [Fact]
    public void A_timer_whose_time_is_up_acts_now()
    {
        Assert.Equal(TimeSpan.Zero, GuestCyclePlan.WakeDelay(TimeSpan.Zero));
        Assert.Equal(TimeSpan.Zero, GuestCyclePlan.WakeDelay(TimeSpan.FromSeconds(-3)));
    }

    [Fact]
    public void Media_urls_carry_the_event_the_cycle_and_a_short_hash_to_bust_the_cache()
    {
        const string sha = "0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f9";

        Assert.Equal("https://media.fotohavn/events/e1/guest-cycles/c1/capture-3.jpg?v=0a1b2c3d", GuestCyclePlan.CaptureUrl("e1", "c1", 3, sha));
        Assert.Equal("https://media.fotohavn/events/e1/guest-cycles/c1/photo-strip.png?v=0a1b2c3d", GuestCyclePlan.StripUrl("e1", "c1", sha));
        Assert.Equal("https://media.fotohavn/events/e1/guest-cycles/c1/photo-strip.png?v=abc", GuestCyclePlan.StripUrl("e1", "c1", "abc"));
    }
}
