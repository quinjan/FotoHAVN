using System.Diagnostics;
using FotoHavn.Host.Windows.Logging;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Screens;

/// <summary>
/// Opens an Event's folder in File Explorer for the brain's fire-and-forget <c>OpenFolder</c>. The brain asks for a path inside the
/// Data folder; anything else is refused, so a bridge message can never open an arbitrary location. Explorer gets the path as a single
/// argument (never a shell string). E2E builds only validate and log: the brain's diagnostics record is the E2E check, and no window opens.
/// </summary>
internal sealed class FolderOpener
{
    private readonly string _dataRoot;
    private readonly bool _launchExplorer;
    private readonly ILogger _logger;
    private readonly string _explorerPath;
    private readonly Action<ProcessStartInfo> _start;
    private readonly Func<string, bool> _directoryExists;
    private readonly Action<string> _createDirectory;

    public FolderOpener(
        string dataRoot,
        bool launchExplorer,
        ILogger logger,
        Action<ProcessStartInfo>? start = null,
        Func<string, bool>? directoryExists = null,
        Action<string>? createDirectory = null,
        string? explorerPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentNullException.ThrowIfNull(logger);
        _dataRoot = Path.GetFullPath(dataRoot);
        _launchExplorer = launchExplorer;
        _logger = logger;
        _start = start ?? (info => Process.Start(info)?.Dispose());
        _directoryExists = directoryExists ?? Directory.Exists;
        _createDirectory = createDirectory ?? (path => Directory.CreateDirectory(path));
        _explorerPath = explorerPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
    }

    /// <summary>
    /// The full path to open, or null when <paramref name="path"/> isn't a fully qualified path strictly inside the Data folder
    /// (the Data folder itself, a sibling that shares its name as a prefix, and any <c>..</c> escape are all refused).
    /// </summary>
    public static string? Resolve(string? path, string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathFullyQualified(path))
        {
            return null;
        }

        string full;
        string root;
        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        var prefix = root + Path.DirectorySeparatorChar;
        return full.Length > prefix.Length && full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    /// <summary>Validates the path, then (field builds) creates the folder if it is missing and starts Explorer on it. Never throws.</summary>
    public void Open(string path)
    {
        if (Resolve(path, _dataRoot) is not { } full)
        {
            HostLog.OpenFolderRefused(_logger, path);
            return;
        }

        if (!_launchExplorer)
        {
            HostLog.OpenFolderRequested(_logger, full, "not launched (E2E build)");
            return;
        }

        try
        {
            if (!_directoryExists(full))
            {
                _createDirectory(full);
            }

            var info = new ProcessStartInfo(_explorerPath) { UseShellExecute = true };
            info.ArgumentList.Add(full);
            _start(info);
            HostLog.OpenFolderRequested(_logger, full, "started");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.OpenFolderFailed(_logger, full, exception);
        }
    }
}
