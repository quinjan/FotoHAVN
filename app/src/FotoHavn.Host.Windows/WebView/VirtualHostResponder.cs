using System.Text;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Media;

namespace FotoHavn.Host.Windows.WebView;

/// <summary>What the host answers to one virtual-host request.</summary>
internal abstract record VirtualHostResponse(int StatusCode, string ReasonPhrase)
{
    public static VirtualHostResponse NotFound { get; } = new StatusResponse(404, "Not Found");

    public static VirtualHostResponse MethodNotAllowed { get; } = new StatusResponse(405, "Method Not Allowed");
}

/// <summary>A status with no body.</summary>
internal sealed record StatusResponse(int StatusCode, string ReasonPhrase) : VirtualHostResponse(StatusCode, ReasonPhrase);

/// <summary>A file to serve read-only; the host answers 404 when it doesn't exist.</summary>
internal sealed record FileResponse(string Path, string ContentType, bool IncludeBody) : VirtualHostResponse(200, "OK");

/// <summary>In-memory content to serve, with the <c>Cache-Control</c> value to answer it with.</summary>
internal sealed record ContentResponse(ReadOnlyMemory<byte> Content, string ContentType, bool IncludeBody, string CacheControl = VirtualHostResponder.DefaultCacheControl) : VirtualHostResponse(200, "OK");

/// <summary>
/// The pure part of the <c>media.fotohavn</c>, <c>preview.fotohavn</c>, and fallback <c>app.fotohavn</c> virtual hosts:
/// request → response decision. Media paths are decided only by <see cref="MediaPathPolicy"/> and previews only by
/// <see cref="IPreviewSource"/>; the host does the file I/O.
/// </summary>
internal static class VirtualHostResponder
{
    /// <summary>The <c>Cache-Control</c> of media and the fallback page.</summary>
    public const string DefaultCacheControl = "no-cache";

    /// <summary>The <c>Cache-Control</c> of composed previews: a key is never reused for other pixels, but the page must not keep them.</summary>
    public const string NoStore = "no-store";

    /// <summary><c>https://media.fotohavn/&lt;path&gt;</c>: GET or HEAD a file the media policy accepts.</summary>
    public static VirtualHostResponse Media(string method, string requestUri, string dataRoot)
    {
        if (!TryReadRequest(method, requestUri, WebViewSettings.MediaHostName, out var uri, out var includeBody, out var refusal))
        {
            return refusal;
        }

        return MediaPathPolicy.TryResolve(dataRoot, uri.AbsolutePath, out var fullPath)
            ? new FileResponse(fullPath, ContentTypes.ForPath(fullPath), includeBody)
            : VirtualHostResponse.NotFound;
    }

    /// <summary><c>https://preview.fotohavn/&lt;key&gt;.png</c>: GET or HEAD an in-memory preview.</summary>
    public static VirtualHostResponse Preview(string method, string requestUri, IPreviewSource previews)
    {
        ArgumentNullException.ThrowIfNull(previews);
        if (!TryReadRequest(method, requestUri, WebViewSettings.PreviewHostName, out var uri, out var includeBody, out var refusal))
        {
            return refusal;
        }

        const string suffix = ".png";
        var path = uri.AbsolutePath;
        if (!path.StartsWith('/') || !path.EndsWith(suffix, StringComparison.Ordinal))
        {
            return VirtualHostResponse.NotFound;
        }

        var key = path[1..^suffix.Length];
        if (!PreviewKey.IsValid(key))
        {
            return VirtualHostResponse.NotFound;
        }

        return previews.TryGet(key, out var png)
            ? new ContentResponse(png, ContentTypes.Png, includeBody, NoStore)
            : VirtualHostResponse.NotFound;
    }

    /// <summary>
    /// <c>https://app.fotohavn/</c> while the UI isn't built: the fallback page at <c>/</c> and <c>/index.html</c>,
    /// 404 for everything else. It keeps the page on the app origin, so the navigation allow-list never needs an exception.
    /// </summary>
    public static VirtualHostResponse AppFallback(string method, string requestUri)
    {
        if (!TryReadRequest(method, requestUri, WebViewSettings.AppHostName, out var uri, out var includeBody, out var refusal))
        {
            return refusal;
        }

        return uri.AbsolutePath is "/" or "/index.html"
            ? new ContentResponse(Encoding.UTF8.GetBytes(FallbackPage.Html), ContentTypes.Html, includeBody)
            : VirtualHostResponse.NotFound;
    }

    /// <summary>The response headers for a 200 response.</summary>
    public static string Headers(string contentType, long contentLength, string cacheControl = DefaultCacheControl) =>
        $"Content-Type: {contentType}\r\n"
        + $"Content-Length: {contentLength.ToString(System.Globalization.CultureInfo.InvariantCulture)}\r\n"
        + StatusHeaders
        + $"\r\nCache-Control: {cacheControl}";

    /// <summary>
    /// The headers on every response, errors included: without the CORS header a page's <c>fetch</c> of a missing media
    /// file would fail as a network error instead of seeing its 404.
    /// </summary>
    public static string StatusHeaders { get; } =
        $"X-Content-Type-Options: nosniff\r\nAccess-Control-Allow-Origin: {WebViewSettings.AppOrigin}";

    private static bool TryReadRequest(
        string method,
        string requestUri,
        string hostName,
        out Uri uri,
        out bool includeBody,
        out VirtualHostResponse refusal)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(requestUri);
        uri = null!;
        includeBody = false;
        refusal = VirtualHostResponse.NotFound;

        var isGet = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
        if (!isGet && !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            refusal = VirtualHostResponse.MethodNotAllowed;
            return false;
        }

        if (!Uri.TryCreate(requestUri, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || !string.Equals(parsed.Host, hostName, StringComparison.OrdinalIgnoreCase)
            || !parsed.IsDefaultPort)
        {
            return false;
        }

        uri = parsed;
        includeBody = isGet;
        return true;
    }
}

/// <summary>Content types by file extension for the files the media host serves.</summary>
internal static class ContentTypes
{
    public const string Png = "image/png";
    public const string Html = "text/html; charset=utf-8";
    public const string OctetStream = "application/octet-stream";

    public static string ForPath(string path) =>
        Path.GetExtension(path).ToUpperInvariant() switch
        {
            ".JPG" or ".JPEG" => "image/jpeg",
            ".PNG" => Png,
            ".WEBP" => "image/webp",
            ".GIF" => "image/gif",
            ".BMP" => "image/bmp",
            ".JSON" => "application/json",
            ".TXT" => "text/plain; charset=utf-8",
            _ => OctetStream,
        };
}
