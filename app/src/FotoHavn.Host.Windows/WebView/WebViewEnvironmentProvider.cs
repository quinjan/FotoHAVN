using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace FotoHavn.Host.Windows.WebView;

/// <summary>
/// The one <see cref="CoreWebView2Environment"/> both windows share (one browser process, one user-data folder under
/// <c>Data/</c>). Used on the UI thread only. When the browser process exits, the failed environment is invalidated
/// and the next window to recover creates a new one, which the other window then shares.
/// </summary>
/// <remarks>
/// <paramref name="runtimeFolder"/> is the Fixed Version runtime folder (<c>runtime/webview2</c> beside the exe, or
/// <c>--webview2-runtime</c>, resolved by <see cref="HostPaths"/>); null means the Evergreen runtime installed on the
/// machine (development and E2E builds). The field build's zip always carries the folder.
/// </remarks>
internal sealed class WebViewEnvironmentProvider(
    string userDataFolder,
    string? runtimeFolder,
    string? additionalBrowserArguments,
    ILogger? logger = null,
    Func<string, bool>? fileExists = null)
{
    private Task<CoreWebView2Environment>? _current;

    public Task<CoreWebView2Environment> GetAsync()
    {
        if (_current is null || _current.IsFaulted || _current.IsCanceled)
        {
            _current = CreateAsync();
        }

        return _current;
    }

    /// <summary>Forgets <paramref name="failed"/> if it is still the shared environment.</summary>
    public void Invalidate(CoreWebView2Environment failed)
    {
        if (_current is { IsCompletedSuccessfully: true } current && ReferenceEquals(current.Result, failed))
        {
            _current = null;
        }
    }

    private async Task<CoreWebView2Environment> CreateAsync()
    {
        var selection = WebViewRuntimeSelection.Select(runtimeFolder, fileExists ?? File.Exists);
        var environment = await CoreWebView2Environment.CreateAsync(selection.Folder, userDataFolder, BuildOptions(additionalBrowserArguments));
        if (logger is not null)
        {
            WebViewEnvironmentLog.RuntimeSelected(logger, selection, environment.BrowserVersionString);
        }

        return environment;
    }

    /// <summary>
    /// Fluent overlay scrollbars reserve no layout space, so scrolling regions (the Guest gallery strip row, Operator
    /// pages) lay out like the Chromium fixture lane.
    /// </summary>
    internal static CoreWebView2EnvironmentOptions BuildOptions(string? additionalBrowserArguments) => new()
    {
        AdditionalBrowserArguments = additionalBrowserArguments,
        ScrollBarStyle = CoreWebView2ScrollbarStyle.FluentOverlay,
    };
}

/// <summary>Which WebView2 runtime a host runs on.</summary>
internal enum WebViewRuntimeKind
{
    /// <summary>The runtime installed on the machine, updated by Windows (development and E2E builds).</summary>
    Evergreen,

    /// <summary>The pinned Fixed Version runtime that the field build carries beside the executable.</summary>
    FixedVersion,
}

/// <summary>The runtime choice: a Fixed Version folder when one is configured, Evergreen otherwise.</summary>
internal sealed record WebViewRuntimeSelection(WebViewRuntimeKind Kind, string? Folder)
{
    /// <summary>
    /// A configured folder is the Fixed Version runtime and must hold <see cref="HostPaths.WebView2RuntimeExecutable"/>;
    /// WebView2 would otherwise fail with a bare HRESULT, so the failure names the folder instead. Nothing configured
    /// means Evergreen.
    /// </summary>
    public static WebViewRuntimeSelection Select(string? runtimeFolder, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(runtimeFolder))
        {
            return new WebViewRuntimeSelection(WebViewRuntimeKind.Evergreen, null);
        }

        if (!fileExists(Path.Combine(runtimeFolder, HostPaths.WebView2RuntimeExecutable)))
        {
            throw new InvalidOperationException(
                $"The WebView2 runtime folder '{runtimeFolder}' does not hold {HostPaths.WebView2RuntimeExecutable}. " +
                "Unpack the field build's zip as a whole, or point --webview2-runtime at a Fixed Version runtime folder.");
        }

        return new WebViewRuntimeSelection(WebViewRuntimeKind.FixedVersion, runtimeFolder);
    }
}

/// <summary>Which runtime and version the host runs on. Every property is on the log allow-list (Kind, Version, Runtime).</summary>
internal static partial class WebViewEnvironmentLog
{
    public static void RuntimeSelected(ILogger logger, WebViewRuntimeSelection selection, string version)
    {
        var kind = selection.Kind.ToString();
        var runtime = selection.Folder ?? "system";
        RuntimeSelected(logger, kind, version, runtime);
    }

    [LoggerMessage(EventId = 900, EventName = "webViewRuntime", Level = LogLevel.Information, Message = "WebView2 runtime {Kind} version {Version} at {Runtime}")]
    private static partial void RuntimeSelected(ILogger logger, string kind, string version, string runtime);
}

/// <summary>
/// Limits WebView2 recreation after process failures, so a page that crashes its renderer on every load can't spin the
/// host: at most <paramref name="maxRecoveries"/> recoveries within <paramref name="window"/>.
/// </summary>
internal sealed class RecoveryBudget(int maxRecoveries, TimeSpan window)
{
    private readonly Queue<DateTimeOffset> _recent = new();

    public static RecoveryBudget Default() => new(5, TimeSpan.FromMinutes(1));

    public bool TryConsume(DateTimeOffset now)
    {
        while (_recent.Count > 0 && now - _recent.Peek() >= window)
        {
            _recent.Dequeue();
        }

        if (_recent.Count >= maxRecoveries)
        {
            return false;
        }

        _recent.Enqueue(now);
        return true;
    }
}

/// <summary>Which <see cref="CoreWebView2ProcessFailedKind"/>s make a window recreate its WebView2.</summary>
internal static class ProcessFailurePolicy
{
    /// <summary>
    /// The page is gone or hung when the browser process or the window's main-frame renderer exits or stops responding.
    /// Other processes (GPU, utility, subframe renderers) are restarted by WebView2 itself and are only logged.
    /// </summary>
    public static bool RequiresRecreate(CoreWebView2ProcessFailedKind kind) =>
        kind is CoreWebView2ProcessFailedKind.BrowserProcessExited
            or CoreWebView2ProcessFailedKind.RenderProcessExited
            or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive;
}
