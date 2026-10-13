namespace FotoHavn.Host.Windows;

/// <summary>
/// Folders the host uses. The Data folder sits beside the executable unless <c>--data</c> names another folder.
/// The host owns <c>logs/</c> and <c>webview2-user-data/</c>; the Event store owns the rest of the Data folder.
/// </summary>
internal sealed class HostPaths
{
    private HostPaths(string appFolder, string dataFolder)
    {
        AppFolder = appFolder;
        DataFolder = dataFolder;
        LogsFolder = Path.Combine(dataFolder, "logs");
        LogFile = Path.Combine(LogsFolder, "fotohavn.jsonl");
        WebViewUserDataFolder = Path.Combine(dataFolder, "webview2-user-data");
        WebRoot = Path.Combine(appFolder, "wwwroot");
        UiIndexFile = Path.Combine(WebRoot, "index.html");
    }

    public string AppFolder { get; }

    public string DataFolder { get; }

    public string LogsFolder { get; }

    public string LogFile { get; }

    public string WebViewUserDataFolder { get; }

    public string WebRoot { get; }

    public string UiIndexFile { get; }

    public static HostPaths Create(string appFolder, string? dataFolder)
    {
        var app = Path.GetFullPath(appFolder);
        var data = string.IsNullOrWhiteSpace(dataFolder) ? Path.Combine(app, "Data") : Path.GetFullPath(dataFolder);
        return new HostPaths(app, data);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(LogsFolder);
        Directory.CreateDirectory(WebViewUserDataFolder);
    }
}
