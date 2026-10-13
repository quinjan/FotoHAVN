using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FotoHavn.Host.Headless;

/// <summary>
/// The headless host serves only this machine. Requests must name a loopback host (<c>127.0.0.1</c>, <c>localhost</c>,
/// <c>[::1]</c>) so a DNS-rebinding page can't read the Data folder through it, and a WebSocket's <c>Origin</c>, when
/// present, must be a loopback http(s) origin (any port, so <c>npm run dev</c> on Vite's port can connect).
/// </summary>
internal static class LoopbackGuard
{
    public static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var address = host.StartsWith('[') && host.EndsWith(']') ? host[1..^1] : host;
        return IPAddress.TryParse(address, out var ip) && IPAddress.IsLoopback(ip);
    }

    /// <summary>True when there is no <c>Origin</c> (non-browser clients) or it is one loopback http(s) origin.</summary>
    public static bool IsAllowedOrigin(StringValues origin)
    {
        if (StringValues.IsNullOrEmpty(origin))
        {
            return true;
        }

        return origin.Count == 1
            && Uri.TryCreate(origin[0], UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && IsLoopbackHost(uri.Host);
    }

    /// <summary>Middleware: 400 for any request whose Host isn't loopback.</summary>
    public static Task RequireLoopbackHost(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (!IsLoopbackHost(context.Request.Host.Host))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        }

        return next(context);
    }
}
