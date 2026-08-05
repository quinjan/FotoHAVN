namespace FotoHavn.Core;

public sealed class EventGuestCycleOrchestrator
{
    private readonly IEventFileSystem fileSystem;
    private readonly IEventIdentityGenerator identityGenerator;
    private readonly object presentationLock = new();
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private EventSetupDraft? setup;
    private ApplicationPresentation currentPresentation = CreateSavedEventsPresentation([]);

    public EventGuestCycleOrchestrator(
        IEventFileSystem fileSystem,
        ICameraBoundary camera,
        IPhotoStripCompositor compositor,
        IApplicationClock clock,
        IEventIdentityGenerator? identityGenerator = null)
    {
        this.fileSystem = fileSystem;
        Camera = camera;
        Compositor = compositor;
        Clock = clock;
        this.identityGenerator = identityGenerator ?? new UuidV7EventIdentityGenerator();
        Camera.AvailableCamerasChanged += OnAvailableCamerasChanged;
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
        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
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
                StartSavedEvent start => await StartSavedEventAsync(start.EventId, cancellationToken).ConfigureAwait(false),
                DeleteSavedEvent delete => await DeleteSavedEventAsync(delete.EventId, cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown application command."),
            };

            return presentation;
        }
        finally
        {
            commandGate.Release();
        }
    }

    private async Task<ApplicationPresentation> LaunchAsync(CancellationToken cancellationToken)
    {
        var savedEvents = await fileSystem.LoadEventsAsync(cancellationToken).ConfigureAwait(false);
        var tiles = new List<EventTilePresentation>(savedEvents.Count + 1)
        {
            new(EventTileKind.NewEvent, "New Event", "Set up a new booth run", "＋"),
        };

        tiles.AddRange(savedEvents
            .OrderByDescending(savedEvent => savedEvent.LastSavedAt)
            .Select(savedEvent => new EventTilePresentation(
                EventTileKind.SavedEvent,
                savedEvent.Name,
                FormatSavedAt(savedEvent.LastSavedAt),
                "▶",
                savedEvent.Id,
                savedEvent.LastSavedAt)));

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
        if (!draft.CanSave)
        {
            throw new InvalidOperationException("Event setup is not ready to save.");
        }

        return draft.EventId is null
            ? SaveSetupAsync(startEvent, cancellationToken)
            : Task.FromResult(PublishSetup(draft with
            {
                Confirmation = startEvent
                    ? EventSetupConfirmation.SaveAndStart
                    : EventSetupConfirmation.SaveAndClose,
            }));
    }

    private async Task<ApplicationPresentation> SaveSetupAsync(bool startEvent, CancellationToken cancellationToken)
    {
        var draft = setup ?? throw new InvalidOperationException("Event setup is not open.");
        if (!draft.CanSave)
        {
            throw new InvalidOperationException("Event setup is not ready to save.");
        }

        if (startEvent && draft.EventId is not null &&
            !await fileSystem.ProbeStorageAsync(cancellationToken).ConfigureAwait(false))
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

            setup = null;
            return Publish(CurrentPresentation with
            {
                Setup = null,
                ActiveEvent = new ActiveEventPresentation(configuration.Id, configuration.Name, configuration.Camera, streamId!),
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

    private async Task<ApplicationPresentation> DeleteSavedEventAsync(EventId eventId, CancellationToken cancellationToken)
    {
        await fileSystem.DeleteEventAsync(eventId, cancellationToken).ConfigureAwait(false);
        return await LaunchAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApplicationPresentation> StartSavedEventAsync(EventId eventId, CancellationToken cancellationToken)
    {
        var savedEvent = await DiscoverSavedEventAsync(eventId, cancellationToken).ConfigureAwait(false);
        if (savedEvent.AvailableCamera is null)
        {
            setup = await CreateSavedEventDraftAsync(savedEvent, cancellationToken).ConfigureAwait(false);
            return PublishSetup(setup);
        }

        var result = await Camera.OpenAsync(savedEvent.Configuration.Camera.DeviceId, cancellationToken).ConfigureAwait(false);
        if (result != CameraOpenResult.Ready || Camera.StreamId is not { } streamId)
        {
            var state = result == CameraOpenResult.Ready
                ? CameraConnectionState.Disconnected
                : ToConnectionState(result);
            setup = (await CreateSavedEventDraftAsync(savedEvent, cancellationToken).ConfigureAwait(false))
                .WithCameraState(state);
            return PublishSetup(setup);
        }

        return Publish(CurrentPresentation with
        {
            Setup = null,
            ActiveEvent = new ActiveEventPresentation(
                savedEvent.Configuration.Id,
                savedEvent.Configuration.Name,
                savedEvent.Configuration.Camera,
                streamId),
        });
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
            !string.IsNullOrWhiteSpace(EventName) &&
            CameraState == CameraConnectionState.Ready &&
            NoPrinterSelected &&
            StorageReady &&
            (EventId is null || IsDirty);

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
    }

    private sealed record EventDraftBaseline(string Name, CameraBinding? Camera, bool NoPrinterSelected)
    {
        public static EventDraftBaseline Empty { get; } = new(string.Empty, null, false);
    }

    private sealed record SavedEventDiscovery(
        EventConfiguration Configuration,
        AvailableCamera? AvailableCamera);
}
