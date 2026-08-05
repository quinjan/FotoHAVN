namespace FotoHavn.Core;

public sealed class EventGuestCycleOrchestrator
{
    private readonly IEventFileSystem fileSystem;
    private readonly IEventIdentityGenerator identityGenerator;
    private readonly IGuestCycleIdentityGenerator guestCycleIdentityGenerator;
    private readonly IActiveEventWakeLock wakeLock;
    private readonly object presentationLock = new();
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private readonly CancellationTokenSource shutdownCancellation = new();
    private EventSetupDraft? setup;
    private GuestCycleRun? guestCycle;
    private CancellationTokenSource? guestCycleAttemptCancellation;
    private CameraStreamFailure interruptedCameraFailure = CameraStreamFailure.None;
    private bool guestCycleCameraSensitive;
    private TaskCompletionSource<bool>? photoStripVisible;
    private ApplicationPresentation currentPresentation = CreateSavedEventsPresentation([]);

    public EventGuestCycleOrchestrator(
        IEventFileSystem fileSystem,
        ICameraBoundary camera,
        IPhotoStripCompositor compositor,
        IApplicationClock clock,
        IEventIdentityGenerator? identityGenerator = null,
        IActiveEventWakeLock? wakeLock = null,
        IGuestCycleIdentityGenerator? guestCycleIdentityGenerator = null)
    {
        this.fileSystem = fileSystem;
        Camera = camera;
        Compositor = compositor;
        Clock = clock;
        this.identityGenerator = identityGenerator ?? new UuidV7EventIdentityGenerator();
        this.wakeLock = wakeLock ?? new NoOpActiveEventWakeLock();
        this.guestCycleIdentityGenerator = guestCycleIdentityGenerator ?? new UuidV7GuestCycleIdentityGenerator();
        Camera.AvailableCamerasChanged += OnAvailableCamerasChanged;
        Camera.StreamHealthChanged += OnStreamHealthChanged;
    }

    public event EventHandler<ApplicationPresentation>? PresentationChanged;

    public ApplicationPresentation CurrentPresentation
    {
        get
        {
            lock (presentationLock)
            {
                return currentPresentation;
            }
        }
    }

    public ICameraBoundary Camera { get; }

    public IPhotoStripCompositor Compositor { get; }

    public IApplicationClock Clock { get; }

    public async Task<ApplicationPresentation> ExecuteAsync(
        ApplicationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command is ShutdownApplication)
        {
            shutdownCancellation.Cancel();
            photoStripVisible?.TrySetCanceled();
        }
        else if (command is ConfirmPhotoStripVisible)
        {
            photoStripVisible?.TrySetResult(true);
        }
        else if (command is ReportPhotoStripDecodeFailure)
        {
            photoStripVisible?.TrySetResult(false);
        }

        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var guestCycleCommandCancellation = command is StartGuestCycle or RetryGuestCycle
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, shutdownCancellation.Token)
                : null;
            if (guestCycleCommandCancellation is not null)
            {
                interruptedCameraFailure = CameraStreamFailure.None;
                guestCycleAttemptCancellation = guestCycleCommandCancellation;
            }
            var commandCancellationToken = guestCycleCommandCancellation?.Token ?? cancellationToken;

            if (CurrentPresentation.ActiveEvent is not null &&
                command is not ExitActiveEvent and
                not ConfirmExitActiveEvent and
                not CancelExitActiveEvent and
                not StartGuestCycle and
                not RetryGuestStartReadiness and
                not RetryGuestCycle and
                not ConfirmPhotoStripVisible and
                not ReportPhotoStripDecodeFailure and
                not ShutdownApplication)
            {
                return CurrentPresentation;
            }

            if (CurrentPresentation.ActiveEvent?.ShowsExitConfirmation == true &&
                command is not ConfirmExitActiveEvent and not CancelExitActiveEvent and not ShutdownApplication)
            {
                return CurrentPresentation;
            }

            if (CurrentPresentation.EventDeletion is { Stage: EventDeletionStage.Confirmation } &&
                command is not ConfirmDeleteSavedEvent and not CancelDeleteSavedEvent)
            {
                return CurrentPresentation;
            }

            if (CurrentPresentation.EventDeletion is
                { Stage: EventDeletionStage.CouldNotStart or EventDeletionStage.Incomplete or EventDeletionStage.Deleted } &&
                command is not DismissEventDeletionResult)
            {
                return CurrentPresentation;
            }

            if (setup?.Confirmation == EventSetupConfirmation.DiscardChanges &&
                command is not KeepEditingEventSetup and not DiscardEventSetupDraft)
            {
                return CurrentPresentation;
            }

            if (setup?.Confirmation is EventSetupConfirmation.SaveAndClose or EventSetupConfirmation.SaveAndStart &&
                command is not ConfirmEventSetupSave and not CancelEventSetupSave)
            {
                return CurrentPresentation;
            }

            var presentation = command switch
            {
                LaunchApplication => await LaunchAsync(cancellationToken).ConfigureAwait(false),
                OpenNewEvent => await OpenNewEventAsync(cancellationToken).ConfigureAwait(false),
                OpenSavedEvent open => await OpenSavedEventAsync(open.EventId, cancellationToken).ConfigureAwait(false),
                ChangeEventName change when setup is not null => PublishSetup(setup.WithName(change.Name)),
                ToggleCameraMenu when setup is not null => PublishSetup(setup.WithCameraMenuOpen(!setup.CameraMenuOpen)),
                DismissCameraMenu when setup is not null => PublishSetup(setup.WithCameraMenuOpen(false)),
                ChangeEventName or ToggleCameraMenu or DismissCameraMenu => CurrentPresentation,
                SelectCamera select => await SelectCameraAsync(select.DeviceId, cancellationToken).ConfigureAwait(false),
                SelectNoPrinter => PublishSetup(setup!.WithNoPrinterSelected()),
                RetryEventStorage when setup is not null => await RetryEventStorageAsync(cancellationToken).ConfigureAwait(false),
                RetryEventStorage => CurrentPresentation,
                CancelEventSetup => await CloseSetupAsync(releaseCamera: true, cancellationToken).ConfigureAwait(false),
                KeepEditingEventSetup when setup is not null => PublishSetup(setup with { Confirmation = EventSetupConfirmation.None }),
                DiscardEventSetupDraft => await CloseSetupAsync(releaseCamera: true, cancellationToken, force: true).ConfigureAwait(false),
                SaveAndCloseEventSetup => await RequestSaveSetupAsync(startEvent: false, cancellationToken).ConfigureAwait(false),
                SaveAndStartEvent => await RequestSaveSetupAsync(startEvent: true, cancellationToken).ConfigureAwait(false),
                ConfirmEventSetupSave when setup?.Confirmation == EventSetupConfirmation.SaveAndClose =>
                    await SaveSetupAsync(startEvent: false, cancellationToken).ConfigureAwait(false),
                ConfirmEventSetupSave when setup?.Confirmation == EventSetupConfirmation.SaveAndStart =>
                    await SaveSetupAsync(startEvent: true, cancellationToken).ConfigureAwait(false),
                CancelEventSetupSave when setup is not null => PublishSetup(setup with { Confirmation = EventSetupConfirmation.None }),
                ConfirmEventSetupSave or CancelEventSetupSave => CurrentPresentation,
                StartSavedEvent start => await RequestStartSavedEventAsync(start.EventId, cancellationToken).ConfigureAwait(false),
                ConfirmStartSavedEvent => await ConfirmStartSavedEventAsync(cancellationToken).ConfigureAwait(false),
                CancelStartSavedEvent => Publish(CurrentPresentation with { StartEventConfirmation = null }),
                ExitActiveEvent => Publish(CurrentPresentation with
                {
                    ActiveEvent = CurrentPresentation.ActiveEvent! with { ShowsExitConfirmation = true },
                }),
                CancelExitActiveEvent => Publish(CurrentPresentation with
                {
                    ActiveEvent = CurrentPresentation.ActiveEvent! with { ShowsExitConfirmation = false },
                }),
                ConfirmExitActiveEvent => await ExitActiveEventAsync(cancellationToken).ConfigureAwait(false),
                RetryGuestStartReadiness when CurrentPresentation.ActiveEvent is not null =>
                    await RetryGuestStartReadinessAsync(cancellationToken).ConfigureAwait(false),
                StartGuestCycle when CurrentPresentation.ActiveEvent?.GuestCycle.Phase is GuestCyclePhase.Start or GuestCyclePhase.StartUnavailable =>
                    await StartGuestCycleAsync(commandCancellationToken).ConfigureAwait(false),
                RetryGuestCycle when CurrentPresentation.ActiveEvent?.GuestCycle.Phase == GuestCyclePhase.OperatorAssistance =>
                    await RetryGuestCycleAsync(commandCancellationToken).ConfigureAwait(false),
                RetryGuestCycle when CurrentPresentation.ActiveEvent?.GuestCycle.Phase == GuestCyclePhase.StartUnavailable =>
                    await RetryGuestStartReadinessAsync(commandCancellationToken).ConfigureAwait(false),
                StartGuestCycle or RetryGuestStartReadiness or RetryGuestCycle or ConfirmPhotoStripVisible or ReportPhotoStripDecodeFailure => CurrentPresentation,
                ShutdownApplication => await ShutdownAsync(cancellationToken).ConfigureAwait(false),
                DeleteSavedEvent delete => RequestDeleteSavedEvent(delete.EventId),
                ConfirmDeleteSavedEvent => await ConfirmDeleteSavedEventAsync(cancellationToken).ConfigureAwait(false),
                CancelDeleteSavedEvent => Publish(CurrentPresentation with { EventDeletion = null }),
                RetryEventDeletion retry => await RetryEventDeletionAsync(retry.EventId, cancellationToken).ConfigureAwait(false),
                DismissEventDeletionResult => Publish(CurrentPresentation with { EventDeletion = null }),
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown application command."),
            };

            return presentation;
        }
        catch (OperationCanceledException) when (
            interruptedCameraFailure != CameraStreamFailure.None &&
            !shutdownCancellation.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            var captureNumber = Math.Min((guestCycle?.Captures.Count ?? 0) + 1, 4);
            return await PauseGuestCycleAsync(
                ResolveCameraFailureSource(GuestCycleFailureSource.CameraStale),
                GuestCycleInterruptedStep.Capture,
                captureNumber,
                GuestCycleFailure.CameraUnavailable,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            shutdownCancellation.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            photoStripVisible = null;
            return CurrentPresentation;
        }
        finally
        {
            guestCycleAttemptCancellation = null;
            interruptedCameraFailure = CameraStreamFailure.None;
            guestCycleCameraSensitive = false;
            commandGate.Release();
        }
    }

    private async Task<ApplicationPresentation> LaunchAsync(CancellationToken cancellationToken)
    {
        var savedEvents = await fileSystem.LoadEventsAsync(cancellationToken).ConfigureAwait(false);
        var quarantines = await fileSystem.LoadEventDeletionQuarantinesAsync(cancellationToken).ConfigureAwait(false);
        var quarantinedIds = quarantines.Select(item => item.EventId).ToHashSet();
        var tiles = new List<EventTilePresentation>(savedEvents.Count + 1)
        {
            new(EventTileKind.NewEvent, "New Event", "Set up a new booth run", "＋"),
        };

        tiles.AddRange(savedEvents
            .Where(savedEvent => !quarantinedIds.Contains(savedEvent.Id))
            .OrderByDescending(savedEvent => savedEvent.LastSavedAt)
            .Select(savedEvent => new EventTilePresentation(
                EventTileKind.SavedEvent,
                savedEvent.Name,
                FormatSavedAt(savedEvent.LastSavedAt),
                "▶",
                savedEvent.Id,
                savedEvent.LastSavedAt)));

        tiles.AddRange(quarantines
            .OrderByDescending(item => item.LastSavedAt)
            .Select(item => new EventTilePresentation(
                EventTileKind.SavedEvent,
                item.EventName,
                "Deletion incomplete",
                "!",
                item.EventId,
                item.LastSavedAt,
                DeletionIncomplete: true)));

        return Publish(CreateSavedEventsPresentation(tiles));
    }

    private async Task<ApplicationPresentation> OpenNewEventAsync(CancellationToken cancellationToken)
    {
        await Camera.StartDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        var storageReady = await fileSystem.ProbeStorageAsync(cancellationToken).ConfigureAwait(false);
        setup = EventSetupDraft.New(storageReady);
        return PublishSetup(setup);
    }

    private async Task<ApplicationPresentation> OpenSavedEventAsync(EventId eventId, CancellationToken cancellationToken)
    {
        var savedEvent = await DiscoverSavedEventAsync(eventId, cancellationToken).ConfigureAwait(false);
        setup = await CreateSavedEventDraftAsync(savedEvent, cancellationToken).ConfigureAwait(false);
        if (savedEvent.AvailableCamera is null)
        {
            return PublishSetup(setup);
        }

        PublishSetup(setup.WithCameraState(CameraConnectionState.Connecting));
        var state = await OpenCameraAsync(savedEvent.Configuration.Camera.DeviceId, cancellationToken).ConfigureAwait(false);
        return PublishSetup(setup.WithCameraState(state));
    }

    private async Task<ApplicationPresentation> SelectCameraAsync(CameraDeviceId deviceId, CancellationToken cancellationToken)
    {
        var camera = Camera.AvailableCameras.FirstOrDefault(item => item.DeviceId == deviceId)
            ?? throw new ArgumentException("The Camera is not currently available.", nameof(deviceId));

        if (setup!.SelectedCamera is not null)
        {
            await Camera.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        }

        PublishSetup(setup.WithSelectedCamera(camera, CameraConnectionState.Connecting));
        var state = await OpenCameraAsync(deviceId, cancellationToken).ConfigureAwait(false);
        return PublishSetup(setup.WithCameraState(state));
    }

    private async Task<CameraConnectionState> OpenCameraAsync(
        CameraDeviceId deviceId,
        CancellationToken cancellationToken)
    {
        var result = await Camera.OpenAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (result == CameraOpenResult.Ready && Camera.AvailableCameras.All(item => item.DeviceId != deviceId))
        {
            result = CameraOpenResult.Disconnected;
        }

        return ToConnectionState(result);
    }

    private Task<ApplicationPresentation> RequestSaveSetupAsync(bool startEvent, CancellationToken cancellationToken)
    {
        var draft = setup ?? throw new InvalidOperationException("Event setup is not open.");
        if (startEvent ? !draft.CanStart : !draft.CanSave)
        {
            throw new InvalidOperationException($"Event setup is not ready to {(startEvent ? "start" : "save")}.");
        }

        return draft.EventId is null || !draft.IsDirty
            ? SaveSetupAsync(startEvent, cancellationToken)
            : Task.FromResult(PublishSetup(draft with
            {
                Confirmation = startEvent
                    ? EventSetupConfirmation.SaveAndStart
                    : EventSetupConfirmation.SaveAndClose,
            }));
    }

    private async Task<ApplicationPresentation> RetryEventStorageAsync(CancellationToken cancellationToken)
    {
        var storageReady = await fileSystem.ProbeStorageAsync(cancellationToken).ConfigureAwait(false);
        return PublishSetup(setup!.WithStorageReady(storageReady));
    }

    private async Task<ApplicationPresentation> SaveSetupAsync(bool startEvent, CancellationToken cancellationToken)
    {
        var draft = setup ?? throw new InvalidOperationException("Event setup is not open.");
        if (startEvent ? !draft.CanStart : !draft.CanSave)
        {
            throw new InvalidOperationException($"Event setup is not ready to {(startEvent ? "start" : "save")}.");
        }

        if (startEvent && !await fileSystem.ProbeStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            return PublishSetup(draft with
            {
                StorageReady = false,
                Confirmation = EventSetupConfirmation.None,
            });
        }

        var streamId = startEvent ? Camera.StreamId : null;
        if (startEvent && streamId is null)
        {
            return PublishSetup(draft.WithCameraState(CameraConnectionState.Disconnected) with
            {
                Confirmation = EventSetupConfirmation.None,
            });
        }

        var lastSavedAt = Clock.UtcNow.ToUniversalTime();
        var mode = draft.EventId is null ? EventSaveMode.CreateNew : EventSaveMode.UpdateExisting;
        EventConfiguration configuration;
        while (true)
        {
            var eventId = draft.EventId ?? identityGenerator.Create();
            var cameraBinding = draft.IsCameraDirty || draft.Baseline.Camera is null
                ? new CameraBinding(draft.SelectedCamera!.DeviceId, draft.SelectedCamera.DisplayName)
                : draft.Baseline.Camera;
            configuration = new EventConfiguration(
                eventId,
                draft.EventName.Trim(),
                cameraBinding,
                PrinterChoice.NoPrinter,
                draft.CreatedAt ?? lastSavedAt,
                lastSavedAt);
            var result = await fileSystem.SaveEventAtomicallyAsync(configuration, mode, cancellationToken).ConfigureAwait(false);
            if (result == EventSaveResult.Saved)
            {
                break;
            }

            if (mode != EventSaveMode.CreateNew)
            {
                throw new IOException($"The existing Event '{eventId}' could not be saved.");
            }
        }

        if (startEvent)
        {
            if (Camera.StreamId != streamId)
            {
                var availableCamera = Camera.AvailableCameras.FirstOrDefault(
                    item => item.DeviceId == configuration.Camera.DeviceId);
                setup = CreateSavedEventDraft(
                        new SavedEventDiscovery(configuration, availableCamera),
                        storageReady: true)
                    .WithCameraState(CameraConnectionState.Disconnected);
                return PublishSetup(setup);
            }

            await wakeLock.AcquireAsync(cancellationToken).ConfigureAwait(false);
            setup = null;
            return Publish(CurrentPresentation with
            {
                Setup = null,
                ActiveEvent = CreateActiveEventPresentation(configuration, streamId!, storageReady: true),
            });
        }

        await CloseSetupAsync(releaseCamera: true, cancellationToken, force: true).ConfigureAwait(false);
        return await LaunchAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApplicationPresentation> CloseSetupAsync(
        bool releaseCamera,
        CancellationToken cancellationToken,
        bool force = false)
    {
        if (!force && setup is { IsDirty: true, Confirmation: EventSetupConfirmation.None })
        {
            return PublishSetup(setup with { Confirmation = EventSetupConfirmation.DiscardChanges });
        }

        if (releaseCamera && setup?.SelectedCamera is not null)
        {
            await Camera.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        }

        setup = null;
        return Publish(CurrentPresentation with { Setup = null });
    }

    private ApplicationPresentation RequestDeleteSavedEvent(EventId eventId)
    {
        var tile = CurrentPresentation.EventTiles.SingleOrDefault(item => item.EventId == eventId && !item.DeletionIncomplete)
            ?? throw new InvalidOperationException($"Event '{eventId}' is not available for deletion.");
        return Publish(CurrentPresentation with
        {
            EventDeletion = new EventDeletionPresentation(eventId, tile.Label, EventDeletionStage.Confirmation),
        });
    }

    private async Task<ApplicationPresentation> ConfirmDeleteSavedEventAsync(CancellationToken cancellationToken)
    {
        var deletion = CurrentPresentation.EventDeletion
            ?? throw new InvalidOperationException("No Event is awaiting deletion confirmation.");
        var tile = CurrentPresentation.EventTiles.Single(item => item.EventId == deletion.EventId);
        return await QuarantineAndDeleteEventAsync(
            deletion.EventId,
            deletion.EventName,
            tile.LastSavedAt ?? Clock.UtcNow,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApplicationPresentation> RetryEventDeletionAsync(
        EventId eventId,
        CancellationToken cancellationToken)
    {
        var tile = CurrentPresentation.EventTiles.SingleOrDefault(item => item.EventId == eventId && item.DeletionIncomplete)
            ?? throw new InvalidOperationException($"Event '{eventId}' is not quarantined.");
        return await QuarantineAndDeleteEventAsync(
            eventId,
            tile.Label,
            tile.LastSavedAt ?? Clock.UtcNow,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApplicationPresentation> QuarantineAndDeleteEventAsync(
        EventId eventId,
        string eventName,
        DateTimeOffset lastSavedAt,
        CancellationToken cancellationToken)
    {
        Publish(CurrentPresentation with
        {
            EventDeletion = new EventDeletionPresentation(eventId, eventName, EventDeletionStage.Deleting),
        });

        try
        {
            await fileSystem.QuarantineEventForDeletionAsync(
                new EventDeletionQuarantine(eventId, eventName, lastSavedAt),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Publish(CurrentPresentation with
            {
                EventDeletion = new EventDeletionPresentation(eventId, eventName, EventDeletionStage.CouldNotStart),
            });
        }

        var result = await fileSystem.DeleteQuarantinedEventAsync(eventId, cancellationToken).ConfigureAwait(false);
        var refreshed = await LaunchAsync(cancellationToken).ConfigureAwait(false);
        return Publish(refreshed with
        {
            EventDeletion = new EventDeletionPresentation(
                eventId,
                eventName,
                result == EventDeletionResult.Deleted ? EventDeletionStage.Deleted : EventDeletionStage.Incomplete),
        });
    }

    private async Task<ApplicationPresentation> RequestStartSavedEventAsync(
        EventId eventId,
        CancellationToken cancellationToken)
    {
        var configuration = await fileSystem.LoadEventAsync(eventId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Event '{eventId}' was not found.");
        return Publish(CurrentPresentation with
        {
            StartEventConfirmation = new StartEventConfirmationPresentation(configuration.Id, configuration.Name),
        });
    }

    private async Task<ApplicationPresentation> ExitActiveEventAsync(CancellationToken cancellationToken)
    {
        if (CurrentPresentation.ActiveEvent?.GuestCycle.Phase is not (GuestCyclePhase.Start or GuestCyclePhase.StartUnavailable))
        {
            return CurrentPresentation;
        }

        await ReleaseActiveEventResourcesAsync(cancellationToken).ConfigureAwait(false);
        Publish(CurrentPresentation with { ActiveEvent = null });
        return await LaunchAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApplicationPresentation> StartGuestCycleAsync(CancellationToken cancellationToken)
    {
        var activeEvent = CurrentPresentation.ActiveEvent
            ?? throw new InvalidOperationException("No Event is Active.");
        bool storageReady;
        try
        {
            storageReady = await fileSystem.ProbeEventStorageAsync(activeEvent.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            storageReady = false;
        }

        if (!IsCameraReady(activeEvent.Camera, activeEvent.CameraStreamId, Clock.UtcNow))
        {
            return PublishGuestCycle(new GuestCyclePresentation(
                GuestCyclePhase.StartUnavailable,
                Failure: GuestCycleFailure.CameraUnavailable));
        }

        if (!storageReady)
        {
            return PublishGuestCycle(new GuestCyclePresentation(
                GuestCyclePhase.StartUnavailable,
                Failure: GuestCycleFailure.StorageUnavailable));
        }

        while (true)
        {
            var guestCycleId = guestCycleIdentityGenerator.Create();
            GuestCycleCreateResult createResult;
            try
            {
                createResult = await fileSystem.CreateGuestCycleAsync(
                    activeEvent.Id,
                    guestCycleId,
                    Clock.UtcNow,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return PublishGuestCycle(new GuestCyclePresentation(
                    GuestCyclePhase.StartUnavailable,
                    Failure: GuestCycleFailure.StorageUnavailable));
            }
            if (createResult == GuestCycleCreateResult.Created)
            {
                guestCycle = new GuestCycleRun(activeEvent.Id, guestCycleId, []);
                break;
            }
        }

        return await RunGuestCycleAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApplicationPresentation> RunGuestCycleAsync(CancellationToken cancellationToken)
    {
        var activeEvent = CurrentPresentation.ActiveEvent
            ?? throw new InvalidOperationException("No Event is Active.");
        var run = guestCycle ?? throw new InvalidOperationException("No Guest Cycle is in progress.");

        for (var captureNumber = run.Captures.Count + 1; captureNumber <= 4; captureNumber++)
        {
            if (Camera.StreamHealth.Failure != CameraStreamFailure.None)
            {
                return await PauseGuestCycleAsync(
                    ResolveCameraFailureSource(GuestCycleFailureSource.CameraStale),
                    GuestCycleInterruptedStep.Capture,
                    captureNumber,
                    GuestCycleFailure.CameraUnavailable,
                    cancellationToken).ConfigureAwait(false);
            }

            guestCycleCameraSensitive = true;
            for (var remaining = 5; remaining >= 1; remaining--)
            {
                PublishGuestCycle(new GuestCyclePresentation(
                    GuestCyclePhase.Countdown,
                    captureNumber,
                    run.Captures.Count,
                    remaining));
                await Clock.DelayAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }

            CapturedFrame? frame;
            try
            {
                frame = await Camera.CaptureFirstFreshFrameAsync(
                    TimeSpan.FromSeconds(2),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                guestCycleCameraSensitive = false;
                return await PauseGuestCycleAsync(
                    ResolveCameraFailureSource(GuestCycleFailureSource.JpegEncoding),
                    GuestCycleInterruptedStep.Capture,
                    captureNumber,
                    GuestCycleFailure.CameraUnavailable,
                    cancellationToken).ConfigureAwait(false);
            }
            guestCycleCameraSensitive = false;
            if (interruptedCameraFailure != CameraStreamFailure.None)
            {
                return await PauseGuestCycleAsync(
                    ResolveCameraFailureSource(GuestCycleFailureSource.CameraStale),
                    GuestCycleInterruptedStep.Capture,
                    captureNumber,
                    GuestCycleFailure.CameraUnavailable,
                    cancellationToken).ConfigureAwait(false);
            }
            if (frame is null)
            {
                return await PauseGuestCycleAsync(
                    ResolveCameraFailureSource(GuestCycleFailureSource.FreshFrameTimeout),
                    GuestCycleInterruptedStep.Capture,
                    captureNumber,
                    GuestCycleFailure.CameraUnavailable,
                    cancellationToken).ConfigureAwait(false);
            }

            CaptureCommitResult committed;
            try
            {
                committed = await fileSystem.CommitCaptureAsync(
                    activeEvent.Id,
                    run.Id,
                    captureNumber,
                    frame,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return await PauseGuestCycleAsync(
                    GuestCycleFailureSource.Storage,
                    GuestCycleInterruptedStep.Capture,
                    captureNumber,
                    GuestCycleFailure.StorageUnavailable,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!committed.Committed)
            {
                return await PauseGuestCycleAsync(
                    GuestCycleFailureSource.JpegValidation,
                    GuestCycleInterruptedStep.Capture,
                    captureNumber,
                    GuestCycleFailure.StorageUnavailable,
                    cancellationToken).ConfigureAwait(false);
            }

            run.Captures.Add(committed.Capture);
            PublishGuestCycle(new GuestCyclePresentation(
                GuestCyclePhase.Flash,
                captureNumber,
                run.Captures.Count));
            await Clock.DelayAsync(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
            PublishGuestCycle(new GuestCyclePresentation(
                GuestCyclePhase.CaptureSaved,
                captureNumber,
                run.Captures.Count));
            await Clock.DelayAsync(TimeSpan.FromMilliseconds(900), cancellationToken).ConfigureAwait(false);
        }

        if (run.PhotoStripPath is null)
        {
            PhotoStripCompositionResult composition;
            try
            {
                composition = await Compositor.ComposeAsync(
                    new PhotoStripCompositionRequest(activeEvent.Name, run.Captures),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return await PauseGuestCycleAsync(
                    GuestCycleFailureSource.Storage,
                    GuestCycleInterruptedStep.PhotoStrip,
                    4,
                    GuestCycleFailure.StorageUnavailable,
                    cancellationToken).ConfigureAwait(false);
            }
            if (!composition.IsAvailable)
            {
                return await PauseGuestCycleAsync(
                    GuestCycleFailureSource.Storage,
                    GuestCycleInterruptedStep.PhotoStrip,
                    4,
                    GuestCycleFailure.StorageUnavailable,
                    cancellationToken).ConfigureAwait(false);
            }

            PhotoStripCommitResult strip;
            try
            {
                strip = await fileSystem.CommitPhotoStripAsync(
                    activeEvent.Id,
                    run.Id,
                    composition,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return await PauseGuestCycleAsync(
                    GuestCycleFailureSource.Storage,
                    GuestCycleInterruptedStep.PhotoStrip,
                    4,
                    GuestCycleFailure.StorageUnavailable,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!strip.Committed)
            {
                return await PauseGuestCycleAsync(
                    GuestCycleFailureSource.Storage,
                    GuestCycleInterruptedStep.PhotoStrip,
                    4,
                    GuestCycleFailure.StorageUnavailable,
                    cancellationToken).ConfigureAwait(false);
            }

            run.PhotoStripPath = strip.ArtifactPath;
        }

        if (!run.PreviewCompleted)
        {
            photoStripVisible = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            PublishGuestCycle(new GuestCyclePresentation(
                GuestCyclePhase.PhotoStripPreview,
                CaptureNumber: 4,
                CompletedCaptures: 4,
                PhotoStripPath: run.PhotoStripPath,
                PreviewSecondsRemaining: 10));
            bool visible;
            try
            {
                visible = await photoStripVisible.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                photoStripVisible = null;
            }
            if (!visible)
            {
                return await PauseGuestCycleAsync(
                    GuestCycleFailureSource.Storage,
                    GuestCycleInterruptedStep.Preview,
                    4,
                    GuestCycleFailure.StorageUnavailable,
                    cancellationToken).ConfigureAwait(false);
            }

            PublishGuestCycle(new GuestCyclePresentation(
                GuestCyclePhase.PhotoStripPreview,
                CaptureNumber: 4,
                CompletedCaptures: 4,
                PhotoStripPath: run.PhotoStripPath,
                PreviewSecondsRemaining: 10));
            for (var remaining = 10; remaining >= 1; remaining--)
            {
                PublishGuestCycle(new GuestCyclePresentation(
                    GuestCyclePhase.PhotoStripPreview,
                    CaptureNumber: 4,
                    CompletedCaptures: 4,
                    PhotoStripPath: run.PhotoStripPath,
                    PreviewSecondsRemaining: remaining));
                await Clock.DelayAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }

            PublishGuestCycle(new GuestCyclePresentation(
                GuestCyclePhase.Fading,
                CaptureNumber: 4,
                CompletedCaptures: 4,
                PhotoStripPath: run.PhotoStripPath));
            await Clock.DelayAsync(TimeSpan.FromMilliseconds(450), cancellationToken).ConfigureAwait(false);
            run.PreviewCompleted = true;
        }

        try
        {
            await fileSystem.CompleteGuestCycleAsync(
                activeEvent.Id,
                run.Id,
                Clock.UtcNow,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return await PauseGuestCycleAsync(
                GuestCycleFailureSource.Storage,
                GuestCycleInterruptedStep.Completion,
                4,
                GuestCycleFailure.StorageUnavailable,
                cancellationToken).ConfigureAwait(false);
        }

        guestCycle = null;
        return PublishGuestCycle(GuestCyclePresentation.Start);
    }

    private async Task<ApplicationPresentation> RetryGuestCycleAsync(CancellationToken cancellationToken)
    {
        var activeEvent = CurrentPresentation.ActiveEvent
            ?? throw new InvalidOperationException("No Event is Active.");
        var run = guestCycle ?? throw new InvalidOperationException("No Guest Cycle is in progress.");
        if (run.EventId != activeEvent.Id)
        {
            return CurrentPresentation;
        }
        var interrupted = activeEvent.GuestCycle;

        try
        {
            if (!await fileSystem.ProbeEventStorageAsync(activeEvent.Id, cancellationToken).ConfigureAwait(false) ||
                run.LastInterruption is null)
            {
                return CurrentPresentation;
            }

            await fileSystem.RecordGuestCycleInterruptionAsync(
                activeEvent.Id,
                run.Id,
                run.LastInterruption,
                cancellationToken).ConfigureAwait(false);
            if (await fileSystem.PrepareGuestCycleRetryAsync(
                    activeEvent.Id,
                    run.Id,
                    run.Captures,
                    cancellationToken).ConfigureAwait(false) != GuestCycleRetryValidation.Ready)
            {
                return CurrentPresentation;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return CurrentPresentation;
        }

        if (interrupted.Failure != GuestCycleFailure.CameraUnavailable &&
            IsCameraReady(activeEvent.Camera, activeEvent.CameraStreamId, Clock.UtcNow))
        {
            return await RunGuestCycleAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await Camera.ReleaseAsync(cancellationToken).ConfigureAwait(false);
            if (Camera.AvailableCameras.All(camera => camera.DeviceId != activeEvent.Camera.DeviceId))
            {
                return CurrentPresentation;
            }

            var result = await Camera.OpenAsync(activeEvent.Camera.DeviceId, cancellationToken).ConfigureAwait(false);
            var streamId = Camera.StreamId;
            var streamHealth = Camera.StreamHealth;
            if (result != CameraOpenResult.Ready ||
                streamId is null ||
                streamHealth.DeviceId != activeEvent.Camera.DeviceId ||
                streamHealth.StreamId != streamId ||
                streamHealth.LatestFrameAt is null ||
                streamHealth.Failure != CameraStreamFailure.None)
            {
                return CurrentPresentation;
            }

            Publish(CurrentPresentation with
            {
                ActiveEvent = activeEvent with { CameraStreamId = streamId },
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CurrentPresentation;
        }

        return await RunGuestCycleAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApplicationPresentation> PauseGuestCycleAsync(
        GuestCycleFailureSource source,
        GuestCycleInterruptedStep step,
        int captureNumber,
        GuestCycleFailure failure,
        CancellationToken cancellationToken)
    {
        var activeEvent = CurrentPresentation.ActiveEvent
            ?? throw new InvalidOperationException("No Event is Active.");
        var run = guestCycle ?? throw new InvalidOperationException("No Guest Cycle is in progress.");
        var interruption = new GuestCycleInterruption(
            source,
            step,
            captureNumber,
            run.Captures.Count,
            Clock.UtcNow);
        run.LastInterruption = interruption;
        try
        {
            await fileSystem.RecordGuestCycleInterruptionAsync(
                activeEvent.Id,
                run.Id,
                interruption,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // Operator Assistance still preserves the in-process checkpoint when storage cannot record the failure.
        }

        return PublishGuestCycle(new GuestCyclePresentation(
            GuestCyclePhase.OperatorAssistance,
            captureNumber,
            run.Captures.Count,
            Failure: failure));
    }

    private GuestCycleFailureSource ResolveCameraFailureSource(GuestCycleFailureSource fallback) =>
        Camera.StreamHealth.Failure switch
        {
            CameraStreamFailure.Removed => GuestCycleFailureSource.CameraRemoved,
            CameraStreamFailure.StreamFailure => GuestCycleFailureSource.CameraStreamFailure,
            CameraStreamFailure.ExclusiveOwnershipLost => GuestCycleFailureSource.CameraExclusiveOwnershipLost,
            CameraStreamFailure.Stale => GuestCycleFailureSource.CameraStale,
            _ => fallback,
        };

    private ApplicationPresentation PublishGuestCycle(GuestCyclePresentation cycle)
    {
        var activeEvent = CurrentPresentation.ActiveEvent
            ?? throw new InvalidOperationException("No Event is Active.");
        var guestStart = cycle.Phase switch
        {
            GuestCyclePhase.Start => GuestStartPresentation.FromReadiness(true, true),
            GuestCyclePhase.StartUnavailable when cycle.Failure == GuestCycleFailure.CameraUnavailable =>
                GuestStartPresentation.FromReadiness(false, activeEvent.GuestStart.IsStorageReady),
            GuestCyclePhase.StartUnavailable when cycle.Failure == GuestCycleFailure.StorageUnavailable =>
                GuestStartPresentation.FromReadiness(true, false),
            _ => activeEvent.GuestStart,
        };
        return Publish(CurrentPresentation with
        {
            ActiveEvent = activeEvent with
            {
                GuestStartState = guestStart,
                Cycle = cycle,
            },
        });
    }

    private async Task<ApplicationPresentation> ShutdownAsync(CancellationToken cancellationToken)
    {
        if (CurrentPresentation.ActiveEvent is not null)
        {
            await ReleaseActiveEventResourcesAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (setup?.SelectedCamera is not null)
        {
            await Camera.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        }

        setup = null;
        guestCycle = null;
        photoStripVisible = null;
        return Publish(CreateSavedEventsPresentation([]));
    }

    private async Task ReleaseActiveEventResourcesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Camera.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await wakeLock.ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<ApplicationPresentation> ConfirmStartSavedEventAsync(CancellationToken cancellationToken)
    {
        var eventId = CurrentPresentation.StartEventConfirmation?.EventId
            ?? throw new InvalidOperationException("No saved Event is awaiting confirmation.");
        var savedEvent = await DiscoverSavedEventAsync(eventId, cancellationToken).ConfigureAwait(false);
        if (savedEvent.AvailableCamera is null)
        {
            var failedSetup = await CreateSavedEventDraftAsync(savedEvent, cancellationToken).ConfigureAwait(false);
            return PublishFailedStart(failedSetup);
        }

        var result = await Camera.OpenAsync(savedEvent.Configuration.Camera.DeviceId, cancellationToken).ConfigureAwait(false);
        if (result != CameraOpenResult.Ready || Camera.StreamId is not { } streamId)
        {
            var state = result == CameraOpenResult.Ready
                ? CameraConnectionState.Disconnected
                : ToConnectionState(result);
            var failedSetup = (await CreateSavedEventDraftAsync(savedEvent, cancellationToken).ConfigureAwait(false))
                .WithCameraState(state);
            return PublishFailedStart(failedSetup);
        }

        var storageReady = await fileSystem.ProbeStorageAsync(cancellationToken).ConfigureAwait(false);
        if (!storageReady)
        {
            var failedSetup = EventSetupDraft.From(savedEvent.Configuration, savedEvent.AvailableCamera, storageReady)
                .WithCameraState(CameraConnectionState.Ready);
            return PublishFailedStart(failedSetup);
        }

        await wakeLock.AcquireAsync(cancellationToken).ConfigureAwait(false);
        return Publish(CurrentPresentation with
        {
            Setup = null,
            StartEventConfirmation = null,
            ActiveEvent = CreateActiveEventPresentation(savedEvent.Configuration, streamId, storageReady: true),
        });
    }

    private ActiveEventPresentation CreateActiveEventPresentation(
        EventConfiguration configuration,
        string streamId,
        bool storageReady) =>
        CreateActiveEventPresentationCore(configuration, streamId, storageReady);

    private ActiveEventPresentation CreateActiveEventPresentationCore(
        EventConfiguration configuration,
        string streamId,
        bool storageReady)
    {
        var guestStart = CreateGuestStartPresentation(configuration.Camera, streamId, storageReady);
        return new ActiveEventPresentation(
            configuration.Id,
            configuration.Name,
            configuration.Camera,
            streamId,
            GuestStartState: guestStart,
            Cycle: ToGuestCyclePresentation(guestStart));
    }

    private GuestStartPresentation CreateGuestStartPresentation(
        CameraBinding binding,
        string streamId,
        bool storageReady)
    {
        var cameraReady = IsCameraReady(binding, streamId, Clock.UtcNow);
        return GuestStartPresentation.FromReadiness(cameraReady, storageReady);
    }

    private bool IsCameraReady(CameraBinding binding, string streamId, DateTimeOffset now)
    {
        var health = Camera.StreamHealth;
        return health.DeviceId == binding.DeviceId &&
            health.StreamId == streamId &&
            health.Failure == CameraStreamFailure.None &&
            health.LatestFrameAt is { } latestFrameAt &&
            latestFrameAt <= now &&
            now - latestFrameAt <= TimeSpan.FromSeconds(2);
    }

    private static GuestCyclePresentation ToGuestCyclePresentation(GuestStartPresentation guestStart) =>
        guestStart.IsStartEnabled
            ? GuestCyclePresentation.Start
            : new GuestCyclePresentation(
                GuestCyclePhase.StartUnavailable,
                Failure: guestStart.IsCameraReady
                    ? GuestCycleFailure.StorageUnavailable
                    : GuestCycleFailure.CameraUnavailable);

    private async Task<ApplicationPresentation> CheckGuestStartAdmissionAsync(CancellationToken cancellationToken)
    {
        var activeEvent = CurrentPresentation.ActiveEvent
            ?? throw new InvalidOperationException("No Event is Active.");
        var storageReady = await fileSystem.ProbeStorageAsync(cancellationToken).ConfigureAwait(false);
        var guestStart = CreateGuestStartPresentation(
            activeEvent.Camera,
            activeEvent.CameraStreamId,
            storageReady);
        if (guestStart.IsStartEnabled)
        {
            return CurrentPresentation;
        }

        return Publish(CurrentPresentation with
        {
            ActiveEvent = activeEvent with
            {
                GuestStartState = guestStart,
                Cycle = ToGuestCyclePresentation(guestStart),
            },
        });
    }

    private async Task<ApplicationPresentation> RetryGuestStartReadinessAsync(CancellationToken cancellationToken)
    {
        var activeEvent = CurrentPresentation.ActiveEvent
            ?? throw new InvalidOperationException("No Event is Active.");
        var streamId = activeEvent.CameraStreamId;
        var cameraReady = IsCameraReady(activeEvent.Camera, streamId, Clock.UtcNow);
        if (!cameraReady)
        {
            await Camera.ReleaseAsync(cancellationToken).ConfigureAwait(false);
            await Camera.StartDiscoveryAsync(cancellationToken).ConfigureAwait(false);
            if (Camera.AvailableCameras.All(camera => camera.DeviceId != activeEvent.Camera.DeviceId))
            {
                var missing = GuestStartPresentation.FromReadiness(
                    isCameraReady: false,
                    activeEvent.GuestStart.IsStorageReady,
                    requiresEventSetupCorrection: true);
                return Publish(CurrentPresentation with
                {
                    ActiveEvent = activeEvent with
                    {
                        GuestStartState = missing,
                        Cycle = ToGuestCyclePresentation(missing),
                    },
                });
            }

            var openResult = await Camera.OpenAsync(activeEvent.Camera.DeviceId, cancellationToken).ConfigureAwait(false);
            if (openResult == CameraOpenResult.Ready && Camera.StreamId is { } newStreamId)
            {
                streamId = newStreamId;
                cameraReady = IsCameraReady(activeEvent.Camera, streamId, Clock.UtcNow);
            }
        }

        var storageReady = activeEvent.GuestStart.IsStorageReady ||
            await fileSystem.ProbeStorageAsync(cancellationToken).ConfigureAwait(false);
        var guestStart = GuestStartPresentation.FromReadiness(cameraReady, storageReady);
        return Publish(CurrentPresentation with
        {
            ActiveEvent = activeEvent with
            {
                CameraStreamId = streamId,
                GuestStartState = guestStart,
                Cycle = ToGuestCyclePresentation(guestStart),
            },
        });
    }

    private ApplicationPresentation PublishFailedStart(EventSetupDraft failedSetup)
    {
        var failed = PublishSetup(failedSetup);
        return Publish(failed with { StartEventConfirmation = null });
    }

    private async Task<SavedEventDiscovery> DiscoverSavedEventAsync(
        EventId eventId,
        CancellationToken cancellationToken)
    {
        var configuration = await fileSystem.LoadEventAsync(eventId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Event '{eventId}' was not found.");
        await Camera.StartDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        var available = Camera.AvailableCameras.FirstOrDefault(camera => camera.DeviceId == configuration.Camera.DeviceId);
        return new SavedEventDiscovery(configuration, available);
    }

    private async Task<EventSetupDraft> CreateSavedEventDraftAsync(
        SavedEventDiscovery savedEvent,
        CancellationToken cancellationToken)
    {
        var storageReady = await fileSystem.ProbeStorageAsync(cancellationToken).ConfigureAwait(false);
        return CreateSavedEventDraft(savedEvent, storageReady);
    }

    private static EventSetupDraft CreateSavedEventDraft(SavedEventDiscovery savedEvent, bool storageReady)
    {
        var selected = savedEvent.AvailableCamera ?? new AvailableCamera(
            savedEvent.Configuration.Camera.DeviceId,
            savedEvent.Configuration.Camera.DisplayName,
            savedEvent.Configuration.Camera.DeviceId.Value,
            CameraAvailability.Unavailable);
        return EventSetupDraft.From(savedEvent.Configuration, selected, storageReady);
    }

    private void OnAvailableCamerasChanged(object? sender, EventArgs args)
    {
        if (setup is null)
        {
            return;
        }

        var selected = setup.SelectedCamera;
        if (selected is not null && Camera.AvailableCameras.All(camera => camera.DeviceId != selected.DeviceId))
        {
            selected = selected with { Availability = CameraAvailability.Unavailable };
            setup = setup.WithSelectedCamera(selected, CameraConnectionState.Disconnected);
        }

        PublishSetup(setup);
    }

    private void OnStreamHealthChanged(object? sender, EventArgs args)
    {
        var activeEvent = CurrentPresentation.ActiveEvent;
        var streamFailure = Camera.StreamHealth.Failure;
        if (activeEvent?.GuestCycle is
                { Phase: GuestCyclePhase.Countdown or GuestCyclePhase.Flash or GuestCyclePhase.CaptureSaved, CompletedCaptures: < 4 } &&
            guestCycleCameraSensitive &&
            streamFailure != CameraStreamFailure.None)
        {
            interruptedCameraFailure = streamFailure;
            try
            {
                guestCycleAttemptCancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The attempt completed while the Camera notification was being delivered.
            }
            return;
        }

        if (activeEvent is null ||
            activeEvent.GuestCycle.Phase is not (GuestCyclePhase.Start or GuestCyclePhase.StartUnavailable))
        {
            return;
        }

        var guestStart = CreateGuestStartPresentation(
            activeEvent.Camera,
            activeEvent.CameraStreamId,
            activeEvent.GuestStart.IsStorageReady);
        Publish(CurrentPresentation with
        {
            ActiveEvent = activeEvent with
            {
                GuestStartState = guestStart,
                Cycle = ToGuestCyclePresentation(guestStart),
            },
        });
    }

    private ApplicationPresentation PublishSetup(EventSetupDraft draft)
    {
        setup = draft;
        var cameras = Disambiguate(Camera.AvailableCameras);
        var selected = draft.SelectedCamera is null
            ? null
            : cameras.FirstOrDefault(camera => camera.DeviceId == draft.SelectedCamera.DeviceId) ?? draft.SelectedCamera;
        var presentation = new EventSetupPresentation(
            IsOpen: true,
            IsBackdropInert: true,
            ShowsCameraTuning: false,
            draft.EventName,
            cameras,
            selected,
            draft.CameraState,
            draft.CameraState == CameraConnectionState.Ready,
            new CameraMenuPresentation(draft.CameraMenuOpen, IsAnchoredOverlay: true, ChangesModalHeight: false),
            new CameraPreviewPresentation(
                IsMirroredForRenderingOnly: true,
                CropWidthRatio: CaptureCropPolicy.WidthRatio,
                CropHeightRatio: CaptureCropPolicy.HeightRatio,
                UsesSelectedCameraStream: true),
            draft.NoPrinterSelected,
            draft.StorageReady,
            draft.CanSave,
            draft.EventId,
            draft.IsDirty,
            IsNameDirty: draft.IsNameDirty,
            IsCameraDirty: draft.IsCameraDirty,
            Confirmation: draft.Confirmation,
            Title: draft.EventId is null ? "New Event" : "Edit Event");
        return Publish(CurrentPresentation with { Setup = presentation });
    }

    private ApplicationPresentation Publish(ApplicationPresentation presentation)
    {
        lock (presentationLock)
        {
            currentPresentation = presentation;
        }

        PresentationChanged?.Invoke(this, presentation);
        return presentation;
    }

    private static IReadOnlyList<AvailableCamera> Disambiguate(IReadOnlyList<AvailableCamera> cameras)
    {
        var duplicateNames = cameras.GroupBy(camera => camera.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return cameras.Select(camera => duplicateNames.Contains(camera.DisplayName) && string.IsNullOrWhiteSpace(camera.SecondaryLabel)
                ? camera with { SecondaryLabel = CameraIdentityLabel.FromDeviceId(camera.DeviceId) }
                : camera)
            .ToArray();
    }

    private static CameraConnectionState ToConnectionState(CameraOpenResult result) => result switch
    {
        CameraOpenResult.Ready => CameraConnectionState.Ready,
        CameraOpenResult.Unavailable => CameraConnectionState.Unavailable,
        CameraOpenResult.AccessDenied => CameraConnectionState.AccessDenied,
        CameraOpenResult.InUse => CameraConnectionState.InUseByAnotherApp,
        CameraOpenResult.Disconnected => CameraConnectionState.Disconnected,
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
    };

    private static ApplicationPresentation CreateSavedEventsPresentation(IReadOnlyList<EventTilePresentation> tiles) =>
        new(
            "Choose an Event",
            tiles,
            EmptyStateMessage: null,
            new ApplicationCanvasPresentation(1280, 720, AllowsReflow: false));

    private string FormatSavedAt(DateTimeOffset savedAt)
    {
        var localSavedAt = savedAt.ToLocalTime();
        var localToday = Clock.UtcNow.ToLocalTime().Date;
        return localSavedAt.Date == localToday
            ? $"Saved today, {localSavedAt:h:mm tt}"
            : $"Saved {localSavedAt:MMM d, yyyy, h:mm tt}";
    }

    private sealed record EventSetupDraft(
        EventId? EventId,
        string EventName,
        AvailableCamera? SelectedCamera,
        CameraConnectionState CameraState,
        bool CameraMenuOpen,
        bool NoPrinterSelected,
        bool StorageReady,
        DateTimeOffset? CreatedAt,
        EventDraftBaseline Baseline,
        EventSetupConfirmation Confirmation)
    {
        public bool IsNameDirty => !string.Equals(EventName, Baseline.Name, StringComparison.Ordinal);

        public bool IsCameraDirty =>
            SelectedCamera?.DeviceId != Baseline.Camera?.DeviceId;

        public bool CanSave =>
            CanStart &&
            (EventId is null || IsDirty);

        public bool CanStart =>
            !string.IsNullOrWhiteSpace(EventName) &&
            CameraState == CameraConnectionState.Ready &&
            NoPrinterSelected &&
            StorageReady;

        public bool IsDirty =>
            IsNameDirty ||
            IsCameraDirty ||
            NoPrinterSelected != Baseline.NoPrinterSelected;

        public static EventSetupDraft New(bool storageReady) =>
            new(null, string.Empty, null, CameraConnectionState.NotSelected, false, false, storageReady,
                null, EventDraftBaseline.Empty, EventSetupConfirmation.None);

        public static EventSetupDraft From(EventConfiguration configuration, AvailableCamera camera, bool storageReady) =>
            new(
                configuration.Id,
                configuration.Name,
                camera,
                camera.Availability == CameraAvailability.Available ? CameraConnectionState.NotSelected : CameraConnectionState.Unavailable,
                false,
                configuration.Printer == PrinterChoice.NoPrinter,
                storageReady,
                configuration.CreatedAt,
                new EventDraftBaseline(
                    configuration.Name,
                    configuration.Camera,
                    configuration.Printer == PrinterChoice.NoPrinter),
                EventSetupConfirmation.None);

        public EventSetupDraft WithName(string name) => this with { EventName = name };
        public EventSetupDraft WithCameraMenuOpen(bool isOpen) => this with { CameraMenuOpen = isOpen };
        public EventSetupDraft WithSelectedCamera(AvailableCamera camera, CameraConnectionState state) =>
            this with { SelectedCamera = camera, CameraState = state, CameraMenuOpen = false };
        public EventSetupDraft WithCameraState(CameraConnectionState state) => this with { CameraState = state };
        public EventSetupDraft WithNoPrinterSelected() => this with { NoPrinterSelected = true };
        public EventSetupDraft WithStorageReady(bool isReady) => this with { StorageReady = isReady };
    }

    private sealed record EventDraftBaseline(string Name, CameraBinding? Camera, bool NoPrinterSelected)
    {
        public static EventDraftBaseline Empty { get; } = new(string.Empty, null, false);
    }

    private sealed record SavedEventDiscovery(
        EventConfiguration Configuration,
        AvailableCamera? AvailableCamera);

    private sealed class GuestCycleRun(EventId eventId, GuestCycleId id, List<CaptureReference> captures)
    {
        public EventId EventId { get; } = eventId;

        public GuestCycleId Id { get; } = id;

        public List<CaptureReference> Captures { get; } = captures;

        public GuestCycleInterruption? LastInterruption { get; set; }

        public string? PhotoStripPath { get; set; }

        public bool PreviewCompleted { get; set; }
    }
}
