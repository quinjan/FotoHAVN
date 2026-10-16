using FotoHavn.Core.Catalog;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Gallery.Orchestration;

/// <summary>A page of gallery entries built from the store, and the cursor for the one after it.</summary>
/// <typeparam name="TView">The view each entry carries.</typeparam>
/// <param name="Entries">The entries the page contributes, with every thumbnail already made.</param>
/// <param name="NextCursor">The store's cursor for the next page, or null on the last one.</param>
public sealed record GalleryPageResult<TView>(IReadOnlyList<GalleryEntry<TView>> Entries, string? NextCursor);

/// <summary>
/// A Guest Cycle that just ended, as each gallery shows it. A view is null when that gallery isn't shown the cycle (it isn't completed, or its
/// files are gone, or the caller didn't ask for it).
/// </summary>
/// <param name="EventId">The Event the cycle belongs to.</param>
/// <param name="Operator">The operator list's entry.</param>
/// <param name="Guest">The guest list's entry (completed cycles only).</param>
public sealed record EndedCycleEntries(string EventId, GalleryEntry<GalleryItemView>? Operator, GalleryEntry<GuestStripView>? Guest);

/// <summary>
/// Builds what the galleries show from the store (M9 contract section 7): a page of Guest Cycles, then the thumbnail of each item, asked of the
/// store (which makes it on first request) before the item is presented, so a URL always names a file that exists. Items are built with at most
/// <see cref="Parallelism"/> at a time. A source whose file is gone (<see cref="StorageErrorKind.NotFound"/>) makes the operator's item
/// <em>unavailable</em> and leaves the guest's out; any other store failure fails the page. Nothing here touches brain state, so the brain runs it
/// off its executor.
/// </summary>
/// <param name="store">The Event store.</param>
/// <param name="catalog">The Templates and Looks, for the names an operator item carries.</param>
public sealed class GalleryPageBuilder(IEventStore store, ICatalog catalog)
{
    /// <summary>How many items have their thumbnails made at once.</summary>
    public const int Parallelism = 4;

    /// <summary>
    /// One page of the operator list: ended Guest Cycles, newest first. A cold page takes a moment (every thumbnail is decoded and resized), so
    /// <paramref name="onEntry"/>, when given, is called from a worker thread with each item the moment its thumbnails exist: the operator sees the
    /// page fill in, and never an item before its images.
    /// </summary>
    public async Task<GalleryPageResult<GalleryItemView>> OperatorPageAsync(string eventId, string? cursor, CancellationToken ct, Action<GalleryEntry<GalleryItemView>>? onEntry = null)
    {
        var page = await store.ListGuestCyclesAsync(eventId, GuestCycleFilter.All, cursor, GalleryRules.PageSize, ct).ConfigureAwait(false);
        var entries = await BuildAsync(GalleryRules.OperatorRows(page.Items), row => OperatorEntryAsync(row, null, ct), onEntry, ct).ConfigureAwait(false);
        return new GalleryPageResult<GalleryItemView>(entries, page.NextCursor);
    }

    /// <summary>One page of the guest list: completed Photo Strips of the Event, newest first, never one whose file is gone.</summary>
    public async Task<GalleryPageResult<GuestStripView>> GuestPageAsync(string eventId, string? cursor, CancellationToken ct)
    {
        var page = await store.ListGuestCyclesAsync(eventId, GuestCycleFilter.CompletedOnly, cursor, GalleryRules.PageSize, ct).ConfigureAwait(false);
        var entries = await BuildAsync(GalleryRules.GuestRows(page.Items), row => GuestEntryAsync(row, ct), null, ct).ConfigureAwait(false);
        return new GalleryPageResult<GuestStripView>(entries, page.NextCursor);
    }

    /// <summary>
    /// A Guest Cycle that ended: its operator entry when <paramref name="forOperator"/>, and, when it completed, its guest entry when
    /// <paramref name="forGuest"/>. A completed cycle's strip thumbnail is made either way (the warm-up that lets the next page open at once).
    /// Null when the cycle is gone or hasn't ended.
    /// </summary>
    public async Task<EndedCycleEntries?> EndedCycleAsync(string guestCycleId, bool forOperator, bool forGuest, CancellationToken ct)
    {
        var detail = await store.GetGuestCycleAsync(guestCycleId, ct).ConfigureAwait(false);
        if (detail?.Cycle.EndReason is null)
        {
            return null;
        }

        var summary = new GuestCycleSummary(detail.Cycle, detail.Captures.Count(capture => capture.State == CaptureState.Kept), detail.PhotoStrip);
        GalleryEntry<GuestStripView>? guest = null;
        if (GalleryRules.IsGuestVisible(summary))
        {
            // The warm-up is best effort: a strip that can't be thumbnailed yet is made again when someone browses.
            try
            {
                guest = await GuestEntryAsync(summary, ct).ConfigureAwait(false);
            }
            catch (StorageException)
            {
                guest = null;
            }
        }

        var entry = forOperator ? await OperatorEntryAsync(summary, detail, ct).ConfigureAwait(false) : null;
        return new EndedCycleEntries(detail.Cycle.EventId, entry, forGuest ? guest : null);
    }

    private async Task<GalleryEntry<GalleryItemView>?> OperatorEntryAsync(GuestCycleSummary summary, GuestCycleDetail? loaded, CancellationToken ct)
    {
        var cycle = summary.Cycle;
        var strip = summary.PhotoStrip;
        var templateName = strip is null ? null : catalog.FindTemplate(strip.TemplateId)?.Name ?? strip.TemplateId;
        var lookName = strip is null ? null : catalog.FindLook(strip.LookId)?.Name ?? strip.LookId;
        GalleryItemView item;
        if (strip is not null)
        {
            var url = await ThumbnailUrlAsync(cycle.Id, GalleryUrls.StripThumbnailWidth(strip.Width, strip.Height), ct).ConfigureAwait(false);
            item = GalleryRules.OperatorItem(cycle, templateName, lookName, url is null ? null : GalleryRules.StripThumbnail(strip, url), [], url is null);
        }
        else if (summary.KeptCaptureCount == 0)
        {
            item = GalleryRules.OperatorItem(cycle, null, null, null, [], false);
        }
        else
        {
            var detail = loaded ?? await store.GetGuestCycleAsync(cycle.Id, ct).ConfigureAwait(false);
            if (detail is null)
            {
                return null;
            }

            var thumbnails = new List<GalleryCaptureThumbnailView>();
            foreach (var capture in GalleryRules.ArrangedCaptures(detail))
            {
                var url = await ThumbnailUrlAsync(capture.Id, GalleryUrls.CaptureThumbnailWidth(capture.Width, capture.Height), ct).ConfigureAwait(false);
                if (url is null)
                {
                    thumbnails.Clear();
                    break;
                }

                thumbnails.Add(GalleryRules.CaptureThumbnail(capture, url));
            }

            item = GalleryRules.OperatorItem(cycle, null, null, null, thumbnails, thumbnails.Count == 0);
        }

        return new GalleryEntry<GalleryItemView>(cycle.Id, cycle.StartedAt, item);
    }

    private async Task<GalleryEntry<GuestStripView>?> GuestEntryAsync(GuestCycleSummary summary, CancellationToken ct)
    {
        var strip = summary.PhotoStrip!;
        var url = await ThumbnailUrlAsync(summary.Cycle.Id, GalleryUrls.StripThumbnailWidth(strip.Width, strip.Height), ct).ConfigureAwait(false);
        return url is null ? null : new GalleryEntry<GuestStripView>(summary.Cycle.Id, summary.Cycle.StartedAt, GalleryRules.GuestStrip(summary, url));
    }

    /// <summary>The URL of the thumbnail the store made (or already had), or null when its source or file is gone.</summary>
    private async Task<string?> ThumbnailUrlAsync(string sourceId, int width, CancellationToken ct)
    {
        try
        {
            return GalleryUrls.ThumbnailUrlFromStorePath(await store.GetThumbnailPathAsync(sourceId, width, ct).ConfigureAwait(false));
        }
        catch (StorageException exception) when (exception.Kind == StorageErrorKind.NotFound)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<GalleryEntry<TView>>> BuildAsync<TRow, TView>(
        IReadOnlyList<TRow> rows,
        Func<TRow, Task<GalleryEntry<TView>?>> build,
        Action<GalleryEntry<TView>>? onEntry,
        CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(Parallelism);
        var tasks = rows.Select(async row =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var entry = await build(row).ConfigureAwait(false);
                if (entry is not null)
                {
                    onEntry?.Invoke(entry);
                }

                return entry;
            }
            finally
            {
                gate.Release();
            }
        });
        var built = await Task.WhenAll(tasks).ConfigureAwait(false);
        return [.. built.OfType<GalleryEntry<TView>>()];
    }
}
