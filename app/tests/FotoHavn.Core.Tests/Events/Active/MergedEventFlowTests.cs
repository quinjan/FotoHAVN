using FotoHavn.Core.Camera;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Events.Active;

/// <summary>
/// The seams between Event list and setup (wave B1a) and the running Event (wave B1b) on the merged brain: a failed Start opens setup
/// with the reason, SAVE AND START uses the real Start Event, ending an Event wakes setup's camera, the camera subscriptions don't
/// leak, and the running Event locks what it must.
/// </summary>
public sealed class MergedEventFlowTests
{
    private static readonly AvailableCamera SecondCamera = new("camera-b", "Second camera");

    private static void AwaitSetup(ActiveRig rig) =>
        rig.Pump(() => rig.LatestOperator() is { Surface: OperatorSurface.EventSetup, EventSetup: not null });

    private static void OpenSetup(ActiveRig rig, string eventId)
    {
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("EditEvent", $$"""{"eventId":"{{eventId}}"}""").Status);
        AwaitSetup(rig);
    }

    private static int CameraSubscribers(ActiveRig rig) => rig.Brain.CameraStateChanged?.GetInvocationList().Length ?? 0;

    private static void AddSecondCamera(ActiveRig rig)
    {
        rig.Camera.AddCamera(SecondCamera);
        rig.Pump(() => rig.Brain.CameraSlotCameras.Count == 2);
    }

    private static void EndWithOperator(ActiveRig rig)
    {
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("EndEvent").Status);
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("ConfirmEndEvent").Status);
        rig.Pump(() => rig.Brain.ActiveEventId is null);
    }

    // A failed StartEvent opens Event setup with the failure

    [Fact]
    public async Task A_StartEvent_that_fails_on_the_guest_display_opens_setup_showing_the_code()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.Displays.Disconnect("fake-guest");
        rig.Run();

        var result = rig.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.EventId}}"}""");
        AwaitSetup(rig);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var setup = rig.LatestOperator().EventSetup!;
        Assert.Equal(ActiveRig.EventId, setup.EventId);
        Assert.Equal(new EventSetupStartFailure(StartFailureCode.GuestDisplayMissing, null), setup.StartFailure);
        Assert.Null(rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task A_StartEvent_that_fails_on_the_camera_shows_the_code_and_the_camera_reason()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.Camera.InUseByAnotherApp("fake-camera");

        rig.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.EventId}}"}""");
        AwaitSetup(rig);

        Assert.Equal(new EventSetupStartFailure(StartFailureCode.CameraNotEligible, CameraFailureKind.InUseByAnotherApp), rig.LatestOperator().EventSetup!.StartFailure);
        Assert.Null(rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task A_StartEvent_that_fails_on_storage_shows_storageNotWritable()
    {
        var store = new InterceptingStore(new InMemoryEventStore());
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store });
        store.FailProbe = true;

        rig.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.EventId}}"}""");
        AwaitSetup(rig);

        Assert.Equal(new EventSetupStartFailure(StartFailureCode.StorageNotWritable, null), rig.LatestOperator().EventSetup!.StartFailure);
    }

    [Fact]
    public async Task A_StartEvent_whose_Guest_Screen_never_answers_shows_guestScreenNotReady()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { GuestConnected = false });

        rig.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.EventId}}"}""");
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);
        rig.Advance(Brain.GuestScreenReadyTimeout + TimeSpan.FromSeconds(1));
        AwaitSetup(rig);

        Assert.Equal(new EventSetupStartFailure(StartFailureCode.GuestScreenNotReady, null), rig.LatestOperator().EventSetup!.StartFailure);
        Assert.Null(rig.Brain.ActiveEventId);
        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], rig.Recorded.Calls);
    }

    [Fact]
    public async Task After_a_failed_StartEvent_the_setup_camera_is_checked_for_the_operator_preview()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.Displays.Disconnect("fake-guest");
        rig.Run();

        rig.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.EventId}}"}""");
        AwaitSetup(rig);
        rig.Pump(() => rig.LatestOperator().EventSetup!.Camera.State == EventSetupCameraState.Ready);

        Assert.True(rig.LatestOperator().EventSetup!.Camera.PreviewActive);
        Assert.Equal(PreviewTarget.Operator, rig.Diagnostics().Camera.PreviewTarget);
    }

    // SAVE AND START goes through the real Start Event

    [Fact]
    public async Task SaveAndStart_starts_the_saved_Event_for_real_and_shows_the_running_panel()
    {
        using var rig = await ActiveRig.CreateAsync();
        OpenSetup(rig, ActiveRig.EventId);

        var result = rig.OperatorCommand("SaveAndStartEvent");
        rig.Pump(() => rig.LatestOperator() is { EventSetup: null, RunningEvent: not null });

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var operatorView = rig.LatestOperator();
        Assert.Equal(OperatorSurface.EventList, operatorView.Surface);
        Assert.Equal(ActiveRig.EventId, operatorView.RunningEvent!.EventId);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
        Assert.True(rig.WakeLock.IsHeld);
        Assert.Equal(["OpenGuestScreen(fake-guest)"], rig.Recorded.Calls);
        Assert.Equal(PreviewTarget.Guest, rig.Diagnostics().Camera.PreviewTarget);
        Assert.Equal(1, rig.Camera.OpenSessionCount);
    }

    [Fact]
    public async Task SaveAndStart_that_fails_in_Start_Event_keeps_the_draft_open_with_the_reason()
    {
        using var rig = await ActiveRig.CreateAsync();
        OpenSetup(rig, ActiveRig.EventId);
        rig.Displays.Disconnect("fake-guest");
        rig.Run();

        rig.OperatorCommand("SaveAndStartEvent");
        rig.Pump(() => rig.LatestOperator().EventSetup is { StartFailure: not null, Saving: false });

        var setup = rig.LatestOperator().EventSetup!;
        Assert.Equal(new EventSetupStartFailure(StartFailureCode.GuestDisplayMissing, null), setup.StartFailure);
        Assert.Equal(OperatorSurface.EventSetup, rig.LatestOperator().Surface);
        Assert.Null(rig.LatestOperator().RunningEvent);
        Assert.Null(rig.Brain.ActiveEventId);
        Assert.False(rig.WakeLock.IsHeld);
    }

    [Fact]
    public async Task SaveAndStart_that_fails_on_the_Guest_Screen_handshake_says_guestScreenNotReady()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { GuestConnected = false });
        OpenSetup(rig, ActiveRig.EventId);

        rig.OperatorCommand("SaveAndStartEvent");
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);
        rig.Advance(Brain.GuestScreenReadyTimeout + TimeSpan.FromSeconds(1));
        rig.Pump(() => rig.LatestOperator().EventSetup is { StartFailure: not null });

        Assert.Equal(new EventSetupStartFailure(StartFailureCode.GuestScreenNotReady, null), rig.LatestOperator().EventSetup!.StartFailure);
    }

    // Ending an Event wakes setup's camera

    [Fact]
    public async Task Ending_the_Event_with_the_Operator_checks_the_open_drafts_camera_again()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WithOtherEvent = true });
        rig.StartAndExpectSuccess();
        OpenSetup(rig, ActiveRig.OtherEventId);
        Assert.Equal(EventSetupCameraState.InUseByRunningEvent, rig.LatestOperator().EventSetup!.Camera.State);

        EndWithOperator(rig);
        rig.Pump(() => rig.LatestOperator().EventSetup is { Camera.State: EventSetupCameraState.Ready });

        var camera = rig.LatestOperator().EventSetup!.Camera;
        Assert.True(camera.PreviewActive);
        Assert.Equal(OperatorSurface.EventSetup, rig.LatestOperator().Surface);
        Assert.Null(rig.LatestOperator().RunningEvent);
        Assert.Equal("fake-camera", rig.Brain.CameraSlotDeviceId);
        Assert.Equal(PreviewTarget.Operator, rig.Diagnostics().Camera.PreviewTarget);
        Assert.Equal(1, rig.Camera.OpenSessionCount);
    }

    [Fact]
    public async Task Ending_the_Event_with_the_PIN_checks_a_drafts_other_camera_that_was_not_checked()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WithOtherEvent = true });
        AddSecondCamera(rig);
        rig.StartAndExpectSuccess();
        OpenSetup(rig, ActiveRig.OtherEventId);
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("SelectCamera", """{"deviceId":"camera-b"}""").Status);
        Assert.Equal(EventSetupCameraState.NotChecked, rig.LatestOperator().EventSetup!.Camera.State);

        rig.OpenAndCompleteExitWithPin();
        rig.Pump(() => rig.LatestOperator().EventSetup is { Camera.State: EventSetupCameraState.Ready });

        Assert.Equal("camera-b", rig.Brain.CameraSlotDeviceId);
        Assert.True(rig.LatestOperator().EventSetup!.Camera.PreviewActive);
    }

    [Fact]
    public async Task Ending_the_Event_with_no_draft_open_leaves_the_camera_released()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();

        EndWithOperator(rig);
        rig.Run();

        Assert.Equal(CameraState.Idle, rig.Brain.CameraSlotState);
        Assert.Equal(0, rig.Camera.OpenSessionCount);
        Assert.Equal(OperatorSurface.EventList, rig.LatestOperator().Surface);
    }

    // CameraStateChanged subscriptions return to their baseline

    [Fact]
    public async Task The_camera_subscription_exists_only_while_Start_Event_waits_for_the_camera()
    {
        using var rig = await ActiveRig.CreateAsync();
        Assert.Equal(0, CameraSubscribers(rig));

        var start = rig.BeginStart();
        rig.Pump(() => rig.Brain.CameraSlotState == CameraState.Checking);
        Assert.Equal(1, CameraSubscribers(rig));
        rig.Pump(() => start.IsCompleted);

        Assert.True((await start).Succeeded);
        Assert.Equal(0, CameraSubscribers(rig));

        EndWithOperator(rig);
        Assert.Equal(0, CameraSubscribers(rig));
    }

    [Fact]
    public async Task Failed_starts_leave_no_camera_subscription_behind()
    {
        var store = new InterceptingStore(new InMemoryEventStore());
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store, GuestConnected = false });

        rig.Camera.InUseByAnotherApp("fake-camera");
        Assert.Equal(StartFailureCode.CameraNotEligible, rig.Start().Failure);
        Assert.Equal(0, CameraSubscribers(rig));
        rig.Camera.Recover();

        store.FailProbe = true;
        Assert.Equal(StartFailureCode.StorageNotWritable, rig.Start().Failure);
        Assert.Equal(0, CameraSubscribers(rig));
        store.FailProbe = false;

        var start = rig.BeginStart();
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);
        rig.Advance(Brain.GuestScreenReadyTimeout + TimeSpan.FromSeconds(1));
        Assert.Equal(StartFailureCode.GuestScreenNotReady, (await start).Failure);
        Assert.Equal(0, CameraSubscribers(rig));
    }

    [Fact]
    public async Task Opening_and_leaving_Event_setup_adds_no_camera_subscription()
    {
        using var rig = await ActiveRig.CreateAsync();

        OpenSetup(rig, ActiveRig.EventId);
        Assert.Equal(0, CameraSubscribers(rig));

        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("LeaveEventSetup").Status);
        rig.Run();
        Assert.Equal(0, CameraSubscribers(rig));
        Assert.Equal(OperatorSurface.EventList, rig.LatestOperator().Surface);
    }

    // The running lock, end to end

    [Fact]
    public async Task While_an_Event_runs_it_cannot_be_edited_deleted_or_started_and_no_other_Event_can_start()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WithOtherEvent = true });
        rig.StartAndExpectSuccess();

        foreach (var (name, eventId) in new[]
        {
            ("EditEvent", ActiveRig.EventId),
            ("DeleteEvent", ActiveRig.EventId),
            ("StartEvent", ActiveRig.EventId),
            ("StartEvent", ActiveRig.OtherEventId),
        })
        {
            var result = rig.OperatorCommand(name, $$"""{"eventId":"{{eventId}}"}""");
            Assert.Equal((CommandStatus.Rejected, RejectReason.EventRunning), (result.Status, result.Reason));
        }

        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
        var rows = rig.LatestOperator().EventList!.Events;
        Assert.True(rows.Single(row => row.Id == ActiveRig.EventId).Running);
        Assert.All(rows, row => Assert.Equal(StartDisabledReason.EventRunning, row.StartDisabledReason));
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("OpenEventFolder", $$"""{"eventId":"{{ActiveRig.EventId}}"}""").Status);
    }

    [Fact]
    public async Task While_an_Event_runs_another_Event_can_be_edited_and_its_setup_cannot_save_and_start()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WithOtherEvent = true });
        rig.StartAndExpectSuccess();

        OpenSetup(rig, ActiveRig.OtherEventId);

        var setup = rig.LatestOperator().EventSetup!;
        Assert.Equal(ActiveRig.OtherEventId, setup.EventId);
        Assert.False(setup.SaveAndStartAvailable);
        Assert.Equal(StartDisabledReason.EventRunning, setup.SaveAndStartDisabledReason);
        var result = rig.OperatorCommand("SaveAndStartEvent");
        Assert.Equal((CommandStatus.Rejected, RejectReason.EventRunning), (result.Status, result.Reason));
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task While_an_Event_runs_setup_of_another_Event_shows_the_same_camera_as_in_use_and_a_different_one_as_not_checked()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WithOtherEvent = true });
        AddSecondCamera(rig);
        rig.StartAndExpectSuccess();
        OpenSetup(rig, ActiveRig.OtherEventId);

        var same = rig.LatestOperator().EventSetup!.Camera;
        Assert.Equal((EventSetupCameraState.InUseByRunningEvent, false), (same.State, same.PreviewActive));

        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("SelectCamera", """{"deviceId":"camera-b"}""").Status);
        var different = rig.LatestOperator().EventSetup!.Camera;
        Assert.Equal(("camera-b", EventSetupCameraState.NotChecked, false), (different.SelectedDeviceId, different.State, different.PreviewActive));

        Assert.Equal("fake-camera", rig.Brain.CameraSlotDeviceId);
        Assert.Equal(1, rig.Camera.OpenSessionCount);
        Assert.Equal(PreviewTarget.Guest, rig.Diagnostics().Camera.PreviewTarget);
    }

    [Fact]
    public async Task Once_the_Event_ends_every_lock_lifts()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WithOtherEvent = true });
        rig.StartAndExpectSuccess();
        EndWithOperator(rig);
        rig.Run();

        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("DeleteEvent", $$"""{"eventId":"{{ActiveRig.EventId}}"}""").Status);
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("CancelDeleteEvent").Status);
        OpenSetup(rig, ActiveRig.EventId);
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("LeaveEventSetup").Status);
        var restarted = rig.Start(ActiveRig.EventId);
        Assert.True(restarted.Succeeded, $"The start failed: {restarted.Failure}.");
    }
}
