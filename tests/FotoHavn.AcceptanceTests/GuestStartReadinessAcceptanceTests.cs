using FotoHavn.Core;
using Xunit;

namespace FotoHavn.AcceptanceTests;

public sealed class GuestStartReadinessAcceptanceTests
{
    [Theory]
    [InlineData(CameraStreamFailure.StreamFailure)]
    [InlineData(CameraStreamFailure.ExclusiveOwnershipLost)]
    public async Task Background_Camera_failure_disables_visible_Start_and_calls_for_the_operator(
        CameraStreamFailure failure)
    {
        var now = new DateTimeOffset(2026, 8, 5, 8, 0, 0, TimeSpan.Zero);
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-bound", now);
        var fileSystem = new RecordingFileSystem(savedEvent);
        var camera = new RecordingCamera(now, new AvailableCamera("camera-bound", "Booth Camera", "Port 4"));
        var orchestrator = CreateOrchestrator(fileSystem, camera, now);
        await ActivateAsync(orchestrator, savedEvent.Id);

        Assert.True(orchestrator.CurrentPresentation.ActiveEvent!.GuestStart.IsStartEnabled);

        camera.FailActiveStream(failure);

        var guestStart = orchestrator.CurrentPresentation.ActiveEvent!.GuestStart;
        Assert.True(guestStart.IsStartVisible);
        Assert.False(guestStart.IsStartEnabled);
        Assert.Equal("Please call the operator", guestStart.StatusMessage);
        Assert.True(guestStart.ShowsRetry);
        Assert.Equal("Retry", guestStart.RetryActionLabel);
    }

    [Fact]
    public async Task Start_tap_rechecks_frame_freshness_without_reopening_or_creating_artifacts()
    {
        var now = new DateTimeOffset(2026, 8, 5, 8, 0, 0, TimeSpan.Zero);
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-bound", now);
        var fileSystem = new RecordingFileSystem(savedEvent);
        var camera = new RecordingCamera(now, new AvailableCamera("camera-bound", "Booth Camera", "Port 4"));
        var clock = new FixedClock(now);
        var orchestrator = CreateOrchestrator(fileSystem, camera, clock);
        await ActivateAsync(orchestrator, savedEvent.Id);
        clock.UtcNow = now.AddSeconds(3);

        var state = await orchestrator.ExecuteAsync(new StartGuestCycle(), TestContext.Current.CancellationToken);

        Assert.False(state.ActiveEvent!.GuestStart.IsStartEnabled);
        Assert.Equal(GuestStartFailure.CameraUnavailable, state.ActiveEvent.GuestStart.Failure);
        Assert.Equal(1, camera.OpenCount);
        Assert.Equal(0, camera.ReleaseCount);
        Assert.Equal(2, fileSystem.StorageProbeCount);
        Assert.Equal(0, fileSystem.MutationCount);
    }

    [Fact]
    public async Task Storage_Retry_rechecks_only_storage_and_restores_Start()
    {
        var now = new DateTimeOffset(2026, 8, 5, 8, 0, 0, TimeSpan.Zero);
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-bound", now);
        var fileSystem = new RecordingFileSystem(savedEvent);
        fileSystem.StorageProbeResults.Enqueue(true);
        fileSystem.StorageProbeResults.Enqueue(false);
        fileSystem.StorageProbeResults.Enqueue(true);
        var camera = new RecordingCamera(now, new AvailableCamera("camera-bound", "Booth Camera", "Port 4"));
        var orchestrator = CreateOrchestrator(fileSystem, camera, now);
        await ActivateAsync(orchestrator, savedEvent.Id);
        var blocked = await orchestrator.ExecuteAsync(new StartGuestCycle(), TestContext.Current.CancellationToken);

        var retried = await orchestrator.ExecuteAsync(
            new RetryGuestStartReadiness(),
            TestContext.Current.CancellationToken);

        Assert.Equal(GuestStartFailure.StorageUnavailable, blocked.ActiveEvent!.GuestStart.Failure);
        Assert.True(retried.ActiveEvent!.GuestStart.IsStartEnabled);
        Assert.Equal(1, camera.OpenCount);
        Assert.Equal(0, camera.ReleaseCount);
        Assert.Equal(3, fileSystem.StorageProbeCount);
    }

    [Fact]
    public async Task Camera_Retry_disposes_stale_ownership_and_opens_one_fresh_exact_stream()
    {
        var now = new DateTimeOffset(2026, 8, 5, 8, 0, 0, TimeSpan.Zero);
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-bound", now);
        var fileSystem = new RecordingFileSystem(savedEvent);
        var camera = new RecordingCamera(
            now,
            new AvailableCamera("camera-other", "Booth Camera", "Port 7"),
            new AvailableCamera("camera-bound", "Booth Camera", "Port 4"));
        var orchestrator = CreateOrchestrator(fileSystem, camera, now);
        await ActivateAsync(orchestrator, savedEvent.Id);
        camera.FailActiveStream(CameraStreamFailure.StreamFailure);

        var retried = await orchestrator.ExecuteAsync(
            new RetryGuestStartReadiness(),
            TestContext.Current.CancellationToken);

        Assert.True(retried.ActiveEvent!.GuestStart.IsStartEnabled);
        Assert.Equal("stream-2", retried.ActiveEvent.CameraStreamId);
        Assert.Equal(["camera-bound", "camera-bound"], camera.OpenedDeviceIds.Select(id => id.Value));
        Assert.Equal(1, camera.ReleaseCount);
        Assert.Equal(1, fileSystem.StorageProbeCount);
    }

    [Fact]
    public async Task Reconnect_updates_discovery_but_does_not_reopen_or_enable_Start_until_Retry()
    {
        var now = new DateTimeOffset(2026, 8, 5, 8, 0, 0, TimeSpan.Zero);
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-bound", now);
        var cameraDevice = new AvailableCamera("camera-bound", "Booth Camera", "Port 4");
        var camera = new RecordingCamera(now, cameraDevice);
        var orchestrator = CreateOrchestrator(new RecordingFileSystem(savedEvent), camera, now);
        await ActivateAsync(orchestrator, savedEvent.Id);
        camera.Disconnect("camera-bound");

        camera.Reconnect(cameraDevice);

        Assert.False(orchestrator.CurrentPresentation.ActiveEvent!.GuestStart.IsStartEnabled);
        Assert.Equal(1, camera.OpenCount);

        var retried = await orchestrator.ExecuteAsync(
            new RetryGuestStartReadiness(),
            TestContext.Current.CancellationToken);
        Assert.True(retried.ActiveEvent!.GuestStart.IsStartEnabled);
        Assert.Equal(2, camera.OpenCount);
    }

    [Fact]
    public async Task Missing_saved_Camera_ID_never_substitutes_same_named_device_and_requires_setup_correction()
    {
        var now = new DateTimeOffset(2026, 8, 5, 8, 0, 0, TimeSpan.Zero);
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-bound", now);
        var camera = new RecordingCamera(
            now,
            new AvailableCamera("camera-bound", "Booth Camera", "Port 4"),
            new AvailableCamera("camera-other", "Booth Camera", "Port 7"));
        var orchestrator = CreateOrchestrator(new RecordingFileSystem(savedEvent), camera, now);
        await ActivateAsync(orchestrator, savedEvent.Id);
        camera.Disconnect("camera-bound");

        var retried = await orchestrator.ExecuteAsync(
            new RetryGuestStartReadiness(),
            TestContext.Current.CancellationToken);

        Assert.False(retried.ActiveEvent!.GuestStart.IsStartEnabled);
        Assert.True(retried.ActiveEvent.GuestStart.RequiresEventSetupCorrection);
        Assert.False(retried.ActiveEvent.GuestStart.ShowsRetry);
        Assert.Equal(["camera-bound"], camera.OpenedDeviceIds.Select(id => id.Value));
    }

    [Fact]
    public async Task Admission_detects_Camera_failure_that_races_the_storage_check()
    {
        var now = new DateTimeOffset(2026, 8, 5, 8, 0, 0, TimeSpan.Zero);
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-bound", now);
        var camera = new RecordingCamera(now, new AvailableCamera("camera-bound", "Booth Camera", "Port 4"));
        var fileSystem = new RecordingFileSystem(savedEvent)
        {
            OnStorageProbe = count =>
            {
                if (count == 2)
                {
                    camera.FailActiveStream(CameraStreamFailure.StreamFailure);
                }
            },
        };
        var orchestrator = CreateOrchestrator(fileSystem, camera, now);
        await ActivateAsync(orchestrator, savedEvent.Id);

        var state = await orchestrator.ExecuteAsync(new StartGuestCycle(), TestContext.Current.CancellationToken);

        Assert.False(state.ActiveEvent!.GuestStart.IsStartEnabled);
        Assert.Equal(GuestStartFailure.CameraUnavailable, state.ActiveEvent.GuestStart.Failure);
        Assert.Equal(0, fileSystem.MutationCount);
    }

    [Fact]
    public async Task Failed_Camera_Retry_keeps_Start_disabled_without_substitution()
    {
        var now = new DateTimeOffset(2026, 8, 5, 8, 0, 0, TimeSpan.Zero);
        var savedEvent = SavedEvent("event-1", "Summer Party", "camera-bound", now);
        var camera = new RecordingCamera(
            now,
            new AvailableCamera("camera-bound", "Booth Camera", "Port 4"),
            new AvailableCamera("camera-other", "Booth Camera", "Port 7"));
        camera.OpenResults.Enqueue(CameraOpenResult.Ready);
        camera.OpenResults.Enqueue(CameraOpenResult.InUse);
        var orchestrator = CreateOrchestrator(new RecordingFileSystem(savedEvent), camera, now);
        await ActivateAsync(orchestrator, savedEvent.Id);
        camera.FailActiveStream(CameraStreamFailure.StreamFailure);
        var actionStates = new List<GuestStartActionState>();
        orchestrator.PresentationChanged += (_, presentation) =>
            actionStates.Add(presentation.ActiveEvent?.GuestStart.ActionState ?? GuestStartActionState.Idle);

        var retried = await orchestrator.ExecuteAsync(
            new RetryGuestStartReadiness(),
            TestContext.Current.CancellationToken);

        Assert.False(retried.ActiveEvent!.GuestStart.IsStartEnabled);
        Assert.Equal(GuestStartFailure.CameraUnavailable, retried.ActiveEvent.GuestStart.Failure);
        Assert.Contains(GuestStartActionState.Retrying, actionStates);
        Assert.Equal(GuestStartActionState.RetryFailed, retried.ActiveEvent.GuestStart.ActionState);
        Assert.Equal(["camera-bound", "camera-bound"], camera.OpenedDeviceIds.Select(id => id.Value));
    }

    private static async Task ActivateAsync(EventGuestCycleOrchestrator orchestrator, EventId eventId)
    {
        await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new StartSavedEvent(eventId), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new ConfirmStartSavedEvent(), TestContext.Current.CancellationToken);
    }

    private static EventGuestCycleOrchestrator CreateOrchestrator(
        IEventFileSystem fileSystem,
        ICameraBoundary camera,
        DateTimeOffset now) =>
        CreateOrchestrator(fileSystem, camera, new FixedClock(now));

    private static EventGuestCycleOrchestrator CreateOrchestrator(
        IEventFileSystem fileSystem,
        ICameraBoundary camera,
        IApplicationClock clock) =>
        new(fileSystem, camera, new StubCompositor(), clock);

    private static EventConfiguration SavedEvent(
        string eventId,
        string name,
        string cameraId,
        DateTimeOffset now) =>
        new(
            new EventId(eventId),
            name,
            new CameraBinding(cameraId, "Booth Camera"),
            PrinterChoice.NoPrinter,
            now,
            now);

    private sealed class RecordingCamera(DateTimeOffset now, params AvailableCamera[] cameras) : ICameraBoundary
    {
        private readonly List<AvailableCamera> availableCameras = [.. cameras];
        private EventHandler? availableCamerasChanged;

        public event EventHandler? AvailableCamerasChanged
        {
            add => availableCamerasChanged += value;
            remove => availableCamerasChanged -= value;
        }
        public event EventHandler? StreamHealthChanged;

        public IReadOnlyList<AvailableCamera> AvailableCameras => availableCameras;
        public string? StreamId => StreamHealth.StreamId;
        public CameraStreamHealth StreamHealth { get; private set; } = CameraStreamHealth.Unavailable;
        public int OpenCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public List<CameraDeviceId> OpenedDeviceIds { get; } = [];
        public Queue<CameraOpenResult> OpenResults { get; } = new();

        public Task StartDiscoveryAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<CameraOpenResult> OpenAsync(CameraDeviceId deviceId, CancellationToken cancellationToken)
        {
            OpenCount++;
            OpenedDeviceIds.Add(deviceId);
            var result = OpenResults.TryDequeue(out var queuedResult) ? queuedResult : CameraOpenResult.Ready;
            if (result != CameraOpenResult.Ready)
            {
                StreamHealth = CameraStreamHealth.Unavailable;
                return Task.FromResult(result);
            }

            StreamHealth = new CameraStreamHealth(deviceId, $"stream-{OpenCount}", now, CameraStreamFailure.None);
            return Task.FromResult(result);
        }

        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            ReleaseCount++;
            StreamHealth = CameraStreamHealth.Unavailable;
            return Task.CompletedTask;
        }

        public void FailActiveStream(CameraStreamFailure failure)
        {
            StreamHealth = StreamHealth with { Failure = failure };
            StreamHealthChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Disconnect(CameraDeviceId deviceId)
        {
            availableCameras.RemoveAll(camera => camera.DeviceId == deviceId);
            StreamHealth = StreamHealth with { Failure = CameraStreamFailure.Removed };
            StreamHealthChanged?.Invoke(this, EventArgs.Empty);
            availableCamerasChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Reconnect(AvailableCamera camera)
        {
            availableCameras.Add(camera);
            availableCamerasChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class RecordingFileSystem(params EventConfiguration[] events) : IEventFileSystem
    {
        public Queue<bool> StorageProbeResults { get; } = new();
        public Action<int>? OnStorageProbe { get; init; }
        public int StorageProbeCount { get; private set; }
        public int MutationCount { get; private set; }

        public Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SavedEventSummary>>(events
                .Select(item => new SavedEventSummary(item.Id, item.Name, item.LastSavedAt))
                .ToArray());

        public Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken) =>
            Task.FromResult<EventConfiguration?>(events.SingleOrDefault(item => item.Id == eventId));

        public Task<bool> ProbeStorageAsync(CancellationToken cancellationToken)
        {
            StorageProbeCount++;
            OnStorageProbe?.Invoke(StorageProbeCount);
            return Task.FromResult(StorageProbeResults.TryDequeue(out var result) ? result : true);
        }

        public Task<EventSaveResult> SaveEventAtomicallyAsync(
            EventConfiguration configuration,
            EventSaveMode mode,
            CancellationToken cancellationToken)
        {
            MutationCount++;
            return Task.FromResult(EventSaveResult.Saved);
        }

        public Task DeleteEventAsync(EventId eventId, CancellationToken cancellationToken)
        {
            MutationCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubCompositor : IPhotoStripCompositor
    {
        public Task<PhotoStripCompositionResult> ComposeAsync(
            PhotoStripCompositionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PhotoStripCompositionResult(false, ReadOnlyMemory<byte>.Empty, 0, 0));
    }

    private sealed class FixedClock(DateTimeOffset now) : IApplicationClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
