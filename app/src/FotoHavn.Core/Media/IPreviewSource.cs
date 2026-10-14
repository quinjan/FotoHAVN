namespace FotoHavn.Core.Media;

/// <summary>
/// The in-memory images that <c>https://preview.fotohavn/&lt;key&gt;.png</c> serves (composed previews, M7). Hosts call
/// it from their request thread (WebView2: the UI thread in <c>WebResourceRequested</c>; headless: a Kestrel request), so
/// implementations must be thread-safe and must return at once without composing or blocking.
/// </summary>
public interface IPreviewSource
{
    /// <summary>Looks up a preview by key.</summary>
    /// <param name="key">The request path's single segment without the leading <c>/</c> and the <c>.png</c> suffix.</param>
    /// <param name="png">The PNG bytes when found; otherwise empty.</param>
    /// <returns>Whether a preview exists for <paramref name="key"/>. False means the host answers 404.</returns>
    bool TryGet(string key, out ReadOnlyMemory<byte> png);
}

/// <summary>A preview source with no previews, so every request is a 404. The hosts serve a <see cref="PreviewCache"/> instead.</summary>
public sealed class EmptyPreviewSource : IPreviewSource
{
    public static EmptyPreviewSource Instance { get; } = new();

    private EmptyPreviewSource()
    {
    }

    public bool TryGet(string key, out ReadOnlyMemory<byte> png)
    {
        ArgumentNullException.ThrowIfNull(key);
        png = ReadOnlyMemory<byte>.Empty;
        return false;
    }
}
