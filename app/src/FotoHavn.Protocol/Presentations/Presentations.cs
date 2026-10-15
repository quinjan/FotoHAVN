using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Protocol.Presentations;

// Presentation roots (plan section 9, Presentation). A presentation is a complete snapshot of everything one role's UI
// may show; every field is always written, null when absent. Later milestones add fields to these records and give the
// placeholder records (empty today) their content; adding a field is a protocol change (bump ProtocolVersion.Current
// and regenerate the schema and types).

/// <summary>What the Operator Screen shows.</summary>
public enum OperatorSurface
{
    EventList,
    EventSetup,
    EventGallery,
    GuestCycleDetail,

    /// <summary>Single-screen placeholder while the Guest Screen covers the Operator Screen.</summary>
    GuestScreenActive,
    Fatal,

    /// <summary>Storage couldn't open or failed outside a Guest Cycle; the operator sees the cause and <c>RetryStorage</c>.</summary>
    StorageError,

    /// <summary>The brain is opening the Event store for the first time; the operator sees a loading state, never an empty Event list.</summary>
    Starting,

    /// <summary>
    /// The E2E-only camera diagnostics screen (camera list, live preview, frame age, frame rate, capture). Only
    /// <c>test.openCameraDiagnostics</c> shows it, until M6's Event setup exists; the surface exists in every build.
    /// </summary>
    CameraDiagnostics,
}

/// <summary>The operator role's presentation.</summary>
/// <param name="Surface">The view to render.</param>
/// <param name="EventList">Set when <paramref name="Surface"/> is <see cref="OperatorSurface.EventList"/>.</param>
/// <param name="EventSetup">Set when <paramref name="Surface"/> is <see cref="OperatorSurface.EventSetup"/>.</param>
/// <param name="RunningEvent">The running-Event panel; null while no Event runs. Present on every surface while one does, so any view can show an Operator Assistance alert (plan section 8).</param>
/// <param name="Overlays">Overlays on top of the surface; each is null when not shown.</param>
/// <param name="Fatal">Set when <paramref name="Surface"/> is <see cref="OperatorSurface.Fatal"/>.</param>
/// <param name="StorageError">Set when <paramref name="Surface"/> is <see cref="OperatorSurface.StorageError"/>.</param>
/// <param name="CameraDiagnostics">Set when <paramref name="Surface"/> is <see cref="OperatorSurface.CameraDiagnostics"/>.</param>
public sealed record OperatorPresentation(
    OperatorSurface Surface,
    EventListView? EventList,
    EventSetupView? EventSetup,
    RunningEventPanel? RunningEvent,
    OperatorOverlays Overlays,
    FatalView? Fatal,
    StorageErrorView? StorageError,
    CameraDiagnosticsView? CameraDiagnostics);

/// <summary>
/// The camera diagnostics screen (plan section 14, E2E-only until M6). Frame age and frame rate are not here: they change
/// at frame rate, and presentations must not, so the page reads them from <c>test.getDiagnostics</c>.
/// </summary>
/// <param name="Cameras">The Available Cameras, in the order the camera adapter lists them.</param>
/// <param name="SelectedDeviceId">The selected camera's device ID, or null when none is selected.</param>
/// <param name="State">The selected camera's state.</param>
/// <param name="Reason">Why <paramref name="State"/> is <see cref="CameraState.NotAvailable"/> or <see cref="CameraState.InUseByAnotherApp"/>; null otherwise.</param>
/// <param name="Format">The format of the open camera, or null when none is open.</param>
/// <param name="Mirrored">Whether preview and diagnostic captures are mirrored.</param>
/// <param name="LastCapture">The last diagnostic capture, or null before one.</param>
/// <param name="LastComposition">The last diagnostic composition (<c>test.composeDiagnostics</c>), or null before one.</param>
public sealed record CameraDiagnosticsView(
    IReadOnlyList<CameraOption> Cameras,
    string? SelectedDeviceId,
    CameraState State,
    CameraFailureKind? Reason,
    CameraFormatView? Format,
    bool Mirrored,
    CameraCaptureView? LastCapture,
    CompositionView? LastComposition);

/// <summary>A composed preview the brain cached: the PNG is served at <c>https://preview.fotohavn/&lt;PreviewKey&gt;.png</c>.</summary>
/// <param name="PreviewKey">The cache key, one path segment of lowercase letters, digits, <c>.</c>, and <c>-</c>.</param>
/// <param name="TemplateId">The Template composed.</param>
/// <param name="LookId">The Look applied.</param>
/// <param name="Width">The PNG width in pixels (the Template's native width).</param>
/// <param name="Height">The PNG height in pixels (the Template's native height).</param>
public sealed record CompositionView(string PreviewKey, string TemplateId, string LookId, int Width, int Height);

/// <summary>A diagnostic capture: a JPEG the host wrote under its <c>diagnostics/</c> folder (never Event data).</summary>
/// <param name="FileName">The file's name inside the host's diagnostics folder.</param>
/// <param name="Width">The image width in pixels.</param>
/// <param name="Height">The image height in pixels.</param>
/// <param name="ByteLength">The file's size in bytes.</param>
/// <param name="Mirrored">Whether the image is mirrored.</param>
public sealed record CameraCaptureView(string FileName, int Width, int Height, int ByteLength, bool Mirrored);

/// <summary>What went wrong with storage, in the terms the operator screen can act on. The UI owns the copy for each.</summary>
public enum StorageErrorReason
{
    DiskFull,
    AccessDenied,
    Locked,
    NotLocalDisk,
    CloudSynced,
    NotWritable,
    IntegrityFailed,
    Corrupt,
    Unknown,
}

/// <summary>The storage error surface (plan section 10): the cause and the Data folder it concerns.</summary>
/// <param name="Kind">The cause.</param>
/// <param name="DataPath">The absolute Data folder path, shown to the operator. It holds no personal data beyond what the operator already sees.</param>
public sealed record StorageErrorView(StorageErrorReason Kind, string DataPath);

/// <summary>The fatal surface: something the UI can't recover from by itself. <paramref name="Reason"/> is brain copy.</summary>
public sealed record FatalView(string Reason);

/// <summary>What the Guest Screen shows.</summary>
public enum GuestSurface
{
    Start,
    StartUnavailable,
    GuestGallery,
    GuestStripDetail,
    Template,
    Photographs,
    Filters,
    Keepsake,
    OperatorAssistance,
    Fatal,
}

/// <summary>The guest role's presentation. It never contains operator data.</summary>
/// <param name="Surface">The view to render.</param>
/// <param name="Start">Set when <paramref name="Surface"/> is <see cref="GuestSurface.Start"/>.</param>
/// <param name="StartUnavailable">Set when <paramref name="Surface"/> is <see cref="GuestSurface.StartUnavailable"/>.</param>
/// <param name="Overlays">Overlays on top of the surface; each is null when not shown.</param>
/// <param name="Fatal">Set when <paramref name="Surface"/> is <see cref="GuestSurface.Fatal"/>.</param>
/// <param name="GuestCycle">The running Guest Cycle: set on every cycle step and on <see cref="GuestSurface.OperatorAssistance"/> during a cycle; null otherwise (protocol version 8).</param>
/// <param name="Template">Set when <paramref name="Surface"/> is <see cref="GuestSurface.Template"/>.</param>
/// <param name="Photographs">Set when <paramref name="Surface"/> is <see cref="GuestSurface.Photographs"/>.</param>
/// <param name="Filters">Set when <paramref name="Surface"/> is <see cref="GuestSurface.Filters"/>.</param>
/// <param name="Keepsake">Set when <paramref name="Surface"/> is <see cref="GuestSurface.Keepsake"/>.</param>
/// <param name="OperatorAssistance">Set when <paramref name="Surface"/> is <see cref="GuestSurface.OperatorAssistance"/>.</param>
public sealed record GuestPresentation(
    GuestSurface Surface,
    StartView? Start,
    StartUnavailableView? StartUnavailable,
    GuestOverlays Overlays,
    FatalView? Fatal,
    GuestCycleView? GuestCycle,
    TemplateView? Template,
    PhotographsView? Photographs,
    FiltersView? Filters,
    KeepsakeView? Keepsake,
    OperatorAssistanceView? OperatorAssistance);

/// <summary>The Start view (plan section 7, Steps).</summary>
/// <param name="StepInsideAvailable">Whether <c>STEP INSIDE →</c> works: the Camera is eligible and storage is writable. Always true on Start; Start unavailable is its own surface.</param>
/// <param name="GuestGalleryLinkVisible">Whether <c>SEE THE GOOD COMPANY →</c> shows: the Event enables the guest gallery and has a completed Photo Strip.</param>
public sealed record StartView(bool StepInsideAvailable, bool GuestGalleryLinkVisible);

/// <summary>Why Start is unavailable (plan section 7, Start unavailable).</summary>
public enum StartUnavailableReason
{
    /// <summary>The Camera isn't eligible; Start recovers by itself when a fresh frame proves it again.</summary>
    Camera,

    /// <summary>Storage isn't writable; Start recovers by itself when it is again.</summary>
    Storage,
}

/// <summary>The Start unavailable view: Start with <c>STEP INSIDE</c> disabled. The guest sees the same neutral copy for both reasons; the reason is for the UI's data attributes and tests.</summary>
/// <param name="Reason">Why.</param>
/// <param name="GuestGalleryLinkVisible">Whether <c>SEE THE GOOD COMPANY →</c> still shows (the guest gallery needs no camera).</param>
public sealed record StartUnavailableView(StartUnavailableReason Reason, bool GuestGalleryLinkVisible);

/// <summary>Overlays on the Guest Screen; each is null when not shown.</summary>
public sealed record GuestOverlays(
    ExitHoldOverlay? ExitHold,
    PinDialogOverlay? PinDialog,
    PhaseTimerWarningOverlay? PhaseTimerWarning,
    StartOverConfirmationOverlay? StartOverConfirmation,
    NoticeOverlay? Notice)
{
    public static GuestOverlays None { get; } = new(null, null, null, null, null);
}

/// <summary>
/// The exit hold in progress: present from <c>BeginExitHold</c> until <c>CompleteExitHold</c> or <c>CancelExit</c>. The UI draws the
/// 3-second progress itself (a ring, or text with reduced motion); the brain only needs to know a hold began.
/// </summary>
/// <param name="Active">Always true while the overlay is present.</param>
public sealed record ExitHoldOverlay(bool Active);

/// <summary>Why the PIN dialog is showing an error.</summary>
public enum PinDialogError
{
    /// <summary>The last PIN was wrong.</summary>
    WrongPin,
}

/// <summary>
/// The Operator PIN dialog with its on-screen digit pad. The digits typed live only in the UI until <c>SubmitPin</c>; nothing here
/// carries a PIN.
/// </summary>
/// <param name="Error">The last attempt's error; null before an attempt or after the lockout ends.</param>
/// <param name="LockedRemainingSeconds">Whole seconds left in the 30-second lockout; null when not locked. The brain updates it once a second while locked.</param>
/// <param name="AttemptsRemaining">Wrong attempts left before a lockout; null while locked.</param>
public sealed record PinDialogOverlay(PinDialogError? Error, int? LockedRemainingSeconds, int? AttemptsRemaining);

/// <summary>
/// The Phase Timer warning with <c>MORE TIME</c>: shown in the last 20 seconds of the Template, Photographs capture, Photographs review,
/// Filters, and guest gallery phases, never on the Keepsake. The UI announces it once, politely, not every second.
/// </summary>
/// <param name="Phase">The phase running out.</param>
/// <param name="RemainingSeconds">Whole seconds left; the brain pushes it every second while the warning shows.</param>
public sealed record PhaseTimerWarningOverlay(PhaseTimer Phase, int RemainingSeconds);

/// <summary>
/// The Start over confirmation ("Start over? Your photographs stay with the Event, but no strip will be made." with <c>START OVER</c>
/// and <c>KEEP GOING</c>). It carries nothing: its presence is the state.
/// </summary>
public sealed record StartOverConfirmationOverlay;

/// <summary>A notice whose copy comes from the brain (it has domain meaning; plan section 9, Copy).</summary>
public sealed record NoticeOverlay(string Message);