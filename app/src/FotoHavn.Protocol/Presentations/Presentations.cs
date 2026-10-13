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
}

/// <summary>The operator role's presentation.</summary>
/// <param name="Surface">The view to render.</param>
/// <param name="EventList">Set when <paramref name="Surface"/> is <see cref="OperatorSurface.EventList"/>.</param>
/// <param name="RunningEvent">The running-Event panel; null while no Event runs (always null until M6).</param>
/// <param name="Overlays">Overlays on top of the surface; each is null when not shown.</param>
/// <param name="Fatal">Set when <paramref name="Surface"/> is <see cref="OperatorSurface.Fatal"/>.</param>
/// <param name="StorageError">Set when <paramref name="Surface"/> is <see cref="OperatorSurface.StorageError"/>.</param>
public sealed record OperatorPresentation(
    OperatorSurface Surface,
    EventListView? EventList,
    RunningEventPanel? RunningEvent,
    OperatorOverlays Overlays,
    FatalView? Fatal,
    StorageErrorView? StorageError);

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
