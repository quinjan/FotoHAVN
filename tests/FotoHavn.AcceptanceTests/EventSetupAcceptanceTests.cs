using FotoHavn.Core;
using Xunit;

namespace FotoHavn.AcceptanceTests;

public sealed class EventSetupAcceptanceTests
{
    [Fact]
    public async Task New_Event_opens_setup_without_selecting_or_opening_an_Available_Camera()
    {
        var camera = new FakeCameraBoundary(
            new AvailableCamera("camera-1", "USB Camera", "Port 4"));
        var orchestrator = CreateOrchestrator(camera);

        var state = await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);

        Assert.True(state.Setup?.IsOpen);
        Assert.True(state.Setup?.IsBackdropInert);
        Assert.False(state.Setup?.ShowsCameraTuning);
        Assert.Single(state.Setup!.AvailableCameras);
        Assert.Null(state.Setup.SelectedCamera);
        Assert.Equal(CameraConnectionState.NotSelected, state.Setup.CameraState);
        Assert.Equal(0, camera.OpenCount);
        Assert.Equal(1, camera.StartDiscoveryCount);
    }

    [Fact]
    public async Task Discovery_updates_the_open_setup_and_disambiguates_duplicate_names_by_stable_identity()
    {
        var camera = new FakeCameraBoundary(
            new AvailableCamera("usb#vid_1111&pid_2222#alpha", "USB Camera", "Port 4"));
        var orchestrator = CreateOrchestrator(camera);
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);

        camera.SetAvailableCameras(
            new AvailableCamera("usb#vid_1111&pid_2222#alpha", "USB Camera", "Port 4"),
            new AvailableCamera("usb#vid_1111&pid_2222#bravo", "USB Camera", "Port 7"));

        var state = orchestrator.CurrentPresentation;
        Assert.Equal(["Port 4", "Port 7"], state.Setup!.AvailableCameras.Select(item => item.SecondaryLabel));
        Assert.All(state.Setup.AvailableCameras, item => Assert.Equal(CameraAvailability.Available, item.Availability));
    }

    [Fact]
    public async Task Camera_menu_is_an_anchored_overlay_and_closes_when_a_Camera_is_selected()
    {
        var camera = new FakeCameraBoundary(new AvailableCamera("camera-1", "USB Camera", "Port 4"));
        var orchestrator = CreateOrchestrator(camera);
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        var menuState = await orchestrator.ExecuteAsync(new ToggleCameraMenu(), TestContext.Current.CancellationToken);

        Assert.True(menuState.Setup!.CameraMenu.IsOpen);
        Assert.True(menuState.Setup.CameraMenu.IsAnchoredOverlay);
        Assert.False(menuState.Setup.CameraMenu.ChangesModalHeight);

        var selected = await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);

        Assert.False(selected.Setup!.CameraMenu.IsOpen);
        Assert.Equal("camera-1", selected.Setup.SelectedCamera!.DeviceId.Value);
        Assert.Equal(CameraConnectionState.Ready, selected.Setup.CameraState);
        Assert.True(selected.Setup.Preview.IsMirroredForRenderingOnly);
        Assert.Equal((3, 2), (selected.Setup.Preview.CropWidthRatio, selected.Setup.Preview.CropHeightRatio));
        Assert.True(selected.Setup.Preview.UsesSelectedCameraStream);
    }

    [Theory]
    [InlineData(CameraOpenResult.Unavailable, CameraConnectionState.Unavailable)]
    [InlineData(CameraOpenResult.AccessDenied, CameraConnectionState.AccessDenied)]
    [InlineData(CameraOpenResult.InUse, CameraConnectionState.InUseByAnotherApp)]
    [InlineData(CameraOpenResult.Disconnected, CameraConnectionState.Disconnected)]
    public async Task Selection_reports_the_Camera_open_result(
        CameraOpenResult result,
        CameraConnectionState expectedState)
    {
        var camera = new FakeCameraBoundary(new AvailableCamera("camera-1", "USB Camera", "Port 4"))
        {
            NextOpenResult = result,
        };
        var orchestrator = CreateOrchestrator(camera);
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);

        var state = await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);

        Assert.Equal(expectedState, state.Setup!.CameraState);
        Assert.False(state.Setup.IsCameraEligible);
    }

    [Fact]
    public async Task Selection_publishes_Connecting_before_the_Camera_becomes_Eligible()
    {
        var camera = new FakeCameraBoundary(new AvailableCamera("camera-1", "USB Camera", "Port 4"));
        var orchestrator = CreateOrchestrator(camera);
        var states = new List<CameraConnectionState>();
        orchestrator.PresentationChanged += (_, state) =>
        {
            if (state.Setup is not null)
            {
                states.Add(state.Setup.CameraState);
            }
        };
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);

        await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);

        Assert.Contains(CameraConnectionState.Connecting, states);
        Assert.Equal(CameraConnectionState.Ready, states[^1]);
    }

    [Fact]
    public async Task Saved_Camera_Binding_keeps_an_absent_exact_ID_visible_until_explicit_reselection()
    {
        var saved = new EventConfiguration(
            new EventId("event-1"),
            "Summer Party",
            new CameraBinding("missing-camera", "Booth Camera"),
            PrinterChoice.NoPrinter,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
        var fileSystem = new FakeFileSystem();
        fileSystem.SavedEvents.Add(saved);
        var camera = new FakeCameraBoundary(new AvailableCamera("different-camera", "Booth Camera", "Port 7"));
        var orchestrator = CreateOrchestrator(camera, fileSystem);

        var state = await orchestrator.ExecuteAsync(new OpenSavedEvent(saved.Id), TestContext.Current.CancellationToken);

        Assert.Equal("missing-camera", state.Setup!.SelectedCamera!.DeviceId.Value);
        Assert.Equal("Booth Camera", state.Setup.SelectedCamera.DisplayName);
        Assert.Equal(CameraAvailability.Unavailable, state.Setup.SelectedCamera.Availability);
        Assert.Equal("Edit Event", state.Setup.Title);
        Assert.Equal(0, camera.OpenCount);
    }

    [Fact]
    public async Task Camera_can_be_reselected_after_a_failed_open_without_reusing_failed_state()
    {
        var camera = new FakeCameraBoundary(new AvailableCamera("camera-1", "USB Camera", "Port 4"))
        {
            NextOpenResult = CameraOpenResult.InUse,
        };
        var orchestrator = CreateOrchestrator(camera);
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        var failed = await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);
        camera.NextOpenResult = CameraOpenResult.Ready;

        var reconstructed = await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);

        Assert.Equal(CameraConnectionState.InUseByAnotherApp, failed.Setup!.CameraState);
        Assert.Equal(CameraConnectionState.Ready, reconstructed.Setup!.CameraState);
        Assert.Equal(2, camera.OpenCount);
        Assert.Equal(1, camera.ReleaseCount);
    }

    [Fact]
    public async Task Removed_bound_Camera_remains_visible_and_is_never_substituted()
    {
        var camera = new FakeCameraBoundary(
            new AvailableCamera("camera-1", "USB Camera", "Port 4"),
            new AvailableCamera("camera-2", "USB Camera", "Port 7"));
        var orchestrator = CreateOrchestrator(camera);
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);

        camera.SetAvailableCameras(new AvailableCamera("camera-2", "USB Camera", "Port 7"));

        var state = orchestrator.CurrentPresentation;
        Assert.Equal("camera-1", state.Setup!.SelectedCamera!.DeviceId.Value);
        Assert.Equal(CameraAvailability.Unavailable, state.Setup.SelectedCamera.Availability);
        Assert.Equal(CameraConnectionState.Disconnected, state.Setup.CameraState);
    }

    [Fact]
    public async Task No_Printer_validates_without_querying_printer_hardware_and_Save_Close_releases_Camera()
    {
        var camera = new FakeCameraBoundary(new AvailableCamera("camera-1", "USB Camera", "Port 4"));
        var fileSystem = new FakeFileSystem();
        var orchestrator = CreateOrchestrator(camera, fileSystem);
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new ChangeEventName("Summer Party"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectNoPrinter(), TestContext.Current.CancellationToken);

        var state = await orchestrator.ExecuteAsync(new SaveAndCloseEventSetup(), TestContext.Current.CancellationToken);

        Assert.Null(state.Setup);
        Assert.Equal(1, camera.ReleaseCount);
        Assert.Equal(0, fileSystem.PrinterQueryCount);
        var saved = Assert.Single(fileSystem.SavedEvents);
        Assert.Equal("camera-1", saved.Camera.DeviceId.Value);
        Assert.Equal("USB Camera", saved.Camera.DisplayName);
        Assert.Equal(PrinterChoice.NoPrinter, saved.Printer);
    }

    [Fact]
    public async Task Save_Start_transfers_the_same_healthy_stream_to_the_Active_Event()
    {
        var camera = new FakeCameraBoundary(new AvailableCamera("camera-1", "USB Camera", "Port 4"));
        var orchestrator = CreateOrchestrator(camera);
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new ChangeEventName("Summer Party"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectNoPrinter(), TestContext.Current.CancellationToken);

        var state = await orchestrator.ExecuteAsync(new SaveAndStartEvent(), TestContext.Current.CancellationToken);

        Assert.Null(state.Setup);
        Assert.Equal("Summer Party", state.ActiveEvent?.Name);
        Assert.Equal(camera.StreamId, state.ActiveEvent?.CameraStreamId);
        Assert.Equal(1, camera.OpenCount);
        Assert.Equal(0, camera.ReleaseCount);
    }

    [Fact]
    public async Task Replacing_a_Camera_and_discarding_the_dirty_draft_release_the_owned_stream()
    {
        var camera = new FakeCameraBoundary(
            new AvailableCamera("camera-1", "USB Camera", "Port 4"),
            new AvailableCamera("camera-2", "USB Camera", "Port 7"));
        var orchestrator = CreateOrchestrator(camera);
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectCamera("camera-2"), TestContext.Current.CancellationToken);

        var confirmation = await orchestrator.ExecuteAsync(new CancelEventSetup(), TestContext.Current.CancellationToken);
        var state = await orchestrator.ExecuteAsync(new DiscardEventSetupDraft(), TestContext.Current.CancellationToken);

        Assert.True(confirmation.Setup!.ShowsDiscardConfirmation);
        Assert.Null(state.Setup);
        Assert.Equal(2, camera.ReleaseCount);
    }

    private static EventGuestCycleOrchestrator CreateOrchestrator(
        ICameraBoundary camera,
        IEventFileSystem? fileSystem = null) =>
        new(fileSystem ?? new FakeFileSystem(), camera, new StubCompositor(), new StubClock());

    private sealed class FakeCameraBoundary(params AvailableCamera[] cameras) : ICameraBoundary
    {
        private IReadOnlyList<AvailableCamera> availableCameras = cameras;

        public event EventHandler? AvailableCamerasChanged;

        public IReadOnlyList<AvailableCamera> AvailableCameras => availableCameras;
        public CameraOpenResult NextOpenResult { get; set; } = CameraOpenResult.Ready;
        public string? StreamId { get; private set; }
        public int StartDiscoveryCount { get; private set; }
        public int OpenCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public Task StartDiscoveryAsync(CancellationToken cancellationToken)
        {
            StartDiscoveryCount++;
            return Task.CompletedTask;
        }

        public Task<CameraOpenResult> OpenAsync(CameraDeviceId deviceId, CancellationToken cancellationToken)
        {
            OpenCount++;
            StreamId = NextOpenResult is CameraOpenResult.Ready ? $"stream-{OpenCount}" : null;
            return Task.FromResult(NextOpenResult);
        }

        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            ReleaseCount++;
            StreamId = null;
            return Task.CompletedTask;
        }

        public void SetAvailableCameras(params AvailableCamera[] updated)
        {
            availableCameras = updated;
            AvailableCamerasChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeFileSystem : IEventFileSystem
    {
        public List<EventConfiguration> SavedEvents { get; } = [];
        public int PrinterQueryCount { get; private set; }

        public Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SavedEventSummary>>([]);

        public Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken) =>
            Task.FromResult<EventConfiguration?>(SavedEvents.FirstOrDefault(saved => saved.Id == eventId));

        public Task<bool> ProbeStorageAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<EventSaveResult> SaveEventAtomicallyAsync(
            EventConfiguration configuration,
            EventSaveMode mode,
            CancellationToken cancellationToken)
        {
            SavedEvents.RemoveAll(saved => saved.Id == configuration.Id);
            SavedEvents.Add(configuration);
            return Task.FromResult(EventSaveResult.Saved);
        }

        public Task DeleteEventAsync(EventId eventId, CancellationToken cancellationToken)
        {
            SavedEvents.RemoveAll(saved => saved.Id == eventId);
            return Task.CompletedTask;
        }
    }

    private sealed class StubCompositor : IPhotoStripCompositor
    {
        public Task<PhotoStripCompositionResult> ComposeAsync(PhotoStripCompositionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new PhotoStripCompositionResult(false, ReadOnlyMemory<byte>.Empty, 0, 0));
    }

    private sealed class StubClock : IApplicationClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
