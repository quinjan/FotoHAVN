using FotoHavn.Core;
using Xunit;

namespace FotoHavn.AcceptanceTests;

public sealed class GuestCycleAcceptanceTests
{
    [Fact]
    public async Task Start_rejects_a_stale_Camera_without_reopening_or_allocating_a_Guest_Cycle()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera)
        {
            LatestFrameAt = new DateTimeOffset(2026, 8, 5, 0, 59, 57, TimeSpan.Zero),
        };
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var clock = new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero));
        var orchestrator = new EventGuestCycleOrchestrator(fileSystem, camera, new RecordingCompositor(), clock);
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);

        var rejected = await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);

        Assert.Equal(GuestCyclePhase.StartUnavailable, rejected.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(GuestCycleFailure.CameraUnavailable, rejected.ActiveEvent?.GuestCycle.Failure);
        Assert.Empty(fileSystem.CreatedGuestCycles);
        Assert.Equal(1, camera.OpenCount);
    }

    [Fact]
    public async Task Missing_post_zero_frame_pauses_then_Retry_resumes_at_the_next_uncommitted_Capture()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera) { FailOnceOnCaptureAttempt = 3 };
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var compositor = new RecordingCompositor();
        var clock = new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero));
        var orchestrator = new EventGuestCycleOrchestrator(fileSystem, camera, compositor, clock);
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);

        var paused = await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);

        Assert.Equal(GuestCyclePhase.OperatorAssistance, paused.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(2, paused.ActiveEvent?.GuestCycle.CompletedCaptures);
        Assert.Equal([1, 2], fileSystem.CommittedCaptures.Select(capture => capture.CaptureNumber));
        Assert.Equal(TimeSpan.FromSeconds(2), camera.LastCaptureTimeout);
        orchestrator.PresentationChanged += (_, presentation) =>
        {
            if (presentation.ActiveEvent?.GuestCycle.Phase == GuestCyclePhase.PhotoStripPreview)
            {
                _ = orchestrator.ExecuteAsync(new ConfirmPhotoStripVisible(), cancellationToken);
            }
        };

        var completed = await orchestrator.ExecuteAsync(new RetryGuestCycle(), cancellationToken);

        Assert.Single(fileSystem.CreatedGuestCycles);
        Assert.Equal([1, 2, 3, 4], fileSystem.CommittedCaptures.Select(capture => capture.CaptureNumber));
        Assert.Equal(GuestCyclePhase.Start, completed.ActiveEvent?.GuestCycle.Phase);
    }

    [Fact]
    public async Task Healthy_Guest_Cycle_captures_four_fresh_frames_composes_and_returns_to_Start()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera);
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var compositor = new RecordingCompositor();
        var clock = new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero));
        var orchestrator = new EventGuestCycleOrchestrator(fileSystem, camera, compositor, clock);
        var cancellationToken = TestContext.Current.CancellationToken;

        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);
        orchestrator.PresentationChanged += (_, presentation) =>
        {
            if (presentation.ActiveEvent?.GuestCycle.Phase == GuestCyclePhase.PhotoStripPreview)
            {
                _ = orchestrator.ExecuteAsync(new ConfirmPhotoStripVisible(), cancellationToken);
            }
        };

        var completed = await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);

        var guestCycleId = Assert.Single(fileSystem.CreatedGuestCycles);
        Assert.Equal(7, Guid.Parse(guestCycleId.Value).Version);
        Assert.Equal([1, 2, 3, 4], fileSystem.CommittedCaptures.Select(capture => capture.CaptureNumber));
        Assert.Equal([0L, 1L, 2L, 3L], camera.RequestedAfterSequences);
        Assert.Equal(4, compositor.LastRequest?.Captures.Count);
        Assert.Equal(savedEvent.Name, compositor.LastRequest?.EventName);
        Assert.Equal(guestCycleId, Assert.Single(fileSystem.CompletedGuestCycles));
        Assert.Equal(GuestCyclePhase.Start, completed.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(1, camera.OpenCount);
        Assert.Equal(30, clock.Delays.Count(delay => delay == TimeSpan.FromSeconds(1)));
        Assert.Equal(4, clock.Delays.Count(delay => delay == TimeSpan.FromMilliseconds(600)));
        Assert.Equal(4, clock.Delays.Count(delay => delay == TimeSpan.FromMilliseconds(900)));
        Assert.Contains(TimeSpan.FromMilliseconds(450), clock.Delays);
    }

    private static async Task ActivateAsync(
        EventGuestCycleOrchestrator orchestrator,
        EventId eventId,
        CancellationToken cancellationToken)
    {
        await orchestrator.ExecuteAsync(new LaunchApplication(), cancellationToken);
        await orchestrator.ExecuteAsync(new StartSavedEvent(eventId), cancellationToken);
        await orchestrator.ExecuteAsync(new ConfirmStartSavedEvent(), cancellationToken);
    }

    private static EventConfiguration SavedEvent() =>
        new(
            new EventId("event-1"),
            "Mika & Paolo's Wedding",
            new CameraBinding("camera-1", "Sony Alpha"),
            PrinterChoice.NoPrinter,
            new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 5, 0, 30, 0, TimeSpan.Zero));

    private sealed class GuestCycleCamera(CameraBinding binding) : ICameraBoundary
    {
        private long sequence;

        public event EventHandler? AvailableCamerasChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<AvailableCamera> AvailableCameras { get; } =
            [new(binding.DeviceId, binding.DisplayName, null)];

        public string? StreamId { get; private set; }

        public int OpenCount { get; private set; }

        public List<long> RequestedAfterSequences { get; } = [];

        public DateTimeOffset LatestFrameAt { get; set; } =
            new(2026, 8, 5, 1, 0, 0, TimeSpan.Zero);

        public int? FailOnceOnCaptureAttempt { get; init; }

        public TimeSpan? LastCaptureTimeout { get; private set; }

        private int captureAttempts;

        public CameraStreamHealth StreamHealth => new(
            binding.DeviceId,
            StreamId,
            sequence,
            LatestFrameAt,
            CameraStreamFailure.None);

        public Task StartDiscoveryAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<CameraOpenResult> OpenAsync(CameraDeviceId deviceId, CancellationToken cancellationToken)
        {
            OpenCount++;
            StreamId = "stream-1";
            return Task.FromResult(CameraOpenResult.Ready);
        }

        public Task<CapturedFrame?> CaptureFirstFreshFrameAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var afterSequence = sequence;
            RequestedAfterSequences.Add(afterSequence);
            LastCaptureTimeout = timeout;
            captureAttempts++;
            if (captureAttempts == FailOnceOnCaptureAttempt)
            {
                return Task.FromResult<CapturedFrame?>(null);
            }

            sequence = afterSequence + 1;
            return Task.FromResult<CapturedFrame?>(new(
                sequence,
                new DateTimeOffset(2026, 8, 5, 1, 0, (int)sequence, TimeSpan.Zero),
                1920,
                1080,
                new byte[] { 0xff, 0xd8, (byte)sequence, 0xff, 0xd9 }));
        }

        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            StreamId = null;
            return Task.CompletedTask;
        }
    }

    private sealed class GuestCycleFileSystem(EventConfiguration savedEvent) : IEventFileSystem
    {
        public List<GuestCycleId> CreatedGuestCycles { get; } = [];
        public List<(int CaptureNumber, CaptureReference Reference)> CommittedCaptures { get; } = [];
        public List<GuestCycleId> CompletedGuestCycles { get; } = [];

        public Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SavedEventSummary>>(
                [new(savedEvent.Id, savedEvent.Name, savedEvent.LastSavedAt)]);

        public Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken) =>
            Task.FromResult<EventConfiguration?>(savedEvent);

        public Task<bool> ProbeStorageAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<EventSaveResult> SaveEventAtomicallyAsync(
            EventConfiguration configuration,
            EventSaveMode mode,
            CancellationToken cancellationToken) => Task.FromResult(EventSaveResult.Saved);

        public Task DeleteEventAsync(EventId eventId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<GuestCycleCreateResult> CreateGuestCycleAsync(
            EventId eventId,
            GuestCycleId guestCycleId,
            DateTimeOffset startedAt,
            CancellationToken cancellationToken)
        {
            CreatedGuestCycles.Add(guestCycleId);
            return Task.FromResult(GuestCycleCreateResult.Created);
        }

        public Task<CaptureCommitResult> CommitCaptureAsync(
            EventId eventId,
            GuestCycleId guestCycleId,
            int captureNumber,
            CapturedFrame frame,
            CancellationToken cancellationToken)
        {
            var reference = new CaptureReference($"capture-{captureNumber}.jpg");
            CommittedCaptures.Add((captureNumber, reference));
            return Task.FromResult(new CaptureCommitResult(true, reference));
        }

        public Task<PhotoStripCommitResult> CommitPhotoStripAsync(
            EventId eventId,
            GuestCycleId guestCycleId,
            PhotoStripCompositionResult composition,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PhotoStripCommitResult(true, "photo-strip.png"));

        public Task CompleteGuestCycleAsync(
            EventId eventId,
            GuestCycleId guestCycleId,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken)
        {
            CompletedGuestCycles.Add(guestCycleId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCompositor : IPhotoStripCompositor
    {
        public PhotoStripCompositionRequest? LastRequest { get; private set; }

        public Task<PhotoStripCompositionResult> ComposeAsync(
            PhotoStripCompositionRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new PhotoStripCompositionResult(true, new byte[] { 1, 2, 3 }, 600, 1800));
        }
    }

    private sealed class RecordingClock(DateTimeOffset utcNow) : IApplicationClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            UtcNow += delay;
            return Task.CompletedTask;
        }
    }
}
