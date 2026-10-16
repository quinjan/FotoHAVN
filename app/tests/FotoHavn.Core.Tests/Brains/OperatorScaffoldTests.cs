using FotoHavn.Core.Catalog;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Storage;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Brains;

/// <summary>
/// The M6 wave A scaffolding: the operator and guest presentations the waves build on, the shared overlay and host-request
/// helpers, the camera slot, and the diagnostics and clock hooks. The commands themselves are stubs until wave B.
/// </summary>
public sealed class OperatorScaffoldTests
{
    private const string RunningId = "11111111-1111-4111-8111-111111111111";
    private const string OtherId = "22222222-2222-4222-8222-222222222222";

    private static readonly DisplayInfo GuestDisplay = FakeDisplayAdapter.DefaultGuest;

    private static ActiveEventState Running(bool singleScreen = false) =>
        new(StorageSamples.Event(RunningId, "Studio open day") with { GuestDisplayId = GuestDisplay.Id, GuestDisplayName = GuestDisplay.Name, CameraDeviceId = "fake-camera" },
            new DateTimeOffset(2026, 10, 13, 1, 30, 0, TimeSpan.Zero),
            GuestDisplay,
            singleScreen);

    private static async Task<InMemoryEventStore> StoreWithTwoEventsAsync()
    {
        var store = new InMemoryEventStore();
        await store.OpenAsync(CancellationToken.None);
        await store.CreateEventAsync(StorageSamples.Event(RunningId, "Studio open day", new DateOnly(2026, 10, 13)), CancellationToken.None);
        await store.CreateEventAsync(StorageSamples.Event(OtherId, "Harbour gala", new DateOnly(2026, 6, 12)), CancellationToken.None);
        return store;
    }

    private static OperatorPresentation Latest(TestClient client) => client.Messages
        .Select(message => message switch
        {
            OperatorPresentationMessage update => update.Presentation,
            OperatorWelcomeMessage welcome => welcome.Presentation,
            _ => null,
        })
        .Last(presentation => presentation is not null)!;

    private static DiagnosticsResult Diagnostics(BrainHarness harness, TestClient client)
    {
        var id = client.Test(TestHookNames.GetDiagnostics);
        harness.Run();
        var result = Assert.Single(client.Messages.OfType<TestResultMessage>(), message => message.Id == id);
        Assert.True(result.Ok);
        return ProtocolJson.Deserialize<DiagnosticsResult>(result.Result!.Value);
    }

    // The Event list

    [Fact]
    public async Task The_Event_list_rows_carry_their_actions_and_every_row_can_open_its_folder()
    {
        using var harness = new BrainHarness(store: await StoreWithTwoEventsAsync());
        var client = harness.Connect(Role.Operator);
        client.Hello();
        harness.Run();

        var presentation = client.Single<OperatorWelcomeMessage>().Presentation;
        var list = presentation.EventList!;

        Assert.True(list.NewEventAvailable);
        Assert.Equal([RunningId, OtherId], list.Events.Select(row => row.Id));
        Assert.All(list.Events, row =>
        {
            Assert.False(row.Running);
            Assert.Equal(new EventRowActions(true, true, true, true, true), row.Actions);
            Assert.Null(row.StartDisabledReason);
        });
        Assert.Null(presentation.RunningEvent);
        Assert.Null(presentation.EventSetup);
        Assert.Equal(OperatorOverlays.None, presentation.Overlays);
    }

    [Fact]
    public async Task While_an_Event_runs_its_row_is_locked_and_Start_is_disabled_on_every_row()
    {
        using var harness = new BrainHarness(store: await StoreWithTwoEventsAsync());
        var client = harness.Handshake(Role.Operator);

        harness.Brain.ShowActiveEvent(Running());
        harness.Run();

        var rows = Latest(client).EventList!.Events;
        var running = Assert.Single(rows, row => row.Id == RunningId);
        var other = Assert.Single(rows, row => row.Id == OtherId);
        Assert.True(running.Running);
        Assert.Equal(new EventRowActions(false, false, false, true, true), running.Actions);
        Assert.False(other.Running);
        Assert.Equal(new EventRowActions(false, true, true, true, true), other.Actions);
        Assert.All(rows, row => Assert.Equal(StartDisabledReason.EventRunning, row.StartDisabledReason));
    }

    [Fact]
    public void The_running_Event_is_exposed_to_the_waves_through_two_accessors()
    {
        using var harness = new BrainHarness();
        Assert.Null(harness.Brain.ActiveEventId);
        Assert.Null(harness.Brain.ActiveEventCameraDeviceId);

        harness.Brain.ShowActiveEvent(Running());
        harness.Run();

        Assert.Equal(RunningId, harness.Brain.ActiveEventId);
        Assert.Equal("fake-camera", harness.Brain.ActiveEventCameraDeviceId);

        harness.Brain.ShowActiveEvent(null);
        harness.Run();
        Assert.Null(harness.Brain.ActiveEventId);
    }

    // The running-Event panel

    [Fact]
    public void The_running_Event_panel_shows_the_facts_with_the_display_connected()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        harness.Brain.ShowActiveEvent(Running());
        harness.Run();

        var presentation = Latest(client);
        Assert.Equal(OperatorSurface.EventList, presentation.Surface);
        Assert.Equal(
            new RunningEventPanel(
                RunningId,
                "Studio open day",
                new RunningEventDisplay(GuestDisplay.Name, RunningDisplayState.Connected),
                new RunningEventCamera(CameraState.Idle, null),
                RunningEventStep.Start,
                0,
                new DateTimeOffset(2026, 10, 13, 1, 30, 0, TimeSpan.Zero),
                null,
                false),
            presentation.RunningEvent);
    }

    [Fact]
    public void The_panel_names_a_disconnected_display_and_carries_assistance_and_the_cycle_count()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);
        var state = Running();
        state.CompletedGuestCycles = 12;
        state.Step = RunningEventStep.OperatorAssistance;
        state.Assistance = new RunningEventAssistance(AssistanceReason.GuestDisplay);
        harness.Brain.ShowActiveEvent(state);
        harness.Run();

        harness.Displays.Disconnect(GuestDisplay.Id);
        harness.Run();

        var panel = Latest(client).RunningEvent!;
        Assert.Equal(RunningDisplayState.Disconnected, panel.GuestDisplay.State);
        Assert.Equal(12, panel.CompletedGuestCycles);
        Assert.Equal(RunningEventStep.OperatorAssistance, panel.CurrentStep);
        Assert.Equal(new RunningEventAssistance(AssistanceReason.GuestDisplay), panel.Assistance);
    }

    [Fact]
    public void A_single_screen_Event_shows_the_Guest_Screen_active_placeholder_with_the_panel_still_present()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        harness.Brain.ShowActiveEvent(Running(singleScreen: true));
        harness.Run();

        var presentation = Latest(client);
        Assert.Equal(OperatorSurface.GuestScreenActive, presentation.Surface);
        Assert.Null(presentation.EventList);
        Assert.True(presentation.RunningEvent!.SingleScreen);

        harness.Brain.ShowActiveEvent(null);
        harness.Run();
        Assert.Equal(OperatorSurface.EventList, Latest(client).Surface);
    }

    // The surfaces

    [Fact]
    public void Event_setup_shows_the_Event_list_until_wave_B_renders_a_draft()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Operator);
        client.Hello();
        harness.Brain.ShowOperatorSurface(OperatorSurface.EventSetup);
        harness.Run();

        var presentation = client.Single<OperatorWelcomeMessage>().Presentation;
        Assert.Equal(OperatorSurface.EventList, presentation.Surface);
        Assert.Null(presentation.EventSetup);
    }

    [Fact]
    public void The_fatal_surface_overrides_the_navigated_one_and_drops_the_panel()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);
        harness.Brain.ShowOperatorSurface(OperatorSurface.EventSetup);
        harness.Brain.ShowActiveEvent(Running());
        harness.Run();

        harness.Brain.ShowFatal("FOTOHAVN stopped.");
        harness.Run();

        var fatal = Latest(client);
        Assert.Equal(OperatorSurface.Fatal, fatal.Surface);
        Assert.Null(fatal.RunningEvent);
    }

    // Overlays

    [Fact]
    public void A_confirmation_is_one_slot_that_the_owner_sets_and_clears()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);
        var delete = new ConfirmationOverlay(ConfirmationKind.DeleteEvent, "Harbour gala", new ConfirmationCounts(3, 2, 9), null);

        harness.Executor.Post(() => harness.Brain.SetConfirmation(delete));
        harness.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        harness.Run();
        Assert.Equal(delete, Latest(client).Overlays.Confirmation);

        var end = new ConfirmationOverlay(ConfirmationKind.EndEvent, null, null, null);
        harness.Executor.Post(() => harness.Brain.SetConfirmation(end));
        harness.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        harness.Run();
        Assert.Equal(end, Latest(client).Overlays.Confirmation);

        harness.Executor.Post(() => harness.Brain.SetConfirmation(null));
        harness.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        harness.Run();
        Assert.Null(Latest(client).Overlays.Confirmation);
    }

    [Fact]
    public void A_notice_stays_until_DismissNotice()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);
        var notAvailable = client.Command("DismissNotice");
        harness.Run();
        Assert.Equal(CommandResultMessage.Rejected(notAvailable, RejectReason.NotAvailable), client.Messages[^1]);

        harness.Executor.Post(() => harness.Brain.ShowNotice(OperatorNoticeCode.SaveFailed));
        harness.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        harness.Run();
        Assert.Equal(new OperatorNoticeOverlay(OperatorNoticeCode.SaveFailed), Latest(client).Overlays.Notice);

        var dismissed = client.Command("DismissNotice");
        harness.Run();

        Assert.Contains(CommandResultMessage.Accepted(dismissed), client.Messages);
        Assert.Null(Latest(client).Overlays.Notice);
    }

    // Identify displays

    [Fact]
    public void IdentifyDisplays_is_only_available_on_the_Event_setup_surface()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        var id = client.Command("IdentifyDisplays");
        harness.Run();

        Assert.Equal(CommandResultMessage.Rejected(id, RejectReason.NotAvailable), client.Messages[^1]);
        Assert.Empty(harness.Host.ShownIdentifyOverlays);
    }

    [Fact]
    public void IdentifyDisplays_numbers_every_display_for_three_seconds_then_the_brain_hides_them()
    {
        using var harness = new BrainHarness(hooks: true);
        var client = harness.Handshake(Role.Operator);
        harness.Brain.ShowOperatorSurface(OperatorSurface.EventSetup);
        harness.Run();

        var id = client.Command("IdentifyDisplays");
        harness.Run();

        Assert.Equal(CommandResultMessage.Accepted(id), client.Messages.OfType<CommandResultMessage>().Last());
        var shown = Assert.Single(harness.Host.ShownIdentifyOverlays);
        Assert.Equal([("fake-primary", 1), ("fake-guest", 2)], shown.Select(spec => (spec.Display.Id, spec.Number)));
        Assert.Equal(["Fake primary", "Fake guest"], shown.Select(spec => spec.Name));
        Assert.Equal(
            [new IdentifyDisplayEntry("fake-primary", 1, "Fake primary"), new IdentifyDisplayEntry("fake-guest", 2, "Fake guest")],
            Latest(client).Overlays.IdentifyDisplays!.Displays);
        Assert.Equal([true, true], Diagnostics(harness, client).IdentifyOverlays.Select(overlay => overlay.Visible));

        harness.Clock.Advance(TimeSpan.FromSeconds(2.9));
        harness.Run();
        Assert.Equal(0, harness.Host.HideIdentifyCount);
        Assert.NotNull(Latest(client).Overlays.IdentifyDisplays);

        harness.Clock.Advance(TimeSpan.FromSeconds(0.1));
        harness.Run();
        Assert.Equal(1, harness.Host.HideIdentifyCount);
        Assert.Null(Latest(client).Overlays.IdentifyDisplays);
        Assert.Equal(
            [new IdentifyOverlayDiagnostics("fake-primary", 1, false), new IdentifyOverlayDiagnostics("fake-guest", 2, false)],
            Diagnostics(harness, client).IdentifyOverlays);
    }

    [Fact]
    public void A_second_IdentifyDisplays_restarts_the_three_seconds_and_hiding_twice_asks_the_host_once()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);
        harness.Brain.ShowOperatorSurface(OperatorSurface.EventSetup);
        harness.Run();
        client.Command("IdentifyDisplays");
        harness.Run();
        harness.Clock.Advance(TimeSpan.FromSeconds(2));
        harness.Run();

        client.Command("IdentifyDisplays");
        harness.Run();
        harness.Clock.Advance(TimeSpan.FromSeconds(2));
        harness.Run();
        Assert.Equal(0, harness.Host.HideIdentifyCount);

        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        harness.Run();
        harness.Executor.Post(harness.Brain.HideIdentifyDisplays);
        harness.Run();

        Assert.Equal(2, harness.Host.ShownIdentifyOverlays.Count);
        Assert.Equal(1, harness.Host.HideIdentifyCount);
    }

    [Fact]
    public void Identify_numbers_follow_the_physical_layout_not_the_list_order()
    {
        var displays = new FakeDisplayAdapter(
            FakeDisplayAdapter.Create("right", "Right", 1920, 0, 1920, 1080),
            FakeDisplayAdapter.Create("left", "Left", 0, 0, 1920, 1080, isPrimary: true));
        using var harness = new BrainHarness(displays: displays);
        var client = harness.Handshake(Role.Operator);
        harness.Brain.ShowOperatorSurface(OperatorSurface.EventSetup);
        harness.Run();

        client.Command("IdentifyDisplays");
        harness.Run();

        Assert.Equal([("left", 1), ("right", 2)], Assert.Single(harness.Host.ShownIdentifyOverlays).Select(spec => (spec.Display.Id, spec.Number)));
    }

    // Open folder

    [Fact]
    public void Open_folder_goes_to_the_host_and_the_last_ten_requests_are_in_the_diagnostics()
    {
        using var harness = new BrainHarness(hooks: true);
        var client = harness.Handshake(Role.Operator);

        for (var index = 1; index <= 12; index++)
        {
            var path = $@"C:\FOTOHAVN\Data\Events\event-{index}";
            harness.Executor.Post(() => harness.Brain.RequestOpenFolder(path));
            harness.Run();
            harness.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Equal(12, harness.Host.OpenedFolders.Count);
        var requests = Diagnostics(harness, client).OpenFolderRequests;
        Assert.Equal(10, requests.Count);
        Assert.Equal(@"C:\FOTOHAVN\Data\Events\event-3", requests[0].Path);
        Assert.Equal(@"C:\FOTOHAVN\Data\Events\event-12", requests[^1].Path);
        Assert.Equal(harness.Clock.UtcNow - TimeSpan.FromSeconds(1), requests[^1].At);
    }

    // The wake lock and the clock

    [Fact]
    public void The_diagnostics_report_the_wake_lock_and_the_clock_scale()
    {
        var wakeLock = new NoOpWakeLock();
        using var harness = new BrainHarness(hooks: true, wakeLock: wakeLock);
        var client = harness.Handshake(Role.Operator);

        var idle = Diagnostics(harness, client);
        Assert.False(idle.WakeLock.Held);
        Assert.Equal(1, idle.ClockScale);
        Assert.Empty(idle.OpenFolderRequests);
        Assert.Empty(idle.IdentifyOverlays);

        wakeLock.Acquire("because");
        var held = Diagnostics(harness, client).WakeLock;
        Assert.True(held.Held);
        Assert.Equal("because", held.Reason);
        Assert.Null(idle.WakeLock.Reason);
    }

    [Fact]
    public void SetClockScale_speeds_up_a_scalable_clock_and_the_diagnostics_say_so()
    {
        var clock = new FakeClock();
        var scaled = new ScaledClock(clock);
        using var harness = new BrainHarness(hooks: true, clock: clock, brainClock: scaled);
        var client = harness.Handshake(Role.Operator);

        var id = client.Test(TestHookNames.SetClockScale, """{"factor":10}""");
        harness.Run();

        Assert.Equal(Wire.Json(TestResultMessage.Success(id, EmptyHookResult.Instance)), client.Raw[^1]);
        Assert.Equal(10, scaled.Factor);
        Assert.Equal(10, Diagnostics(harness, client).ClockScale);
    }

    [Theory]
    [InlineData("0.5")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("100.5")]
    [InlineData("1000")]
    public void SetClockScale_outside_one_to_a_hundred_is_invalid(string factor)
    {
        var clock = new FakeClock();
        var scaled = new ScaledClock(clock);
        using var harness = new BrainHarness(hooks: true, clock: clock, brainClock: scaled);
        var client = harness.Handshake(Role.Operator);

        var id = client.Test(TestHookNames.SetClockScale, $$"""{"factor":{{factor}}}""");
        harness.Run();

        Assert.Equal(TestResultMessage.Failure(id, TestHookError.InvalidArgs), client.Messages[^1]);
        Assert.Equal(1, scaled.Factor);
    }

    [Fact]
    public void SetClockScale_needs_a_scalable_clock_and_its_arguments()
    {
        using var harness = new BrainHarness(hooks: true);
        var client = harness.Handshake(Role.Operator);

        var unsupported = client.Test(TestHookNames.SetClockScale, """{"factor":10}""");
        var missing = client.Test(TestHookNames.SetClockScale, """{"factor":"fast"}""");
        harness.Run();

        Assert.Equal(
            [TestResultMessage.Failure(unsupported, TestHookError.NotSupported), TestResultMessage.Failure(missing, TestHookError.InvalidArgs)],
            client.Messages.OfType<TestResultMessage>());
    }

    [Fact]
    public void A_scaled_brain_clock_makes_brain_timers_run_faster()
    {
        var clock = new FakeClock();
        var scaled = new ScaledClock(clock);
        using var harness = new BrainHarness(hooks: true, clock: clock, brainClock: scaled);
        var client = harness.Handshake(Role.Operator);
        harness.Brain.ShowOperatorSurface(OperatorSurface.EventSetup);
        client.Test(TestHookNames.SetClockScale, """{"factor":10}""");
        harness.Run();
        client.Command("IdentifyDisplays");
        harness.Run();

        clock.Advance(TimeSpan.FromSeconds(0.3));
        harness.Run();

        Assert.Equal(1, harness.Host.HideIdentifyCount);
    }

    // The Start Event entry point

    [Fact]
    public void A_start_result_says_what_failed_and_why()
    {
        Assert.False(StartEventResult.Failed(StartFailureCode.InvalidSetup).Succeeded);
        Assert.True(StartEventResult.Started.Succeeded);
        Assert.Null(StartEventResult.Started.CameraReason);
        Assert.Equal(CameraFailureKind.NotFound, StartEventResult.Failed(StartFailureCode.CameraNotEligible, CameraFailureKind.NotFound).CameraReason);
    }

    [Fact]
    public void The_Event_setup_entry_for_a_failed_start_exists_for_wave_B1b_to_call()
    {
        using var harness = new BrainHarness();

        harness.Executor.Post(() => harness.Brain.OpenEventSetupWithStartFailure(RunningId, new EventSetupStartFailure(StartFailureCode.GuestDisplayMissing, null)));
        harness.Run();

        Assert.Null(harness.Brain.ActiveEventId);
    }

    // The guest presentation

    [Fact]
    public void The_guest_shows_the_Start_placeholder_with_nothing_to_step_inside_to_until_wave_B_and_M8()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Guest);
        client.Hello();
        harness.Run();

        var welcome = client.Single<GuestWelcomeMessage>();

        Assert.Equal(
            new GuestPresentation(GuestSurface.Start, new StartView(false, false), null, GuestOverlays.None, null, null, null, null, null, null, null, null, null),
            welcome.Presentation);
    }

    // Blank previews

    [Fact]
    public async Task A_blank_preview_is_composed_off_the_executor_and_cached_under_its_shared_key()
    {
        using var harness = new BrainHarness();

        var key = await harness.Brain.ComposeBlankPreviewAsync("panorama-four", TestContext.Current.CancellationToken);

        Assert.NotNull(key);
        Assert.Equal("panorama-four.naturale-v1.r0", key);
        Assert.Equal(key, harness.Brain.BlankPreviewKeyFor("panorama-four"));
        Assert.True(harness.Previews.TryGet(key, out var png));
        Assert.False(png.IsEmpty);
        var request = Assert.Single(harness.FakeCompositor.Requests);
        Assert.Equal(("panorama-four", "naturale-v1"), (request.TemplateId, request.LookId));
        Assert.All(request.Photos, Assert.Null);
        Assert.Equal(4, request.Photos.Count);
    }

    [Fact]
    public async Task A_blank_preview_of_an_unknown_template_or_a_failing_compositor_is_null_and_caches_nothing()
    {
        using var harness = new BrainHarness();

        Assert.Null(await harness.Brain.ComposeBlankPreviewAsync("no-such-template", TestContext.Current.CancellationToken));
        Assert.Null(await harness.Brain.ComposeBlankPreviewAsync(string.Empty, TestContext.Current.CancellationToken));

        harness.FakeCompositor.Failure = new InvalidOperationException("No compositing today.");
        Assert.Null(await harness.Brain.ComposeBlankPreviewAsync("signature-strip", TestContext.Current.CancellationToken));
        Assert.Equal(0, harness.Previews.Count);
    }

    [Fact]
    public async Task A_blank_preview_with_an_unusable_image_is_not_cached()
    {
        using var harness = new BrainHarness(compositor: new EmptyImageCompositor());

        Assert.Null(await harness.Brain.ComposeBlankPreviewAsync("signature-strip", TestContext.Current.CancellationToken));
        Assert.Equal(0, harness.Previews.Count);
    }

    // The camera slot

    [Fact]
    public void The_camera_slot_opens_a_camera_for_a_target_and_reports_its_state_to_the_observer()
    {
        using var rig = new CameraRig();
        var changes = new List<(CameraState State, CameraFailureKind? Reason)>();
        rig.Harness.Brain.CameraStateChanged = () => changes.Add((rig.Harness.Brain.CameraSlotState, rig.Harness.Brain.CameraSlotReason));

        rig.Harness.Executor.Post(() => rig.Harness.Brain.OpenCameraFor("fake-camera", PreviewTarget.Guest));
        rig.Run(TimeSpan.FromMilliseconds(500));

        Assert.Equal(CameraState.Ready, rig.Harness.Brain.CameraSlotState);
        Assert.Equal("fake-camera", rig.Harness.Brain.CameraSlotDeviceId);
        Assert.Contains("fake-camera", rig.Harness.Brain.CameraSlotCameras.Select(camera => camera.DeviceId));
        Assert.Equal([(CameraState.Checking, (CameraFailureKind?)null), (CameraState.Ready, null)], changes);
        Assert.Equal(PreviewTarget.Guest, rig.Diagnostics().PreviewTarget);

        rig.Harness.Executor.Post(() => rig.Harness.Brain.SetPreviewTarget(PreviewTarget.Operator));
        rig.Run(TimeSpan.FromMilliseconds(100));
        Assert.Equal(PreviewTarget.Operator, rig.Diagnostics().PreviewTarget);
    }

    [Fact]
    public void The_camera_slot_reports_a_failure_and_releases_back_to_idle()
    {
        using var rig = new CameraRig();
        var states = new List<CameraState>();
        rig.Harness.Brain.CameraStateChanged = () => states.Add(rig.Harness.Brain.CameraSlotState);
        rig.Harness.Executor.Post(() => rig.Harness.Brain.OpenCameraFor("fake-camera", PreviewTarget.Operator));
        rig.Run(TimeSpan.FromMilliseconds(500));

        rig.Fake.Fail(CameraFailureKind.Removed);
        rig.Run(TimeSpan.FromMilliseconds(500));
        Assert.Equal(CameraState.NotAvailable, rig.Harness.Brain.CameraSlotState);
        Assert.Equal(CameraFailureKind.Removed, rig.Harness.Brain.CameraSlotReason);

        rig.Harness.Executor.Post(() => rig.Harness.Brain.ReleaseCamera());
        rig.Run(TimeSpan.FromMilliseconds(100));

        Assert.Equal(CameraState.Idle, rig.Harness.Brain.CameraSlotState);
        Assert.Null(rig.Harness.Brain.CameraSlotDeviceId);
        Assert.Null(rig.Harness.Brain.CameraSlotReason);
        Assert.Equal(PreviewTarget.None, rig.Diagnostics().PreviewTarget);
        Assert.Equal(CameraState.Idle, states[^1]);
    }

    [Fact]
    public void Releasing_the_camera_while_it_opens_closes_the_session_when_it_arrives()
    {
        using var rig = new CameraRig();

        rig.Harness.Executor.Post(() => rig.Harness.Brain.OpenCameraFor("fake-camera", PreviewTarget.Operator));
        rig.Harness.Executor.Post(() => rig.Harness.Brain.ReleaseCamera());
        rig.Run(TimeSpan.FromMilliseconds(500));

        Assert.Equal(CameraState.Idle, rig.Harness.Brain.CameraSlotState);
        Assert.Equal(0, rig.Fake.OpenSessionCount);
        Assert.Throws<ArgumentException>(() => rig.Harness.Brain.OpenCameraFor(string.Empty, PreviewTarget.None));
    }

    private sealed class EmptyImageCompositor : ICompositor
    {
        public Task<CompositionResult> ComposeAsync(CompositionRequest request, CancellationToken ct) =>
            Task.FromResult(new CompositionResult(ReadOnlyMemory<byte>.Empty, 600, 1800));

        public Task<CompositionResult> ComposeLookThumbnailAsync(CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}