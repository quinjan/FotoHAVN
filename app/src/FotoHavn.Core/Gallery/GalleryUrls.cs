using FotoHavn.Core.GuestCycles.Orchestration;
using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Gallery;

/// <summary>
/// The media URLs and thumbnail sizes of the Event Gallery (plan section 9, Images). The UI receives URLs and never builds one; the host
/// serves them read-only from <c>Data/Events</c> and <c>Data/Cache/thumbnails</c> (<c>MediaPathPolicy</c>).
/// </summary>
public static class GalleryUrls
{
    /// <summary>The long edge, in pixels, of a Photo Strip's list thumbnail: chosen for the guest gallery's row on a 2560 × 1440 screen (the operator list shares the same file).</summary>
    public const int StripThumbnailLongEdge = 1200;

    /// <summary>The long edge, in pixels, of a Capture's list thumbnail (shown on the operator list for a Guest Cycle with no Photo Strip).</summary>
    public const int CaptureThumbnailLongEdge = 480;

    private const string ThumbnailPrefix = "https://media.fotohavn/thumbnails/";

    /// <summary>The width (the thumbnail's file-name width) of a Photo Strip thumbnail: the long edge scaled to <see cref="StripThumbnailLongEdge"/>, never above the source.</summary>
    public static int StripThumbnailWidth(int width, int height) => ThumbnailWidth(width, height, StripThumbnailLongEdge);

    /// <summary>The width of a Capture thumbnail: the long edge scaled to <see cref="CaptureThumbnailLongEdge"/>, never above the source.</summary>
    public static int CaptureThumbnailWidth(int width, int height) => ThumbnailWidth(width, height, CaptureThumbnailLongEdge);

    /// <summary>The URL of a thumbnail (<c>https://media.fotohavn/thumbnails/&lt;sourceId&gt;-&lt;sha8&gt;-&lt;width&gt;.jpg</c>) for a source whose SHA-256 is <paramref name="sha256"/>.</summary>
    public static string ThumbnailUrl(string sourceId, string sha256, int width) => ThumbnailPrefix + ThumbnailName.Format(sourceId, sha256, width);

    /// <summary>
    /// The URL of the thumbnail <see cref="IEventStore.GetThumbnailPathAsync"/> made: the store's path (<c>Cache/thumbnails/&lt;file&gt;</c>, forward slashes)
    /// with the media prefix. The brain presents this URL only after the store answered, so the file exists.
    /// </summary>
    /// <exception cref="ArgumentException">The path isn't under <c>Cache/thumbnails/</c>.</exception>
    public static string ThumbnailUrlFromStorePath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var prefix = ThumbnailName.Folder + "/";
        if (!relativePath.StartsWith(prefix, StringComparison.Ordinal) || relativePath.Length == prefix.Length || relativePath.IndexOf('/', prefix.Length) >= 0)
        {
            throw new ArgumentException("Not a thumbnail path.", nameof(relativePath));
        }

        return ThumbnailPrefix + relativePath[prefix.Length..];
    }

    /// <summary>The URL of the saved Photo Strip (<c>photo-strip.png</c>); <c>?v=</c> is the file's hash, a cache buster.</summary>
    public static string StripUrl(string eventId, PhotoStripRecord strip)
    {
        ArgumentNullException.ThrowIfNull(strip);
        return GuestCyclePlan.StripUrl(eventId, strip.GuestCycleId, strip.Sha256);
    }

    /// <summary>The URL of a saved Capture (<c>capture-&lt;sequence&gt;.jpg</c>).</summary>
    public static string CaptureUrl(string eventId, CaptureRecord capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return GuestCyclePlan.CaptureUrl(eventId, capture.GuestCycleId, capture.Sequence, capture.Sha256);
    }

    private static int ThumbnailWidth(int width, int height, int longEdge)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        var scaled = (int)Math.Round(width * (double)longEdge / Math.Max(width, height), MidpointRounding.AwayFromZero);
        return Math.Clamp(Math.Min(scaled, width), ThumbnailName.MinimumWidth, ThumbnailName.MaximumWidth);
    }
}
