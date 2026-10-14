using FotoHavn.Core.Storage;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Events.Setup;

/// <summary>What deleting an Event removes, for the delete confirmation (plan section 8, Event list).</summary>
internal static class EventDeletionCounter
{
    /// <summary>Guest Cycles are listed this many to a page (the store's largest page).</summary>
    internal const int PageSize = 200;

    /// <summary>
    /// Counts the Event's Guest Cycles, the Photo Strips among them, and the kept Captures, through the store's paged list. The
    /// brain calls it off the executor and posts the answer back.
    /// </summary>
    public static async Task<ConfirmationCounts> CountAsync(IEventStore store, string eventId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        var guestCycles = 0;
        var photoStrips = 0;
        var captures = 0;
        string? cursor = null;
        do
        {
            var page = await store.ListGuestCyclesAsync(eventId, GuestCycleFilter.All, cursor, PageSize, ct).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                guestCycles++;
                captures += item.KeptCaptureCount;
                if (item.PhotoStrip is not null)
                {
                    photoStrips++;
                }
            }

            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return new ConfirmationCounts(guestCycles, photoStrips, captures);
    }
}

/// <summary>Where an Event's files live (plan section 10: <c>Data/Events/&lt;eventId&gt;</c>), the same folder the store uses.</summary>
internal static class EventFolders
{
    /// <summary>The Event's folder under the store's Data folder; it holds the Event's id, never its name.</summary>
    public static string For(string dataRoot, string eventId)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataRoot);
        ArgumentException.ThrowIfNullOrEmpty(eventId);
        return Path.Combine(dataRoot, "Events", eventId);
    }
}
