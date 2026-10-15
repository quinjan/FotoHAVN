using System.Globalization;

namespace FotoHavn.Host.Windows;

internal static class WebViewSettings
{
    public const string AppHostName = "app.fotohavn";
    public const string MediaHostName = "media.fotohavn";
    public const string PreviewHostName = "preview.fotohavn";

    public const string AppOrigin = "https://" + AppHostName;

    public static Uri StartUri { get; } = new($"https://{AppHostName}/index.html");

    /// <summary>
    /// Chromium switches for E2E builds only: parallel app instances overlap on screen, so a covered or background
    /// window must keep rendering and running timers instead of being occluded or throttled.
    /// </summary>
    public static IReadOnlyList<string> E2EAntiOcclusionArguments { get; } =
    [
        "--disable-features=CalculateNativeWinOcclusion",
        "--disable-backgrounding-occluded-windows",
        "--disable-renderer-backgrounding",
        "--disable-background-timer-throttling",
    ];

    /// <summary>
    /// Browser arguments for the shared WebView2 environment. Only E2E builds open a remote debugging port
    /// (field builds ignore <c>--remote-debugging-port</c>) and add <see cref="E2EAntiOcclusionArguments"/>. One environment
    /// means one browser process, so the port exposes both windows' pages. A field build passes no arguments.
    /// </summary>
    public static string? AdditionalBrowserArguments(HostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!HostBuild.IsE2E)
        {
            return null;
        }
        var arguments = new List<string>();
        if (options.RemoteDebuggingPort is int port)
        {
            arguments.Add(string.Create(CultureInfo.InvariantCulture, $"--remote-debugging-port={port}"));
        }
        arguments.AddRange(E2EAntiOcclusionArguments);
        return string.Join(' ', arguments);
    }

    /// <summary>Whether a web message's source is the app: <c>https://app.fotohavn</c> on the default port.</summary>
    public static bool IsAppOrigin(string? source) => IsAppUri(source);

    /// <summary>
    /// The navigation allow-list for both windows: only <c>https://app.fotohavn/…</c> (default port, no user info).
    /// Everything else, including <c>http:</c>, other hosts, <c>about:</c>, <c>data:</c>, <c>file:</c>, and
    /// <c>javascript:</c>, is cancelled.
    /// </summary>
    public static bool IsAllowedNavigation(string? uri) => IsAppUri(uri);

    private static bool IsAppUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, AppHostName, StringComparison.OrdinalIgnoreCase)
        && uri.IsDefaultPort
        && uri.UserInfo.Length == 0;
}
