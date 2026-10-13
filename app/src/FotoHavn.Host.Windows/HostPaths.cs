namespace FotoHavn.Host.Windows;

/// <summary>
/// Folders the host uses. The Data folder sits beside the executable unless <c>--data</c> names another folder.
/// The host owns <c>logs/</c> and <c>webview2-user-data/</c>; the Event store owns the rest of the Data folder.
/// </summary>
internal sealed class HostPaths
{
    public const string WebView2RuntimeExecutable = "msedgewebview2.exe";

    private HostPaths(string appFolder, string dataFolder, string? webView2RuntimeFolder)
    {
        AppFolder = appFolder;
        DataFolder = dataFolder;
        LogsFolder = Path.Combine(dataFolder, "logs");
        LogFile = Path.Combine(LogsFolder, "fotohavn.jsonl");
        WebViewUserDataFolder = Path.Combine(dataFolder, "webview2-user-data");
        WebRoot = Path.Combine(appFolder, "wwwroot");
        UiIndexFile = Path.Combine(WebRoot, "index.html");
        WebView2RuntimeFolder = webView2RuntimeFolder;
    }

    public string AppFolder { get; }

    public string DataFolder { get; }

    public string LogsFolder { get; }

    public string LogFile { get; }

    public string WebViewUserDataFolder { get; }

    public string WebRoot { get; }

    public string UiIndexFile { get; }

    /// <summary>The Fixed Version WebView2 runtime folder, or null for the installed Evergreen runtime.</summary>
    public string? WebView2RuntimeFolder { get; }

    public static HostPaths Create(string appFolder, string? dataFolder, string? webView2RuntimeFolder = null) =>
        Create(appFolder, dataFolder, webView2RuntimeFolder, File.Exists);

    internal static HostPaths Create(string appFolder, string? dataFolder, string? webView2RuntimeFolder, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        var app = Path.GetFullPath(appFolder);
        var data = string.IsNullOrWhiteSpace(dataFolder) ? Path.Combine(app, "Data") : Path.GetFullPath(dataFolder);
        return new HostPaths(app, data, ResolveWebView2Runtime(app, webView2RuntimeFolder, fileExists));
    }

    /// <summary>
    /// An explicit <c>--webview2-runtime</c> folder wins (a wrong one fails WebView2 start-up loudly). Otherwise
    /// <c>runtime/webview2</c> beside the executable is used when it holds the runtime, and the Evergreen runtime when not.
    /// </summary>
    private static string? ResolveWebView2Runtime(string appFolder, string? explicitFolder, Func<string, bool> fileExists)
    {
        if (!string.IsNullOrWhiteSpace(explicitFolder))
        {
            return Path.GetFullPath(explicitFolder, appFolder);
        }

        var bundled = Path.Combine(appFolder, "runtime", "webview2");
        return fileExists(Path.Combine(bundled, WebView2RuntimeExecutable)) ? bundled : null;
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(LogsFolder);
        Directory.CreateDirectory(WebViewUserDataFolder);
    }
}
