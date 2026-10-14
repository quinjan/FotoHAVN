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
/// <param name="RunningEvent">The running-Event panel; null while no Event runs (always null until M6).</param>
/// <param name="Overlays">Overlays on top of the surface; each is null when not shown.</param>
/// <param name="Fatal">Set when <paramref name="Surface"/> is <see cref="OperatorSurface.Fatal"/>.</param>
/// <param name="StorageError">Set when <paramref name="Surface"/> is <see cref="OperatorSurface.StorageError"/>.</param>
/// <param name="CameraDiagnostics">Set when <paramref name="Surface"/> is <see cref="OperatorSurface.CameraDiagnostics"/>.</param>
public sealed record OperatorPresentation(
    OperatorSurface Surface,
    EventListView? EventList,
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

/// <summary>The Event list, sorted by date, newest first (plan section 8). Empty shows "No Events yet."</summary>
public sealed record EventListView(IReadOnlyList<EventListRow> Events);

/// <summary>One Event list row (plan section 8, Event list).</summary>
/// <param name="Id">The Event ID.</param>
/// <param name="Name">The Event name.</param>
/// <param name="Date">The Event's local date (<c>yyyy-MM-dd</c>).</param>
/// <param name="Venue">The venue, or null when not set.</param>
/// <param name="CompletedGuestCycles">The number of completed Guest Cycles.</param>
/// <param name="LastUsed">When the Event last ran, or null if it never has.</param>
/// <param name="Running">Whether this is the Active Event (shown as <c>RUNNING</c>).</param>
public sealed record EventListRow(
    string Id,
    string Name,
    DateOnly Date,
    string? Venue,
    int CompletedGuestCycles,
    DateTimeOffset? LastUsed,
    bool Running);

/// <summary>The running-Event panel (plan section 8). Placeholder until M6 adds its facts and actions.</summary>
public sealed record RunningEventPanel;

/// <summary>Overlays on the Operator Screen; each is null when not shown.</summary>
public sealed record OperatorOverlays(
    ConfirmationOverlay? Confirmation,
    IdentifyDisplaysOverlay? IdentifyDisplays,
    NoticeOverlay? Notice)
{
    public static OperatorOverlays None { get; } = new(null, null, null);
}

/// <summary>A confirmation dialog (delete, leave with changes, end Event). Placeholder until M6.</summary>
public sealed record ConfirmationOverlay;

/// <summary>The Identify displays overlay state. Placeholder until M6.</summary>
public sealed record IdentifyDisplaysOverlay;

/// <summary>A notice whose copy comes from the brain (it has domain meaning; plan section 9, Copy).</summary>
public sealed record NoticeOverlay(string Message);

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
/// <param name="Start">Set when <paramref name="Surface"/> is <see cref="GuestSurface.Start"/> or <see cref="GuestSurface.StartUnavailable"/>.</param>
/// <param name="Overlays">Overlays on top of the surface; each is null when not shown.</param>
/// <param name="Fatal">Set when <paramref name="Surface"/> is <see cref="GuestSurface.Fatal"/>.</param>
public sealed record GuestPresentation(
    GuestSurface Surface,
    StartView? Start,
    GuestOverlays Overlays,
    FatalView? Fatal);

/// <summary>The Start view. <paramref name="GuestGalleryAvailable"/> shows <c>SEE THE GOOD COMPANY →</c>.</summary>
public sealed record StartView(bool GuestGalleryAvailable);

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

/// <summary>The exit hold in progress. Placeholder until M8.</summary>
public sealed record ExitHoldOverlay;

/// <summary>The Operator PIN dialog. Placeholder until M8.</summary>
public sealed record PinDialogOverlay;

/// <summary>The Phase Timer warning with <c>MORE TIME</c>. Placeholder until M8.</summary>
public sealed record PhaseTimerWarningOverlay;

/// <summary>The Start over confirmation. Placeholder until M8.</summary>
public sealed record StartOverConfirmationOverlay;
