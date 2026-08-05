namespace FotoHavn.Core;

public sealed class EventGuestCycleOrchestrator
{
    private readonly IEventFileSystem fileSystem;
    private readonly object presentationLock = new();
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private EventSetupDraft? setup;
    private ApplicationPresentation currentPresentation = CreateSavedEventsPresentation([]);

    public EventGuestCycleOrchestrator(
        IEventFileSystem fileSystem,
        ICameraBoundary camera,
        IPhotoStripCompositor compositor,
        IApplicationClock clock)
    {
        this.fileSystem = fileSystem;
        Camera = camera;
        Compositor = compositor;
        Clock = clock;
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
                SaveAndCloseEventSetup => await SaveSetupAsync(startEvent: false, cancellationToken).ConfigureAwait(false),
                SaveAndStartEvent => await SaveSetupAsync(startEvent: true, cancellationToken).ConfigureAwait(false),
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
            new(EventTileKind.NewEvent, "New Event", "Set up a booth Event", "＋"),
        };

        tiles.AddRange(savedEvents
            .OrderByDescending(savedEvent => savedEvent.LastSavedAt)
            .Select(savedEvent => new EventTilePresentation(
                EventTileKind.SavedEvent,
                savedEvent.Name,
                $"Last saved {savedEvent.LastSavedAt.ToLocalTime():g}",
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
        var configuration = await fileSystem.LoadEventAsync(eventId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Event '{eventId}' was not found.");
        await Camera.StartDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        var storageReady = await fileSystem.ProbeStorageAsync(cancellationToken).ConfigureAwait(false);
        var available = Camera.AvailableCameras.FirstOrDefault(camera => camera.DeviceId == configuration.Camera.DeviceId);
        var selected = available ?? new AvailableCamera(
            configuration.Camera.DeviceId,
            configuration.Camera.DisplayName,
            configuration.Camera.DeviceId.Value,
            CameraAvailability.Unavailable);
        setup = EventSetupDraft.From(configuration, selected, storageReady);
        return PublishSetup(setup);
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
        var result = await Camera.OpenAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (result == CameraOpenResult.Ready && Camera.AvailableCameras.All(item => item.DeviceId != deviceId))
        {
            result = CameraOpenResult.Disconnected;
        }

        var state = result switch
        {
            CameraOpenResult.Ready => CameraConnectionState.Ready,
            CameraOpenResult.Unavailable => CameraConnectionState.Unavailable,
            CameraOpenResult.AccessDenied => CameraConnectionState.AccessDenied,
            CameraOpenResult.InUse => CameraConnectionState.InUseByAnotherApp,
            CameraOpenResult.Disconnected => CameraConnectionState.Disconnected,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
        };
        return PublishSetup(setup.WithCameraState(state));
    }

    private async Task<ApplicationPresentation> SaveSetupAsync(bool startEvent, CancellationToken cancellationToken)
    {
        var draft = setup ?? throw new InvalidOperationException("Event setup is not open.");
        if (!draft.CanSave)
        {
            throw new InvalidOperationException("Event setup is not ready to save.");
        }

        var configuration = new EventConfiguration(
            draft.EventId,
            draft.EventName.Trim(),
            new CameraBinding(draft.SelectedCamera!.DeviceId, draft.SelectedCamera.DisplayName),
            PrinterId: null,
            Clock.UtcNow);
        await fileSystem.SaveEventAsync(configuration, cancellationToken).ConfigureAwait(false);

        if (startEvent)
        {
            var streamId = Camera.StreamId ?? throw new InvalidOperationException("The Eligible Camera stream was lost.");
            setup = null;
            return Publish(CurrentPresentation with
            {
                Setup = null,
                ActiveEvent = new ActiveEventPresentation(configuration.Id, configuration.Name, configuration.Camera, streamId),
            });
        }

        await CloseSetupAsync(releaseCamera: true, cancellationToken).ConfigureAwait(false);
        return await LaunchAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApplicationPresentation> CloseSetupAsync(bool releaseCamera, CancellationToken cancellationToken)
    {
        if (releaseCamera && setup?.SelectedCamera is not null)
        {
            await Camera.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        }

        setup = null;
        return Publish(CurrentPresentation with { Setup = null });
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
            draft.CanSave);
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

    private static ApplicationPresentation CreateSavedEventsPresentation(IReadOnlyList<EventTilePresentation> tiles) =>
        new(
            "Saved Events",
            tiles,
            EmptyStateMessage: null,
            new ApplicationCanvasPresentation(1280, 720, AllowsReflow: false));

    private sealed record EventSetupDraft(
        EventId EventId,
        string EventName,
        AvailableCamera? SelectedCamera,
        CameraConnectionState CameraState,
        bool CameraMenuOpen,
        bool NoPrinterSelected,
        bool StorageReady)
    {
        public bool CanSave =>
            !string.IsNullOrWhiteSpace(EventName) &&
            CameraState == CameraConnectionState.Ready &&
            NoPrinterSelected &&
            StorageReady;

        public static EventSetupDraft New(bool storageReady) =>
            new(new EventId(Guid.NewGuid().ToString("N")), string.Empty, null, CameraConnectionState.NotSelected, false, false, storageReady);

        public static EventSetupDraft From(EventConfiguration configuration, AvailableCamera camera, bool storageReady) =>
            new(
                configuration.Id,
                configuration.Name,
                camera,
                camera.Availability == CameraAvailability.Available ? CameraConnectionState.NotSelected : CameraConnectionState.Unavailable,
                false,
                configuration.PrinterId is null,
                storageReady);

        public EventSetupDraft WithName(string name) => this with { EventName = name };
        public EventSetupDraft WithCameraMenuOpen(bool isOpen) => this with { CameraMenuOpen = isOpen };
        public EventSetupDraft WithSelectedCamera(AvailableCamera camera, CameraConnectionState state) =>
            this with { SelectedCamera = camera, CameraState = state, CameraMenuOpen = false };
        public EventSetupDraft WithCameraState(CameraConnectionState state) => this with { CameraState = state };
        public EventSetupDraft WithNoPrinterSelected() => this with { NoPrinterSelected = true };
    }
}
