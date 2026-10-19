namespace FotoHavn.Core.Storage;

// The records the Event store speaks (plan section 10). They are plain data: no behavior, no SQL. Times are
// DateTimeOffset values the store writes as UTC (see StorageTimestamp); dates are DateOnly written as yyyy-MM-dd.

/// <summary>The phases that have a Phase Timer (plan section 7). The SQL strings are in <see cref="StorageSqlNames"/>.</summary>
public enum PhaseTimerPhase
{
    Template,
    PhotographsCapture,
    PhotographsReview,
    Filters,
    Keepsake,
    GuestGallery,
}

/// <summary>One Phase Timer of an Event: its length in seconds (0 or more; 0 means no timer).</summary>
public sealed record PhaseTimerSetting(PhaseTimerPhase Phase, int Seconds);

/// <summary>
/// An Event as saved: every <c>events</c> column except the PIN and <c>deleting_at</c>, plus the offered Template ids,
/// offered Look ids, and Phase Timers. The PIN lives behind <see cref="IEventStore.SetPinAsync"/>. A deleting Event is
/// never returned.
/// </summary>
/// <param name="Id">The Event id: a lowercase GUID in <c>D</c> format (<see cref="StorageIds"/>). The brain generates it.</param>
/// <param name="Name">The Event name.</param>
/// <param name="EventDate">The Event's local date.</param>
/// <param name="Venue">Optional.</param>
/// <param name="CustomerName">Optional: the Customer or host.</param>
/// <param name="Notes">Optional.</param>
/// <param name="CameraDeviceId">The bound camera's device id, if chosen.</param>
/// <param name="CameraName">The bound camera's name when it was chosen, for display when it is missing.</param>
/// <param name="GuestDisplayId">The Guest Display Binding's display id, if chosen.</param>
/// <param name="GuestDisplayName">The bound display's name when it was chosen.</param>
/// <param name="Printer">Always <see cref="NoPrinter"/> in v1 (the schema's CHECK).</param>
/// <param name="Mirror">Whether the preview and Captures are mirrored.</param>
/// <param name="CountdownSeconds">3, 5, or 10 (the schema's CHECK).</param>
/// <param name="DefaultTemplateId">The default Template.</param>
/// <param name="DefaultLookId">The default Look.</param>
/// <param name="GuestGalleryEnabled">Whether the Guest Gallery is on.</param>
/// <param name="GuestSoundsEnabled">Whether the Guest Screen plays the countdown tick and the shutter sound.</param>
/// <param name="OfferedTemplateIds">The Templates offered to guests, in the order saved.</param>
/// <param name="OfferedLookIds">The Looks offered to guests, in the order saved.</param>
/// <param name="PhaseTimers">The Phase Timers, at most one per phase.</param>
public sealed record EventRecord(
    string Id,
    string Name,
    DateOnly EventDate,
    string? Venue,
    string? CustomerName,
    string? Notes,
    string? CameraDeviceId,
    string? CameraName,
    string? GuestDisplayId,
    string? GuestDisplayName,
    string Printer,
    bool Mirror,
    int CountdownSeconds,
    string DefaultTemplateId,
    string DefaultLookId,
    bool GuestGalleryEnabled,
    bool GuestSoundsEnabled,
    IReadOnlyList<string> OfferedTemplateIds,
    IReadOnlyList<string> OfferedLookIds,
    IReadOnlyList<PhaseTimerSetting> PhaseTimers)
{
    /// <summary>The only printer value v1 accepts.</summary>
    public const string NoPrinter = "none";

    /// <summary>Output only: when the Event was created. The store assigns it; a supplied value is ignored.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Output only: when the Event was last saved. The store assigns it on every write; a supplied value is ignored.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Output only: whether the Event has an Operator PIN. Ignored on write; use <see cref="IEventStore.SetPinAsync"/>.</summary>
    public bool HasPin { get; init; }
}

/// <summary>An Event Gallery or Event list row's facts (plan section 8, Event list).</summary>
/// <param name="Id">The Event id.</param>
/// <param name="Name">The Event name.</param>
/// <param name="EventDate">The Event's local date.</param>
/// <param name="Venue">Optional.</param>
/// <param name="CompletedGuestCycles">Guest Cycles with end reason <c>completed</c> that aren't deleting.</param>
/// <param name="LastUsedAt">The latest Guest Cycle start, or null if none.</param>
public sealed record EventSummary(
    string Id,
    string Name,
    DateOnly EventDate,
    string? Venue,
    int CompletedGuestCycles,
    DateTimeOffset? LastUsedAt);

/// <summary>Why a Guest Cycle ended. The SQL strings are in <see cref="StorageSqlNames"/>.</summary>
public enum GuestCycleEndReason
{
    /// <summary>The Photo Strip was committed. Set only by <see cref="IEventStore.CommitPhotoStripAsync"/>.</summary>
    Completed,

    StartedOver,
    OperatorExit,
    AppClosed,
    Unrecoverable,
}

/// <summary>One Guest Cycle row.</summary>
/// <param name="Id">The Guest Cycle id (a lowercase GUID in <c>D</c> format; the store generates it).</param>
/// <param name="EventId">The owning Event.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="EndedAt">When it ended, or null while it is open.</param>
/// <param name="EndReason">Why it ended, or null while it is open.</param>
public sealed record GuestCycleRecord(
    string Id,
    string EventId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    GuestCycleEndReason? EndReason);

/// <summary>Whether a Capture is part of the Guest Cycle's result. A <c>discarded</c> Capture's file goes when the cycle ends (D23).</summary>
public enum CaptureState
{
    Kept,
    Discarded,
}

/// <summary>A Capture to commit: JPEG bytes the camera adapter produced (already mirrored if the Event says so).</summary>
/// <param name="GuestCycleId">The open Guest Cycle.</param>
/// <param name="Sequence">1 or more, unique within the cycle (the schema's CHECK and UNIQUE). The file is <c>capture-&lt;sequence&gt;.jpg</c>.</param>
/// <param name="Jpeg">The JPEG bytes. Not empty.</param>
/// <param name="Width">The pixel width, above zero.</param>
/// <param name="Height">The pixel height, above zero.</param>
/// <param name="Mirrored">Whether the bytes are mirrored.</param>
/// <param name="TakenAt">When the frame was taken.</param>
public sealed record CaptureCommit(
    string GuestCycleId,
    int Sequence,
    ReadOnlyMemory<byte> Jpeg,
    int Width,
    int Height,
    bool Mirrored,
    DateTimeOffset TakenAt);

/// <summary>A committed Capture row.</summary>
/// <param name="Id">The Capture id (the store generates it).</param>
/// <param name="GuestCycleId">The owning Guest Cycle.</param>
/// <param name="Sequence">The sequence number.</param>
/// <param name="FileName">The file name inside the Guest Cycle folder (<c>capture-&lt;sequence&gt;.jpg</c>).</param>
/// <param name="ByteLength">The file size.</param>
/// <param name="Sha256">The lowercase hex SHA-256 of the file.</param>
/// <param name="Width">The pixel width.</param>
/// <param name="Height">The pixel height.</param>
/// <param name="Mirrored">Whether the bytes are mirrored.</param>
/// <param name="TakenAt">When the frame was taken.</param>
/// <param name="State">Kept or discarded.</param>
public sealed record CaptureRecord(
    string Id,
    string GuestCycleId,
    int Sequence,
    string FileName,
    long ByteLength,
    string Sha256,
    int Width,
    int Height,
    bool Mirrored,
    DateTimeOffset TakenAt,
    CaptureState State);

/// <summary>A Photo Strip to commit. Committing it also ends the Guest Cycle as <see cref="GuestCycleEndReason.Completed"/>.</summary>
/// <param name="GuestCycleId">The open Guest Cycle.</param>
/// <param name="TemplateId">The Template used.</param>
/// <param name="LookId">The Look used.</param>
/// <param name="Arrangement">One to four distinct ids of the cycle's kept Captures, one per window, in window order.</param>
/// <param name="Png">The composed PNG bytes. Not empty.</param>
/// <param name="Width">The pixel width, above zero.</param>
/// <param name="Height">The pixel height, above zero.</param>
public sealed record PhotoStripCommit(
    string GuestCycleId,
    string TemplateId,
    string LookId,
    IReadOnlyList<string> Arrangement,
    ReadOnlyMemory<byte> Png,
    int Width,
    int Height);

/// <summary>A committed Photo Strip row. The file is <c>photo-strip.png</c> in the Guest Cycle folder.</summary>
/// <param name="GuestCycleId">The owning Guest Cycle (the primary key: one Photo Strip per cycle).</param>
/// <param name="TemplateId">The Template used.</param>
/// <param name="LookId">The Look used.</param>
/// <param name="Arrangement">The Capture ids, one per window, in window order.</param>
/// <param name="FileName">Always <c>photo-strip.png</c>.</param>
/// <param name="ByteLength">The file size.</param>
/// <param name="Sha256">The lowercase hex SHA-256 of the file.</param>
/// <param name="Width">The pixel width.</param>
/// <param name="Height">The pixel height.</param>
/// <param name="CommittedAt">When it was committed (also the cycle's <c>ended_at</c>).</param>
public sealed record PhotoStripRecord(
    string GuestCycleId,
    string TemplateId,
    string LookId,
    IReadOnlyList<string> Arrangement,
    string FileName,
    long ByteLength,
    string Sha256,
    int Width,
    int Height,
    DateTimeOffset CommittedAt);

/// <summary>Which Guest Cycles <see cref="IEventStore.ListGuestCyclesAsync"/> returns. Deleting cycles are never returned.</summary>
public enum GuestCycleFilter
{
    /// <summary>The operator's Event Gallery: every Guest Cycle.</summary>
    All,

    /// <summary>The Guest Gallery: cycles with end reason <c>completed</c> that have a Photo Strip.</summary>
    CompletedOnly,
}

/// <summary>One Event Gallery row: a Guest Cycle with what its tile needs.</summary>
/// <param name="Cycle">The Guest Cycle.</param>
/// <param name="KeptCaptureCount">How many kept Captures it has.</param>
/// <param name="PhotoStrip">Its Photo Strip, or null when it has none.</param>
public sealed record GuestCycleSummary(GuestCycleRecord Cycle, int KeptCaptureCount, PhotoStripRecord? PhotoStrip);

/// <summary>A page of Guest Cycles, newest first.</summary>
/// <param name="Items">The page's rows.</param>
/// <param name="NextCursor">An opaque cursor for the next page, or null on the last page.</param>
public sealed record GuestCyclePage(IReadOnlyList<GuestCycleSummary> Items, string? NextCursor);

/// <summary>A Guest Cycle with its kept Captures (by sequence) and its Photo Strip, for the detail view.</summary>
public sealed record GuestCycleDetail(GuestCycleRecord Cycle, IReadOnlyList<CaptureRecord> Captures, PhotoStripRecord? PhotoStrip);

/// <summary>What the startup reconciler repaired (plan section 10, Startup reconciler, steps 1 to 7).</summary>
/// <param name="StagingFilesDeleted">Step 1: files removed from <c>.staging/</c> folders.</param>
/// <param name="OrphanFilesDeleted">Step 2: files under <c>Data/Events</c> with no row.</param>
/// <param name="ArtifactProblems">Step 3: rows whose file is missing or whose size or hash differs.</param>
/// <param name="CyclesMarkedUnrecoverable">Step 3: unfinished Guest Cycles set to <c>unrecoverable</c>.</param>
/// <param name="CyclesClosedAsAppClosed">Step 4: unfinished Guest Cycles set to <c>app_closed</c>.</param>
/// <param name="GuestCyclesDeleted">Step 5: Guest Cycles marked deleting whose folders and rows were removed.</param>
/// <param name="EventsDeleted">Step 5: Events marked deleting whose folders and rows were removed.</param>
/// <param name="DiscardedFilesDeleted">Step 6: files of discarded Captures removed.</param>
/// <param name="OrphanThumbnailsDeleted">Step 7: thumbnails whose source no longer exists.</param>
/// <param name="Problems">One non-personal sentence per problem found (ids and file names only, no paths or Event names).</param>
public sealed record ReconcileReport(
    int StagingFilesDeleted,
    int OrphanFilesDeleted,
    int ArtifactProblems,
    int CyclesMarkedUnrecoverable,
    int CyclesClosedAsAppClosed,
    int GuestCyclesDeleted,
    int EventsDeleted,
    int DiscardedFilesDeleted,
    int OrphanThumbnailsDeleted,
    IReadOnlyList<string> Problems)
{
    /// <summary>A run that found nothing to repair.</summary>
    public static ReconcileReport Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, []);

    /// <summary>Whether any step repaired or deleted anything.</summary>
    public bool HasRepairs =>
        StagingFilesDeleted + OrphanFilesDeleted + ArtifactProblems + CyclesMarkedUnrecoverable + CyclesClosedAsAppClosed
        + GuestCyclesDeleted + EventsDeleted + DiscardedFilesDeleted + OrphanThumbnailsDeleted > 0
        || Problems.Count > 0;
}

/// <summary>What <see cref="IEventStore.OpenAsync"/> found and did.</summary>
/// <param name="SchemaVersion">The <c>PRAGMA user_version</c> after migrations.</param>
/// <param name="Reconcile">What the startup reconciler repaired.</param>
public sealed record StorageOpenResult(int SchemaVersion, ReconcileReport Reconcile);
