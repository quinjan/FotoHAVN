using FotoHavn.Core.Camera;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Events.Setup;

public sealed class EventSetupCameraTests
{
    private static readonly AvailableCamera SecondCamera = new("camera-b", "Second camera");

    private static async Task<SetupRig> RigAsync(params EventRecord[] extra)
    {
        var store = await SetupSamples.SeededStoreAsync();
        foreach (var record in extra)
        {
            await store.CreateEventAsync(record, CancellationToken.None);
        }

        return new SetupRig(new HookedStore(store), cameras: [FakeCameraAdapter.DefaultCamera, SecondCamera]);
    }

    private static EventSetupCameraView Camera(SetupRig rig) => rig.Setup.Camera;

    [Fact]
    public async Task With_no_camera_chosen_the_field_lists_the_cameras_and_says_none()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        var camera = Camera(rig);
        Assert.Equal(["fake-camera", "camera-b"], camera.Cameras.Select(option => option.DeviceId));
        Assert.Equal(["Fake camera", "Second camera"], camera.Cameras.Select(option => option.Name));
        Assert.Equal((null, null, EventSetupCameraState.None, null, false, true), (camera.SelectedDeviceId, camera.SelectedName, camera.State, camera.Reason, camera.PreviewActive, camera.Mirrored));
    }

    [Fact]
    public async Task Choosing_a_camera_binds_it_checks_it_with_a_preview_and_it_is_ready_after_a_fresh_frame()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.SelectCamera("fake-camera");

        var checking = Camera(rig);
        Assert.Equal(("fake-camera", "Fake camera", EventSetupCameraState.Checking, true), (checking.SelectedDeviceId, checking.SelectedName, checking.State, checking.PreviewActive));

        rig.Advance(TimeSpan.FromMilliseconds(500));

        var ready = Camera(rig);
        Assert.Equal((EventSetupCameraState.Ready, null, true), (ready.State, ready.Reason, ready.PreviewActive));
        Assert.Equal(1, rig.Fake.OpenSessionCount);
        Assert.True(rig.Setup.Dirty);
    }

    [Fact]
    public async Task A_camera_that_is_not_listed_cannot_be_chosen()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Rejects(RejectReason.NotAvailable, "SelectCamera", """{"deviceId":"ghost"}""");

        Assert.Equal(EventSetupCameraState.None, Camera(rig).State);
        Assert.False(rig.Setup.Dirty);
    }

    [Fact]
    public async Task Choosing_another_camera_replaces_the_binding_and_the_check()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.SelectCamera("fake-camera");
        rig.Advance(TimeSpan.FromMilliseconds(500));

        rig.SelectCamera("camera-b");

        Assert.Equal(("camera-b", "Second camera", EventSetupCameraState.Checking), (Camera(rig).SelectedDeviceId, Camera(rig).SelectedName, Camera(rig).State));
        rig.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(EventSetupCameraState.Ready, Camera(rig).State);
        Assert.Equal("camera-b", rig.Brain.CameraSlotDeviceId);
    }

    [Fact]
    public async Task A_camera_Windows_blocks_for_privacy_says_why()
    {
        using var rig = await RigAsync();
        rig.Fake.Fail(CameraFailureKind.PrivacyDenied);
        rig.OpenNew();

        rig.SelectCamera("fake-camera");
        rig.Advance(TimeSpan.FromMilliseconds(500));

        var camera = Camera(rig);
        Assert.Equal((EventSetupCameraState.NotAvailable, CameraFailureKind.PrivacyDenied, false), (camera.State, camera.Reason, camera.PreviewActive));
    }

    [Fact]
    public async Task A_camera_another_app_holds_is_in_use_by_another_app()
    {
        using var rig = await RigAsync();
        rig.Fake.InUseByAnotherApp("fake-camera");
        rig.OpenNew();

        rig.SelectCamera("fake-camera");
        rig.Advance(TimeSpan.FromMilliseconds(500));

        var camera = Camera(rig);
        Assert.Equal((EventSetupCameraState.InUseByAnotherApp, CameraFailureKind.InUseByAnotherApp, false), (camera.State, camera.Reason, camera.PreviewActive));
    }

    [Fact]
    public async Task A_bound_camera_that_is_not_listed_is_missing_and_keeps_its_binding_and_name()
    {
        var ghost = SetupSamples.Event("44444444-4444-4444-8444-444444444444", "Ghost cam party") with { CameraDeviceId = "ghost", CameraName = "Studio camera" };
        using var rig = await RigAsync(ghost);

        rig.OpenEdit(ghost.Id);

        var camera = Camera(rig);
        Assert.Equal(("ghost", "Studio camera", EventSetupCameraState.Missing, null, false), (camera.SelectedDeviceId, camera.SelectedName, camera.State, camera.Reason, camera.PreviewActive));
        Assert.Equal(0, rig.Fake.OpenSessionCount);
        Assert.False(rig.Setup.Dirty);

        rig.Field("notes", "x");
        rig.Accepts("SaveEvent");

        var saved = (await rig.Store.GetEventAsync(ghost.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal(("ghost", "Studio camera"), (saved.CameraDeviceId, saved.CameraName));
    }

    [Fact]
    public async Task Choosing_a_listed_camera_replaces_a_missing_one_and_nothing_else_does()
    {
        var ghost = SetupSamples.Event("44444444-4444-4444-8444-444444444444", "Ghost cam party") with { CameraDeviceId = "ghost", CameraName = "Studio camera" };
        using var rig = await RigAsync(ghost);
        rig.OpenEdit(ghost.Id);

        rig.SelectCamera("camera-b");

        Assert.Equal(("camera-b", EventSetupCameraState.Checking), (Camera(rig).SelectedDeviceId, Camera(rig).State));
    }

    [Fact]
    public async Task A_camera_that_is_unplugged_while_chosen_becomes_missing_and_stays_bound()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.SelectCamera("fake-camera");
        rig.Advance(TimeSpan.FromMilliseconds(500));

        Assert.True(rig.Fake.Disappear("fake-camera"));
        rig.Advance(TimeSpan.FromMilliseconds(500));

        var camera = Camera(rig);
        Assert.Equal(("fake-camera", "Fake camera", EventSetupCameraState.Missing), (camera.SelectedDeviceId, camera.SelectedName, camera.State));
        Assert.Equal(["camera-b"], camera.Cameras.Select(option => option.DeviceId));
    }

    [Fact]
    public async Task Editing_an_Event_with_a_listed_camera_checks_it_at_once()
    {
        using var rig = await RigAsync();

        rig.OpenEdit(SetupSamples.HarbourId);

        Assert.Equal(EventSetupCameraState.Checking, Camera(rig).State);
        rig.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(EventSetupCameraState.Ready, Camera(rig).State);
    }

    [Fact]
    public async Task Leaving_setup_releases_the_camera()
    {
        using var rig = await RigAsync();
        rig.OpenEdit(SetupSamples.HarbourId);
        rig.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(1, rig.Fake.OpenSessionCount);

        rig.Accepts("LeaveEventSetup");

        rig.WaitUntil(() => rig.Fake.OpenSessionCount == 0);
        Assert.Equal(CameraState.Idle, rig.Brain.CameraSlotState);
        Assert.Null(rig.Brain.CameraSlotDeviceId);
    }

    [Fact]
    public async Task While_an_Event_runs_the_same_camera_is_in_use_by_the_running_Event_and_is_not_opened()
    {
        using var rig = await RigAsync();
        var running = await SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId);
        rig.StartRunning(running);
        rig.OnBrain(() => rig.Brain.OpenCameraFor("fake-camera", PreviewTarget.Guest));
        rig.Advance(TimeSpan.FromMilliseconds(500));

        rig.OpenEdit(SetupSamples.WeddingId);

        var camera = Camera(rig);
        Assert.Equal(("fake-camera", "Fake camera", EventSetupCameraState.InUseByRunningEvent, null, false), (camera.SelectedDeviceId, camera.SelectedName, camera.State, camera.Reason, camera.PreviewActive));
        Assert.Equal(1, rig.Fake.OpenSessionCount);
        Assert.Equal(CameraState.Ready, rig.Brain.CameraSlotState);

        rig.Field("notes", "x");
        rig.Accepts("LeaveEventSetup");
        rig.Accepts("ConfirmLeave", """{"choice":"discardChanges"}""");

        Assert.Equal("fake-camera", rig.Brain.CameraSlotDeviceId);
        Assert.Equal(1, rig.Fake.OpenSessionCount);
        Assert.Equal(CameraState.Ready, rig.Brain.CameraSlotState);
    }

    [Fact]
    public async Task While_an_Event_runs_a_different_camera_is_not_checked_and_a_missing_one_is_missing()
    {
        var ghost = SetupSamples.Event("44444444-4444-4444-8444-444444444444", "Ghost cam party") with { CameraDeviceId = "ghost", CameraName = "Studio camera" };
        using var rig = await RigAsync(ghost);
        rig.StartRunning(await SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId));
        rig.OnBrain(() => rig.Brain.OpenCameraFor("fake-camera", PreviewTarget.Guest));
        rig.OpenNew();

        rig.SelectCamera("camera-b");

        Assert.Equal(("camera-b", EventSetupCameraState.NotChecked, false), (Camera(rig).SelectedDeviceId, Camera(rig).State, Camera(rig).PreviewActive));
        Assert.Equal("fake-camera", rig.Brain.CameraSlotDeviceId);

        rig.SelectCamera("fake-camera");
        Assert.Equal(EventSetupCameraState.InUseByRunningEvent, Camera(rig).State);

        rig.Accepts("LeaveEventSetup");
        rig.Accepts("ConfirmLeave", """{"choice":"discardChanges"}""");
        rig.OpenEdit(ghost.Id);
        Assert.Equal(EventSetupCameraState.Missing, Camera(rig).State);
    }

    [Fact]
    public async Task When_the_running_Event_ends_the_bound_camera_is_checked_again_on_request()
    {
        using var rig = await RigAsync();
        rig.StartRunning(await SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId));
        rig.OnBrain(() => rig.Brain.OpenCameraFor("fake-camera", PreviewTarget.Guest));
        rig.OpenEdit(SetupSamples.WeddingId);
        Assert.Equal(EventSetupCameraState.InUseByRunningEvent, Camera(rig).State);

        rig.Brain.ShowActiveEvent(null);
        rig.OnBrain(rig.Brain.ReleaseCamera);
        rig.Brain.ShowOperatorSurface(OperatorSurface.EventSetup);
        rig.Harness.Run();
        Assert.Equal(EventSetupCameraState.NotChecked, Camera(rig).State);

        rig.OnBrain(rig.Brain.RefreshSetupCamera);
        rig.Brain.ShowOperatorSurface(OperatorSurface.EventSetup);
        rig.Harness.Run();
        Assert.Equal(EventSetupCameraState.Checking, Camera(rig).State);
        rig.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(EventSetupCameraState.Ready, Camera(rig).State);
    }

    [Fact]
    public async Task Asking_again_while_the_camera_is_already_held_changes_nothing()
    {
        using var rig = await RigAsync();
        rig.OpenEdit(SetupSamples.HarbourId);
        rig.Advance(TimeSpan.FromMilliseconds(500));

        rig.OnBrain(rig.Brain.RefreshSetupCamera);

        Assert.Equal(1, rig.Fake.OpenSessionCount);
        Assert.Equal(CameraState.Ready, rig.Brain.CameraSlotState);
    }

    [Fact]
    public async Task Asking_for_the_camera_with_no_draft_or_no_binding_does_nothing()
    {
        using var rig = await RigAsync();
        rig.OnBrain(rig.Brain.RefreshSetupCamera);
        rig.OpenNew();

        rig.OnBrain(rig.Brain.RefreshSetupCamera);

        Assert.Equal(0, rig.Fake.OpenSessionCount);
        Assert.Equal(CameraState.Idle, rig.Brain.CameraSlotState);
    }

    [Fact]
    public async Task A_released_slot_with_a_bound_listed_camera_reads_as_not_checked_until_asked()
    {
        using var rig = await RigAsync();
        rig.OpenEdit(SetupSamples.HarbourId);
        rig.Advance(TimeSpan.FromMilliseconds(500));

        rig.OnBrain(rig.Brain.ReleaseCamera);
        rig.Brain.ShowOperatorSurface(OperatorSurface.EventSetup);
        rig.Harness.Run();

        Assert.Equal(EventSetupCameraState.NotChecked, Camera(rig).State);
        Assert.False(Camera(rig).PreviewActive);
    }

    [Fact]
    public async Task Event_setup_leaves_the_camera_alone_while_the_camera_diagnostics_screen_is_open()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.Field("name", "x");
        rig.Brain.ShowCameraDiagnostics(new CameraDiagnosticsView([], null, CameraState.Idle, null, null, false, null, null));
        rig.Harness.Run();

        rig.OnBrain(rig.Brain.RefreshSetupCamera);

        Assert.Equal(0, rig.Fake.OpenSessionCount);
    }

    [Fact]
    public async Task The_failed_Start_of_a_bound_Event_checks_its_camera_again_in_the_form()
    {
        using var rig = await RigAsync();

        rig.OnBrain(() => rig.Brain.OpenEventSetupWithStartFailure(SetupSamples.HarbourId, new EventSetupStartFailure(StartFailureCode.CameraNotEligible, CameraFailureKind.PrivacyDenied)));
        rig.WaitUntil(() => rig.Latest.Surface == OperatorSurface.EventSetup);

        Assert.Equal(new EventSetupStartFailure(StartFailureCode.CameraNotEligible, CameraFailureKind.PrivacyDenied), rig.Setup.StartFailure);
        Assert.Equal(EventSetupCameraState.Checking, Camera(rig).State);
    }
}

public sealed class EventSetupDisplayTests
{
    private static async Task<SetupRig> RigAsync(FakeDisplayAdapter? displays = null, params EventRecord[] extra)
    {
        var store = await SetupSamples.SeededStoreAsync();
        foreach (var record in extra)
        {
            await store.CreateEventAsync(record, CancellationToken.None);
        }

        return new SetupRig(new HookedStore(store), displays: displays);
    }

    private static EventSetupGuestDisplayView Display(SetupRig rig) => rig.Setup.GuestDisplay;

    [Fact]
    public async Task The_picker_lists_the_displays_in_identify_order_with_the_Operator_Screen_marked()
    {
        var right = FakeDisplayAdapter.Create("z-right", "Right", 3840, 0, 1920, 1080);
        var below = FakeDisplayAdapter.Create("a-below", "Below", 0, 1080, 1920, 1080);
        using var rig = await RigAsync(new FakeDisplayAdapter(right, FakeDisplayAdapter.DefaultPrimary, below, FakeDisplayAdapter.DefaultGuest));
        rig.OpenNew();

        var display = Display(rig);

        Assert.Equal(["fake-primary", "a-below", "fake-guest", "z-right"], display.Displays.Select(option => option.Id));
        Assert.Equal([1, 2, 3, 4], display.Displays.Select(option => option.Number));
        Assert.Equal(["fake-primary"], display.Displays.Where(option => option.IsOperatorScreen).Select(option => option.Id));
        Assert.Equal(new GuestDisplayOption("fake-guest", "Fake guest", 1920, 0, 1920, 1080, 3, false), display.Displays[2]);
        Assert.Equal((null, null, GuestDisplayState.None, false), (display.SelectedId, display.SelectedName, display.State, display.CoversOperatorScreen));
    }

    [Fact]
    public async Task The_numbers_match_what_IDENTIFY_DISPLAYS_shows()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Accepts("IdentifyDisplays");

        Assert.Equal(
            rig.Setup.GuestDisplay.Displays.Select(option => (option.Id, option.Number)),
            rig.Latest.Overlays.IdentifyDisplays!.Displays.Select(entry => (entry.Id, entry.Number)));
    }

    [Fact]
    public async Task Choosing_the_other_display_binds_it_without_covering_the_Operator_Screen()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.SelectDisplay("fake-guest");

        Assert.Equal(("fake-guest", "Fake guest", GuestDisplayState.Connected, false), (Display(rig).SelectedId, Display(rig).SelectedName, Display(rig).State, Display(rig).CoversOperatorScreen));
        Assert.True(rig.Setup.Dirty);
    }

    [Fact]
    public async Task Choosing_the_Operator_Screens_own_display_covers_it()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.SelectDisplay("fake-primary");

        Assert.Equal((GuestDisplayState.Connected, true), (Display(rig).State, Display(rig).CoversOperatorScreen));
    }

    [Fact]
    public async Task The_Operator_Screen_is_wherever_the_host_says_the_Operator_window_is()
    {
        using var rig = await RigAsync();
        rig.Host.OperatorDisplayId = "fake-guest";
        rig.OpenNew();
        rig.SelectDisplay("fake-guest");

        Assert.Equal(["fake-guest"], Display(rig).Displays.Where(option => option.IsOperatorScreen).Select(option => option.Id));
        Assert.True(Display(rig).CoversOperatorScreen);

        rig.Host.OperatorDisplayId = null;
        rig.SelectDisplay("fake-primary");

        Assert.Equal(["fake-primary"], Display(rig).Displays.Where(option => option.IsOperatorScreen).Select(option => option.Id));
        Assert.True(Display(rig).CoversOperatorScreen);
    }

    [Fact]
    public async Task With_only_one_display_the_Guest_Screen_covers_the_Operator_Screen()
    {
        using var rig = await RigAsync(new FakeDisplayAdapter(FakeDisplayAdapter.DefaultPrimary));
        rig.OpenNew();

        rig.SelectDisplay("fake-primary");

        Assert.True(Display(rig).CoversOperatorScreen);
        Assert.Single(Display(rig).Displays);
    }

    [Fact]
    public async Task A_display_that_is_not_connected_cannot_be_chosen()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Rejects(RejectReason.NotAvailable, "SelectGuestDisplay", """{"displayId":"ghost"}""");

        Assert.Equal(GuestDisplayState.None, Display(rig).State);
        Assert.False(rig.Setup.Dirty);
    }

    [Fact]
    public async Task A_bound_display_that_is_not_connected_is_missing_keeps_its_binding_and_is_never_replaced()
    {
        var ghost = SetupSamples.Event("44444444-4444-4444-8444-444444444444", "Ghost display party") with { GuestDisplayId = "gone", GuestDisplayName = "DELL P2419H" };
        using var rig = await RigAsync(null, ghost);

        rig.OpenEdit(ghost.Id);

        var display = Display(rig);
        Assert.Equal(("gone", "DELL P2419H", GuestDisplayState.Missing, false), (display.SelectedId, display.SelectedName, display.State, display.CoversOperatorScreen));
        Assert.Equal(2, display.Displays.Count);
        Assert.DoesNotContain(rig.Setup.FieldErrors, error => error.Field == EventSetupFieldNames.GuestDisplay);

        rig.Field("notes", "x");
        rig.Accepts("SaveEvent");

        var saved = (await rig.Store.GetEventAsync(ghost.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal(("gone", "DELL P2419H"), (saved.GuestDisplayId, saved.GuestDisplayName));
    }

    [Fact]
    public async Task A_display_that_is_unplugged_while_bound_becomes_missing_and_comes_back_connected()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.SelectDisplay("fake-guest");

        Assert.True(rig.Displays().Disconnect("fake-guest"));
        rig.Harness.Run();

        var display = Display(rig);
        Assert.Equal(("fake-guest", "Fake guest", GuestDisplayState.Missing, false), (display.SelectedId, display.SelectedName, display.State, display.CoversOperatorScreen));
        Assert.Single(display.Displays);

        rig.Displays().Connect(FakeDisplayAdapter.DefaultGuest);
        rig.Harness.Run();
        Assert.Equal(GuestDisplayState.Connected, Display(rig).State);
    }

    [Fact]
    public async Task Choosing_a_display_replaces_a_missing_one()
    {
        var ghost = SetupSamples.Event("44444444-4444-4444-8444-444444444444", "Ghost display party") with { GuestDisplayId = "gone", GuestDisplayName = "DELL P2419H" };
        using var rig = await RigAsync(null, ghost);
        rig.OpenEdit(ghost.Id);

        rig.SelectDisplay("fake-guest");

        Assert.Equal(("fake-guest", GuestDisplayState.Connected), (Display(rig).SelectedId, Display(rig).State));
    }
}

internal static class SetupRigDisplays
{
    public static FakeDisplayAdapter Displays(this SetupRig rig) => rig.Harness.Displays;
}
