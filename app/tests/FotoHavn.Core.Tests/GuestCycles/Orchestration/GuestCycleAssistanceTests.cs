using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>Failures inside a Guest Cycle and Operator Assistance (plan section 7, Failures): camera, storage, and the guest display, and RETRY.</summary>
public sealed class GuestCycleAssistanceTests
{
    private static readonly TimeSpan Fine = TimeSpan.FromMilliseconds(100);

    private static void FailCamera(GuestCycleRig rig)
    {
        rig.Active.Camera.Fail(CameraFailureKind.Failed);
        rig.Settle();
    }

    private static void Retry(GuestCycleRig rig, bool asOperator = false)
    {
        (asOperator ? rig.Operator("RetryAssistance") : rig.Guest("RetryAssistance")).ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(1), Fine);
    }

    private static void AssertInAssistance(GuestCycleRig rig, AssistanceReason reason)
    {
        Assert.Equal(GuestSurface.OperatorAssistance, rig.Surface);
        Assert.Equal(reason, rig.View.OperatorAssistance!.Reason);
        var panel = rig.OperatorView.RunningEvent!;
        Assert.Equal(RunningEventStep.OperatorAssistance, panel.CurrentStep);
        Assert.Equal(reason, panel.Assistance!.Reason);
    }

    private static void AssertRecovered(GuestCycleRig rig)
    {
        Assert.NotEqual(GuestSurface.OperatorAssistance, rig.Surface);
        Assert.Null(rig.OperatorView.RunningEvent!.Assistance);
    }

    [Fact]
    public async Task A_camera_failure_during_a_cycle_enters_Operator_Assistance_and_pauses_the_timer()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.Advance(TimeSpan.FromSeconds(5), Fine);
        var remaining = rig.CycleDiagnostics().Timer.RemainingMs;

        FailCamera(rig);

        AssertInAssistance(rig, AssistanceReason.Camera);
        Assert.False(rig.View.OperatorAssistance!.Retrying);
        Assert.Equal(GuestCycleStepName.Template, rig.View.GuestCycle!.Step);
        Assert.Null(rig.View.Template);
        var timer = rig.CycleDiagnostics().Timer;
        Assert.True(timer.Paused);
        Assert.Equal(GuestCycleStepName.Template, rig.CycleDiagnostics().Step);
        rig.Advance(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1));
        Assert.Equal(remaining, rig.CycleDiagnostics().Timer.RemainingMs);
        Assert.Equal(GuestSurface.OperatorAssistance, rig.Surface);
        var overlay = rig.View.Overlays.PhaseTimer!;
        Assert.Equal((PhaseTimer.Template, true), (overlay.Phase, overlay.Paused));
        Assert.Equal((int)Math.Ceiling(remaining!.Value / 1000.0), overlay.RemainingSeconds);
    }

    [Fact]
    public async Task During_assistance_guest_commands_are_refused()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        FailCamera(rig);

        rig.Guest("GoToStep", """{"step":"photographs"}""").ShouldBeRejected();
        rig.Guest("SelectTemplate", """{"templateId":"panorama-four"}""").ShouldBeRejected();
        rig.Guest("BeginGuestCycle").ShouldBeRejected();
    }

    [Fact]
    public async Task RETRY_fails_while_the_camera_is_still_down_and_succeeds_once_it_is_back_returning_to_the_same_step()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.Advance(TimeSpan.FromSeconds(5), Fine);
        var remaining = rig.CycleDiagnostics().Timer.RemainingMs!.Value;
        FailCamera(rig);

        Retry(rig);
        AssertInAssistance(rig, AssistanceReason.Camera);
        Assert.False(rig.View.OperatorAssistance!.Retrying);

        rig.Active.Camera.Recover();
        Retry(rig);

        AssertRecovered(rig);
        Assert.Equal(GuestSurface.Template, rig.Surface);
        var timer = rig.CycleDiagnostics().Timer;
        Assert.False(timer.Paused);
        Assert.InRange(timer.RemainingMs!.Value, remaining - 2_000, remaining);
    }

    [Fact]
    public async Task The_operator_can_press_RETRY_too()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        FailCamera(rig);
        rig.Active.Camera.Recover();

        Retry(rig, asOperator: true);

        AssertRecovered(rig);
        Assert.Equal(GuestSurface.Template, rig.Surface);
    }

    [Fact]
    public async Task RETRY_shows_its_busy_state_and_is_not_taken_twice_while_the_camera_is_checked()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        FailCamera(rig);
        rig.Active.Camera.Recover();
        rig.Active.Camera.Stall();

        rig.Guest("RetryAssistance").ShouldBeAccepted();
        Assert.True(rig.View.OperatorAssistance!.Retrying);
        rig.Guest("RetryAssistance").ShouldBeRejected();
        rig.Operator("RetryAssistance").ShouldBeRejected();

        // A camera that never delivers a frame fails the check (the staleness limit), and RETRY is available again.
        rig.Advance(TimeSpan.FromSeconds(4), Fine);
        Assert.False(rig.View.OperatorAssistance!.Retrying);
        AssertInAssistance(rig, AssistanceReason.Camera);
    }

    [Fact]
    public async Task A_camera_that_never_opens_fails_the_RETRY_after_five_seconds()
    {
        HangableCameraAdapter? hang = null;
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { CameraDecorator = fake => hang = new HangableCameraAdapter(fake) });
        rig.Begin();
        FailCamera(rig);
        rig.Active.Camera.Recover();
        hang!.HangOpen = true;

        rig.Guest("RetryAssistance").ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(4), Fine);
        Assert.True(rig.View.OperatorAssistance!.Retrying);
        rig.Advance(TimeSpan.FromSeconds(2), Fine);

        Assert.False(rig.View.OperatorAssistance!.Retrying);
        AssertInAssistance(rig, AssistanceReason.Camera);
    }

    [Fact]
    public async Task A_camera_failure_during_a_countdown_cancels_it_keeps_the_Captures_and_RETRY_continues_from_the_next_empty_window()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Windows[0].CaptureId is not null && rig.Photographs.Sequence.CountdownRemaining == 3, TimeSpan.FromSeconds(30), Fine));

        FailCamera(rig);

        AssertInAssistance(rig, AssistanceReason.Camera);
        var diagnostics = rig.CycleDiagnostics();
        Assert.False(diagnostics.SequenceRunning);
        Assert.Equal(1, diagnostics.WindowsFilled);

        rig.Active.Camera.Recover();
        Retry(rig);

        Assert.Equal(GuestSurface.Photographs, rig.Surface);
        Assert.Equal(1, rig.Photographs.Windows.Count(window => window.CaptureId is not null));
        Assert.False(rig.Photographs.Sequence.Running);
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.Equal(1, rig.Photographs.Sequence.TargetWindow);
        Assert.Equal(3, rig.Photographs.Sequence.TotalInSequence);
        Assert.True(rig.AdvanceUntil(() => !rig.Photographs.Sequence.Running, TimeSpan.FromMinutes(1), Fine));
        Assert.Equal(4, (await rig.OnlyCycleAsync()).Captures.Count);
    }

    [Fact]
    public async Task A_camera_that_goes_stale_while_a_frame_is_awaited_is_a_failure_too()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(4), Fine);
        rig.Active.Camera.Stall();

        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.OperatorAssistance, TimeSpan.FromSeconds(10), Fine));

        AssertInAssistance(rig, AssistanceReason.Camera);
        Assert.False(rig.CycleDiagnostics().SequenceRunning);
        Assert.Equal(0, rig.CycleDiagnostics().WindowsFilled);
    }

    [Fact]
    public async Task A_camera_that_never_answers_for_a_Capture_fails_it_after_the_timeout()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.Guest("TakePhotographs").ShouldBeAccepted();

        // Stall only after the countdown ends: the brain's own timeout fires before the staleness check can, because frames are never "stale" to it.
        rig.Advance(TimeSpan.FromSeconds(5), Fine);
        rig.Active.Camera.Stall();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.OperatorAssistance, TimeSpan.FromSeconds(15), Fine));

        AssertInAssistance(rig, AssistanceReason.Camera);
    }

    [Fact]
    public async Task A_camera_failure_while_the_Keepsake_shows_is_not_assistance_and_Start_then_shows_unavailable()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), Fine));

        FailCamera(rig);

        Assert.Equal(GuestSurface.Keepsake, rig.Surface);
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.StartUnavailable, TimeSpan.FromSeconds(20), Fine));
        Assert.Equal(1, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
    }

    [Fact]
    public async Task A_storage_failure_on_a_commit_discards_that_Capture_and_RETRY_continues()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Windows[1].CaptureId is not null && rig.Photographs.Sequence.CountdownRemaining == 5, TimeSpan.FromSeconds(30), Fine));
        rig.Store.FailCaptureCommits = 1;

        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.OperatorAssistance, TimeSpan.FromSeconds(20), Fine));

        AssertInAssistance(rig, AssistanceReason.Storage);
        Assert.Equal(2, rig.CycleDiagnostics().WindowsFilled);
        Assert.False(rig.CycleDiagnostics().SequenceRunning);
        Assert.Equal(2, (await rig.OnlyCycleAsync()).Captures.Count);

        Retry(rig);
        Assert.Equal(GuestSurface.Photographs, rig.Surface);
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => !rig.Photographs.Sequence.Running, TimeSpan.FromMinutes(1), Fine));
        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(4, detail.Captures.Count);
        Assert.Equal(4, detail.Captures.Select(capture => capture.Sequence).Distinct().Count());
    }

    [Fact]
    public async Task A_Capture_that_cannot_be_encoded_is_a_storage_failure()
    {
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { Encoder = new FakeCaptureEncoder { Failure = new IOException("no") } });
        rig.Begin();
        rig.ToPhotographs();
        rig.Guest("TakePhotographs").ShouldBeAccepted();

        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.OperatorAssistance, TimeSpan.FromSeconds(20), Fine));

        AssertInAssistance(rig, AssistanceReason.Storage);
        Assert.Equal(0, rig.CycleDiagnostics().WindowsFilled);
    }

    [Fact]
    public async Task A_host_without_a_Capture_encoder_cannot_save_a_Capture()
    {
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { NoEncoder = true });
        rig.Begin();
        rig.ToPhotographs();
        rig.Guest("TakePhotographs").ShouldBeAccepted();

        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.OperatorAssistance, TimeSpan.FromSeconds(20), Fine));

        AssertInAssistance(rig, AssistanceReason.Storage);
    }

    [Fact]
    public async Task A_late_Capture_still_lands_after_assistance_began_so_no_photograph_is_lost()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.Store.CommitGate = new TaskCompletionSource();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Sequence.FlashSequence == 1, TimeSpan.FromSeconds(20), Fine));

        FailCamera(rig);
        AssertInAssistance(rig, AssistanceReason.Camera);
        rig.Store.CommitGate.SetResult();
        rig.Store.CommitGate = null;
        rig.Advance(TimeSpan.FromSeconds(2), Fine);

        Assert.Equal(1, rig.CycleDiagnostics().WindowsFilled);
        Assert.False(rig.CycleDiagnostics().SequenceRunning);
        Assert.Single((await rig.OnlyCycleAsync()).Captures);
    }

    [Fact]
    public async Task A_storage_failure_creating_the_cycle_is_assistance_with_no_cycle_and_RETRY_returns_to_Start()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Store.FailStartCycle = true;

        rig.Guest("BeginGuestCycle").ShouldBeAccepted();

        AssertInAssistance(rig, AssistanceReason.Storage);
        Assert.Null(rig.View.GuestCycle);
        Assert.Null(rig.Diagnostics().GuestCycle);

        rig.Store.FailProbe = true;
        Retry(rig);
        AssertInAssistance(rig, AssistanceReason.Storage);
        Assert.False(rig.View.OperatorAssistance!.Retrying);

        rig.Store.FailProbe = false;
        rig.Store.FailStartCycle = false;
        Retry(rig);
        AssertRecovered(rig);
        Assert.Equal(GuestSurface.Start, rig.Surface);
        rig.Guest("BeginGuestCycle").ShouldBeAccepted();
        Assert.Equal(GuestSurface.Template, rig.Surface);
    }

    [Fact]
    public async Task RETRY_with_no_cycle_checks_only_the_display_and_storage_and_Start_shows_unavailable_for_a_camera_still_down()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Store.FailStartCycle = true;
        rig.Guest("BeginGuestCycle").ShouldBeAccepted();
        rig.Active.Camera.Fail(CameraFailureKind.Failed);
        rig.Settle();
        rig.Store.FailStartCycle = false;

        Retry(rig);

        AssertRecovered(rig);
        Assert.Equal(GuestSurface.StartUnavailable, rig.Surface);
    }

    [Fact]
    public async Task A_storage_failure_discarding_a_Capture_is_assistance_too()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.Guest("BeginRetake", """{"index":0}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.RetakeCandidate is not null, TimeSpan.FromSeconds(30), Fine));
        rig.Store.FailSetState = true;

        rig.Guest("AcceptRetake").ShouldBeAccepted();
        rig.Settle();

        AssertInAssistance(rig, AssistanceReason.Storage);
        Assert.Equal(4, rig.CycleDiagnostics().WindowsFilled);
    }

    [Fact]
    public async Task A_storage_failure_committing_the_Photo_Strip_is_assistance_and_the_cycle_can_finish_after_RETRY()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        rig.Store.FailStripCommit = true;

        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.OperatorAssistance, TimeSpan.FromSeconds(10), Fine));

        AssertInAssistance(rig, AssistanceReason.Storage);
        Assert.Null((await rig.OnlyCycleAsync()).Cycle.EndReason);
        Assert.Equal(GuestCycleStepName.Filters, rig.View.GuestCycle!.Step);

        rig.Store.FailStripCommit = false;
        Retry(rig);
        Assert.Equal(GuestSurface.Filters, rig.Surface);
        Assert.True(rig.Filters.CanFinish);
        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), Fine));
        Assert.Equal(1, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
    }

    [Fact]
    public async Task Losing_the_guest_display_during_a_cycle_closes_the_Guest_Screen_and_RETRY_reopens_it_on_the_returned_display()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(2), Fine);

        rig.Active.Displays.Disconnect("fake-guest");
        rig.Settle();

        AssertInAssistance(rig, AssistanceReason.GuestDisplay);
        Assert.Equal("CloseGuestScreen", rig.Active.Recorded.Calls[^1]);
        Assert.False(rig.CycleDiagnostics().SequenceRunning);
        Assert.Equal(RunningDisplayState.Disconnected, rig.OperatorView.RunningEvent!.GuestDisplay.State);

        Retry(rig);
        AssertInAssistance(rig, AssistanceReason.GuestDisplay);

        rig.Active.Displays.Connect(FakeDisplayAdapter.DefaultGuest);
        rig.Settle();
        var opened = rig.Active.Recorded.OpenedGuestScreens.Count;
        Retry(rig);

        AssertRecovered(rig);
        Assert.Equal(opened + 1, rig.Active.Recorded.OpenedGuestScreens.Count);
        Assert.Equal(GuestSurface.Photographs, rig.Surface);
    }

    [Fact]
    public async Task Losing_the_guest_display_on_Start_is_assistance_with_no_cycle_and_RETRY_returns_to_Start()
    {
        using var rig = await GuestCycleRig.CreateAsync();

        rig.Active.Displays.Disconnect("fake-guest");
        rig.Settle();

        AssertInAssistance(rig, AssistanceReason.GuestDisplay);
        Assert.Null(rig.View.GuestCycle);
        rig.Active.Displays.Connect(FakeDisplayAdapter.DefaultGuest);
        rig.Settle();
        Retry(rig);
        AssertRecovered(rig);
        Assert.Equal(GuestSurface.Start, rig.Surface);
    }

    [Fact]
    public async Task The_first_reason_stands_while_assistance_lasts()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        FailCamera(rig);

        rig.Active.Displays.Disconnect("fake-guest");
        rig.Settle();

        AssertInAssistance(rig, AssistanceReason.Camera);
    }

    [Fact]
    public async Task RETRY_waits_for_the_reopened_Guest_Screen_to_show()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        FailCamera(rig);
        rig.Active.Camera.Recover();
        rig.Active.Guest!.Connection.Dispose();
        rig.Settle();

        rig.Operator("RetryAssistance").ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(3), Fine);
        Assert.NotNull(rig.OperatorView.RunningEvent!.Assistance);
        rig.Active.ConnectGuest();
        Assert.True(rig.View.OperatorAssistance!.Retrying);

        rig.Active.ConnectGuest();
        rig.Advance(TimeSpan.FromSeconds(1), Fine);
        AssertRecovered(rig);
        Assert.Equal(GuestSurface.Template, rig.Surface);
    }

    [Fact]
    public async Task A_Guest_Screen_that_never_shows_fails_the_RETRY()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        FailCamera(rig);
        rig.Active.Camera.Recover();
        rig.Active.Guest!.Connection.Dispose();
        rig.Settle();

        rig.Operator("RetryAssistance").ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(12), Fine);

        Assert.True(rig.OperatorView.RunningEvent!.Assistance is not null);
        rig.Active.ConnectGuest();
        Assert.False(rig.View.OperatorAssistance!.Retrying);
    }

    [Fact]
    public async Task A_host_that_cannot_open_the_Guest_Screen_fails_the_RETRY()
    {
        var host = new GuestScreenHost("fake-primary");
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { Host = host });
        rig.Begin();
        rig.Active.Displays.Disconnect("fake-guest");
        rig.Settle();
        rig.Active.Displays.Connect(FakeDisplayAdapter.DefaultGuest);
        rig.Settle();
        host.FailOpen = true;

        Retry(rig);
        AssertInAssistance(rig, AssistanceReason.GuestDisplay);
        Assert.False(rig.View.OperatorAssistance!.Retrying);

        host.FailOpen = false;
        Retry(rig);
        AssertRecovered(rig);
    }

    [Fact]
    public async Task RETRY_is_refused_when_there_is_no_assistance()
    {
        using var rig = await GuestCycleRig.CreateAsync();

        rig.Guest("RetryAssistance").ShouldBeRejected();
        rig.Operator("RetryAssistance").ShouldBeRejected();
    }

    [Fact]
    public async Task Ending_the_Event_during_assistance_ends_the_cycle_keeping_the_Captures()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.TakeAll();
        FailCamera(rig);

        rig.Operator("EndEvent").ShouldBeAccepted();
        rig.Operator("ConfirmEndEvent").ShouldBeAccepted();

        Assert.Null(rig.OperatorView.RunningEvent);
        var ended = Assert.Single(rig.Store.Ended);
        Assert.Equal(GuestCycleEndReason.OperatorExit, ended.Reason);
        Assert.Equal(4, (await rig.DetailAsync(ended.CycleId)).Captures.Count);
    }

    [Fact]
    public async Task Ending_the_Event_during_a_RETRY_stops_it()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        FailCamera(rig);
        rig.Active.Camera.Recover();
        rig.Active.Camera.Stall();
        rig.Guest("RetryAssistance").ShouldBeAccepted();

        rig.Operator("EndEvent").ShouldBeAccepted();
        rig.Operator("ConfirmEndEvent").ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(6), Fine);

        Assert.Null(rig.OperatorView.RunningEvent);
        Assert.Equal(GuestSurface.Start, rig.Surface);
    }
}
