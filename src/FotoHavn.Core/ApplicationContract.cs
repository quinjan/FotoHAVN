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

public sealed record StartSavedEvent(EventId EventId) : ApplicationCommand;

public sealed record ConfirmStartSavedEvent : ApplicationCommand;

public sealed record CancelStartSavedEvent : ApplicationCommand;

public sealed record ExitActiveEvent : ApplicationCommand;

public sealed record ConfirmExitActiveEvent : ApplicationCommand;

public sealed record CancelExitActiveEvent : ApplicationCommand;

public sealed record ShutdownApplication : ApplicationCommand;

public sealed record DeleteSavedEvent(EventId EventId) : ApplicationCommand;

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
    DateTimeOffset? LastSavedAt = null)
{
    public bool ShowsCreate => Kind == EventTileKind.NewEvent;
    public bool ShowsStart => Kind == EventTileKind.SavedEvent;
    public bool ShowsEdit => Kind == EventTileKind.SavedEvent;
    public bool ShowsDelete => Kind == EventTileKind.SavedEvent;
}

public sealed record ApplicationPresentation(
    string Heading,
    IReadOnlyList<EventTilePresentation> EventTiles,
    string? EmptyStateMessage,
    ApplicationCanvasPresentation Canvas,
    EventSetupPresentation? Setup = null,
    ActiveEventPresentation? ActiveEvent = null,
    StartEventConfirmationPresentation? StartEventConfirmation = null);

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
    bool ShowsDiscardConfirmation = false,
    string Title = "New Event")
{
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
    bool ShowsExitConfirmation = false)
{
    public string Heading => "Let’s take some photos.";
    public string Explanation => "We’ll take four photos to create your Photo Strip.";
    public string StartActionLabel => "Touch to start";
    public bool ShowsExitEvent => true;
    public bool ShowsHardwareStatus => false;
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

    IReadOnlyList<AvailableCamera> AvailableCameras { get; }

    string? StreamId { get; }

    Task StartDiscoveryAsync(CancellationToken cancellationToken);

    Task<CameraOpenResult> OpenAsync(CameraDeviceId deviceId, CancellationToken cancellationToken);

    Task ReleaseAsync(CancellationToken cancellationToken);
}

public sealed record CaptureReference(string ArtifactPath);

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

public interface IEventFileSystem
{
    Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken);

    Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken);

    Task<bool> ProbeStorageAsync(CancellationToken cancellationToken);

    Task<EventSaveResult> SaveEventAtomicallyAsync(
        EventConfiguration configuration,
        EventSaveMode mode,
        CancellationToken cancellationToken);

    Task DeleteEventAsync(EventId eventId, CancellationToken cancellationToken);
}

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
