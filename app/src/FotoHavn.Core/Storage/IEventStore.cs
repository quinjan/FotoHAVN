namespace FotoHavn.Core.Storage;

/// <summary>
/// The store's background removal of deleting Events and Guest Cycles. <see cref="IEventStore.MarkEventDeletingAsync"/> and
/// <see cref="IEventStore.MarkGuestCycleDeletingAsync"/> return as soon as the transaction commits; a background task
/// removes folders, thumbnails, and rows. <see cref="DrainAsync"/> completes when that task is idle, so tests and the
/// reconciler can wait deterministically. The brain never calls it.
/// </summary>
public interface IBackgroundDeletion
{
    /// <summary>Completes when no background deletion is queued or running.</summary>
    Task DrainAsync(CancellationToken ct);
}

/// <summary>
/// The brain's storage port (plan section 10). The Event store is the only writer of Event data. Rules for every member:
/// <list type="bullet">
/// <item>Each call is asynchronous, takes a <see cref="CancellationToken"/>, and is safe to call from the brain's executor;
/// the brain awaits it and posts the result back onto its executor. The store never calls back into the brain.</item>
/// <item>Environment and state failures throw <see cref="StorageException"/>. Contract violations (a malformed id or an
/// out-of-range argument) throw <see cref="ArgumentException"/> before any I/O.</item>
/// <item>Any member other than <see cref="OpenAsync"/> throws <see cref="InvalidOperationException"/> until a call to
/// <see cref="OpenAsync"/> has succeeded.</item>
/// <item>Ids are lowercase GUIDs in <c>D</c> format (<see cref="StorageIds"/>). The brain generates Event ids; the store
/// generates Guest Cycle and Capture ids.</item>
/// </list>
/// </summary>
public interface IEventStore : IBackgroundDeletion, IAsyncDisposable
{
    /// <summary>The absolute Data folder path. The brain shows it to the operator when storage fails; it isn't personal.</summary>
    string DataRoot { get; }

    /// <summary>
    /// Opens storage: classifies the data path, opens SQLite (WAL, <c>synchronous=FULL</c>, <c>foreign_keys=ON</c>,
    /// <c>busy_timeout=5000</c>), runs migrations, runs the integrity check, and runs the startup reconciler. Idempotent
    /// after a failure: Retry calls it again and it starts from scratch. Throws <see cref="StorageException"/> with kind
    /// <see cref="StorageErrorKind.NotLocalDisk"/>, <see cref="StorageErrorKind.CloudSynced"/>,
    /// <see cref="StorageErrorKind.NotWritable"/>, <see cref="StorageErrorKind.IntegrityFailed"/>, or an I/O kind.
    /// </summary>
    Task<StorageOpenResult> OpenAsync(CancellationToken ct);

    /// <summary>
    /// Proves the Data folder accepts writes: writes a small probe file under the Data folder's <c>.staging</c> area, flushes it to
    /// disk, and deletes it. Start Event step 5 uses it, so an Event never starts on storage that can't write. Throws
    /// <see cref="StorageException"/> with the I/O kind the failure maps to (for example
    /// <see cref="StorageErrorKind.NotWritable"/> or <see cref="StorageErrorKind.DiskFull"/>).
    /// </summary>
    Task ProbeWritableAsync(CancellationToken ct);

    /// <summary>
    /// The Events that aren't deleting, by <c>event_date</c> descending, then <c>created_at</c> descending.
    /// </summary>
    Task<IReadOnlyList<EventSummary>> ListEventsAsync(CancellationToken ct);

    /// <summary>The Event, or null when it doesn't exist or is deleting.</summary>
    Task<EventRecord?> GetEventAsync(string eventId, CancellationToken ct);

    /// <summary>
    /// Inserts the Event with its offered Templates, offered Looks, and Phase Timers in one transaction. The store
    /// assigns <see cref="EventRecord.CreatedAt"/> and <see cref="EventRecord.UpdatedAt"/>. Throws
    /// <see cref="StorageErrorKind.Conflict"/> when the id exists. Throws <see cref="ArgumentException"/> when the printer
    /// isn't <see cref="EventRecord.NoPrinter"/>, the countdown isn't 3, 5, or 10, a Phase Timer is negative or repeated,
    /// or a required text value is empty.
    /// </summary>
    Task CreateEventAsync(EventRecord record, CancellationToken ct);

    /// <summary>
    /// Replaces every column except <c>created_at</c> and the PIN, and replaces the offered Templates, Looks, and Phase
    /// Timers, in one transaction. Sets <c>updated_at</c>. Throws <see cref="StorageErrorKind.NotFound"/> when the Event
    /// doesn't exist or is deleting. Same argument rules as <see cref="CreateEventAsync"/>.
    /// </summary>
    Task UpdateEventAsync(EventRecord record, CancellationToken ct);

    /// <summary>
    /// Hashes <paramref name="pin"/> (PBKDF2-SHA256, 16-byte random salt, at least 210,000 iterations) and stores the
    /// hash, salt, and iteration count. Throws <see cref="StorageErrorKind.NotFound"/> when the Event is missing or
    /// deleting, and <see cref="ArgumentException"/> when the PIN is empty. The brain checks digit count.
    /// </summary>
    Task SetPinAsync(string eventId, string pin, CancellationToken ct);

    /// <summary>
    /// Compares <paramref name="pin"/> with the stored hash in constant time. Returns false when the Event has no PIN or
    /// the PIN differs. Throws <see cref="StorageErrorKind.NotFound"/> when the Event is missing or deleting.
    /// </summary>
    Task<bool> VerifyPinAsync(string eventId, string pin, CancellationToken ct);

    /// <summary>
    /// Sets <c>deleting_at</c> in one transaction, so the Event vanishes from lists at once; a background task then
    /// removes its folder, thumbnails, and rows (see <see cref="IBackgroundDeletion"/>). Throws
    /// <see cref="StorageErrorKind.NotFound"/> when the Event is missing or already deleting.
    /// </summary>
    Task MarkEventDeletingAsync(string eventId, CancellationToken ct);

    /// <summary>
    /// Inserts an open Guest Cycle (no end reason) with a new id and the current time. Throws
    /// <see cref="StorageErrorKind.NotFound"/> when the Event is missing or deleting.
    /// </summary>
    Task<GuestCycleRecord> StartGuestCycleAsync(string eventId, CancellationToken ct);

    /// <summary>
    /// Ends an open Guest Cycle with <paramref name="reason"/> and the current time, and deletes the files of its
    /// <c>discarded</c> Captures (D23). <see cref="GuestCycleEndReason.Completed"/> is refused with
    /// <see cref="ArgumentException"/>: a Photo Strip commit completes a cycle. Throws
    /// <see cref="StorageErrorKind.NotFound"/> when the cycle is missing or deleting, and
    /// <see cref="StorageErrorKind.Conflict"/> when it has already ended.
    /// </summary>
    Task EndGuestCycleAsync(string guestCycleId, GuestCycleEndReason reason, CancellationToken ct);

    /// <summary>
    /// Commits a Capture: write <c>.staging/&lt;uuid&gt;.tmp</c>, flush to disk, rename to <c>capture-&lt;sequence&gt;.jpg</c>,
    /// insert the row (state <see cref="CaptureState.Kept"/>), computing the SHA-256 and byte length. A failure before the
    /// row exists leaves no file under its final name. Throws <see cref="StorageErrorKind.NotFound"/> when the cycle is
    /// missing or deleting, <see cref="StorageErrorKind.Conflict"/> when the cycle has ended or the sequence is used, and
    /// <see cref="ArgumentException"/> for a sequence below 1, empty bytes, or a non-positive size.
    /// </summary>
    Task<CaptureRecord> CommitCaptureAsync(CaptureCommit commit, CancellationToken ct);

    /// <summary>
    /// Sets a Capture kept or discarded. A discarded Capture's file stays until the cycle ends. Throws
    /// <see cref="StorageErrorKind.NotFound"/> when the Capture is missing, and <see cref="StorageErrorKind.Conflict"/>
    /// when its Guest Cycle has ended.
    /// </summary>
    Task SetCaptureStateAsync(string captureId, CaptureState state, CancellationToken ct);

    /// <summary>
    /// Commits a Photo Strip with the same staging order as a Capture (the file is <c>photo-strip.png</c>) and, in the
    /// same transaction as the row, ends the cycle as <see cref="GuestCycleEndReason.Completed"/>. Also deletes the
    /// files of the cycle's discarded Captures. The arrangement must be 1 to 4 distinct ids (otherwise
    /// <see cref="ArgumentException"/>) of kept Captures of this cycle (otherwise <see cref="StorageErrorKind.Conflict"/>).
    /// Throws <see cref="StorageErrorKind.NotFound"/> when the cycle is missing or deleting, and
    /// <see cref="StorageErrorKind.Conflict"/> when it has ended or already has a Photo Strip.
    /// </summary>
    Task<PhotoStripRecord> CommitPhotoStripAsync(PhotoStripCommit commit, CancellationToken ct);

    /// <summary>
    /// Sets <c>deleting_at</c> in one transaction, so the cycle vanishes from both Event Gallery views at once; a
    /// background task then removes its folder and thumbnails, then its rows. Works for open and ended cycles. Throws
    /// <see cref="StorageErrorKind.NotFound"/> when the cycle is missing or already deleting.
    /// </summary>
    Task MarkGuestCycleDeletingAsync(string guestCycleId, CancellationToken ct);

    /// <summary>
    /// A page of the Event's Guest Cycles, newest first, with a stable keyset cursor on (<c>started_at</c> descending,
    /// <c>id</c> descending). <paramref name="cursor"/> is null for the first page, otherwise a
    /// <see cref="GuestCyclePage.NextCursor"/> from the same Event and filter. <paramref name="pageSize"/> is 1 to 200.
    /// Throws <see cref="ArgumentException"/> for an invalid cursor or page size.
    /// </summary>
    Task<GuestCyclePage> ListGuestCyclesAsync(string eventId, GuestCycleFilter filter, string? cursor, int pageSize, CancellationToken ct);

    /// <summary>The cycle with its kept Captures (by sequence) and its Photo Strip, or null when missing or deleting.</summary>
    Task<GuestCycleDetail?> GetGuestCycleAsync(string guestCycleId, CancellationToken ct);

    /// <summary>
    /// The thumbnail's path relative to the Data folder, with forward slashes:
    /// <c>Cache/thumbnails/&lt;sourceId&gt;-&lt;sha8&gt;-&lt;width&gt;.jpg</c> (<see cref="ThumbnailName"/>). Generates it on
    /// first request. <paramref name="sourceId"/> is a Capture id, or a Guest Cycle id for its Photo Strip; the width is
    /// 64 to 2048 pixels. Throws <see cref="StorageErrorKind.NotFound"/> when the source or its file doesn't exist.
    /// </summary>
    Task<string> GetThumbnailPathAsync(string sourceId, int width, CancellationToken ct);
}
