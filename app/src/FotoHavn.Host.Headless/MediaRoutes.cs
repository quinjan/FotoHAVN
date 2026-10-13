using FotoHavn.Core.Media;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;

namespace FotoHavn.Host.Headless;

/// <summary>
/// The headless versions of the WebView2 virtual hosts, on the page's own origin:
/// <c>https://media.fotohavn/&lt;path&gt;</c> is <c>/media/&lt;path&gt;</c> and <c>https://preview.fotohavn/&lt;key&gt;.png</c>
/// is <c>/preview/&lt;key&gt;.png</c>. Every path rule is <see cref="MediaPathPolicy"/>'s; this class only strips the prefix,
/// query, and fragment, and serves the file read-only.
/// </summary>
internal static class MediaRoutes
{
    public const string MediaPrefix = "/media/";
    public const string PreviewPrefix = "/preview/";
    private const string PngSuffix = ".png";

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>GET/HEAD <c>/media/…</c>: the file <see cref="MediaPathPolicy"/> resolves inside the Data folder, or 404.</summary>
    public static IResult Media(HttpContext context, string dataFolder)
    {
        ArgumentNullException.ThrowIfNull(context);
        var target = RawPath(context);
        if (!target.StartsWith(MediaPrefix, StringComparison.Ordinal)
            || !MediaPathPolicy.TryResolve(dataFolder, target[MediaPrefix.Length..], out var fullPath)
            || !File.Exists(fullPath))
        {
            return Results.NotFound();
        }

        if (!ContentTypes.TryGetContentType(fullPath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(fullPath, contentType, enableRangeProcessing: true);
    }

    /// <summary>GET/HEAD <c>/preview/&lt;key&gt;.png</c>: the preview from <paramref name="previews"/>, or 404.</summary>
    public static IResult Preview(HttpContext context, IPreviewSource previews)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(previews);
        var target = RawPath(context);
        if (!target.StartsWith(PreviewPrefix, StringComparison.Ordinal)
            || !target.EndsWith(PngSuffix, StringComparison.Ordinal))
        {
            return Results.NotFound();
        }

        var key = target[PreviewPrefix.Length..^PngSuffix.Length];
        if (key.Length == 0 || key.Contains('/', StringComparison.Ordinal) || !previews.TryGet(key, out var png))
        {
            return Results.NotFound();
        }

        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = "no-store";
        return Results.Bytes(png, "image/png");
    }

    /// <summary>The request target as the client sent it (still percent-encoded), without the query or fragment.</summary>
    internal static string RawPath(HttpContext context)
    {
        var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        if (string.IsNullOrEmpty(raw))
        {
            raw = context.Request.PathBase.Add(context.Request.Path).ToUriComponent();
        }

        var end = raw.AsSpan().IndexOfAny('?', '#');
        return end >= 0 ? raw[..end] : raw;
    }
}
