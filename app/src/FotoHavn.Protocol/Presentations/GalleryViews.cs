namespace FotoHavn.Protocol.Presentations;

// The Event Gallery's views (plan section 8, Event Gallery; M9 contract). One gallery per Event with two views: the operator's
// (the whole record, with delete) and the guest's (images and alt text only). Each surface renders from its view alone. Media
// URLs are built by the brain; the UI never makes one. Times are instants (UTC, as in the Event list); the UI shows them in the
// operator machine's local time.

/// <summary>How a Guest Cycle ended, as the operator gallery names it. The wire names are the camelCase of the member.</summary>
public enum GalleryOutcome
{
    /// <summary>Completed by the guest or by Phase Timers (D28): it has a Photo Strip.</summary>
    Completed,
    StartedOver,
    EndedByOperator,
    AppClosed,
    Unrecoverable,
}

/// <summary>A Photo Strip's thumbnail in the operator list: <c>https://media.fotohavn/thumbnails/&lt;sourceId&gt;-&lt;sha8&gt;-&lt;width&gt;.jpg</c>.</summary>
/// <param name="ThumbnailUrl">The derived JPEG, which the brain made sure exists before presenting the item.</param>
/// <param name="Width">The full Photo Strip's width in pixels, for its proportion: the thumbnail has the same proportion.</param>
/// <param name="Height">The full Photo Strip's height in pixels.</param>
/// <param name="Landscape">Whether the strip is wider than tall. Operator views keep every strip in native orientation.</param>
public sealed record GalleryStripThumbnailView(string ThumbnailUrl, int Width, int Height, bool Landscape);

/// <summary>A kept Capture's thumbnail in the operator list, shown when the Guest Cycle has no Photo Strip.</summary>
/// <param name="ThumbnailUrl">The derived JPEG.</param>
/// <param name="Width">The full Capture's width in pixels.</param>
/// <param name="Height">The full Capture's height in pixels.</param>
public sealed record GalleryCaptureThumbnailView(string ThumbnailUrl, int Width, int Height);

/// <summary>One Guest Cycle in the operator gallery list. An open (unfinished) Guest Cycle is never listed: it appears when it ends.</summary>
/// <param name="GuestCycleId">The Guest Cycle's id (what <c>OpenGuestCycle</c>, <c>OpenGuestCycleFolder</c> and <c>DeleteGuestCycle</c> take).</param>
/// <param name="StartedAt">When it began (an instant).</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="TemplateName">The Template's name (brain copy), or null when the cycle has no Photo Strip (the Template is recorded with the strip).</param>
/// <param name="LookName">The Look's name (brain copy), or null when the cycle has no Photo Strip.</param>
/// <param name="Strip">The Photo Strip's thumbnail, or null when there is none.</param>
/// <param name="Captures">The kept Captures' thumbnails in sequence order; filled only when <paramref name="Strip"/> is null (the UI says "No Photo Strip"), otherwise empty.</param>
/// <param name="Unavailable">Whether the files can't be read (the reconciler kept a row whose file is missing, plan section 10): the item has no images and says so.</param>
public sealed record GalleryItemView(
    string GuestCycleId,
    DateTimeOffset StartedAt,
    GalleryOutcome Outcome,
    string? TemplateName,
    string? LookName,
    GalleryStripThumbnailView? Strip,
    IReadOnlyList<GalleryCaptureThumbnailView> Captures,
    bool Unavailable);

/// <summary>The operator's Event Gallery (<c>surface: eventGallery</c>): the Event's ended Guest Cycles, newest first, 40 per page.</summary>
/// <param name="EventId">The Event.</param>
/// <param name="EventName">The Event's name (for the eyebrow).</param>
/// <param name="Running">Whether this is the Active Event: the gallery is live, and new Guest Cycles appear as they end.</param>
/// <param name="Items">The Guest Cycles loaded so far, newest first.</param>
/// <param name="NextCursor">The cursor for <c>LoadMoreGuestCycles</c>; null when every page is loaded (or while the first page loads).</param>
/// <param name="LoadingMore">Whether a page is loading (the first page while <paramref name="Items"/> is empty and <paramref name="Empty"/> is false).</param>
/// <param name="LoadFailed">Whether the last page failed to load: the UI offers to try again (<c>OpenGallery</c> for the first page, <c>LoadMoreGuestCycles</c> with <paramref name="NextCursor"/> otherwise).</param>
/// <param name="Empty">Whether the first page loaded and has no Guest Cycles: "No photographs yet."</param>
public sealed record EventGalleryView(
    string EventId,
    string EventName,
    bool Running,
    IReadOnlyList<GalleryItemView> Items,
    string? NextCursor,
    bool LoadingMore,
    bool LoadFailed,
    bool Empty);

/// <summary>The full Photo Strip in the operator detail: <c>https://media.fotohavn/events/&lt;eventId&gt;/guest-cycles/&lt;guestCycleId&gt;/photo-strip.png?v=&lt;sha8&gt;</c>.</summary>
/// <param name="ImageUrl">The saved file.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Landscape">Whether it is wider than tall.</param>
public sealed record GalleryStripImageView(string ImageUrl, int Width, int Height, bool Landscape);

/// <summary>A full kept Capture in the operator detail: <c>…/capture-&lt;sequence&gt;.jpg?v=&lt;sha8&gt;</c>.</summary>
/// <param name="ImageUrl">The saved file.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
public sealed record GalleryCaptureImageView(string ImageUrl, int Width, int Height);

/// <summary>The operator's Guest Cycle detail (<c>surface: guestCycleDetail</c>): the full Photo Strip and its kept Captures with the same metadata as the list.</summary>
/// <param name="EventId">The Event.</param>
/// <param name="EventName">The Event's name.</param>
/// <param name="GuestCycleId">The Guest Cycle.</param>
/// <param name="StartedAt">When it began (an instant).</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="TemplateName">The Template's name, or null when there is no Photo Strip.</param>
/// <param name="LookName">The Look's name, or null when there is no Photo Strip.</param>
/// <param name="Strip">The full Photo Strip, or null when there is none ("No Photo Strip").</param>
/// <param name="Captures">The kept Captures in arrangement order when there is a Photo Strip (the Captures it uses), otherwise in sequence order. Empty shows "No Captures".</param>
/// <param name="Unavailable">Whether the files can't be read.</param>
/// <param name="CanOpenFolder">Whether <c>OPEN FOLDER</c> works.</param>
/// <param name="CanDelete">Whether <c>DELETE</c> works (false while a delete confirmation is being prepared).</param>
public sealed record GuestCycleDetailView(
    string EventId,
    string EventName,
    string GuestCycleId,
    DateTimeOffset StartedAt,
    GalleryOutcome Outcome,
    string? TemplateName,
    string? LookName,
    GalleryStripImageView? Strip,
    IReadOnlyList<GalleryCaptureImageView> Captures,
    bool Unavailable,
    bool CanOpenFolder,
    bool CanDelete);

/// <summary>
/// The delete confirmation for one Guest Cycle, inside the operator <c>confirmation</c> overlay: it names the time and counts the files
/// the delete removes (kept Captures plus the Photo Strip; thumbnails are derived and not counted).
/// </summary>
/// <param name="GuestCycleId">The Guest Cycle the confirmation is for.</param>
/// <param name="StartedAt">When it began (an instant); the UI names it in local time.</param>
/// <param name="FileCount">How many files deleting removes.</param>
public sealed record DeleteGuestCycleConfirmation(string GuestCycleId, DateTimeOffset StartedAt, int FileCount);

/// <summary>
/// One Photo Strip in the guest gallery row: an image, its proportion, and its alternative text, and nothing else. The guest view
/// carries no time, name, Template, Look, end reason, count, or action, so the UI can't show what a guest must not see.
/// </summary>
/// <param name="GuestCycleId">Opaque: what <c>OpenGuestStrip</c> takes. Never shown.</param>
/// <param name="ThumbnailUrl">The derived JPEG of the Photo Strip.</param>
/// <param name="Width">The full strip's width in pixels, for its proportion.</param>
/// <param name="Height">The full strip's height in pixels.</param>
/// <param name="Landscape">Whether the strip is wider than tall (the guest gallery and detail turn a landscape strip a quarter counterclockwise).</param>
/// <param name="Alt">"Photo Strip from this Event", with no names or faces described.</param>
public sealed record GuestStripView(string GuestCycleId, string ThumbnailUrl, int Width, int Height, bool Landscape, string Alt);

/// <summary>The guest gallery (<c>surface: guestGallery</c>): the Active Event's completed Photo Strips, newest first.</summary>
/// <param name="Strips">The strips loaded so far, newest first.</param>
/// <param name="NextCursor">The cursor for <c>LoadMoreGuestStrips</c>; null when every page is loaded.</param>
/// <param name="LoadingMore">Whether a page is loading ("Loading more photographs."); with no <paramref name="Strips"/> it is the first page.</param>
public sealed record GuestGalleryView(IReadOnlyList<GuestStripView> Strips, string? NextCursor, bool LoadingMore);

/// <summary>The Photo Strip shown large in the guest detail.</summary>
/// <param name="ImageUrl">The saved file.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Landscape">Whether it is wider than tall.</param>
/// <param name="Alt">"Photo Strip from this Event".</param>
public sealed record GuestStripImageView(string ImageUrl, int Width, int Height, bool Landscape, string Alt);

/// <summary>An arranged Capture shown in the guest detail. Discarded and unarranged Captures never appear.</summary>
/// <param name="ImageUrl">The saved file.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Alt">"Photograph from this Event", with no names or faces described.</param>
public sealed record GuestCaptureImageView(string ImageUrl, int Width, int Height, string Alt);

/// <summary>The guest strip detail (<c>surface: guestStripDetail</c>): the strip large with its arranged Captures.</summary>
/// <param name="Strip">The Photo Strip.</param>
/// <param name="Captures">The arranged Captures in window order.</param>
public sealed record GuestStripDetailView(GuestStripImageView Strip, IReadOnlyList<GuestCaptureImageView> Captures);
