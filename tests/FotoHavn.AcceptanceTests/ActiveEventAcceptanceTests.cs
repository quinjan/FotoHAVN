using FotoHavn.Core;
using Xunit;

namespace FotoHavn.AcceptanceTests;

public sealed class ActiveEventAcceptanceTests
{
    [Fact]
    public async Task Save_Start_reuses_the_healthy_Camera_and_enters_guest_Start_with_a_wake_lock()
    {
        var camera = new RecordingCamera(new AvailableCamera("camera-1", "Booth Camera", "Port 4"));
        var fileSystem = new RecordingFileSystem();
        var wakeLock = new RecordingWakeLock();
        var orchestrator = CreateOrchestrator(fileSystem, camera, wakeLock);
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new ChangeEventName("Mika & Paolo's Wedding"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectNoPrinter(), TestContext.Current.CancellationToken);

        var state = await orchestrator.ExecuteAsync(new SaveAndStartEvent(), TestContext.Current.CancellationToken);

        Assert.Null(state.Setup);
        Assert.Equal("Mika & Paolo's Wedding", state.ActiveEvent!.Name);
        Assert.Equal("Let’s take some photos.", state.ActiveEvent.Heading);
        Assert.Equal("We’ll take four photos to create your Photo Strip.", state.ActiveEvent.Explanation);
        Assert.Equal("Touch to start", state.ActiveEvent.StartActionLabel);
        Assert.True(state.ActiveEvent.ShowsExitEvent);
        Assert.False(state.ActiveEvent.ShowsHardwareStatus);
        Assert.Equal("stream-1", state.ActiveEvent.CameraStreamId);
        Assert.Equal(1, camera.OpenCount);
        Assert.Equal(0, camera.ReleaseCount);
        Assert.Equal(1, fileSystem.SaveCount);
        Assert.Equal(2, fileSystem.StorageProbeCount);
        Assert.Equal(1, wakeLock.AcquireCount);
    }

    [Fact]
    public async Task Saved_Event_requires_confirmation_then_opens_only_its_exact_Camera_Binding()
    {
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-bound");
        var fileSystem = new RecordingFileSystem(savedEvent);
        var camera = new RecordingCamera(
            new AvailableCamera("camera-other", "Booth Camera", "Port 7"),
            new AvailableCamera("camera-bound", "Booth Camera", "Port 4"));
        var wakeLock = new RecordingWakeLock();
        var orchestrator = CreateOrchestrator(fileSystem, camera, wakeLock);
        await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);

        var confirmation = await orchestrator.ExecuteAsync(
            new StartSavedEvent(savedEvent.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal("Start “Summer Party”?", confirmation.StartEventConfirmation!.Prompt);
        Assert.Equal(0, camera.OpenCount);

        var active = await orchestrator.ExecuteAsync(
            new ConfirmStartSavedEvent(),
            TestContext.Current.CancellationToken);

        Assert.Equal("camera-bound", Assert.Single(camera.OpenedDeviceIds).Value);
        Assert.Equal("Summer Party", active.ActiveEvent!.Name);
        Assert.Null(active.StartEventConfirmation);
        Assert.Equal(1, fileSystem.StorageProbeCount);
        Assert.Equal(1, wakeLock.AcquireCount);
    }

    [Fact]
    public async Task Failed_saved_Event_storage_preflight_keeps_it_inactive_and_opens_actionable_setup()
    {
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-bound");
        var fileSystem = new RecordingFileSystem(savedEvent);
        fileSystem.StorageProbeResults.Enqueue(false);
        var camera = new RecordingCamera(new AvailableCamera("camera-bound", "Booth Camera", "Port 4"));
        var wakeLock = new RecordingWakeLock();
        var orchestrator = CreateOrchestrator(fileSystem, camera, wakeLock);
        await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new StartSavedEvent(savedEvent.Id), TestContext.Current.CancellationToken);

        var failed = await orchestrator.ExecuteAsync(new ConfirmStartSavedEvent(), TestContext.Current.CancellationToken);

        Assert.Null(failed.ActiveEvent);
        Assert.False(failed.Setup!.IsStorageReady);
        Assert.Equal(CameraConnectionState.Ready, failed.Setup.CameraState);
        Assert.Equal("camera-bound", failed.Setup.SelectedCamera!.DeviceId.Value);
        Assert.Equal(0, wakeLock.AcquireCount);

        var retried = await orchestrator.ExecuteAsync(new RetryEventStorage(), TestContext.Current.CancellationToken);
        var active = await orchestrator.ExecuteAsync(new SaveAndStartEvent(), TestContext.Current.CancellationToken);

        Assert.True(retried.Setup!.IsStorageReady);
        Assert.False(retried.Setup.CanSave);
        Assert.True(retried.Setup.CanStart);
        Assert.Equal("stream-1", active.ActiveEvent!.CameraStreamId);
        Assert.Equal(1, camera.OpenCount);
        Assert.Equal(0, camera.ReleaseCount);
    }

    [Fact]
    public async Task Failed_saved_Event_Camera_preflight_never_substitutes_another_Camera()
    {
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-bound");
        var fileSystem = new RecordingFileSystem(savedEvent);
        var camera = new RecordingCamera(
            new AvailableCamera("camera-bound", "Booth Camera", "Port 4"),
            new AvailableCamera("camera-other", "Booth Camera", "Port 7"))
        {
            NextOpenResult = CameraOpenResult.InUse,
        };
        var orchestrator = CreateOrchestrator(fileSystem, camera, new RecordingWakeLock());
        await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new StartSavedEvent(savedEvent.Id), TestContext.Current.CancellationToken);

        var failed = await orchestrator.ExecuteAsync(new ConfirmStartSavedEvent(), TestContext.Current.CancellationToken);

        Assert.Null(failed.ActiveEvent);
        Assert.Equal(CameraConnectionState.InUseByAnotherApp, failed.Setup!.CameraState);
        Assert.Equal(["camera-bound"], camera.OpenedDeviceIds.Select(id => id.Value));
    }

    [Fact]
    public async Task Active_Event_is_the_only_Event_in_the_process_and_ignores_other_Event_commands()
    {
        var first = SavedEvent("event-1", "Summer Party", "camera-1");
        var second = SavedEvent("event-2", "Winter Party", "camera-2");
        var fileSystem = new RecordingFileSystem(first, second);
        var camera = new RecordingCamera(
            new AvailableCamera("camera-1", "First Camera", "Port 4"),
            new AvailableCamera("camera-2", "Second Camera", "Port 7"));
        var orchestrator = CreateOrchestrator(fileSystem, camera, new RecordingWakeLock());
        await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new StartSavedEvent(first.Id), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new ConfirmStartSavedEvent(), TestContext.Current.CancellationToken);

        var unchanged = await orchestrator.ExecuteAsync(
            new StartSavedEvent(second.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(first.Id, unchanged.ActiveEvent!.Id);
        Assert.Null(unchanged.StartEventConfirmation);
        Assert.Equal(["camera-1"], camera.OpenedDeviceIds.Select(id => id.Value));
    }

    [Fact]
    public async Task Confirmed_Exit_Event_releases_ownership_and_returns_to_Saved_Events()
    {
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-1");
        var fileSystem = new RecordingFileSystem(savedEvent);
        var camera = new RecordingCamera(new AvailableCamera("camera-1", "Booth Camera", "Port 4"));
        var wakeLock = new RecordingWakeLock();
        var orchestrator = CreateOrchestrator(fileSystem, camera, wakeLock);
        await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new StartSavedEvent(savedEvent.Id), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new ConfirmStartSavedEvent(), TestContext.Current.CancellationToken);

        var confirmation = await orchestrator.ExecuteAsync(new ExitActiveEvent(), TestContext.Current.CancellationToken);

        Assert.True(confirmation.ActiveEvent!.ShowsExitConfirmation);
        Assert.Equal(0, camera.ReleaseCount);
        Assert.Equal(0, wakeLock.ReleaseCount);

        var exited = await orchestrator.ExecuteAsync(new ConfirmExitActiveEvent(), TestContext.Current.CancellationToken);

        Assert.Null(exited.ActiveEvent);
        Assert.Null(exited.Setup);
        Assert.Contains(exited.EventTiles, tile => tile.EventId == savedEvent.Id);
        Assert.Single(fileSystem.SavedEvents);
        Assert.Equal(1, camera.ReleaseCount);
        Assert.Equal(1, wakeLock.ReleaseCount);
    }

    [Fact]
    public async Task Closing_releases_the_Active_Event_and_restart_never_resumes_it()
    {
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-1");
        var fileSystem = new RecordingFileSystem(savedEvent);
        var camera = new RecordingCamera(new AvailableCamera("camera-1", "Booth Camera", "Port 4"));
        var wakeLock = new RecordingWakeLock();
        var orchestrator = CreateOrchestrator(fileSystem, camera, wakeLock);
        await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new StartSavedEvent(savedEvent.Id), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new ConfirmStartSavedEvent(), TestContext.Current.CancellationToken);

        var closed = await orchestrator.ExecuteAsync(new ShutdownApplication(), TestContext.Current.CancellationToken);

        Assert.Null(closed.ActiveEvent);
        Assert.Equal(1, camera.ReleaseCount);
        Assert.Equal(1, wakeLock.ReleaseCount);

        var restarted = CreateOrchestrator(
            fileSystem,
            new RecordingCamera(new AvailableCamera("camera-1", "Booth Camera", "Port 4")),
            new RecordingWakeLock());
        var launch = await restarted.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);
        Assert.Null(launch.ActiveEvent);
        Assert.Contains(launch.EventTiles, tile => tile.EventId == savedEvent.Id);
    }

    private static EventConfiguration SavedEvent(string id, string name, string cameraId) =>
        new(
            new EventId(id),
            name,
            new CameraBinding(cameraId, "Booth Camera"),
            PrinterChoice.NoPrinter,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

    private static EventGuestCycleOrchestrator CreateOrchestrator(
        IEventFileSystem fileSystem,
        ICameraBoundary camera,
        IActiveEventWakeLock wakeLock) =>
        new(fileSystem, camera, new StubCompositor(), new StubClock(), wakeLock: wakeLock);

    private sealed class RecordingCamera(params AvailableCamera[] cameras) : ICameraBoundary
    {
        public event EventHandler? AvailableCamerasChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<AvailableCamera> AvailableCameras { get; } = cameras;
        public string? StreamId { get; private set; }
        public CameraOpenResult NextOpenResult { get; set; } = CameraOpenResult.Ready;
        public List<CameraDeviceId> OpenedDeviceIds { get; } = [];
        public int OpenCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public Task StartDiscoveryAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<CameraOpenResult> OpenAsync(CameraDeviceId deviceId, CancellationToken cancellationToken)
        {
            OpenCount++;
            OpenedDeviceIds.Add(deviceId);
            StreamId = NextOpenResult == CameraOpenResult.Ready ? $"stream-{OpenCount}" : null;
            return Task.FromResult(NextOpenResult);
        }

        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            ReleaseCount++;
            StreamId = null;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingFileSystem(params EventConfiguration[] savedEvents) : IEventFileSystem
    {
        public List<EventConfiguration> SavedEvents { get; } = [.. savedEvents];
        public Queue<bool> StorageProbeResults { get; } = [];
        public int SaveCount { get; private set; }
        public int StorageProbeCount { get; private set; }

        public Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SavedEventSummary>>(SavedEvents
                .Select(saved => new SavedEventSummary(saved.Id, saved.Name, saved.LastSavedAt))
                .ToArray());

        public Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken) =>
            Task.FromResult<EventConfiguration?>(SavedEvents.FirstOrDefault(saved => saved.Id == eventId));

        public Task<bool> ProbeStorageAsync(CancellationToken cancellationToken)
        {
            StorageProbeCount++;
            return Task.FromResult(StorageProbeResults.TryDequeue(out var result) ? result : true);
        }

        public Task<EventSaveResult> SaveEventAtomicallyAsync(
            EventConfiguration configuration,
            EventSaveMode mode,
            CancellationToken cancellationToken)
        {
            SaveCount++;
            SavedEvents.RemoveAll(saved => saved.Id == configuration.Id);
            SavedEvents.Add(configuration);
            return Task.FromResult(EventSaveResult.Saved);
        }

        public Task DeleteEventAsync(EventId eventId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingWakeLock : IActiveEventWakeLock
    {
        public int AcquireCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public Task AcquireAsync(CancellationToken cancellationToken)
        {
            AcquireCount++;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            ReleaseCount++;
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
