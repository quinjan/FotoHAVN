using FotoHavn.Core.Camera;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Storage;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Events.Active;

/// <summary>Start Event (plan section 8, steps 1 to 9): the happy path, each failing precondition in order, and the rollback of what the earlier steps did.</summary>
public sealed class StartEventTests
{
    private static InterceptingStore NewStore() => new(new InMemoryEventStore());

    /// <summary>After a failed start nothing is held: no Event, no wake lock, no camera, and no Guest Screen.</summary>
    private static void AssertNothingHeld(ActiveRig rig, bool guestScreenWasOpened)
    {
        Assert.Null(rig.Brain.ActiveEventId);
        Assert.False(rig.WakeLock.IsHeld);
        Assert.Equal(CameraState.Idle, rig.Brain.CameraSlotState);
        Assert.Null(rig.Brain.CameraSlotDeviceId);
        Assert.Equal(0, rig.Camera.OpenSessionCount);
        var diagnostics = rig.Diagnostics();
        Assert.False(diagnostics.GuestScreenOpen);
        Assert.False(diagnostics.WakeLock.Held);
        Assert.Equal(PreviewTarget.None, diagnostics.Camera.PreviewTarget);
        Assert.Equal(guestScreenWasOpened ? 1 : 0, rig.Recorded.Calls.Count(call => call == "CloseGuestScreen"));
        Assert.Null(rig.LatestOperator().RunningEvent);
        Assert.Equal(GuestSurface.Start, rig.LatestGuest().Surface);
        Assert.Equal(new StartView(false, false), rig.LatestGuest().Start);
    }

    // The happy path

    [Fact]
    public async Task Start_runs_every_step_and_leaves_the_Event_running_with_everything_held()
    {
        var wakeLock = new NoOpWakeLock();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Hooks = true, WakeLock = wakeLock });
        Assert.False(rig.Diagnostics().WakeLock.Held);

        var result = rig.StartAndExpectSuccess();

        Assert.Equal(StartEventResult.Started, result);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
        Assert.Equal("fake-camera", rig.Brain.ActiveEventCameraDeviceId);
        Assert.Equal(["OpenGuestScreen(fake-guest)"], rig.Recorded.Calls);
        Assert.True(wakeLock.IsHeld);
        var diagnostics = rig.Diagnostics();
        Assert.True(diagnostics.WakeLock.Held);
        Assert.True(diagnostics.GuestScreenOpen);
        Assert.Equal(CameraState.Ready, diagnostics.Camera.State);
        Assert.Equal(PreviewTarget.Guest, diagnostics.Camera.PreviewTarget);
        Assert.Equal(1, rig.Camera.OpenSessionCount);

        var panel = rig.LatestOperator().RunningEvent!;
        Assert.Equal(OperatorSurface.EventList, rig.LatestOperator().Surface);
        Assert.Equal(ActiveRig.EventId, panel.EventId);
        Assert.Equal("Studio open day", panel.Name);
        Assert.Equal(new RunningEventDisplay("Fake guest", RunningDisplayState.Connected), panel.GuestDisplay);
        Assert.Equal(new RunningEventCamera(CameraState.Ready, null), panel.Camera);
        Assert.Equal(RunningEventStep.Start, panel.CurrentStep);
        Assert.Equal(0, panel.CompletedGuestCycles);
        Assert.False(panel.SingleScreen);
        Assert.Null(panel.Assistance);
        var row = Assert.Single(rig.LatestOperator().EventList!.Events);
        Assert.True(row.Running);
        Assert.Equal(new GuestPresentation(GuestSurface.Start, new StartView(true, false), null, GuestOverlays.None, null), rig.LatestGuest());
    }

    [Fact]
    public async Task Start_warms_the_blank_previews_of_every_offered_Template()
    {
        using var rig = await ActiveRig.CreateAsync();

        rig.StartAndExpectSuccess();

        var previews = rig.Previews;
        Assert.True(previews.TryGet("signature-strip.naturale-v1.r0", out _));
        Assert.True(previews.TryGet("panorama-four.naturale-v1.r0", out _));
        Assert.Equal(2, previews.Count);
    }

    [Fact]
    public async Task A_failing_blank_preview_never_stops_the_start()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.FakeCompositor.Failure = new InvalidOperationException("No compositing today.");

        Assert.True(rig.Start().Succeeded);
        Assert.Equal(0, rig.Previews.Count);
    }

    [Fact]
    public async Task A_slow_blank_preview_is_given_up_on_after_five_seconds()
    {
        var release = new TaskCompletionSource();
        var catalog = new FotoHavn.Catalog.CatalogData();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Compositor = new GatedCompositor(new FakeCompositor(catalog), release.Task) });
        var start = rig.BeginStart();
        rig.Pump(() => rig.Brain.CameraSlotState == CameraState.Ready);
        rig.Run();
        Assert.False(start.IsCompleted);

        rig.Advance(Brain.StartPreviewTimeout + TimeSpan.FromSeconds(1));

        Assert.True((await start).Succeeded);
        release.SetResult();
    }

    [Fact]
    public async Task The_panel_counts_the_Events_completed_Guest_Cycles_from_the_store()
    {
        var store = new InMemoryEventStore();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store });
        var cycle = await store.StartGuestCycleAsync(ActiveRig.EventId, TestContext.Current.CancellationToken);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), TestContext.Current.CancellationToken);
        await store.CommitPhotoStripAsync(StorageSamples.StripCommit(cycle.Id, [capture.Id]), TestContext.Current.CancellationToken);
        Assert.Equal(1, (await store.ListEventsAsync(TestContext.Current.CancellationToken)).Single().CompletedGuestCycles);

        rig.StartAndExpectSuccess();

        Assert.Equal(1, rig.LatestOperator().RunningEvent!.CompletedGuestCycles);
        Assert.Equal(1, Assert.Single(rig.LatestOperator().EventList!.Events).CompletedGuestCycles);
    }

    // Step 1

    [Fact]
    public async Task Step_1_another_Event_running_fails_with_eventRunning_and_touches_nothing()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WithOtherEvent = true });
        rig.StartAndExpectSuccess();
        var calls = rig.Recorded.Calls.Count;

        var result = rig.Start(ActiveRig.OtherEventId);

        Assert.Equal(StartEventResult.Failed(StartFailureCode.EventRunning), result);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
        Assert.Equal(calls, rig.Recorded.Calls.Count);
        Assert.True(rig.WakeLock.IsHeld);
        Assert.Equal(CameraState.Ready, rig.Brain.CameraSlotState);
    }

    [Fact]
    public async Task Step_1_a_start_already_in_progress_counts_as_a_running_Event()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { GuestConnected = false });
        var first = rig.BeginStart();
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);

        var second = rig.BeginStart();

        Assert.Equal(StartEventResult.Failed(StartFailureCode.EventRunning), await second);
        Assert.False(first.IsCompleted);
        rig.ConnectGuest();
        Assert.True((await first).Succeeded);
    }

    [Fact]
    public async Task A_start_needs_an_Event_id()
    {
        using var rig = await ActiveRig.CreateAsync();

        Assert.Throws<ArgumentException>(() => { _ = rig.Brain.TryStartEventAsync(string.Empty); });
        Assert.Throws<ArgumentNullException>(() => { _ = rig.Brain.TryStartEventAsync(null!); });
    }

    // Step 2

    [Fact]
    public async Task Step_2_an_unknown_Event_is_an_invalid_setup()
    {
        using var rig = await ActiveRig.CreateAsync();

        Assert.Equal(StartEventResult.Failed(StartFailureCode.InvalidSetup), rig.Start("33333333-3333-4333-8333-333333333333"));
        AssertNothingHeld(rig, guestScreenWasOpened: false);
        Assert.Empty(rig.Recorded.Calls);
    }

    [Fact]
    public async Task Step_2_a_saved_Event_that_breaks_a_field_rule_is_an_invalid_setup()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions
        {
            Tweak = record => record with { PhaseTimers = [new PhaseTimerSetting(PhaseTimerPhase.Keepsake, 0)] },
        });

        Assert.Equal(StartEventResult.Failed(StartFailureCode.InvalidSetup), rig.Start());
        AssertNothingHeld(rig, guestScreenWasOpened: false);
    }

    [Fact]
    public async Task Step_2_an_Event_without_a_PIN_stops_with_pinMissing_after_the_setup_is_valid()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WithPin = false });

        Assert.Equal(StartEventResult.Failed(StartFailureCode.PinMissing), rig.Start());
        AssertNothingHeld(rig, guestScreenWasOpened: false);
        Assert.Empty(rig.Recorded.Calls);
    }

    [Fact]
    public async Task Step_2_an_invalid_setup_wins_over_a_missing_PIN()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions
        {
            WithPin = false,
            Tweak = record => record with { CameraDeviceId = null },
        });

        Assert.Equal(StartFailureCode.InvalidSetup, rig.Start().Failure);
    }

    [Fact]
    public async Task Step_2_storage_that_isnt_open_stops_with_storageNotWritable()
    {
        var store = new InMemoryEventStore();
        store.FailOpen(StorageErrorKind.NotWritable);
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store, Seed = false });

        Assert.Equal(StartEventResult.Failed(StartFailureCode.StorageNotWritable), rig.Start());
        AssertNothingHeld(rig, guestScreenWasOpened: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Step_2_a_store_that_cant_load_the_Event_stops_with_storageNotWritable(bool throws)
    {
        var store = NewStore();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store });
        store.FailGet = !throws;
        store.ThrowOnGet = throws;

        Assert.Equal(StartEventResult.Failed(StartFailureCode.StorageNotWritable), rig.Start());
        AssertNothingHeld(rig, guestScreenWasOpened: false);
    }

    // Step 3

    [Fact]
    public async Task Step_3_a_guest_display_that_isnt_connected_stops_the_start_and_no_other_display_is_used()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.Displays.Disconnect("fake-guest");
        rig.Run();

        Assert.Equal(StartEventResult.Failed(StartFailureCode.GuestDisplayMissing), rig.Start());

        AssertNothingHeld(rig, guestScreenWasOpened: false);
        Assert.Empty(rig.Recorded.OpenedGuestScreens);
        Assert.Single(rig.Displays.GetDisplays());
    }

    [Fact]
    public async Task Step_3_matches_the_display_by_its_id_only_never_by_name_or_position()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.Displays.SetDisplays(
        [
            FakeDisplayAdapter.DefaultPrimary,
            FakeDisplayAdapter.Create("fake-guest-on-another-port", "Fake guest", 1920, 0, 1920, 1080),
        ]);
        rig.Run();

        Assert.Equal(StartFailureCode.GuestDisplayMissing, rig.Start().Failure);
        Assert.Empty(rig.Recorded.OpenedGuestScreens);
    }

    [Fact]
    public async Task Step_3_is_checked_before_the_camera_is_touched()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Tweak = record => record with { CameraDeviceId = "no-such-camera" } });
        rig.Displays.Disconnect("fake-guest");
        rig.Run();

        Assert.Equal(StartFailureCode.GuestDisplayMissing, rig.Start().Failure);
        Assert.Equal(0, rig.Camera.OpenSessionCount);
    }

    // Step 4

    [Fact]
    public async Task Step_4_a_camera_that_isnt_there_stops_the_start_with_its_reason_and_no_other_camera_is_opened()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Tweak = record => record with { CameraDeviceId = "removed-camera" } });

        var result = rig.Start();

        Assert.Equal(StartEventResult.Failed(StartFailureCode.CameraNotEligible, CameraFailureKind.NotFound), result);
        AssertNothingHeld(rig, guestScreenWasOpened: false);
        Assert.Empty(rig.Recorded.Calls);
    }

    [Fact]
    public async Task Step_4_a_camera_another_app_holds_is_not_eligible()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.Camera.InUseByAnotherApp("fake-camera");

        Assert.Equal(StartEventResult.Failed(StartFailureCode.CameraNotEligible, CameraFailureKind.InUseByAnotherApp), rig.Start());
        AssertNothingHeld(rig, guestScreenWasOpened: false);
    }

    [Fact]
    public async Task Step_4_a_camera_that_opens_but_never_delivers_a_fresh_frame_is_not_eligible()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.Camera.Stall();

        var result = rig.Start();

        Assert.Equal(StartEventResult.Failed(StartFailureCode.CameraNotEligible, CameraFailureKind.Stale), result);
        AssertNothingHeld(rig, guestScreenWasOpened: false);
    }

    [Fact]
    public async Task Step_4_a_camera_that_never_finishes_opening_times_out_after_five_seconds()
    {
        var adapter = new ScriptedCameraAdapter { OpenBehavior = (_, _) => new TaskCompletionSource<ICameraSession>().Task };
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { CameraPort = adapter, Tweak = record => record with { CameraDeviceId = "scripted" } });
        var start = rig.BeginStart();
        rig.Advance(TimeSpan.FromSeconds(4.9));
        Assert.False(start.IsCompleted);

        rig.Advance(TimeSpan.FromSeconds(0.2));

        Assert.Equal(StartEventResult.Failed(StartFailureCode.CameraNotEligible, CameraFailureKind.Stale), await start);
        Assert.Equal(CameraState.Idle, rig.Brain.CameraSlotState);
        Assert.False(rig.WakeLock.IsHeld);
    }

    [Fact]
    public async Task Step_4_a_camera_someone_else_released_meanwhile_is_not_eligible()
    {
        using var rig = await ActiveRig.CreateAsync();
        var start = rig.BeginStart();
        rig.Executor.Post(rig.Brain.ReleaseCamera);
        rig.Run();

        Assert.Equal(StartEventResult.Failed(StartFailureCode.CameraNotEligible, CameraFailureKind.Unknown), await start);
    }

    [Fact]
    public async Task Step_4_replaces_a_camera_the_setup_preview_had_open()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.Executor.Post(() => rig.Brain.OpenCameraFor("fake-camera", PreviewTarget.Operator));
        rig.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(CameraState.Ready, rig.Brain.CameraSlotState);

        rig.StartAndExpectSuccess();

        Assert.Equal(PreviewTarget.Guest, rig.Diagnostics().Camera.PreviewTarget);
        Assert.Equal(1, rig.Camera.OpenSessionCount);
    }

    // Step 5

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Step_5_storage_that_stops_answering_after_the_camera_is_ready_rolls_the_camera_back(bool throws)
    {
        var store = NewStore();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store, Hooks = true });
        var start = rig.BeginStart();
        rig.Pump(() => rig.Brain.CameraSlotState == CameraState.Checking);
        store.FailList = !throws;
        store.ThrowOnList = throws;

        rig.Pump(() => start.IsCompleted);

        Assert.Equal(StartEventResult.Failed(StartFailureCode.StorageNotWritable), await start);
        AssertNothingHeld(rig, guestScreenWasOpened: false);
    }

    [Fact]
    public async Task Step_5_is_checked_before_the_previews_and_the_wake_lock()
    {
        var store = NewStore();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store });
        store.FailList = true;

        Assert.Equal(StartFailureCode.StorageNotWritable, rig.Start().Failure);
        Assert.Empty(((FakeCompositor)rig.Dependencies.Compositor).Requests);
        Assert.False(rig.WakeLock.IsHeld);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Step_5_a_store_that_cant_be_written_stops_the_start_before_the_list_is_read(bool throws)
    {
        var store = NewStore();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store });
        var listsBefore = store.ListCalls;
        store.FailProbe = !throws;
        store.ThrowOnProbe = throws;

        Assert.Equal(StartEventResult.Failed(StartFailureCode.StorageNotWritable), rig.Start());

        Assert.Equal(1, store.ProbeCalls);
        Assert.Equal(listsBefore, store.ListCalls);
        Assert.Empty(((FakeCompositor)rig.Dependencies.Compositor).Requests);
        AssertNothingHeld(rig, guestScreenWasOpened: false);
    }

    [Fact]
    public async Task Step_5_probes_the_store_once_per_start_and_still_counts_the_completed_Guest_Cycles()
    {
        var store = NewStore();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store });

        rig.StartAndExpectSuccess();

        Assert.Equal(1, store.ProbeCalls);
        Assert.NotNull(rig.LatestOperator().RunningEvent);
    }

    // Step 8

    [Fact]
    public async Task Step_8_a_Guest_Screen_that_never_finishes_its_handshake_rolls_everything_back()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { GuestConnected = false, Hooks = true });
        var start = rig.BeginStart();
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);
        Assert.True(rig.WakeLock.IsHeld);
        Assert.False(start.IsCompleted);

        rig.Advance(Brain.GuestScreenReadyTimeout + TimeSpan.FromSeconds(1));

        Assert.Equal(StartEventResult.Failed(StartFailureCode.GuestScreenNotReady), await start);
        AssertNothingHeldAfterGuestScreen(rig);
    }

    private static void AssertNothingHeldAfterGuestScreen(ActiveRig rig)
    {
        Assert.Null(rig.Brain.ActiveEventId);
        Assert.False(rig.WakeLock.IsHeld);
        Assert.Equal(CameraState.Idle, rig.Brain.CameraSlotState);
        Assert.Equal(0, rig.Camera.OpenSessionCount);
        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], rig.Recorded.Calls);
        Assert.False(rig.Diagnostics().GuestScreenOpen);
    }

    [Fact]
    public async Task Step_8_a_Guest_Screen_that_connects_after_it_opened_completes_the_start()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { GuestConnected = false });
        var start = rig.BeginStart();
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);
        Assert.False(start.IsCompleted);

        rig.ConnectGuest();

        Assert.True((await start).Succeeded);
        Assert.Equal(new StartView(true, false), rig.LatestGuest().Start);
    }

    [Fact]
    public async Task Step_8_a_second_handshake_of_the_Guest_UI_after_the_start_does_not_restart_anything()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();
        var calls = rig.Recorded.Calls.ToArray();

        var recovered = rig.Handshake(Role.Guest);

        Assert.Equal(calls, rig.Recorded.Calls);
        Assert.Empty(recovered.Raw);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task Step_8_a_guest_handshake_with_no_start_in_progress_changes_nothing()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { GuestConnected = false });

        rig.Handshake(Role.Guest);

        Assert.Empty(rig.Recorded.Calls);
        Assert.Null(rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task Step_8_a_display_that_vanishes_while_the_camera_was_opening_stops_the_start()
    {
        using var rig = await ActiveRig.CreateAsync();
        var start = rig.BeginStart();
        rig.Pump(() => rig.Brain.CameraSlotState == CameraState.Checking);
        rig.Displays.Disconnect("fake-guest");
        rig.Run();
        rig.Pump(() => start.IsCompleted);

        Assert.Equal(StartEventResult.Failed(StartFailureCode.GuestDisplayMissing), await start);
        Assert.Empty(rig.Recorded.OpenedGuestScreens);
        AssertNothingHeld(rig, guestScreenWasOpened: false);
    }

    [Fact]
    public async Task Step_8_a_host_that_cant_open_the_Guest_Screen_fails_the_start_and_releases_what_was_taken()
    {
        var host = new ThrowingHost();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Host = host });

        Assert.Equal(StartEventResult.Failed(StartFailureCode.GuestScreenNotReady), rig.Start());

        Assert.Null(rig.Brain.ActiveEventId);
        Assert.False(rig.WakeLock.IsHeld);
        Assert.Equal(CameraState.Idle, rig.Brain.CameraSlotState);
        Assert.Equal(0, host.Closed);
    }

    [Fact]
    public async Task A_Guest_Screen_the_host_could_not_open_is_not_closed_again_on_rollback()
    {
        var host = new ThrowingHost { Throw = false };
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Host = host, GuestConnected = false });
        var start = rig.BeginStart();
        rig.Pump(() => host.Opened == 1);
        rig.Advance(Brain.GuestScreenReadyTimeout + TimeSpan.FromSeconds(1));

        Assert.Equal(StartFailureCode.GuestScreenNotReady, (await start).Failure);
        Assert.Equal(1, host.Closed);
    }

    // The StartEvent command

    [Fact]
    public async Task StartEvent_from_the_list_starts_the_Event_and_accepts_the_command()
    {
        using var rig = await ActiveRig.CreateAsync();

        var result = rig.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.EventId}}"}""");
        rig.Pump(() => rig.Brain.ActiveEventId is not null);

        Assert.Equal(CommandResultMessage.Accepted(result.Id), result);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
        Assert.NotNull(rig.LatestOperator().RunningEvent);
    }

    [Fact]
    public async Task StartEvent_whose_precondition_fails_is_accepted_and_leaves_only_the_setup_preview_held()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WithPin = false });

        var result = rig.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.EventId}}"}""");
        rig.Pump(() => rig.LatestOperator().Surface == OperatorSurface.EventSetup);

        Assert.Equal(CommandResultMessage.Accepted(result.Id), result);
        Assert.Equal(new EventSetupStartFailure(StartFailureCode.PinMissing, null), rig.LatestOperator().EventSetup!.StartFailure);
        Assert.Null(rig.Brain.ActiveEventId);
        Assert.False(rig.WakeLock.IsHeld);
        Assert.False(rig.Diagnostics().GuestScreenOpen);
        Assert.Empty(rig.Recorded.Calls);
        Assert.Null(rig.LatestOperator().RunningEvent);
        Assert.Equal(new StartView(false, false), rig.LatestGuest().Start);
        // Event setup's own preview of the bound camera, for the Operator Screen: not the Event's.
        Assert.Equal(PreviewTarget.Operator, rig.Diagnostics().Camera.PreviewTarget);
    }

    [Fact]
    public async Task StartEvent_is_rejected_eventRunning_for_every_row_while_an_Event_runs()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WithOtherEvent = true });
        rig.StartAndExpectSuccess();

        var running = rig.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.EventId}}"}""");
        var other = rig.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.OtherEventId}}"}""");

        Assert.Equal(CommandResultMessage.Rejected(running.Id, RejectReason.EventRunning), running);
        Assert.Equal(CommandResultMessage.Rejected(other.Id, RejectReason.EventRunning), other);
    }

    [Fact]
    public async Task StartEvent_for_an_unknown_Event_is_not_available_and_so_is_StartEvent_before_storage_opens()
    {
        using var rig = await ActiveRig.CreateAsync();
        var unknown = rig.OperatorCommand("StartEvent", """{"eventId":"33333333-3333-4333-8333-333333333333"}""");
        Assert.Equal(CommandResultMessage.Rejected(unknown.Id, RejectReason.NotAvailable), unknown);

        var failing = new InMemoryEventStore();
        failing.FailOpen(StorageErrorKind.DiskFull);
        using var broken = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = failing, Seed = false });
        var inError = broken.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.EventId}}"}""");
        Assert.Equal(CommandResultMessage.Rejected(inError.Id, RejectReason.NotAvailable), inError);
    }

    [Fact]
    public async Task A_second_StartEvent_while_the_first_is_still_starting_is_rejected_eventRunning()
    {
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { GuestConnected = false, WithOtherEvent = true });
        var first = rig.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.EventId}}"}""");
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);

        var second = rig.OperatorCommand("StartEvent", $$"""{"eventId":"{{ActiveRig.OtherEventId}}"}""");

        Assert.Equal(CommandResultMessage.Accepted(first.Id), first);
        Assert.Equal(CommandResultMessage.Rejected(second.Id, RejectReason.EventRunning), second);
    }

    // A host that can't open the Guest Screen

    private sealed class GatedCompositor(FakeCompositor inner, Task gate) : FotoHavn.Core.Catalog.ICompositor
    {
        public async Task<FotoHavn.Core.Catalog.CompositionResult> ComposeAsync(FotoHavn.Core.Catalog.CompositionRequest request, CancellationToken ct)
        {
            await gate.ConfigureAwait(false);
            return await inner.ComposeAsync(request, ct).ConfigureAwait(false);
        }

        public Task<FotoHavn.Core.Catalog.CompositionResult> ComposeLookThumbnailAsync(FotoHavn.Core.Catalog.CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct) =>
            inner.ComposeLookThumbnailAsync(photo, aspectRatio, lookId, width, ct);
    }

    private sealed class ThrowingHost : IHostServices
    {
        public bool Throw { get; init; } = true;

        public int Opened { get; private set; }

        public int Closed { get; private set; }

        public void OpenGuestScreen(DisplayInfo display)
        {
            Opened++;
            if (Throw)
            {
                throw new InvalidOperationException("No window today.");
            }
        }

        public void CloseGuestScreen() => Closed++;

        public void OpenFolder(string path)
        {
        }

        public void ShowIdentifyOverlays(IReadOnlyList<IdentifyOverlaySpec> overlays)
        {
        }

        public void HideIdentifyOverlays()
        {
        }

        public string? GetOperatorDisplayId() => "fake-primary";
    }
}
