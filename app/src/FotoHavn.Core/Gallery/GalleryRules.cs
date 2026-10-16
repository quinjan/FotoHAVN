using FotoHavn.Core.Storage;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Gallery;

/// <summary>The words of the Event Gallery that carry domain meaning and come from the brain (plan section 9, Copy).</summary>
public static class GalleryCopy
{
    /// <summary>The alt text of every Photo Strip in the guest views: no names or faces described.</summary>
    public const string StripAlt = "Photo Strip from this Event";

    /// <summary>The alt text of every Capture in the guest strip detail.</summary>
    public const string CaptureAlt = "Photograph from this Event";
}

/// <summary>
/// The Event Gallery's pure rules (plan section 8, Event Gallery; M9 contract): who sees which Guest Cycle, the Start link, how an end reason
/// reads, and how store records become the views each surface renders from. Nothing here does I/O or reads a clock. The guest builders take only
/// what a guest may see and produce views with nothing else in them, so no metadata can reach the Guest Screen through them.
/// </summary>
public static class GalleryRules
{
    /// <summary>Guest Cycles per page, in both views (plan section 8).</summary>
    public const int PageSize = 40;

    /// <summary>
    /// Whether <c>SEE THE GOOD COMPANY →</c> shows on Start: the Event enables the guest gallery and its Active Event has at least one
    /// completed Photo Strip that isn't being deleted.
    /// </summary>
    public static bool GuestLinkVisible(bool guestGalleryEnabled, int completedPhotoStrips) => guestGalleryEnabled && completedPhotoStrips > 0;

    /// <summary>How a Guest Cycle's end reason reads in the operator gallery.</summary>
    public static GalleryOutcome OutcomeOf(GuestCycleEndReason reason) => reason switch
    {
        GuestCycleEndReason.Completed => GalleryOutcome.Completed,
        GuestCycleEndReason.StartedOver => GalleryOutcome.StartedOver,
        GuestCycleEndReason.OperatorExit => GalleryOutcome.EndedByOperator,
        GuestCycleEndReason.AppClosed => GalleryOutcome.AppClosed,
        GuestCycleEndReason.Unrecoverable => GalleryOutcome.Unrecoverable,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Not an end reason."),
    };

    /// <summary>The operator list shows every Guest Cycle that has ended; an open one appears when it ends.</summary>
    public static bool IsOperatorListed(GuestCycleSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return summary.Cycle.EndReason is not null;
    }

    /// <summary>The guest gallery shows only completed Guest Cycles that have a Photo Strip. (The store's <see cref="GuestCycleFilter.CompletedOnly"/> already filters; the brain checks again.)</summary>
    public static bool IsGuestVisible(GuestCycleSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return summary.Cycle.EndReason == GuestCycleEndReason.Completed && summary.PhotoStrip is not null;
    }

    /// <summary>The guest strip detail's rule, on a loaded Guest Cycle.</summary>
    public static bool IsGuestVisible(GuestCycleDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return detail.Cycle.EndReason == GuestCycleEndReason.Completed && detail.PhotoStrip is not null;
    }

    /// <summary>The rows of a store page the operator list shows, in the store's order.</summary>
    public static IReadOnlyList<GuestCycleSummary> OperatorRows(IEnumerable<GuestCycleSummary> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows.Where(IsOperatorListed)];
    }

    /// <summary>The rows of a store page the guest list shows, in the store's order.</summary>
    public static IReadOnlyList<GuestCycleSummary> GuestRows(IEnumerable<GuestCycleSummary> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows.Where(IsGuestVisible)];
    }

    /// <summary>The kept Captures in the order the Photo Strip arranged them (windows in order), or by sequence when there is no Photo Strip. A Capture the strip names but the cycle no longer keeps is skipped.</summary>
    public static IReadOnlyList<CaptureRecord> ArrangedCaptures(GuestCycleDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        var kept = detail.Captures.Where(capture => capture.State == CaptureState.Kept).OrderBy(capture => capture.Sequence).ToList();
        if (detail.PhotoStrip is not { } strip)
        {
            return kept;
        }

        var byId = kept.ToDictionary(capture => capture.Id, StringComparer.Ordinal);
        return [.. strip.Arrangement.Where(byId.ContainsKey).Select(id => byId[id])];
    }

    /// <summary>The files deleting the Guest Cycle removes: its kept Captures and its Photo Strip. Derived thumbnails aren't counted.</summary>
    public static int FileCount(GuestCycleDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return detail.Captures.Count(capture => capture.State == CaptureState.Kept) + (detail.PhotoStrip is null ? 0 : 1);
    }

    /// <summary>The Guest Cycle's folder, <c>&lt;dataRoot&gt;\Events\&lt;eventId&gt;\GuestCycles\&lt;guestCycleId&gt;</c>, which <c>OPEN FOLDER</c> asks the host to open. It holds the Event's and the cycle's ids, never a name.</summary>
    public static string GuestCycleFolder(string dataRoot, string eventId, string guestCycleId)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataRoot);
        StorageIds.ThrowIfInvalid(eventId, nameof(eventId));
        StorageIds.ThrowIfInvalid(guestCycleId, nameof(guestCycleId));
        return Path.Combine(dataRoot, "Events", eventId, "GuestCycles", guestCycleId);
    }

    /// <summary>One operator list item. <paramref name="captures"/> are kept only when there is no strip.</summary>
    public static GalleryItemView OperatorItem(
        GuestCycleRecord cycle,
        string? templateName,
        string? lookName,
        GalleryStripThumbnailView? strip,
        IReadOnlyList<GalleryCaptureThumbnailView> captures,
        bool unavailable)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(captures);
        if (cycle.EndReason is not { } reason)
        {
            throw new ArgumentException("An open Guest Cycle isn't listed.", nameof(cycle));
        }

        return new GalleryItemView(cycle.Id, cycle.StartedAt, OutcomeOf(reason), templateName, lookName, strip, strip is null ? captures : [], unavailable);
    }

    /// <summary>A Photo Strip's list thumbnail view: its native size gives the proportion; <paramref name="thumbnailUrl"/> is the derived JPEG.</summary>
    public static GalleryStripThumbnailView StripThumbnail(PhotoStripRecord strip, string thumbnailUrl)
    {
        ArgumentNullException.ThrowIfNull(strip);
        return new GalleryStripThumbnailView(thumbnailUrl, strip.Width, strip.Height, strip.Width > strip.Height);
    }

    /// <summary>A Capture's list thumbnail view.</summary>
    public static GalleryCaptureThumbnailView CaptureThumbnail(CaptureRecord capture, string thumbnailUrl)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return new GalleryCaptureThumbnailView(thumbnailUrl, capture.Width, capture.Height);
    }

    /// <summary>The operator's Guest Cycle detail for a loaded, ended Guest Cycle.</summary>
    public static GuestCycleDetailView OperatorDetail(
        string eventId,
        string eventName,
        GuestCycleDetail detail,
        string? templateName,
        string? lookName,
        bool unavailable,
        bool canDelete)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(detail);
        if (detail.Cycle.EndReason is not { } reason)
        {
            throw new ArgumentException("An open Guest Cycle has no detail.", nameof(detail));
        }

        return new GuestCycleDetailView(
            eventId,
            eventName,
            detail.Cycle.Id,
            detail.Cycle.StartedAt,
            OutcomeOf(reason),
            templateName,
            lookName,
            detail.PhotoStrip is { } strip ? new GalleryStripImageView(GalleryUrls.StripUrl(eventId, strip), strip.Width, strip.Height, strip.Width > strip.Height) : null,
            [.. ArrangedCaptures(detail).Select(capture => new GalleryCaptureImageView(GalleryUrls.CaptureUrl(eventId, capture), capture.Width, capture.Height))],
            unavailable,
            CanOpenFolder: true,
            canDelete);
    }

    /// <summary>The delete confirmation for a loaded Guest Cycle: it names the time and counts the files.</summary>
    public static ConfirmationOverlay DeleteConfirmation(GuestCycleDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return new ConfirmationOverlay(
            ConfirmationKind.DeleteGuestCycle,
            null,
            null,
            new DeleteGuestCycleConfirmation(detail.Cycle.Id, detail.Cycle.StartedAt, FileCount(detail)));
    }

    /// <summary>One guest list item: the Photo Strip's thumbnail, its proportion, and its alt text, taken from a visible Guest Cycle and nothing else.</summary>
    /// <exception cref="ArgumentException">The Guest Cycle isn't visible to guests (<see cref="IsGuestVisible(GuestCycleSummary)"/>).</exception>
    public static GuestStripView GuestStrip(GuestCycleSummary summary, string thumbnailUrl)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (!IsGuestVisible(summary))
        {
            throw new ArgumentException("Only a completed Guest Cycle with a Photo Strip is visible to guests.", nameof(summary));
        }

        var strip = summary.PhotoStrip!;
        return new GuestStripView(summary.Cycle.Id, thumbnailUrl, strip.Width, strip.Height, strip.Width > strip.Height, GalleryCopy.StripAlt);
    }

    /// <summary>The guest strip detail: the Photo Strip large and its arranged Captures, never a discarded or unarranged one.</summary>
    /// <exception cref="ArgumentException">The Guest Cycle isn't visible to guests (<see cref="IsGuestVisible(GuestCycleDetail)"/>).</exception>
    public static GuestStripDetailView GuestStripDetail(string eventId, GuestCycleDetail detail)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(detail);
        if (!IsGuestVisible(detail))
        {
            throw new ArgumentException("Only a completed Guest Cycle with a Photo Strip is visible to guests.", nameof(detail));
        }

        var strip = detail.PhotoStrip!;
        return new GuestStripDetailView(
            new GuestStripImageView(GalleryUrls.StripUrl(eventId, strip), strip.Width, strip.Height, strip.Width > strip.Height, GalleryCopy.StripAlt),
            [.. ArrangedCaptures(detail).Select(capture => new GuestCaptureImageView(GalleryUrls.CaptureUrl(eventId, capture), capture.Width, capture.Height, GalleryCopy.CaptureAlt))]);
    }
}
