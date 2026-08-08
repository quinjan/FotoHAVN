namespace FotoHavn.Core;

public abstract record ApplicationCommand;

public sealed record LaunchApplication : ApplicationCommand;

public sealed record OpenNewEvent : ApplicationCommand;

public sealed record OpenSavedEvent(EventId EventId) : ApplicationCommand;

public sealed record ChangeEventName(string Name) : ApplicationCommand;

public sealed record ToggleCameraMenu : ApplicationCommand;

public sealed record DismissCameraMenu : ApplicationCommand;

public sealed record SelectCamera(CameraDeviceId DeviceId) : ApplicationCommand;

public sealed record SelectNoPrinter : ApplicationCommand;

public sealed record RetryEventStorage : ApplicationCommand;

public sealed record CancelEventSetup : ApplicationCommand;

public sealed record KeepEditingEventSetup : ApplicationCommand;

public sealed record DiscardEventSetupDraft : ApplicationCommand;

public sealed record SaveAndCloseEventSetup : ApplicationCommand;

public sealed record SaveAndStartEvent : ApplicationCommand;

public sealed record ConfirmEventSetupSave : ApplicationCommand;

public sealed record CancelEventSetupSave : ApplicationCommand;

public sealed record StartSavedEvent(EventId EventId) : ApplicationCommand;

public sealed record ConfirmStartSavedEvent : ApplicationCommand;

public sealed record CancelStartSavedEvent : ApplicationCommand;

public sealed record ExitActiveEvent : ApplicationCommand;

public sealed record ConfirmExitActiveEvent : ApplicationCommand;

public sealed record CancelExitActiveEvent : ApplicationCommand;

public sealed record StartGuestCycle : ApplicationCommand;

public sealed record RetryGuestStartReadiness : ApplicationCommand;

public sealed record RetryGuestCycle : ApplicationCommand;

public sealed record ConfirmPhotoStripVisible : ApplicationCommand;

public sealed record ReportPhotoStripDecodeFailure : ApplicationCommand;

public sealed record ShutdownApplication : ApplicationCommand;

public sealed record DeleteSavedEvent(EventId EventId) : ApplicationCommand;

public sealed record ConfirmDeleteSavedEvent : ApplicationCommand;

public sealed record CancelDeleteSavedEvent : ApplicationCommand;

public sealed record RetryEventDeletion(EventId EventId) : ApplicationCommand;

public sealed record DismissEventDeletionResult : ApplicationCommand;

public enum EventTileKind
{
    NewEvent,
    SavedEvent,
}

public sealed record EventTilePresentation(
    EventTileKind Kind,
    string Label,
    string SupportingText,
    string Glyph,
    EventId? EventId = null,
    DateTimeOffset? LastSavedAt = null,
    bool DeletionIncomplete = false)
{
    public bool ShowsCreate => Kind == EventTileKind.NewEvent;
    public bool ShowsSavedEventCard => Kind == EventTileKind.SavedEvent;
    public bool ShowsStart => Kind == EventTileKind.SavedEvent && !DeletionIncomplete;
    public bool ShowsEdit => Kind == EventTileKind.SavedEvent && !DeletionIncomplete;
    public bool ShowsDelete => Kind == EventTileKind.SavedEvent && !DeletionIncomplete;
    public bool ShowsRetryDeletion => Kind == EventTileKind.SavedEvent && DeletionIncomplete;
}

public sealed record ApplicationPresentation(
    string Heading,
    IReadOnlyList<EventTilePresentation> EventTiles,
    string? EmptyStateMessage,
    ApplicationCanvasPresentation Canvas,
    EventSetupPresentation? Setup = null,
    ActiveEventPresentation? ActiveEvent = null,
    StartEventConfirmationPresentation? StartEventConfirmation = null,
    EventDeletionPresentation? EventDeletion = null);

public interface IApplicationPresentationController
{
    event EventHandler<ApplicationPresentation>? PresentationChanged;

    ApplicationPresentation CurrentPresentation { get; }

    Task<ApplicationPresentation> ExecuteAsync(
        ApplicationCommand command,
        CancellationToken cancellationToken = default);
}

public enum ApplicationSurface
{
    SavedEvents,
    EventSetup,
    GuestStart,
    GuestStartUnavailable,
    Capture,
    OperatorAssistance,
    PhotoStrip,
    Confirmation,
}

public static class ApplicationSurfaceResolver
{
    public static ApplicationSurface Resolve(ApplicationPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        if (presentation.StartEventConfirmation is not null ||
            presentation.EventDeletion is not null ||
            presentation.Setup is { Confirmation: not EventSetupConfirmation.None } ||
            presentation.ActiveEvent?.ShowsExitConfirmation == true)
        {
            return ApplicationSurface.Confirmation;
        }

        if (presentation.Setup is not null)
        {
            return ApplicationSurface.EventSetup;
        }

        return presentation.ActiveEvent?.GuestCycle.Phase switch
        {
            null => ApplicationSurface.SavedEvents,
            GuestCyclePhase.Start => ApplicationSurface.GuestStart,
            GuestCyclePhase.StartUnavailable => ApplicationSurface.GuestStartUnavailable,
            GuestCyclePhase.Countdown or GuestCyclePhase.Flash or GuestCyclePhase.CaptureSaved =>
                ApplicationSurface.Capture,
            GuestCyclePhase.OperatorAssistance => ApplicationSurface.OperatorAssistance,
            GuestCyclePhase.PhotoStripPreview or GuestCyclePhase.Fading => ApplicationSurface.PhotoStrip,
            _ => throw new ArgumentOutOfRangeException(nameof(presentation)),
        };
    }
}

public enum EventDeletionStage
{
    Confirmation,
    Deleting,
    CouldNotStart,
    Incomplete,
    Deleted,
}

public sealed record EventDeletionPresentation(
    EventId EventId,
    string EventName,
    EventDeletionStage Stage)
{
    public string Title => Stage switch
    {
        EventDeletionStage.Confirmation => $"Delete “{EventName}”?",
        EventDeletionStage.Deleting => $"Deleting “{EventName}”…",
        EventDeletionStage.CouldNotStart => $"Couldn’t start deleting “{EventName}”",
        EventDeletionStage.Incomplete => $"Couldn’t finish deleting “{EventName}”",
        EventDeletionStage.Deleted => "Event deleted",
        _ => throw new ArgumentOutOfRangeException(),
    };

    public string Warning =>
        "The Event, all Guest Cycles, and all photos will be permanently deleted and cannot be recovered.";

    public string Message => Stage switch
    {
        EventDeletionStage.Deleting => "Deletion is in progress. Please keep FotoHAVN open.",
        EventDeletionStage.CouldNotStart => "FotoHAVN could not create the safety record, so no Event data was removed. Check storage access and try again.",
        EventDeletionStage.Incomplete => "Some data could not be removed. The Event is quarantined; retry deletion when storage is available.",
        EventDeletionStage.Deleted => $"“{EventName}” and all of its data were permanently deleted.",
        _ => Warning,
    };

    public string? CancelActionLabel => Stage == EventDeletionStage.Confirmation ? "Cancel" : null;
    public string PrimaryActionLabel => Stage switch
    {
        EventDeletionStage.Confirmation => "Delete Event",
        EventDeletionStage.Deleting => string.Empty,
        _ => "Done",
    };
    public bool IsBusy => Stage == EventDeletionStage.Deleting;
    public bool IsDismissible => Stage != EventDeletionStage.Deleting;
}

public sealed record StartEventConfirmationPresentation(EventId EventId, string EventName)
{
    public string Prompt => $"Start “{EventName}”?";
}

public sealed record ApplicationCanvasPresentation(
    int Width,
    int Height,
    bool AllowsReflow);

public sealed record SavedEventSummary(
    EventId Id,
    string Name,
    DateTimeOffset LastSavedAt);

public readonly record struct CameraDeviceId
{
    public CameraDeviceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    public static implicit operator CameraDeviceId(string value) => new(value);
}

public sealed record CameraBinding(CameraDeviceId DeviceId, string DisplayName);

public static class CameraIdentityLabel
{
    public static string FromDeviceId(CameraDeviceId deviceId)
    {
        var tail = deviceId.Value.Split(['#', '\\', '&'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? deviceId.Value;
        return tail.Length <= 12 ? tail : tail[^12..];
    }
}

public sealed record EventConfiguration(
    EventId Id,
    string Name,
    CameraBinding Camera,
    PrinterChoice Printer,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSavedAt);

public enum PrinterChoice
{
    NoPrinter,
}

public readonly record struct EventId
{
    public EventId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public enum CameraAvailability
{
    Unavailable,
    Available,
}

public sealed record AvailableCamera(
    CameraDeviceId DeviceId,
    string DisplayName,
    string? SecondaryLabel,
    CameraAvailability Availability = CameraAvailability.Available);

public enum CameraOpenResult
{
    Ready,
    Unavailable,
    AccessDenied,
    InUse,
    Disconnected,
}

public enum CameraConnectionState
{
    NotSelected,
    Connecting,
    Ready,
    Unavailable,
    AccessDenied,
    InUseByAnotherApp,
    Disconnected,
}

public sealed record CameraMenuPresentation(
    bool IsOpen,
    bool IsAnchoredOverlay,
    bool ChangesModalHeight);

public sealed record CameraPreviewPresentation(
    bool IsMirroredForRenderingOnly,
    int CropWidthRatio,
    int CropHeightRatio,
    bool UsesSelectedCameraStream);

public enum EventSetupConfirmation
{
    None,
    DiscardChanges,
    SaveAndClose,
    SaveAndStart,
}

public sealed record EventSetupPresentation(
    bool IsOpen,
    bool IsBackdropInert,
    bool ShowsCameraTuning,
    string EventName,
    IReadOnlyList<AvailableCamera> AvailableCameras,
    AvailableCamera? SelectedCamera,
    CameraConnectionState CameraState,
    bool IsCameraEligible,
    CameraMenuPresentation CameraMenu,
    CameraPreviewPresentation Preview,
    bool IsNoPrinterSelected,
    bool IsStorageReady,
    bool CanSave,
    EventId? EventId = null,
    bool IsDirty = false,
    bool IsNameDirty = false,
    bool IsCameraDirty = false,
    EventSetupConfirmation Confirmation = EventSetupConfirmation.None,
    string Title = "New Event")
{
    public bool ShowsDiscardConfirmation => Confirmation == EventSetupConfirmation.DiscardChanges;
    public bool ShowsSaveConfirmation => Confirmation is EventSetupConfirmation.SaveAndClose or EventSetupConfirmation.SaveAndStart;
    public bool SaveConfirmationStartsEvent => Confirmation == EventSetupConfirmation.SaveAndStart;
    public bool CanStart =>
        !string.IsNullOrWhiteSpace(EventName) &&
        CameraState == CameraConnectionState.Ready &&
        IsNoPrinterSelected &&
        IsStorageReady;

    public string? ActionableFailureMessage => !IsStorageReady
        ? "Event storage is not writable. Fix access to the Events folder, then check storage again."
        : CameraState switch
        {
            CameraConnectionState.Unavailable => "Connect the Camera bound to this Event, then choose that exact Camera again.",
            CameraConnectionState.AccessDenied => "Allow Windows access to the Camera bound to this Event, then choose it again.",
            CameraConnectionState.InUseByAnotherApp => "Close the app using the Camera bound to this Event, then choose it again.",
            CameraConnectionState.Disconnected => "Reconnect the Camera bound to this Event, then choose that exact Camera again.",
            _ => null,
        };
}

public sealed record ActiveEventPresentation(
    EventId Id,
    string Name,
    CameraBinding Camera,
    string CameraStreamId,
    bool ShowsExitConfirmation = false,
    GuestStartPresentation? GuestStartState = null,
    GuestCyclePresentation? Cycle = null,
    ExitHoldState ExitHoldState = ExitHoldState.Idle)
{
    public string Heading => "Let’s take some photos.";
    public string Explanation => "Four Captures. A quick countdown before each one.";
    public string StartActionLabel => "Touch to start";
    public GuestCyclePresentation GuestCycle => Cycle ?? GuestCyclePresentation.Start;
    public bool ShowsExitEvent => GuestCycle.Phase is GuestCyclePhase.Start or GuestCyclePhase.StartUnavailable;
    public bool ShowsHardwareStatus => false;
    public GuestStartPresentation GuestStart => GuestStartState ?? GuestStartPresentation.Unavailable;
}

public enum GuestStartFailure
{
    None,
    CameraUnavailable,
    StorageUnavailable,
}

public enum ExitHoldState
{
    Idle,
    Holding,
    Cancelled,
}

public enum GuestStartActionState
{
    Idle,
    Retrying,
    RetryFailed,
}

public sealed record GuestStartPresentation(
    bool IsCameraReady,
    bool IsStorageReady,
    GuestStartFailure Failure = GuestStartFailure.None,
    bool RequiresEventSetupCorrection = false,
    GuestStartActionState ActionState = GuestStartActionState.Idle)
{
    public static GuestStartPresentation Unavailable { get; } =
        new(false, false, GuestStartFailure.CameraUnavailable);

    public bool IsStartVisible => true;
    public bool IsStartEnabled => IsCameraReady && IsStorageReady && Failure == GuestStartFailure.None;
    public string? StatusMessage => IsStartEnabled ? null : "Please call the operator";
    public bool ShowsRetry => !IsStartEnabled && !RequiresEventSetupCorrection;
    public bool IsRetrying => ActionState == GuestStartActionState.Retrying;
    public string RetryActionLabel => "Retry";

    public static GuestStartPresentation FromReadiness(
        bool isCameraReady,
        bool isStorageReady,
        bool requiresEventSetupCorrection = false) =>
        new(
            isCameraReady,
            isStorageReady,
            isCameraReady
                ? isStorageReady ? GuestStartFailure.None : GuestStartFailure.StorageUnavailable
                : GuestStartFailure.CameraUnavailable,
            requiresEventSetupCorrection);
}

public enum GuestCyclePhase
{
    Start,
    StartUnavailable,
    Countdown,
    Flash,
    CaptureSaved,
    OperatorAssistance,
    PhotoStripPreview,
    Fading,
}

public enum GuestCycleFailure
{
    None,
    CameraUnavailable,
    StorageUnavailable,
}

public enum GuestCycleRecovery
{
    Retry,
    ExitOnly,
}

public enum GuestCycleActionState
{
    Idle,
    Retrying,
    RetryFailed,
}

public sealed record GuestCyclePresentation(
    GuestCyclePhase Phase,
    int CaptureNumber = 0,
    int CompletedCaptures = 0,
    int CountdownSeconds = 0,
    GuestCycleFailure Failure = GuestCycleFailure.None,
    string? PhotoStripPath = null,
    int PreviewSecondsRemaining = 0,
    GuestCycleRecovery Recovery = GuestCycleRecovery.Retry,
    GuestCycleActionState ActionState = GuestCycleActionState.Idle)
{
    public static GuestCyclePresentation Start { get; } = new(GuestCyclePhase.Start);

    public string ProgressText => CaptureNumber is >= 1 and <= 4
        ? $"Capture {CaptureNumber} of 4"
        : $"{CompletedCaptures} of 4 Captures saved";

    public string AssistanceDetail => Failure switch
    {
        GuestCycleFailure.CameraUnavailable => "The Camera isn’t available right now.",
        GuestCycleFailure.StorageUnavailable => "Event storage isn’t available right now.",
        _ => string.Empty,
    };

    public bool ShowsRetry => Recovery == GuestCycleRecovery.Retry;

    public bool IsRetrying => ActionState == GuestCycleActionState.Retrying;
}

public interface IActiveEventWakeLock
{
    Task AcquireAsync(CancellationToken cancellationToken);

    Task ReleaseAsync(CancellationToken cancellationToken);
}

public sealed class NoOpActiveEventWakeLock : IActiveEventWakeLock
{
    public Task AcquireAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ReleaseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public interface ICameraBoundary
{
    event EventHandler? AvailableCamerasChanged;

    event EventHandler? StreamHealthChanged
    {
        add { }
        remove { }
    }

    IReadOnlyList<AvailableCamera> AvailableCameras { get; }

    string? StreamId { get; }

    CameraStreamHealth StreamHealth => CameraStreamHealth.Unavailable;

    Task StartDiscoveryAsync(CancellationToken cancellationToken);

    Task<CameraOpenResult> OpenAsync(CameraDeviceId deviceId, CancellationToken cancellationToken);

    Task<CapturedFrame?> CaptureFirstFreshFrameAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        Task.FromResult<CapturedFrame?>(null);

    Task ReleaseAsync(CancellationToken cancellationToken);
}

public enum CameraStreamFailure
{
    None,
    Unavailable,
    Removed,
    StreamFailure,
    ExclusiveOwnershipLost,
    Stale,
}

public sealed record CameraStreamHealth(
    CameraDeviceId? DeviceId,
    string? StreamId,
    long LatestFrameSequence,
    DateTimeOffset? LatestFrameAt,
    CameraStreamFailure Failure)
{
    public CameraStreamHealth(
        CameraDeviceId? deviceId,
        string? streamId,
        DateTimeOffset? latestFrameAt,
        CameraStreamFailure failure)
        : this(deviceId, streamId, 0, latestFrameAt, failure)
    {
    }

    public static CameraStreamHealth Unavailable { get; } =
        new(null, null, 0, null, CameraStreamFailure.Unavailable);
}

public sealed record CapturedFrame(
    long Sequence,
    DateTimeOffset ReceivedAt,
    int Width,
    int Height,
    ReadOnlyMemory<byte> JpegBytes);

public sealed record CaptureReference(
    string ArtifactPath,
    long ByteLength = 0,
    string Sha256 = "",
    int Width = 0,
    int Height = 0);

public enum GuestCycleFailureSource
{
    CameraRemoved,
    CameraStreamFailure,
    CameraExclusiveOwnershipLost,
    CameraStale,
    FreshFrameTimeout,
    JpegEncoding,
    JpegValidation,
    Storage,
}

public enum GuestCycleInterruptedStep
{
    Capture,
    PhotoStrip,
    Preview,
    Completion,
}

public sealed record GuestCycleInterruption(
    GuestCycleFailureSource Source,
    GuestCycleInterruptedStep Step,
    int CaptureNumber,
    int CompletedCaptures,
    DateTimeOffset FailedAt)
{
    public string LastDurableCheckpoint => CompletedCaptures == 0
        ? "guestCycleCreated"
        : $"capture{CompletedCaptures}Committed";
}

public enum GuestCycleRetryValidation
{
    Ready,
    Unrecoverable,
}

public sealed record PhotoStripCompositionRequest(
    string EventName,
    IReadOnlyList<CaptureReference> Captures,
    double CaptureCropAspectRatio = CaptureCropPolicy.AspectRatio);

public sealed record PhotoStripCompositionResult(
    bool IsAvailable,
    ReadOnlyMemory<byte> PngBytes,
    int Width,
    int Height);

public interface IPhotoStripCompositor
{
    Task<PhotoStripCompositionResult> ComposeAsync(
        PhotoStripCompositionRequest request,
        CancellationToken cancellationToken);
}

public interface IApplicationClock
{
    DateTimeOffset UtcNow { get; }

    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public interface IEventIdentityGenerator
{
    EventId Create();
}

public readonly record struct GuestCycleId
{
    public GuestCycleId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public interface IGuestCycleIdentityGenerator
{
    GuestCycleId Create();
}

public sealed class UuidV7GuestCycleIdentityGenerator : IGuestCycleIdentityGenerator
{
    public GuestCycleId Create() => new(Guid.CreateVersion7().ToString());
}

public sealed class UuidV7EventIdentityGenerator : IEventIdentityGenerator
{
    public EventId Create() => new(Guid.CreateVersion7().ToString());
}

public enum EventSaveMode
{
    CreateNew,
    UpdateExisting,
}

public enum EventSaveResult
{
    Saved,
    IdentityCollision,
}

public sealed record EventDeletionQuarantine(
    EventId EventId,
    string EventName,
    DateTimeOffset LastSavedAt);

public enum EventDeletionResult
{
    Deleted,
    Incomplete,
}

public interface IEventFileSystem
{
    Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken);

    Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken);

    Task<bool> ProbeStorageAsync(CancellationToken cancellationToken);

    Task<bool> ProbeEventStorageAsync(EventId eventId, CancellationToken cancellationToken) =>
        ProbeStorageAsync(cancellationToken);

    Task<EventSaveResult> SaveEventAtomicallyAsync(
        EventConfiguration configuration,
        EventSaveMode mode,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EventDeletionQuarantine>> LoadEventDeletionQuarantinesAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EventDeletionQuarantine>>([]);

    Task QuarantineEventForDeletionAsync(
        EventDeletionQuarantine quarantine,
        CancellationToken cancellationToken) => Task.CompletedTask;

    Task<EventDeletionResult> DeleteQuarantinedEventAsync(
        EventId eventId,
        CancellationToken cancellationToken) => Task.FromResult(EventDeletionResult.Incomplete);

    Task DeleteEventAsync(EventId eventId, CancellationToken cancellationToken) => Task.CompletedTask;

    Task<GuestCycleCreateResult> CreateGuestCycleAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken) =>
        Task.FromResult(GuestCycleCreateResult.IdentityCollision);

    Task<CaptureCommitResult> CommitCaptureAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        int captureNumber,
        CapturedFrame frame,
        CancellationToken cancellationToken) =>
        Task.FromResult(new CaptureCommitResult(false, new CaptureReference(string.Empty)));

    Task RecordGuestCycleInterruptionAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        GuestCycleInterruption interruption,
        CancellationToken cancellationToken) => Task.CompletedTask;

    Task<GuestCycleRetryValidation> PrepareGuestCycleRetryAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        IReadOnlyList<CaptureReference> completedCaptures,
        CancellationToken cancellationToken) => Task.FromResult(GuestCycleRetryValidation.Ready);

    Task<PhotoStripCommitResult> CommitPhotoStripAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        PhotoStripCompositionResult composition,
        CancellationToken cancellationToken) =>
        Task.FromResult(new PhotoStripCommitResult(false, string.Empty));

    Task CompleteGuestCycleAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

public enum GuestCycleCreateResult
{
    Created,
    IdentityCollision,
}

public sealed record CaptureCommitResult(bool Committed, CaptureReference Capture);

public sealed record PhotoStripCommitResult(bool Committed, string ArtifactPath);

public static class MotionPolicy
{
    public static TimeSpan ResolveDuration(bool animationsEnabled, TimeSpan preferredDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(preferredDuration, TimeSpan.Zero);
        return animationsEnabled ? preferredDuration : TimeSpan.Zero;
    }
}

public static class CaptureCropPolicy
{
    public const int WidthRatio = 3;
    public const int HeightRatio = 2;
    public const double AspectRatio = (double)WidthRatio / HeightRatio;
}
