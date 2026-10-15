using FotoHavn.Core.Camera;
using FotoHavn.Core.Events.Active;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Events.Active;

/// <summary>
/// An Event that is starting is held like a running one (it can't be edited, saved, deleted, or started again, and its camera
/// can't be replaced or released), and ending it while the start waits for the Guest Screen cancels the start cleanly.
/// A stalled camera keeps the start in step 4 for five seconds; a Guest Screen that never connects keeps it in step 8.
/// </summary>
public sealed class StartHoldTests
{
    private static readonly AvailableCamera SecondCamera = new("camera-b", "Second camera");

    private static string Payload(string eventId) => $$"""{"eventId":"{{eventId}}"}""";

    private static void AwaitSetup(ActiveRig rig, string? eventId = null) =>
        rig.Pump(() => rig.LatestOperator() is { Surface: OperatorSurface.EventSetup, EventSetup: { } setup } && (eventId is null || setup.EventId == eventId));

    private static void OpenSetup(ActiveRig rig, string eventId)
    {
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("EditEvent", Payload(eventId)).Status);
        AwaitSetup(rig, eventId);
    }

    /// <summary>Starts the Event with a camera that never delivers a fresh frame, so the start waits in step 4 (the Event isn't running yet).</summary>
    private static Task<StartEventResult> StartWaitingForCamera(ActiveRig rig, string eventId = ActiveRig.EventId)
    {
        rig.Camera.Stall();
        var start = rig.BeginStart(eventId);
        rig.Pump(() => rig.Brain.CameraSlotState == CameraState.Checking);
        Assert.Null(rig.Brain.ActiveEventId);
        Assert.Equal(eventId, rig.Brain.HeldEventId);
        return start;
    }

    /// <summary>Lets the stalled camera deliver again and runs the brain until the start ended.</summary>
    private static StartEventResult Finish(ActiveRig rig, Task<StartEventResult> start)
    {
        rig.Camera.Recover();
        rig.Pump(() => start.IsCompleted);
        return start.Result;
    }

    /// <summary>The start's answer reaches the brain from the thread pool: runs the brain until the held Event is released.</summary>
    private static void AwaitStartEnded(ActiveRig rig) =>
        rig.Pump(() => rig.Brain.HeldEventId is null);

    private static void AssertRejected(CommandResultMessage result) =>
        Assert.Equal((CommandStatus.Rejected, RejectReason.EventRunning), (result.Status, result.Reason));

    private static async Task<ActiveRig> StartingRigAsync(bool guestConnected = true) =>
        await ActiveRig.CreateAsync(new ActiveRigOptions { WithOtherEvent = true, GuestConnected = guestConnected });

    // The lock

    [Fact]
    public async Task A_starting_Event_cant_be_edited_deleted_or_started_again()
    {
        using var rig = await StartingRigAsync();
        var start = StartWaitingForCamera(rig);

        AssertRejected(rig.OperatorCommand("EditEvent", Payload(ActiveRig.EventId)));
        AssertRejected(rig.OperatorCommand("DeleteEvent", Payload(ActiveRig.EventId)));
        AssertRejected(rig.OperatorCommand("StartEvent", Payload(ActiveRig.EventId)));
        AssertRejected(rig.OperatorCommand("StartEvent", Payload(ActiveRig.OtherEventId)));
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("OpenEventFolder", Payload(ActiveRig.EventId)).Status);
        Assert.Null(rig.LatestOperator().Overlays.Confirmation);

        Assert.True(Finish(rig, start).Succeeded);
    }

    [Fact]
    public async Task The_Event_list_shows_a_starting_row_held_but_not_running()
    {
        using var rig = await StartingRigAsync();
        var start = StartWaitingForCamera(rig);
        AssertRejected(rig.OperatorCommand("EditEvent", Payload(ActiveRig.EventId)));
        rig.Run();

        var rows = rig.LatestOperator().EventList!.Events;
        var starting = rows.Single(row => row.Id == ActiveRig.EventId);
        Assert.False(starting.Running);
        Assert.Equal(new EventRowActions(false, false, false, true, true), starting.Actions);
        var other = rows.Single(row => row.Id == ActiveRig.OtherEventId);
        Assert.False(other.Running);
        Assert.Equal(new EventRowActions(false, true, true, true, true), other.Actions);
        Assert.All(rows, row => Assert.Equal(StartDisabledReason.EventRunning, row.StartDisabledReason));
        Assert.Null(rig.LatestOperator().RunningEvent);

        Assert.True(Finish(rig, start).Succeeded);
        rig.Run();
        Assert.True(rig.LatestOperator().EventList!.Events.Single(row => row.Id == ActiveRig.EventId).Running);
    }

    [Fact]
    public async Task A_confirmed_delete_of_the_starting_Event_is_rejected()
    {
        using var rig = await StartingRigAsync();
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("DeleteEvent", Payload(ActiveRig.EventId)).Status);
        rig.Pump(() => rig.LatestOperator().Overlays.Confirmation is { Kind: ConfirmationKind.DeleteEvent });
        var start = StartWaitingForCamera(rig);

        AssertRejected(rig.OperatorCommand("ConfirmDeleteEvent"));

        Assert.True(Finish(rig, start).Succeeded);
        Assert.Contains(rig.LatestOperator().EventList!.Events, row => row.Id == ActiveRig.EventId);
    }

    [Fact]
    public async Task A_draft_of_the_starting_Event_cant_be_saved_left_with_a_save_or_save_and_started()
    {
        using var rig = await StartingRigAsync();
        OpenSetup(rig, ActiveRig.EventId);
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("ChangeEventField", """{"field":"name","value":"Renamed while starting"}""").Status);
        var start = StartWaitingForCamera(rig);

        AssertRejected(rig.OperatorCommand("SaveEvent"));
        AssertRejected(rig.OperatorCommand("SaveAndStartEvent"));
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("LeaveEventSetup").Status);
        AssertRejected(rig.OperatorCommand("ConfirmLeave", """{"choice":"save"}"""));

        var setup = rig.LatestOperator().EventSetup!;
        Assert.False(setup.SaveAndStartAvailable);
        Assert.Equal(StartDisabledReason.EventRunning, setup.SaveAndStartDisabledReason);
        Assert.True(Finish(rig, start).Succeeded);
        var saved = (await rig.Store.ListEventsAsync(TestContext.Current.CancellationToken)).Single(summary => summary.Id == ActiveRig.EventId);
        Assert.Equal("Studio open day", saved.Name);
    }

    [Fact]
    public async Task Another_Events_draft_cant_save_and_start_while_an_Event_is_starting()
    {
        using var rig = await StartingRigAsync();
        var start = StartWaitingForCamera(rig);
        OpenSetup(rig, ActiveRig.OtherEventId);

        AssertRejected(rig.OperatorCommand("SaveAndStartEvent"));

        Assert.Equal(StartDisabledReason.EventRunning, rig.LatestOperator().EventSetup!.SaveAndStartDisabledReason);
        Assert.True(Finish(rig, start).Succeeded);
    }

    // The camera

    [Fact]
    public async Task Event_setup_never_replaces_or_releases_the_camera_a_start_holds()
    {
        using var rig = await StartingRigAsync();
        rig.Camera.AddCamera(SecondCamera);
        rig.Pump(() => rig.Brain.CameraSlotCameras.Count == 2);
        var start = StartWaitingForCamera(rig);
        var sessions = rig.Camera.OpenSessionCount;

        OpenSetup(rig, ActiveRig.OtherEventId);
        Assert.Equal(EventSetupCameraState.InUseByRunningEvent, rig.LatestOperator().EventSetup!.Camera.State);
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("SelectCamera", """{"deviceId":"camera-b"}""").Status);
        rig.Run();

        Assert.Equal(EventSetupCameraState.NotChecked, rig.LatestOperator().EventSetup!.Camera.State);
        Assert.Equal("fake-camera", rig.Brain.CameraSlotDeviceId);
        Assert.Equal(sessions, rig.Camera.OpenSessionCount);

        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("LeaveEventSetup").Status);
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("ConfirmLeave", """{"choice":"discardChanges"}""").Status);
        rig.Run();

        Assert.Equal(OperatorSurface.EventList, rig.LatestOperator().Surface);
        Assert.Equal("fake-camera", rig.Brain.CameraSlotDeviceId);
        Assert.Equal(CameraState.Checking, rig.Brain.CameraSlotState);
        Assert.Equal(sessions, rig.Camera.OpenSessionCount);

        Assert.True(Finish(rig, start).Succeeded);
        Assert.Equal("fake-camera", rig.Brain.CameraSlotDeviceId);
        Assert.Equal(PreviewTarget.Guest, rig.Diagnostics().Camera.PreviewTarget);
        Assert.Equal(1, rig.Camera.OpenSessionCount);
    }

    // A failed start with another draft open

    [Fact]
    public async Task A_failed_start_keeps_another_open_draft_and_shows_its_reason_when_that_draft_closes()
    {
        using var rig = await StartingRigAsync();
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("StartEvent", Payload(ActiveRig.EventId)).Status);
        rig.Camera.Stall();
        rig.Pump(() => rig.Brain.CameraSlotState == CameraState.Checking);
        OpenSetup(rig, ActiveRig.OtherEventId);

        rig.Advance(Brain.StartCameraTimeout + TimeSpan.FromSeconds(1));
        AwaitStartEnded(rig);

        var open = rig.LatestOperator().EventSetup!;
        Assert.Equal(ActiveRig.OtherEventId, open.EventId);
        Assert.Null(open.StartFailure);
        Assert.Null(rig.Brain.HeldEventId);

        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("LeaveEventSetup").Status);
        AwaitSetup(rig, ActiveRig.EventId);

        Assert.Equal(new EventSetupStartFailure(StartFailureCode.CameraNotEligible, CameraFailureKind.Stale), rig.LatestOperator().EventSetup!.StartFailure);
    }

    [Fact]
    public async Task A_failed_start_shows_its_reason_in_an_open_draft_of_the_same_Event()
    {
        using var rig = await StartingRigAsync();
        OpenSetup(rig, ActiveRig.EventId);
        rig.Camera.Stall();
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("StartEvent", Payload(ActiveRig.EventId)).Status);
        rig.Pump(() => rig.Brain.HeldEventId is not null);

        rig.Advance(Brain.StartCameraTimeout + TimeSpan.FromSeconds(1));
        rig.Pump(() => rig.LatestOperator().EventSetup is { StartFailure: not null });

        var setup = rig.LatestOperator().EventSetup!;
        Assert.Equal(ActiveRig.EventId, setup.EventId);
        Assert.Equal(new EventSetupStartFailure(StartFailureCode.CameraNotEligible, CameraFailureKind.Stale), setup.StartFailure);
        Assert.Null(rig.Brain.HeldEventId);
    }

    // END EVENT while the start waits for the Guest Screen

    private static async Task<(ActiveRig Rig, Task<StartEventResult> Start)> WaitingForGuestAsync()
    {
        var rig = await StartingRigAsync(guestConnected: false);
        var start = rig.BeginStart();
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);
        Assert.False(start.IsCompleted);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
        Assert.NotNull(rig.LatestOperator().RunningEvent);
        return (rig, start);
    }

    private static void EndWithOperator(ActiveRig rig)
    {
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("EndEvent").Status);
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("ConfirmEndEvent").Status);
    }

    private static void AssertEndedOnce(ActiveRig rig)
    {
        Assert.Null(rig.Brain.ActiveEventId);
        Assert.Null(rig.Brain.HeldEventId);
        Assert.False(rig.WakeLock.IsHeld);
        Assert.Equal(CameraState.Idle, rig.Brain.CameraSlotState);
        Assert.Equal(0, rig.Camera.OpenSessionCount);
        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], rig.Recorded.Calls);
        Assert.False(rig.Diagnostics().GuestScreenOpen);
        Assert.Null(rig.LatestOperator().RunningEvent);
        Assert.Equal(OperatorSurface.EventList, rig.LatestOperator().Surface);
        Assert.Null(rig.LatestOperator().EventSetup);
        Assert.Null(rig.LatestOperator().Overlays.Confirmation);
    }

    [Fact]
    public async Task Ending_the_Event_while_the_start_waits_for_the_Guest_Screen_releases_everything_once_without_a_failure()
    {
        var (rig, start) = await WaitingForGuestAsync();
        using var _ = rig;

        EndWithOperator(rig);
        rig.Pump(() => start.IsCompleted);

        Assert.Equal(StartEventResult.Ended, await start);
        Assert.False((await start).Succeeded);
        AssertEndedOnce(rig);
    }

    [Fact]
    public async Task A_new_start_is_accepted_at_once_after_an_Event_ended_during_its_start()
    {
        var (rig, start) = await WaitingForGuestAsync();
        using var _ = rig;
        EndWithOperator(rig);
        rig.Pump(() => start.IsCompleted);

        var again = rig.OperatorCommand("StartEvent", Payload(ActiveRig.EventId));
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 2);
        rig.ConnectGuest();
        rig.Pump(() => rig.LatestOperator().RunningEvent is not null);

        Assert.Equal(CommandStatus.Accepted, again.Status);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
        Assert.Equal(1, rig.Recorded.Calls.Count(call => call == "CloseGuestScreen"));
    }

    [Fact]
    public async Task A_late_handshake_after_the_Event_ended_during_its_start_does_nothing()
    {
        var (rig, start) = await WaitingForGuestAsync();
        using var _ = rig;
        EndWithOperator(rig);
        rig.Pump(() => start.IsCompleted);

        rig.ConnectGuest();
        rig.Run();

        Assert.Equal(StartEventResult.Ended, await start);
        AssertEndedOnce(rig);
    }

    [Fact]
    public async Task The_Guest_Screen_timeout_after_the_Event_ended_during_its_start_does_nothing()
    {
        var (rig, start) = await WaitingForGuestAsync();
        using var _ = rig;
        EndWithOperator(rig);
        rig.Pump(() => start.IsCompleted);

        rig.Advance(Brain.GuestScreenReadyTimeout + TimeSpan.FromSeconds(5));

        Assert.Equal(StartEventResult.Ended, await start);
        AssertEndedOnce(rig);
    }

    [Fact]
    public async Task Ending_a_SAVE_AND_START_that_waits_for_the_Guest_Screen_leaves_the_draft_open_with_no_failure()
    {
        using var rig = await StartingRigAsync(guestConnected: false);
        OpenSetup(rig, ActiveRig.EventId);
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("SaveAndStartEvent").Status);
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);

        EndWithOperator(rig);
        rig.Pump(() => rig.LatestOperator().EventSetup is { Saving: false });

        var setup = rig.LatestOperator().EventSetup!;
        Assert.Null(setup.StartFailure);
        Assert.Equal(OperatorSurface.EventSetup, rig.LatestOperator().Surface);
        Assert.Null(rig.Brain.HeldEventId);
        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], rig.Recorded.Calls);
        rig.Advance(Brain.GuestScreenReadyTimeout + TimeSpan.FromSeconds(1));
        Assert.Null(rig.LatestOperator().EventSetup!.StartFailure);
        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], rig.Recorded.Calls);
    }
}
