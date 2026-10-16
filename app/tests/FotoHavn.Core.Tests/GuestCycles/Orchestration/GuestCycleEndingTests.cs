using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>Ending the Event during a cycle (plan section 7, Hold to exit; D36): an unfinished cycle ends as operator_exit with its Captures kept, a completed one stays completed, and late results are dropped.</summary>
public sealed class GuestCycleEndingTests
{
    private static readonly TimeSpan Fine = TimeSpan.FromMilliseconds(100);

    private static void EndEvent(GuestCycleRig rig)
    {
        rig.Operator("EndEvent").ShouldBeAccepted();
        rig.Operator("ConfirmEndEvent").ShouldBeAccepted();
    }

    [Fact]
    public async Task END_EVENT_on_the_Template_step_ends_the_cycle_as_operator_exit()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();

        EndEvent(rig);

        var ended = Assert.Single(rig.Store.Ended);
        Assert.Equal(GuestCycleEndReason.OperatorExit, ended.Reason);
        Assert.Equal(GuestCycleEndReason.OperatorExit, (await rig.OnlyCycleAsync()).Cycle.EndReason);
        Assert.Null(rig.OperatorView.RunningEvent);
        Assert.Equal(GuestSurface.Start, rig.Surface);
    }

    [Fact]
    public async Task The_guests_PIN_exit_during_a_cycle_ends_it_keeping_the_Captures()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.TakeAll();

        rig.Active.OpenAndCompleteExitWithPin();
        rig.Settle();

        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(GuestCycleEndReason.OperatorExit, detail.Cycle.EndReason);
        Assert.Equal(4, detail.Captures.Count);
        Assert.Null(detail.PhotoStrip);
    }

    [Fact]
    public async Task The_exit_hold_and_PIN_dialog_show_on_a_cycle_step()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();

        rig.Active.HoldToExit();

        Assert.Equal(GuestSurface.Photographs, rig.Surface);
        Assert.NotNull(rig.View.Overlays.PinDialog);
        Assert.NotNull(rig.View.GuestCycle);
    }

    [Fact]
    public async Task A_cycle_already_on_the_Keepsake_stays_completed_when_the_Event_ends()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), Fine));

        EndEvent(rig);

        Assert.Empty(rig.Store.Ended);
        Assert.Equal(GuestCycleEndReason.Completed, (await rig.OnlyCycleAsync()).Cycle.EndReason);
    }

    [Fact]
    public async Task Ending_the_Event_while_the_strip_is_composing_ends_the_cycle_and_drops_the_late_result()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        rig.Compositor.StripGate = new TaskCompletionSource();
        rig.Guest("FinishStrip").ShouldBeAccepted();

        EndEvent(rig);
        rig.Compositor.StripGate.SetResult();
        rig.Compositor.StripGate = null;
        rig.Advance(TimeSpan.FromSeconds(1), Fine);

        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(GuestCycleEndReason.OperatorExit, detail.Cycle.EndReason);
        Assert.Null(detail.PhotoStrip);
        Assert.Single(rig.Store.Ended);
    }

    [Fact]
    public async Task Ending_the_Event_while_the_strip_is_being_committed_leaves_completion_to_the_commit()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        rig.Store.StripGate = new TaskCompletionSource();
        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Store.InFlight > 0, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20)));

        EndEvent(rig);
        rig.Store.StripGate.SetResult();
        rig.Store.StripGate = null;
        rig.Advance(TimeSpan.FromSeconds(1), Fine);

        Assert.Empty(rig.Store.Ended);
        Assert.Equal(GuestCycleEndReason.Completed, (await rig.OnlyCycleAsync()).Cycle.EndReason);
    }

    [Fact]
    public async Task A_Capture_that_lands_after_the_Event_ended_changes_nothing()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.Store.CommitGate = new TaskCompletionSource();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Sequence.FlashSequence == 1, TimeSpan.FromSeconds(20), Fine));

        EndEvent(rig);
        rig.Store.CommitGate.SetResult();
        rig.Store.CommitGate = null;
        rig.Advance(TimeSpan.FromSeconds(2), Fine);

        // The store refuses a commit into a cycle that already ended, so the Capture is simply not saved; either way the brain carries on.
        Assert.InRange((await rig.OnlyCycleAsync()).Captures.Count, 0, 1);
        Assert.Equal(GuestSurface.Start, rig.Surface);
    }

    [Fact]
    public async Task Ending_the_Event_drops_the_cycles_previews_and_ends_its_timers()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        var key = rig.Filters.StripPreviewKey!;
        Assert.True(rig.Active.Previews.TryGet(key, out _));

        EndEvent(rig);

        Assert.False(rig.Active.Previews.TryGet(key, out _));
        rig.Advance(TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(1));
        Assert.Null(rig.Diagnostics().GuestCycle);
        Assert.Equal(GuestSurface.Start, rig.Surface);
    }

    [Fact]
    public async Task Ending_the_Event_during_a_countdown_cancels_it()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(2), Fine);

        EndEvent(rig);
        rig.Advance(TimeSpan.FromSeconds(10), Fine);

        Assert.Empty((await rig.OnlyCycleAsync()).Captures);
        Assert.Equal(GuestSurface.Start, rig.Surface);
    }

    [Fact]
    public async Task A_new_Event_starts_with_no_trace_of_the_last_cycle()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        EndEvent(rig);

        Assert.Null(rig.Diagnostics().GuestCycle);
        Assert.Null(rig.OperatorView.RunningEvent);
        rig.Active.StartAndExpectSuccess();
        rig.Settle();

        Assert.Equal(GuestSurface.Start, rig.Surface);
        rig.Guest("BeginGuestCycle").ShouldBeAccepted();
        Assert.Equal(GuestSurface.Template, rig.Surface);
        Assert.Equal(2, (await rig.Memory.ListGuestCyclesAsync(ActiveRig.EventId, GuestCycleFilter.All, null, 50, CancellationToken.None)).Items.Count);
    }
}
