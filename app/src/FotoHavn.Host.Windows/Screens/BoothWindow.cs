using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FotoHavn.Core;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Media;
using FotoHavn.Host.Windows.Bridge;
using FotoHavn.Host.Windows.Interop;
using FotoHavn.Host.Windows.Logging;
using FotoHavn.Host.Windows.Preview;
using FotoHavn.Host.Windows.WebView;
using FotoHavn.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace FotoHavn.Host.Windows.Screens;

/// <summary>What every window shares.</summary>
internal sealed record BoothWindowServices(
    IBrain Brain,
    WebViewEnvironmentProvider Environments,
    HostPaths Paths,
    IPreviewSource Previews,
    SharedBufferPreviewChannel Frames,
    TimeProvider Time,
    ILogger Logger);

/// <summary>
/// One screen's window: the Operator window or the Guest window. It hosts one WebView2 at a time with the guardrails,
/// the virtual hosts, and a <see cref="BridgeSession"/> whose role is the window's. After a renderer or browser process
/// failure it recreates its WebView2 and opens a new brain connection; the other window is unaffected.
/// </summary>
internal sealed class BoothWindow : Window
{
    private readonly BoothWindowServices _services;
    private readonly RecoveryBudget _recoveries = RecoveryBudget.Default();
    private WebView2? _webView;
    private BridgeSession? _session;
    private WebView2PreviewSurface? _frameSurface;
    private int _generation;
    private int _failedGeneration = -1;
    private bool _closed;
    private bool _hostClose;
    private DisplayRect? _pinnedBounds;
    private (int Width, int Height)? _minimumMaxTrackSize;
    private NativeMethods.WindowProcedure? _windowProcedure;
    private IntPtr _previousWindowProcedure;

    public BoothWindow(Role role, BoothWindowServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Role = role;
        _services = services;
        Title = role == Role.Operator ? "FOTOHAVN" : "FOTOHAVN Guest Screen";
        Background = Brushes.Black;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Closing += OnClosing;
        Closed += OnClosed;
        DpiChanged += OnDpiChanged;
    }

    public Role Role { get; }

    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    /// <summary>The current WebView2 core, or null while none is loaded.</summary>
    public CoreWebView2? CurrentCoreWebView2 => _webView?.CoreWebView2;

    public IntPtr EnsureHandle() => new WindowInteropHelper(this).EnsureHandle();

    /// <summary>Places the window at <paramref name="bounds"/> (physical pixels) and keeps it there across DPI changes.</summary>
    public void PinBounds(DisplayRect bounds)
    {
        _pinnedBounds = bounds;
        ApplyBounds(bounds);
    }

    /// <summary>
    /// Restores the window if maximized and sizes its client area, which the WebView2 fills (the page viewport), to
    /// <paramref name="width"/> × <paramref name="height"/> physical pixels, keeping the window's outer top-left.
    /// </summary>
    public void SetClientSize(int width, int height)
    {
        var origin = GetPhysicalBounds();
        if (WindowState != WindowState.Normal)
        {
            WindowState = WindowState.Normal;
        }

        // The frame can change size after a restore, so measure, place, and measure again.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var outer = GetPhysicalBounds();
            var client = GetClientSize();
            if (client == (width, height) && outer.X == origin.X && outer.Y == origin.Y)
            {
                break;
            }

            var bounds = new DisplayRect(origin.X, origin.Y, width + outer.Width - client.Width, height + outer.Height - client.Height);
            if (_pinnedBounds is not null)
            {
                _pinnedBounds = bounds;
            }

            // Windows caps a window at about the virtual screen's size; the layout sizes (up to 2560×1440) may exceed it.
            AllowTrackSize(bounds.Width, bounds.Height);
            ApplyBounds(bounds);
        }
    }

    /// <summary>The client area's size in physical pixels.</summary>
    public (int Width, int Height) GetClientSize()
    {
        var handle = Handle;
        return handle != IntPtr.Zero && NativeMethods.GetClientRect(handle, out var rect)
            ? (rect.Right - rect.Left, rect.Bottom - rect.Top)
            : default;
    }

    /// <summary>The window's outer bounds in physical pixels.</summary>
    public DisplayRect GetPhysicalBounds()
    {
        var handle = Handle;
        return handle != IntPtr.Zero && NativeMethods.GetWindowRect(handle, out var rect)
            ? new DisplayRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)
            : default;
    }

    /// <summary>Closes the window on the host's behalf (the Guest window refuses to close otherwise, e.g. on Alt+F4).</summary>
    public void CloseByHost()
    {
        _hostClose = true;
        Close();
    }

    /// <summary>
    /// Creates a new WebView2 (replacing any current one and its brain connection), applies the guardrails, maps the
    /// virtual hosts, connects to the brain with the window's role, and navigates to the UI.
    /// </summary>
    public async Task LoadWebViewAsync()
    {
        var generation = ++_generation;
        TearDownWebView();

        var webView = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.Black };
        _webView = webView;
        Content = webView;

        CoreWebView2Environment environment;
        try
        {
            environment = await _services.Environments.GetAsync();
            if (!IsCurrent(generation))
            {
                return;
            }

            await webView.EnsureCoreWebView2Async(environment);
        }
        catch (Exception) when (!IsCurrent(generation))
        {
            return;
        }

        if (!IsCurrent(generation))
        {
            return;
        }

        var core = webView.CoreWebView2;
        WebViewGuardrails.Current.Apply(core.Settings);
        core.NavigationStarting += (_, e) =>
        {
            e.Cancel = !AllowNavigation(e.Uri);
            if (!e.Cancel)
            {
                // A new document never acks the frame the old one was drawing.
                _services.Frames.ResetInFlight(Role);
            }
        };
        core.FrameNavigationStarting += (_, e) => e.Cancel = !AllowNavigation(e.Uri);
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            HostLog.NewWindowBlocked(_services.Logger, Role, e.Uri);
        };
        core.LaunchingExternalUriScheme += (_, e) =>
        {
            e.Cancel = true;
            HostLog.ExternalSchemeBlocked(_services.Logger, Role, e.Uri);
        };
        core.DownloadStarting += (_, e) =>
        {
            e.Cancel = true;
            HostLog.DownloadBlocked(_services.Logger, Role, e.DownloadOperation.Uri);
        };
        core.PermissionRequested += (_, e) =>
        {
            e.State = CoreWebView2PermissionState.Deny;
            HostLog.PermissionDenied(_services.Logger, Role, e.PermissionKind, e.Uri);
        };
        core.ProcessFailed += (_, e) => OnProcessFailed(generation, environment, e);
        MapVirtualHosts(core, environment);

        var session = new BridgeSession(
            _services.Brain,
            Role,
            work => Dispatcher.InvokeAsync(work),
            core.PostWebMessageAsJson,
            _services.Logger,
            text => _services.Frames.TryHandleAck(Role, text));
        _session = session;
        core.WebMessageReceived += (_, e) => session.Receive(e.Source, e.WebMessageAsJson);

        var surface = new WebView2PreviewSurface(core, environment);
        _frameSurface = surface;
        _services.Frames.Attach(Role, surface);

        ApplyZoom(VisualTreeHelper.GetDpi(this).DpiScaleX);
        core.Navigate(WebViewSettings.StartUri.AbsoluteUri);
    }

    /// <summary>
    /// The Guest Screen ignores Windows display scaling for layout (plan section 13, Q14): its WebView2 zoom factor is
    /// 1 / scale, so one CSS pixel is one physical pixel. The Operator Screen follows Windows scaling (zoom 1).
    /// </summary>
    internal static double ZoomFactorFor(Role role, double scale) =>
        role == Role.Guest && scale > 0 ? 1.0 / scale : 1.0;

    private bool IsCurrent(int generation) => generation == _generation && !_closed;

    private bool AllowNavigation(string uri)
    {
        if (WebViewSettings.IsAllowedNavigation(uri))
        {
            return true;
        }

        HostLog.NavigationBlocked(_services.Logger, Role, uri);
        return false;
    }

    private void MapVirtualHosts(CoreWebView2 core, CoreWebView2Environment environment)
    {
        if (File.Exists(_services.Paths.UiIndexFile))
        {
            core.SetVirtualHostNameToFolderMapping(
                WebViewSettings.AppHostName,
                _services.Paths.WebRoot,
                CoreWebView2HostResourceAccessKind.Deny);
        }
        else
        {
            HostLog.UiNotBuilt(_services.Logger, _services.Paths.UiIndexFile);
            core.AddWebResourceRequestedFilter($"{WebViewSettings.AppOrigin}/*", CoreWebView2WebResourceContext.All);
        }

        core.AddWebResourceRequestedFilter($"https://{WebViewSettings.MediaHostName}/*", CoreWebView2WebResourceContext.All);
        core.AddWebResourceRequestedFilter($"https://{WebViewSettings.PreviewHostName}/*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) => OnWebResourceRequested(environment, e);
    }

    private async void OnWebResourceRequested(CoreWebView2Environment environment, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var request = e.Request;
        var uri = request.Uri;
        var host = Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.Host : string.Empty;
        var response = host.ToUpperInvariant() switch
        {
            "MEDIA.FOTOHAVN" => VirtualHostResponder.Media(request.Method, uri, _services.Paths.DataFolder),
            "PREVIEW.FOTOHAVN" => VirtualHostResponder.Preview(request.Method, uri, _services.Previews),
            "APP.FOTOHAVN" => VirtualHostResponder.AppFallback(request.Method, uri),
            _ => VirtualHostResponse.NotFound,
        };

        switch (response)
        {
            case ContentResponse content:
                e.Response = Respond(environment, content.Content, content.ContentType, content.IncludeBody);
                break;
            case FileResponse file:
                using (e.GetDeferral())
                {
                    e.Response = await ReadFileResponseAsync(environment, file);
                }

                break;
            default:
                e.Response = environment.CreateWebResourceResponse(null, response.StatusCode, response.ReasonPhrase, VirtualHostResponder.StatusHeaders);
                break;
        }
    }

    private async Task<CoreWebView2WebResourceResponse> ReadFileResponseAsync(CoreWebView2Environment environment, FileResponse file)
    {
        if (!File.Exists(file.Path))
        {
            return environment.CreateWebResourceResponse(null, 404, "Not Found", VirtualHostResponder.StatusHeaders);
        }

        try
        {
            // Read into memory: WebView2 owns the response stream, and an open file handle would block deleting Events.
            var content = await File.ReadAllBytesAsync(file.Path);
            return Respond(environment, content, file.ContentType, file.IncludeBody);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HostLog.MediaReadFailed(_services.Logger, file.Path, exception);
            return environment.CreateWebResourceResponse(null, 404, "Not Found", VirtualHostResponder.StatusHeaders);
        }
    }

    private static CoreWebView2WebResourceResponse Respond(
        CoreWebView2Environment environment,
        ReadOnlyMemory<byte> content,
        string contentType,
        bool includeBody)
    {
        var body = includeBody ? new MemoryStream(content.ToArray(), writable: false) : null;
        return environment.CreateWebResourceResponse(body, 200, "OK", VirtualHostResponder.Headers(contentType, content.Length));
    }

    private void OnProcessFailed(int generation, CoreWebView2Environment environment, CoreWebView2ProcessFailedEventArgs e)
    {
        HostLog.ProcessFailed(_services.Logger, Role, e.ProcessFailedKind, e.Reason, e.ExitCode);
        if (!ProcessFailurePolicy.RequiresRecreate(e.ProcessFailedKind) || !IsCurrent(generation) || _failedGeneration == generation)
        {
            return;
        }

        _failedGeneration = generation;
        if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
        {
            _services.Environments.Invalidate(environment);
        }

        if (!_recoveries.TryConsume(_services.Time.GetUtcNow()))
        {
            HostLog.RecoveryAbandoned(_services.Logger, Role);
            TearDownWebView();
            return;
        }

        HostLog.WebViewRecreating(_services.Logger, Role);
        Dispatcher.InvokeAsync(RecoverAsync);
    }

    private async Task RecoverAsync()
    {
        try
        {
            await LoadWebViewAsync();
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or WebView2RuntimeNotFoundException or ObjectDisposedException)
        {
            HostLog.WebViewFailed(_services.Logger, Role, exception);
        }
    }

    private void ApplyZoom(double scale)
    {
        if (_webView is null)
        {
            return;
        }

        var zoom = ZoomFactorFor(Role, scale);
        _webView.ZoomFactor = zoom;
        if (Role == Role.Guest)
        {
            HostLog.GuestZoom(_services.Logger, zoom, scale);
        }
    }

    /// <summary>
    /// Lets <see cref="SetClientSize"/> exceed the screen: Windows caps a window's size at its maximum tracking size
    /// (about the virtual screen), which WPF only lowers. A subclass procedure above WPF's raises it after WPF answers
    /// <c>WM_GETMINMAXINFO</c>. Installed on first use (only the E2E test hook resizes windows) and removed at
    /// <c>WM_NCDESTROY</c>.
    /// </summary>
    private void AllowTrackSize(int width, int height)
    {
        _minimumMaxTrackSize = (width, height);
        if (_windowProcedure is not null)
        {
            return;
        }

        _windowProcedure = SubclassProcedure;
        _previousWindowProcedure = NativeMethods.SetWindowLongPtr(EnsureHandle(), NativeMethods.GwlpWndProc, Marshal.GetFunctionPointerForDelegate(_windowProcedure));
    }

    private IntPtr SubclassProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam)
    {
        var previous = _previousWindowProcedure;
        if (message == NativeMethods.WmNcDestroy)
        {
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlpWndProc, previous);
            return NativeMethods.CallWindowProc(previous, hwnd, message, wParam, lParam);
        }

        if (message == NativeMethods.WmGetMinMaxInfo && _minimumMaxTrackSize is { } wanted)
        {
            // Raise it before WPF reads it (WPF keeps the value as its own layout limit) and again after WPF answers.
            RaiseMaxTrackSize(lParam, wanted);
            var answer = NativeMethods.CallWindowProc(previous, hwnd, message, wParam, lParam);
            RaiseMaxTrackSize(lParam, wanted);
            return answer;
        }

        return NativeMethods.CallWindowProc(previous, hwnd, message, wParam, lParam);
    }

    private static void RaiseMaxTrackSize(IntPtr lParam, (int Width, int Height) wanted)
    {
        var info = Marshal.PtrToStructure<NativeMethods.MinMaxInfo>(lParam);
        info.MaxTrackSize.X = Math.Max(info.MaxTrackSize.X, wanted.Width);
        info.MaxTrackSize.Y = Math.Max(info.MaxTrackSize.Y, wanted.Height);
        Marshal.StructureToPtr(info, lParam, fDeleteOld: false);
    }

    private void ApplyBounds(DisplayRect bounds)
    {
        var handle = EnsureHandle();
        NativeMethods.SetWindowPos(
            handle,
            IntPtr.Zero,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);
    }

    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        ApplyZoom(e.NewDpi.DpiScaleX);
        if (_pinnedBounds is { } bounds)
        {
            // WPF applies the suggested rectangle for the new DPI; put the window back on its exact physical bounds.
            Dispatcher.InvokeAsync(() => ApplyBounds(bounds), System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Guests can't close the Guest Screen (Alt+F4); only the host closes it, when the brain asks.
        if (Role == Role.Guest && !_hostClose)
        {
            e.Cancel = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        TearDownWebView();
    }

    private void TearDownWebView()
    {
        if (_frameSurface is { } surface)
        {
            _frameSurface = null;
            _services.Frames.Detach(Role, surface);
        }

        _session?.Dispose();
        _session = null;
        if (_webView is { } webView)
        {
            _webView = null;
            Content = null;
            webView.Dispose();
        }
    }
}
