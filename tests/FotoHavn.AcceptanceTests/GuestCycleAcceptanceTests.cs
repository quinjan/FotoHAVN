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
        Assert.Equal(
            new GuestCycleInterruption(
                GuestCycleFailureSource.FreshFrameTimeout,
                GuestCycleInterruptedStep.Capture,
                3,
                2,
                clock.UtcNow),
            Assert.Single(fileSystem.Interruptions));
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
        Assert.Equal(1, camera.ReleaseCount);
        Assert.Equal([savedEvent.Camera.DeviceId, savedEvent.Camera.DeviceId], camera.OpenedDeviceIds);
        Assert.Equal(GuestCyclePhase.Start, completed.ActiveEvent?.GuestCycle.Phase);
    }

    [Fact]
    public async Task Retry_rejects_changed_durable_history_without_reopening_or_overwriting_anything()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera) { FailOnceOnCaptureAttempt = 3 };
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var clock = new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero));
        var orchestrator = new EventGuestCycleOrchestrator(
            fileSystem,
            camera,
            new RecordingCompositor(),
            clock);
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);
        await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);
        fileSystem.NextRetryValidation = GuestCycleRetryValidation.Unrecoverable;

        var rejected = await orchestrator.ExecuteAsync(new RetryGuestCycle(), cancellationToken);

        Assert.Equal(GuestCyclePhase.OperatorAssistance, rejected.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(GuestCycleRecovery.ExitOnly, rejected.ActiveEvent?.GuestCycle.Recovery);
        Assert.Equal(GuestCycleActionState.RetryFailed, rejected.ActiveEvent?.GuestCycle.ActionState);
        Assert.Equal(2, rejected.ActiveEvent?.GuestCycle.CompletedCaptures);
        Assert.Equal([1, 2], fileSystem.CommittedCaptures.Select(capture => capture.CaptureNumber));
        Assert.Single(fileSystem.CreatedGuestCycles);
        Assert.Equal(1, camera.OpenCount);
        Assert.Equal(0, camera.ReleaseCount);
    }

    [Theory]
    [InlineData(CameraStreamFailure.Removed, GuestCycleFailureSource.CameraRemoved)]
    [InlineData(CameraStreamFailure.StreamFailure, GuestCycleFailureSource.CameraStreamFailure)]
    [InlineData(CameraStreamFailure.ExclusiveOwnershipLost, GuestCycleFailureSource.CameraExclusiveOwnershipLost)]
    [InlineData(CameraStreamFailure.Stale, GuestCycleFailureSource.CameraStale)]
    public async Task Camera_stream_failures_record_their_exact_source(
        CameraStreamFailure streamFailure,
        GuestCycleFailureSource expectedSource)
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera)
        {
            FailOnceOnCaptureAttempt = 1,
            FailureOnFailedCapture = streamFailure,
        };
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var orchestrator = new EventGuestCycleOrchestrator(
            fileSystem,
            camera,
            new RecordingCompositor(),
            new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero)));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);

        var paused = await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);

        Assert.Equal(GuestCyclePhase.OperatorAssistance, paused.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(expectedSource, Assert.Single(fileSystem.Interruptions).Source);
    }

    [Fact]
    public async Task Stream_failure_during_countdown_immediately_pauses_the_active_attempt()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera);
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var clock = new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero));
        clock.DelayCompleted += (_, delayNumber) =>
        {
            if (delayNumber == 2)
            {
                camera.ReportStreamFailure(CameraStreamFailure.StreamFailure);
            }
        };
        var orchestrator = new EventGuestCycleOrchestrator(
            fileSystem,
            camera,
            new RecordingCompositor(),
            clock);
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);

        var paused = await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);

        Assert.Equal(GuestCyclePhase.OperatorAssistance, paused.ActiveEvent?.GuestCycle.Phase);
        Assert.Empty(camera.RequestedAfterSequences);
        Assert.Equal(GuestCycleFailureSource.CameraStreamFailure, Assert.Single(fileSystem.Interruptions).Source);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Storage_failure_at_each_Capture_checkpoint_retries_only_the_first_missing_Capture(
        int failedCapture)
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera);
        var fileSystem = new GuestCycleFileSystem(savedEvent) { FailCommitOnceOnCaptureNumber = failedCapture };
        var orchestrator = new EventGuestCycleOrchestrator(
            fileSystem,
            camera,
            new RecordingCompositor(),
            new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero)));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);

        var paused = await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);
        camera.LatestFrameAt = orchestrator.Clock.UtcNow;
        orchestrator.PresentationChanged += (_, presentation) =>
        {
            if (presentation.ActiveEvent?.GuestCycle.Phase == GuestCyclePhase.PhotoStripPreview)
            {
                _ = orchestrator.ExecuteAsync(new ConfirmPhotoStripVisible(), cancellationToken);
            }
        };
        var completed = await orchestrator.ExecuteAsync(new RetryGuestCycle(), cancellationToken);

        Assert.Equal(failedCapture - 1, paused.ActiveEvent?.GuestCycle.CompletedCaptures);
        Assert.Equal(GuestCycleFailureSource.Storage, Assert.Single(fileSystem.Interruptions).Source);
        Assert.Equal([1, 2, 3, 4], fileSystem.CommittedCaptures.Select(capture => capture.CaptureNumber));
        Assert.Equal(0, camera.ReleaseCount);
        Assert.Equal([savedEvent.Camera.DeviceId], camera.OpenedDeviceIds);
        Assert.Equal(GuestCyclePhase.Start, completed.ActiveEvent?.GuestCycle.Phase);
    }

    [Fact]
    public async Task Process_restart_leaves_the_interrupted_Guest_Cycle_untouched_and_Start_uses_a_new_identity()
    {
        var savedEvent = SavedEvent();
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var cancellationToken = TestContext.Current.CancellationToken;
        var interrupted = new EventGuestCycleOrchestrator(
            fileSystem,
            new GuestCycleCamera(savedEvent.Camera) { FailOnceOnCaptureAttempt = 1 },
            new RecordingCompositor(),
            new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero)),
            guestCycleIdentityGenerator: new FixedGuestCycleIdentityGenerator("interrupted-cycle"));
        await ActivateAsync(interrupted, savedEvent.Id, cancellationToken);
        await interrupted.ExecuteAsync(new StartGuestCycle(), cancellationToken);
        var recordedInterruption = Assert.Single(fileSystem.Interruptions);

        var restarted = new EventGuestCycleOrchestrator(
            fileSystem,
            new GuestCycleCamera(savedEvent.Camera)
            {
                LatestFrameAt = new DateTimeOffset(2026, 8, 5, 2, 0, 0, TimeSpan.Zero),
            },
            new RecordingCompositor(),
            new RecordingClock(new DateTimeOffset(2026, 8, 5, 2, 0, 0, TimeSpan.Zero)),
            guestCycleIdentityGenerator: new FixedGuestCycleIdentityGenerator("new-cycle"));
        await ActivateAsync(restarted, savedEvent.Id, cancellationToken);
        restarted.PresentationChanged += (_, presentation) =>
        {
            if (presentation.ActiveEvent?.GuestCycle.Phase == GuestCyclePhase.PhotoStripPreview)
            {
                _ = restarted.ExecuteAsync(new ConfirmPhotoStripVisible(), cancellationToken);
            }
        };

        await restarted.ExecuteAsync(new StartGuestCycle(), cancellationToken);

        Assert.Equal(
            [new GuestCycleId("interrupted-cycle"), new GuestCycleId("new-cycle")],
            fileSystem.CreatedGuestCycles);
        Assert.Equal(recordedInterruption, Assert.Single(fileSystem.Interruptions));
    }

    [Fact]
    public async Task Healthy_Guest_Cycle_captures_four_fresh_frames_composes_and_returns_to_Start()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera);
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var compositor = new RecordingCompositor();
        var clock = new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero));
        clock.DelayCompleted += (_, delayNumber) =>
        {
            if (delayNumber == 5)
            {
                camera.DeliverFrameAtCountdownBoundary();
            }
        };
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
        Assert.Equal([41L, 42L, 43L, 44L], camera.RequestedAfterSequences);
        Assert.Equal([42L, 43L, 44L, 45L], fileSystem.CommittedFrames.Select(frame => frame.Sequence));
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

    [Fact]
    public async Task Composition_failure_retries_only_composition_after_all_Captures_are_committed()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera);
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var compositor = new RecordingCompositor { FailOnce = true };
        var orchestrator = new EventGuestCycleOrchestrator(
            fileSystem,
            camera,
            compositor,
            new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero)));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);

        var paused = await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);
        camera.ReportStreamFailure(CameraStreamFailure.Removed);
        orchestrator.PresentationChanged += (_, presentation) =>
        {
            if (presentation.ActiveEvent?.GuestCycle.Phase == GuestCyclePhase.PhotoStripPreview)
            {
                _ = orchestrator.ExecuteAsync(new ConfirmPhotoStripVisible(), cancellationToken);
            }
        };

        var completed = await orchestrator.ExecuteAsync(new RetryGuestCycle(), cancellationToken);

        Assert.Equal(GuestCyclePhase.OperatorAssistance, paused.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(GuestCycleInterruptedStep.PhotoStrip, Assert.Single(fileSystem.Interruptions).Step);
        Assert.Equal(4, fileSystem.CommittedFrames.Count);
        Assert.Equal(2, compositor.ComposeCount);
        Assert.Equal(1, camera.OpenCount);
        Assert.Equal(0, camera.ReleaseCount);
        Assert.Equal(GuestCyclePhase.StartUnavailable, completed.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(GuestCycleFailure.CameraUnavailable, completed.ActiveEvent?.GuestCycle.Failure);
    }

    [Fact]
    public async Task Photo_Strip_commit_failure_recomposes_once_without_recapturing_or_reopening_the_Camera()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera);
        var fileSystem = new GuestCycleFileSystem(savedEvent) { FailPhotoStripCommitOnce = true };
        var compositor = new RecordingCompositor();
        var orchestrator = new EventGuestCycleOrchestrator(
            fileSystem,
            camera,
            compositor,
            new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero)));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);

        var paused = await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);
        orchestrator.PresentationChanged += (_, presentation) =>
        {
            if (presentation.ActiveEvent?.GuestCycle.Phase == GuestCyclePhase.PhotoStripPreview)
            {
                _ = orchestrator.ExecuteAsync(new ConfirmPhotoStripVisible(), cancellationToken);
            }
        };
        var completed = await orchestrator.ExecuteAsync(new RetryGuestCycle(), cancellationToken);

        Assert.Equal(GuestCyclePhase.OperatorAssistance, paused.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(4, fileSystem.CommittedFrames.Count);
        Assert.Equal(2, compositor.ComposeCount);
        Assert.Equal(2, fileSystem.PhotoStripCommitAttempts);
        Assert.Equal(1, camera.OpenCount);
        Assert.Equal(0, camera.ReleaseCount);
        Assert.Equal(GuestCyclePhase.Start, completed.ActiveEvent?.GuestCycle.Phase);
    }

    [Fact]
    public async Task Visible_decode_failure_reuses_the_Photo_Strip_and_restarts_the_full_ten_second_timer()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera);
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var compositor = new RecordingCompositor();
        var clock = new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero));
        var orchestrator = new EventGuestCycleOrchestrator(fileSystem, camera, compositor, clock);
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);
        var rejectFirstDecode = true;
        orchestrator.PresentationChanged += (_, presentation) =>
        {
            if (presentation.ActiveEvent?.GuestCycle.Phase != GuestCyclePhase.PhotoStripPreview)
            {
                return;
            }

            if (rejectFirstDecode)
            {
                rejectFirstDecode = false;
                _ = orchestrator.ExecuteAsync(new ReportPhotoStripDecodeFailure(), cancellationToken);
            }
            else
            {
                _ = orchestrator.ExecuteAsync(new ConfirmPhotoStripVisible(), cancellationToken);
            }
        };

        var paused = await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);
        var oneSecondDelaysBeforeRetry = clock.Delays.Count(delay => delay == TimeSpan.FromSeconds(1));
        var completed = await orchestrator.ExecuteAsync(new RetryGuestCycle(), cancellationToken);

        Assert.Equal(GuestCyclePhase.OperatorAssistance, paused.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(20, oneSecondDelaysBeforeRetry);
        Assert.Equal(30, clock.Delays.Count(delay => delay == TimeSpan.FromSeconds(1)));
        Assert.Equal(1, compositor.ComposeCount);
        Assert.Equal(1, fileSystem.PhotoStripCommitAttempts);
        Assert.Equal(GuestCyclePhase.Start, completed.ActiveEvent?.GuestCycle.Phase);
    }

    [Fact]
    public async Task Camera_loss_during_final_preview_does_not_interrupt_completion_and_blocks_the_next_Start()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera);
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var orchestrator = new EventGuestCycleOrchestrator(
            fileSystem,
            camera,
            new RecordingCompositor(),
            new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero)));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);
        var cameraLost = false;
        orchestrator.PresentationChanged += (_, presentation) =>
        {
            if (!cameraLost && presentation.ActiveEvent?.GuestCycle.Phase == GuestCyclePhase.PhotoStripPreview)
            {
                cameraLost = true;
                camera.ReportStreamFailure(CameraStreamFailure.Removed);
                _ = orchestrator.ExecuteAsync(new ConfirmPhotoStripVisible(), cancellationToken);
            }
        };

        var completed = await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);

        Assert.Single(fileSystem.CompletedGuestCycles);
        Assert.Empty(fileSystem.Interruptions);
        Assert.Equal(GuestCyclePhase.StartUnavailable, completed.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(GuestCycleFailure.CameraUnavailable, completed.ActiveEvent?.GuestCycle.Failure);
    }

    [Fact]
    public async Task Completion_storage_failure_enters_assistance_and_Retry_finishes_without_replaying_the_preview()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera);
        var fileSystem = new GuestCycleFileSystem(savedEvent)
        {
            FailCompletionOnce = true,
            FailInterruptionRecording = true,
            StorageReadyAfterSuccessfulCompletion = false,
        };
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

        var paused = await orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);
        var delaysAfterPreview = clock.Delays.Count;
        var completed = await orchestrator.ExecuteAsync(new RetryGuestCycle(), cancellationToken);

        Assert.Equal(GuestCyclePhase.OperatorAssistance, paused.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(GuestCycleFailure.StorageUnavailable, paused.ActiveEvent?.GuestCycle.Failure);
        Assert.Equal(2, fileSystem.CompletionAttempts);
        Assert.Equal(4, fileSystem.CommittedFrames.Count);
        Assert.Equal(1, compositor.ComposeCount);
        Assert.Equal(delaysAfterPreview, clock.Delays.Count);
        Assert.Equal(GuestCyclePhase.StartUnavailable, completed.ActiveEvent?.GuestCycle.Phase);
        Assert.Equal(GuestCycleFailure.StorageUnavailable, completed.ActiveEvent?.GuestCycle.Failure);
    }

    [Fact]
    public async Task Shutdown_preempts_a_Guest_Cycle_waiting_for_visible_decode_and_releases_the_Camera()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera);
        var orchestrator = new EventGuestCycleOrchestrator(
            new GuestCycleFileSystem(savedEvent),
            camera,
            new RecordingCompositor(),
            new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero)));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);
        var previewReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        orchestrator.PresentationChanged += (_, presentation) =>
        {
            if (presentation.ActiveEvent?.GuestCycle.Phase == GuestCyclePhase.PhotoStripPreview)
            {
                previewReached.TrySetResult();
            }
        };

        var cycle = orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);
        await previewReached.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        var shutdown = orchestrator.ExecuteAsync(new ShutdownApplication(), cancellationToken);
        await Task.WhenAll(cycle, shutdown).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        Assert.Null(camera.StreamId);
        Assert.Null(orchestrator.CurrentPresentation.ActiveEvent);
    }

    [Fact]
    public async Task Shutdown_preempts_Guest_Cycle_admission_before_identity_creation()
    {
        var savedEvent = SavedEvent();
        var camera = new GuestCycleCamera(savedEvent.Camera);
        var fileSystem = new GuestCycleFileSystem(savedEvent);
        var orchestrator = new EventGuestCycleOrchestrator(
            fileSystem,
            camera,
            new RecordingCompositor(),
            new RecordingClock(new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero)));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ActivateAsync(orchestrator, savedEvent.Id, cancellationToken);
        fileSystem.BlockStorageProbe = true;

        var cycle = orchestrator.ExecuteAsync(new StartGuestCycle(), cancellationToken);
        await fileSystem.StorageProbeEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        var shutdown = orchestrator.ExecuteAsync(new ShutdownApplication(), cancellationToken);
        await Task.WhenAll(cycle, shutdown).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        Assert.Empty(fileSystem.CreatedGuestCycles);
        Assert.Null(camera.StreamId);
        Assert.Null(orchestrator.CurrentPresentation.ActiveEvent);
    }

    private static async Task ActivateAsync(
        EventGuestCycleOrchestrator orchestrator,
        EventId eventId,
        CancellationToken cancellationToken)
    {
        if (orchestrator.Camera is GuestCycleCamera camera && orchestrator.Clock is RecordingClock clock)
        {
            clock.DelayCompleted += (_, _) => camera.LatestFrameAt = clock.UtcNow;
        }

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
        private long sequence = 40;
        private CameraStreamFailure currentFailure;

        public event EventHandler? AvailableCamerasChanged
        {
            add { }
            remove { }
        }

        public event EventHandler? StreamHealthChanged;

        public IReadOnlyList<AvailableCamera> AvailableCameras { get; } =
            [new(binding.DeviceId, binding.DisplayName, null)];

        public string? StreamId { get; private set; }

        public int OpenCount { get; private set; }

        public int ReleaseCount { get; private set; }

        public List<CameraDeviceId> OpenedDeviceIds { get; } = [];

        public List<long> RequestedAfterSequences { get; } = [];

        public DateTimeOffset LatestFrameAt { get; set; } =
            new(2026, 8, 5, 1, 0, 0, TimeSpan.Zero);

        public int? FailOnceOnCaptureAttempt { get; init; }

        public CameraStreamFailure FailureOnFailedCapture { get; init; }

        public TimeSpan? LastCaptureTimeout { get; private set; }

        private int captureAttempts;

        public void DeliverFrameAtCountdownBoundary() => sequence++;

        public void ReportStreamFailure(CameraStreamFailure failure)
        {
            currentFailure = failure;
            StreamHealthChanged?.Invoke(this, EventArgs.Empty);
        }

        public CameraStreamHealth StreamHealth => new(
            binding.DeviceId,
            StreamId,
            sequence,
            LatestFrameAt,
            currentFailure);

        public Task StartDiscoveryAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<CameraOpenResult> OpenAsync(CameraDeviceId deviceId, CancellationToken cancellationToken)
        {
            OpenCount++;
            OpenedDeviceIds.Add(deviceId);
            StreamId = "stream-1";
            currentFailure = CameraStreamFailure.None;
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
                currentFailure = FailureOnFailedCapture;
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
            ReleaseCount++;
            StreamId = null;
            return Task.CompletedTask;
        }
    }

    private sealed class GuestCycleFileSystem(EventConfiguration savedEvent) : IEventFileSystem
    {
        public List<GuestCycleId> CreatedGuestCycles { get; } = [];
        public List<(int CaptureNumber, CaptureReference Reference)> CommittedCaptures { get; } = [];
        public List<CapturedFrame> CommittedFrames { get; } = [];
        public List<GuestCycleId> CompletedGuestCycles { get; } = [];
        public List<GuestCycleInterruption> Interruptions { get; } = [];

        public bool FailCompletionOnce { get; init; }

        public bool FailPhotoStripCommitOnce { get; init; }

        public bool FailInterruptionRecording { get; init; }

        public bool? StorageReadyAfterSuccessfulCompletion { get; init; }

        public int? FailCommitOnceOnCaptureNumber { get; init; }

        private bool captureCommitFailed;

        public bool BlockStorageProbe { get; set; }

        public TaskCompletionSource StorageProbeEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseStorageProbe { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CompletionAttempts { get; private set; }

        public int PhotoStripCommitAttempts { get; private set; }

        private bool storageReady = true;

        public GuestCycleRetryValidation NextRetryValidation { get; set; } = GuestCycleRetryValidation.Ready;

        public Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SavedEventSummary>>(
                [new(savedEvent.Id, savedEvent.Name, savedEvent.LastSavedAt)]);

        public Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken) =>
            Task.FromResult<EventConfiguration?>(savedEvent);

        public async Task<bool> ProbeStorageAsync(CancellationToken cancellationToken)
        {
            if (BlockStorageProbe)
            {
                StorageProbeEntered.TrySetResult();
                await ReleaseStorageProbe.Task.WaitAsync(cancellationToken);
            }

            return storageReady;
        }

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
            if (!captureCommitFailed && captureNumber == FailCommitOnceOnCaptureNumber)
            {
                captureCommitFailed = true;
                throw new IOException("Injected Capture commit failure.");
            }

            var reference = new CaptureReference($"capture-{captureNumber}.jpg");
            CommittedCaptures.Add((captureNumber, reference));
            CommittedFrames.Add(frame);
            return Task.FromResult(new CaptureCommitResult(true, reference));
        }

        public Task<PhotoStripCommitResult> CommitPhotoStripAsync(
            EventId eventId,
            GuestCycleId guestCycleId,
            PhotoStripCompositionResult composition,
            CancellationToken cancellationToken)
        {
            PhotoStripCommitAttempts++;
            if (FailPhotoStripCommitOnce && PhotoStripCommitAttempts == 1)
            {
                throw new IOException("Injected Photo Strip commit failure.");
            }

            return Task.FromResult(new PhotoStripCommitResult(true, "photo-strip.png"));
        }

        public Task RecordGuestCycleInterruptionAsync(
            EventId eventId,
            GuestCycleId guestCycleId,
            GuestCycleInterruption interruption,
            CancellationToken cancellationToken)
        {
            if (FailInterruptionRecording)
            {
                throw new IOException("Injected interruption-record failure.");
            }

            if (Interruptions.LastOrDefault() != interruption)
            {
                Interruptions.Add(interruption);
            }
            return Task.CompletedTask;
        }

        public Task<GuestCycleRetryValidation> PrepareGuestCycleRetryAsync(
            EventId eventId,
            GuestCycleId guestCycleId,
            IReadOnlyList<CaptureReference> completedCaptures,
            CancellationToken cancellationToken) => Task.FromResult(NextRetryValidation);

        public Task CompleteGuestCycleAsync(
            EventId eventId,
            GuestCycleId guestCycleId,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken)
        {
            CompletionAttempts++;
            if (FailCompletionOnce && CompletionAttempts == 1)
            {
                throw new IOException("Storage was unavailable while completing the Guest Cycle.");
            }

            CompletedGuestCycles.Add(guestCycleId);
            storageReady = StorageReadyAfterSuccessfulCompletion ?? storageReady;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCompositor : IPhotoStripCompositor
    {
        public PhotoStripCompositionRequest? LastRequest { get; private set; }

        public int ComposeCount { get; private set; }

        public bool FailOnce { get; init; }

        public Task<PhotoStripCompositionResult> ComposeAsync(
            PhotoStripCompositionRequest request,
            CancellationToken cancellationToken)
        {
            ComposeCount++;
            LastRequest = request;
            if (FailOnce && ComposeCount == 1)
            {
                throw new IOException("Injected Photo Strip composition failure.");
            }

            return Task.FromResult(new PhotoStripCompositionResult(true, new byte[] { 1, 2, 3 }, 600, 1800));
        }
    }

    private sealed class FixedGuestCycleIdentityGenerator(string value) : IGuestCycleIdentityGenerator
    {
        public GuestCycleId Create() => new(value);
    }

    private sealed class RecordingClock(DateTimeOffset utcNow) : IApplicationClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public List<TimeSpan> Delays { get; } = [];

        public event Action<TimeSpan, int>? DelayCompleted;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            UtcNow += delay;
            DelayCompleted?.Invoke(delay, Delays.Count);
            return Task.CompletedTask;
        }
    }
}
