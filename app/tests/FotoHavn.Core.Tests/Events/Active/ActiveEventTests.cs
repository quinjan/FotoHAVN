using FotoHavn.Core.Displays;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Events.Active;

/// <summary>A running Event: the panel's live facts, the Guest Screen's Start and Start unavailable, ending it, single-screen setups, and the wake lock.</summary>
public sealed class ActiveEventTests
{
    // The running-Event panel

    [Fact]
    public async Task The_panel_follows_the_guest_display_live_and_the_Guest_Screen_closes_when_it_goes()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Hooks = true });
        rig.StartAndExpectSuccess();
        Assert.Equal(RunningDisplayState.Connected, rig.LatestOperator().RunningEvent!.GuestDisplay.State);

        rig.Displays.Disconnect("fake-guest");
        rig.Run();

        var panel = rig.LatestOperator().RunningEvent!;
        Assert.Equal(new RunningEventDisplay("Fake guest", RunningDisplayState.Disconnected), panel.GuestDisplay);
        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], rig.Recorded.Calls);
        Assert.False(rig.Diagnostics().GuestScreenOpen);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);

        rig.Displays.Connect(FakeDisplayAdapter.DefaultGuest);
        rig.Run();
        Assert.Equal(RunningDisplayState.Connected, rig.LatestOperator().RunningEvent!.GuestDisplay.State);
    }

    [Fact]
    public async Task The_panel_follows_the_camera_live()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();
        Assert.Equal(new RunningEventCamera(CameraState.Ready, null), rig.LatestOperator().RunningEvent!.Camera);

        rig.Camera.Stall();
        rig.Advance(TimeSpan.FromSeconds(2.5));

        Assert.Equal(new RunningEventCamera(CameraState.NotAvailable, CameraFailureKind.Stale), rig.LatestOperator().RunningEvent!.Camera);
    }

    [Fact]
    public async Task The_panel_stays_on_the_Start_step_with_zero_completed_Guest_Cycles_in_M6()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();
        rig.Advance(TimeSpan.FromSeconds(30));

        var panel = rig.LatestOperator().RunningEvent!;
        Assert.Equal(RunningEventStep.Start, panel.CurrentStep);
        Assert.Equal(0, panel.CompletedGuestCycles);
        Assert.Equal(rig.Clock.UtcNow - TimeSpan.FromSeconds(30), panel.StartedAt, TimeSpan.FromSeconds(2));
    }

    // The Guest Screen shell

    [Fact]
    public async Task Without_an_Event_the_Guest_Screen_shows_the_Start_placeholder()
    {
        using var rig = await ActiveRig.CreateAsync();

        Assert.Equal(new GuestPresentation(GuestSurface.Start, new StartView(false, false), null, GuestOverlays.None, null, null, null, null, null, null, null, null, null), rig.LatestGuest());
    }

    [Fact]
    public async Task Start_shows_STEP_INSIDE_only_while_the_camera_and_storage_are_good()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();

        Assert.Equal(new StartView(true, false), rig.LatestGuest().Start);
        Assert.Null(rig.LatestGuest().StartUnavailable);
    }

    [Fact]
    public async Task A_camera_that_stops_delivering_makes_Start_unavailable_and_it_recovers_by_itself()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Hooks = true });
        rig.StartAndExpectSuccess();

        rig.Camera.Stall();
        rig.Advance(TimeSpan.FromSeconds(2.5));

        var unavailable = rig.LatestGuest();
        Assert.Equal(GuestSurface.StartUnavailable, unavailable.Surface);
        Assert.Equal(new StartUnavailableView(StartUnavailableReason.Camera, false), unavailable.StartUnavailable);
        Assert.Null(unavailable.Start);

        rig.Camera.Recover();
        rig.Pump(() => rig.LatestGuest().Surface == GuestSurface.Start, maxIterations: 600);

        Assert.Equal(new StartView(true, false), rig.LatestGuest().Start);
        Assert.Equal(CameraState.Ready, rig.Brain.CameraSlotState);
        Assert.Equal(PreviewTarget.Guest, rig.Diagnostics().Camera.PreviewTarget);
    }

    [Fact]
    public async Task A_camera_another_app_took_is_retried_until_it_is_free_again()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();
        rig.Camera.Fail(CameraFailureKind.Failed);
        rig.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(GuestSurface.StartUnavailable, rig.LatestGuest().Surface);
        Assert.Equal(CameraFailureKind.Failed, rig.LatestOperator().RunningEvent!.Camera.Reason);

        rig.Advance(Brain.CameraRecoveryInterval + TimeSpan.FromSeconds(1));
        Assert.Equal(GuestSurface.StartUnavailable, rig.LatestGuest().Surface);

        rig.Camera.Recover();
        rig.Pump(() => rig.LatestGuest().Surface == GuestSurface.Start, maxIterations: 600);
        Assert.Equal(CameraState.Ready, rig.LatestOperator().RunningEvent!.Camera.State);
    }

    [Fact]
    public async Task Storage_that_stops_being_writable_makes_Start_unavailable_until_it_is_writable_again()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();

        rig.Executor.Post(() => rig.Brain.SetActiveStorageFailed(true));
        rig.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        rig.Run();
        Assert.Equal(new StartUnavailableView(StartUnavailableReason.Storage, false), rig.LatestGuest().StartUnavailable);

        rig.Executor.Post(() => rig.Brain.SetActiveStorageFailed(false));
        rig.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        rig.Run();
        Assert.Equal(new StartView(true, false), rig.LatestGuest().Start);
    }

    [Fact]
    public async Task With_both_a_bad_camera_and_bad_storage_Start_unavailable_names_the_camera()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();
        rig.Camera.Stall();
        rig.Executor.Post(() => rig.Brain.SetActiveStorageFailed(true));
        rig.Advance(TimeSpan.FromSeconds(2.5));

        Assert.Equal(StartUnavailableReason.Camera, rig.LatestGuest().StartUnavailable!.Reason);
    }

    [Fact]
    public async Task Reporting_storage_trouble_with_no_Event_running_does_nothing()
    {
        using var rig = await ActiveRig.CreateAsync();

        rig.Executor.Post(() => rig.Brain.SetActiveStorageFailed(true));
        rig.Run();

        Assert.Equal(new StartView(false, false), rig.LatestGuest().Start);
    }

    [Fact]
    public async Task The_fatal_surface_overrides_the_Guest_Screen_while_an_Event_runs()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();

        rig.Brain.ShowFatal("FOTOHAVN stopped. Close it and open it again.");
        rig.Run();

        Assert.Equal(GuestSurface.Fatal, rig.LatestGuest().Surface);
    }

    // Ending the Event

    [Fact]
    public async Task END_EVENT_asks_first_and_KEEP_RUNNING_closes_the_question()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();

        var end = rig.OperatorCommand("EndEvent");
        Assert.Equal(CommandResultMessage.Accepted(end.Id), end);
        Assert.Equal(new ConfirmationOverlay(ConfirmationKind.EndEvent, null, null, null), rig.LatestOperator().Overlays.Confirmation);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);

        var keep = rig.OperatorCommand("KeepRunning");
        Assert.Equal(CommandResultMessage.Accepted(keep.Id), keep);
        Assert.Null(rig.LatestOperator().Overlays.Confirmation);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
        Assert.True(rig.WakeLock.IsHeld);
        Assert.DoesNotContain("CloseGuestScreen", rig.Recorded.Calls);
    }

    [Fact]
    public async Task Confirming_END_EVENT_closes_the_Guest_Screen_releases_the_camera_and_wake_lock_and_shows_the_plain_list()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Hooks = true });
        rig.StartAndExpectSuccess();
        rig.OperatorCommand("EndEvent");

        var confirm = rig.OperatorCommand("ConfirmEndEvent");

        Assert.Equal(CommandResultMessage.Accepted(confirm.Id), confirm);
        Assert.Null(rig.Brain.ActiveEventId);
        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], rig.Recorded.Calls);
        Assert.Equal(CameraState.Idle, rig.Brain.CameraSlotState);
        Assert.Equal(0, rig.Camera.OpenSessionCount);
        var diagnostics = rig.Diagnostics();
        Assert.False(diagnostics.WakeLock.Held);
        Assert.False(diagnostics.GuestScreenOpen);
        Assert.Equal(PreviewTarget.None, diagnostics.Camera.PreviewTarget);
        var presentation = rig.LatestOperator();
        Assert.Equal(OperatorSurface.EventList, presentation.Surface);
        Assert.Null(presentation.RunningEvent);
        Assert.Null(presentation.Overlays.Confirmation);
        var row = Assert.Single(presentation.EventList!.Events);
        Assert.False(row.Running);
        Assert.Equal(new EventRowActions(true, true, true, true, true), row.Actions);
        Assert.Equal(new GuestPresentation(GuestSurface.Start, new StartView(false, false), null, GuestOverlays.None, null, null, null, null, null, null, null, null, null), rig.LatestGuest());
    }

    [Fact]
    public async Task The_Event_can_be_started_again_after_it_ended()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();
        rig.OperatorCommand("EndEvent");
        rig.OperatorCommand("ConfirmEndEvent");

        var again = rig.Start();

        Assert.True(again.Succeeded);
        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen", "OpenGuestScreen(fake-guest)"], rig.Recorded.Calls);
        Assert.Equal(1, rig.Camera.OpenSessionCount);
    }

    [Fact]
    public async Task Ending_refreshes_the_Event_list_from_the_store()
    {
        var store = new InterceptingStore(new InMemoryEventStore());
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store });
        rig.StartAndExpectSuccess();
        var listed = store.ListCalls;
        rig.OperatorCommand("EndEvent");

        rig.OperatorCommand("ConfirmEndEvent");

        Assert.Equal(listed + 1, store.ListCalls);
    }

    [Fact]
    public async Task Ending_still_works_when_the_refresh_fails_or_the_store_throws()
    {
        var store = new InterceptingStore(new InMemoryEventStore());
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store });
        rig.StartAndExpectSuccess();
        store.FailList = true;
        rig.OperatorCommand("EndEvent");
        rig.OperatorCommand("ConfirmEndEvent");
        Assert.Null(rig.Brain.ActiveEventId);
        Assert.Single(rig.LatestOperator().EventList!.Events);

        store.FailList = false;
        store.ThrowOnList = false;
        rig.StartAndExpectSuccess();
        store.ThrowOnList = true;
        rig.OperatorCommand("EndEvent");
        rig.OperatorCommand("ConfirmEndEvent");
        Assert.Null(rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task The_end_commands_are_not_available_without_the_right_state()
    {
        using var rig = await ActiveRig.CreateAsync();

        Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("EndEvent").Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("ConfirmEndEvent").Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("KeepRunning").Reason);

        rig.StartAndExpectSuccess();
        Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("ConfirmEndEvent").Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("KeepRunning").Reason);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task Another_confirmation_replaces_the_end_question_and_then_END_EVENT_is_not_confirmable()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();
        rig.OperatorCommand("EndEvent");

        rig.Executor.Post(() => rig.Brain.SetConfirmation(new ConfirmationOverlay(ConfirmationKind.LeaveSetup, "Harbour gala", null, null)));
        rig.Run();

        Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("ConfirmEndEvent").Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("KeepRunning").Reason);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task Ending_with_a_different_confirmation_open_leaves_that_confirmation_alone()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();
        rig.Executor.Post(() => rig.Brain.SetConfirmation(new ConfirmationOverlay(ConfirmationKind.LeaveSetup, "Harbour gala", null, null)));
        rig.Run();

        rig.OpenAndCompleteExitWithPin();

        Assert.Null(rig.Brain.ActiveEventId);
        Assert.Equal(ConfirmationKind.LeaveSetup, rig.LatestOperator().Overlays.Confirmation!.Kind);
    }

    [Fact]
    public async Task Ending_from_the_Guest_Screen_closes_a_pending_END_EVENT_question()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();
        rig.OperatorCommand("EndEvent");

        rig.OpenAndCompleteExitWithPin();

        Assert.Null(rig.LatestOperator().Overlays.Confirmation);
    }

    [Fact]
    public async Task Ending_an_Event_that_is_not_running_does_nothing()
    {
        using var rig = await ActiveRig.CreateAsync();

        rig.Executor.Post(() => rig.Brain.ShowActiveEvent(null));
        rig.Run();

        Assert.Empty(rig.Recorded.Calls);
        Assert.False(rig.WakeLock.IsHeld);
    }

    // The wake lock

    [Fact]
    public async Task The_wake_lock_is_held_only_while_the_Event_runs()
    {
        var wakeLock = new NoOpWakeLock();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Hooks = true, WakeLock = wakeLock });
        Assert.False(rig.Diagnostics().WakeLock.Held);

        rig.StartAndExpectSuccess();
        Assert.True(rig.Diagnostics().WakeLock.Held);
        Assert.True(wakeLock.IsHeld);

        rig.OperatorCommand("EndEvent");
        rig.OperatorCommand("ConfirmEndEvent");
        Assert.False(rig.Diagnostics().WakeLock.Held);
        Assert.False(wakeLock.IsHeld);
    }

    [Fact]
    public async Task A_failed_start_never_leaves_the_wake_lock_held()
    {
        var wakeLock = new NoOpWakeLock();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Hooks = true, WakeLock = wakeLock, GuestConnected = false });
        var start = rig.BeginStart();
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);
        Assert.True(rig.Diagnostics().WakeLock.Held);

        rig.Advance(Brain.GuestScreenReadyTimeout + TimeSpan.FromSeconds(1));

        Assert.Equal(StartFailureCode.GuestScreenNotReady, (await start).Failure);
        Assert.False(rig.Diagnostics().WakeLock.Held);
    }

    // Single screen (D38)

    [Fact]
    public async Task With_one_display_the_Guest_Screen_covers_the_Operator_Screen()
    {
        var onlyDisplay = FakeDisplayAdapter.Create("fake-guest", "Only display", 0, 0, 1920, 1080, isPrimary: true);
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Displays = new FakeDisplayAdapter(onlyDisplay) });

        rig.StartAndExpectSuccess();

        var presentation = rig.LatestOperator();
        Assert.Equal(OperatorSurface.GuestScreenActive, presentation.Surface);
        Assert.Null(presentation.EventList);
        Assert.True(presentation.RunningEvent!.SingleScreen);
    }

    [Fact]
    public async Task With_two_displays_the_Guest_Screen_on_the_second_one_is_not_single_screen()
    {
        using var rig = await ActiveRig.CreateAsync();

        rig.StartAndExpectSuccess();

        Assert.Equal(OperatorSurface.EventList, rig.LatestOperator().Surface);
        Assert.False(rig.LatestOperator().RunningEvent!.SingleScreen);
    }

    [Fact]
    public async Task A_Guest_Screen_bound_to_the_display_that_holds_the_Operator_window_is_single_screen_even_with_two_displays()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions
        {
            Tweak = record => record with { GuestDisplayId = "fake-primary", GuestDisplayName = "Fake primary" },
        });

        rig.StartAndExpectSuccess();

        Assert.Equal(OperatorSurface.GuestScreenActive, rig.LatestOperator().Surface);
        Assert.True(rig.LatestOperator().RunningEvent!.SingleScreen);
        Assert.Equal(["OpenGuestScreen(fake-primary)"], rig.Recorded.Calls);
    }

    [Fact]
    public async Task The_Operator_window_moving_to_the_other_display_makes_the_second_display_the_single_screen_one()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.Recorded.OperatorDisplayId = "fake-guest";

        rig.StartAndExpectSuccess();

        Assert.True(rig.LatestOperator().RunningEvent!.SingleScreen);
    }

    [Fact]
    public async Task Only_the_hold_and_PIN_return_the_operator_from_a_single_screen_Event()
    {
        var onlyDisplay = FakeDisplayAdapter.Create("fake-guest", "Only display", 0, 0, 1920, 1080, isPrimary: true);
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Displays = new FakeDisplayAdapter(onlyDisplay) });
        rig.StartAndExpectSuccess();
        Assert.Equal(OperatorSurface.GuestScreenActive, rig.LatestOperator().Surface);

        rig.OpenAndCompleteExitWithPin();

        Assert.Equal(OperatorSurface.EventList, rig.LatestOperator().Surface);
        Assert.Null(rig.LatestOperator().RunningEvent);
        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], rig.Recorded.Calls);
    }

    // The stand-in used by fixtures and other owners' tests

    [Fact]
    public async Task Replacing_the_running_Event_through_the_stand_in_stops_the_old_Events_timers()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();
        var pending = rig.Clock.PendingCount;
        Assert.True(pending > 0);

        rig.Brain.ShowActiveEvent(null);
        rig.Run();

        Assert.Equal(pending - 1, rig.Clock.PendingCount);
    }

    [Fact]
    public async Task The_stand_in_keeps_the_timers_of_the_same_Event()
    {
        using var rig = await ActiveRig.CreateAsync();
        var state = new ActiveEventState(ActiveRig.SampleEvent(), ActiveRig.FixedStart, FakeDisplayAdapter.DefaultGuest, false);

        rig.Brain.ShowActiveEvent(state);
        rig.Brain.ShowActiveEvent(state);
        rig.Run();

        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
    }
}
