using Microsoft.Web.WebView2.Core;

namespace FotoHavn.Host.Windows.WebView;

/// <summary>
/// The one <see cref="CoreWebView2Environment"/> both windows share (one browser process, one user-data folder under
/// <c>Data/</c>). Used on the UI thread only. When the browser process exits, the failed environment is invalidated
/// and the next window to recover creates a new one, which the other window then shares.
/// </summary>
internal sealed class WebViewEnvironmentProvider(string userDataFolder, string? runtimeFolder, string? additionalBrowserArguments)
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

    private Task<CoreWebView2Environment> CreateAsync()
    {
        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = additionalBrowserArguments,
        };
        return CoreWebView2Environment.CreateAsync(runtimeFolder, userDataFolder, options);
    }
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
