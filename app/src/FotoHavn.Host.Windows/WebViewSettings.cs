namespace FotoHavn.Host.Windows;

internal static class WebViewSettings
{
    public const string AppHostName = "app.fotohavn";

    public static Uri StartUri { get; } = new($"https://{AppHostName}/index.html");

    /// <summary>
    /// Browser arguments for the shared WebView2 environment. Only E2E builds open a remote debugging port;
    /// field builds ignore <c>--remote-debugging-port</c>.
    /// </summary>
    public static string? AdditionalBrowserArguments(HostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
#if E2E_HOOKS
        return options.RemoteDebuggingPort is int port
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"--remote-debugging-port={port}")
            : null;
#else
        return null;
#endif
    }

    public static bool IsAppOrigin(string? source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, AppHostName, StringComparison.OrdinalIgnoreCase);
}
