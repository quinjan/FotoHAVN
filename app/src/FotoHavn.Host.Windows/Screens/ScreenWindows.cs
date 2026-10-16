using System.Windows;
using System.Windows.Threading;
using FotoHavn.Core;
using FotoHavn.Core.Displays;
using FotoHavn.Host.Windows.Displays;
using FotoHavn.Host.Windows.Hardening;
using FotoHavn.Host.Windows.Interop;
using FotoHavn.Host.Windows.Logging;
using FotoHavn.Protocol;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Screens;

/// <summary>One open window, for diagnostics: its role, outer bounds in physical pixels, and the display it is on.</summary>
internal sealed record WindowFacts(Role Role, DisplayRect Bounds, string? DisplayId);

/// <summary>
/// The host's two windows and the brain's <see cref="IHostServices"/>. The brain calls the services on its executor;
/// each queues the work on the UI thread and returns at once. <see cref="GetOperatorDisplayId"/> and
/// <see cref="HasWindow"/> read fields that the UI thread keeps current, so they never touch the UI thread.
/// </summary>
internal sealed class ScreenWindows : IHostServices
{
    private readonly Dispatcher _dispatcher;
    private readonly WindowsDisplayAdapter _displays;
    private readonly ILogger _logger;
    private readonly FolderOpener _folders;
    private readonly string _fontsFolder;
    private readonly List<IdentifyOverlayWindow> _overlays = [];
    private OverlayFonts? _fonts;
    private BoothWindowServices? _services;
    private BoothWindow? _operator;
    private BoothWindow? _guest;
    private volatile string? _operatorDisplayId;
    private volatile bool _operatorOpen;
    private volatile bool _guestOpen;

    public ScreenWindows(Dispatcher dispatcher, WindowsDisplayAdapter displays, ILogger logger, FolderOpener folders, string? fontsFolder = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(displays);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(folders);
        _dispatcher = dispatcher;
        _displays = displays;
        _logger = logger;
        _folders = folders;
        _fontsFolder = fontsFolder ?? OverlayFonts.DefaultFolder;
        _displays.SnapshotUpdated += (_, _) => UpdateOperatorDisplayId();
    }

    public BoothWindow? Operator => _operator;

    public BoothWindow? Guest => _guest;

    /// <summary>
    /// Opens the Operator window maximized on the primary display and starts its WebView2 (UI thread). The returned
    /// task faults when the WebView2 can't start.
    /// </summary>
    public Task StartAsync(BoothWindowServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;

        var window = new BoothWindow(Role.Operator, services);
        var handle = window.EnsureHandle();
        var primary = NativeMethods.MonitorFromPoint(default, NativeMethods.MonitorDefaultToPrimary);
        var info = new NativeMethods.MonitorInfoEx
        {
            Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
            Device = string.Empty,
        };
        if (NativeMethods.GetMonitorInfo(primary, ref info))
        {
            // Restore bounds on the primary display's work area, so maximizing happens there.
            var work = WindowsDisplayAdapter.ToDisplayRect(info.Work);
            NativeMethods.SetWindowPos(handle, IntPtr.Zero, work.X, work.Y, work.Width, work.Height, NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        }

        window.WindowState = WindowState.Maximized;
        window.LocationChanged += (_, _) => UpdateOperatorDisplayId();
        window.SizeChanged += (_, _) => UpdateOperatorDisplayId();
        window.StateChanged += (_, _) => UpdateOperatorDisplayId();
        window.DpiChanged += (_, _) => UpdateOperatorDisplayId();
        window.Closed += (_, _) =>
        {
            _operatorOpen = false;
            _operatorDisplayId = null;
            CloseOverlays();
        };

        _operator = window;
        _operatorOpen = true;
        Application.Current.MainWindow = window;
        window.Show();
        UpdateOperatorDisplayId();
        return window.LoadWebViewAsync();
    }

    /// <summary>Brings the Operator window forward: restored if minimized, then the foreground window if Windows allows (UI thread). Returns whether it took the foreground.</summary>
    public bool ActivateOperator() => _operator is { } window && ForegroundActivator.Bring(new BoothWindowForeground(window));

    public bool HasWindow(Role role) => role == Role.Operator ? _operatorOpen : _guestOpen;

    public BoothWindow? WindowFor(Role role) => role == Role.Operator ? _operator : _guest;

    public void OpenGuestScreen(DisplayInfo display)
    {
        ArgumentNullException.ThrowIfNull(display);
        _guestOpen = true;
        _dispatcher.InvokeAsync(() => OpenGuest(display));
    }

    public void CloseGuestScreen()
    {
        _guestOpen = false;
        _dispatcher.InvokeAsync(CloseGuest);
    }

    public void OpenFolder(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        // Starting Explorer can take a moment; the brain's call returns at once.
        _ = Task.Run(() => _folders.Open(path));
    }

    public string? GetOperatorDisplayId() => _operatorDisplayId;

    public void ShowIdentifyOverlays(IReadOnlyList<IdentifyOverlaySpec> overlays)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        IReadOnlyList<IdentifyOverlaySpec> specs = [.. overlays];
        HostLog.IdentifyOverlaysRequested(_logger, specs.Count);
        _dispatcher.InvokeAsync(() => ShowOverlays(specs));
    }

    public void HideIdentifyOverlays()
    {
        HostLog.IdentifyOverlaysHideRequested(_logger);
        _dispatcher.InvokeAsync(CloseOverlays);
    }

    /// <summary>The identify overlays showing now (UI thread).</summary>
    public IReadOnlyList<IdentifyOverlayWindow> Overlays => _overlays;

    /// <summary>Closes every identify overlay (UI thread; also used at shutdown).</summary>
    public void CloseOverlays()
    {
        var showing = _overlays.ToArray();
        _overlays.Clear();
        foreach (var overlay in showing)
        {
            overlay.Close();
        }
    }

    private void ShowOverlays(IReadOnlyList<IdentifyOverlaySpec> specs)
    {
        // A new Show replaces the overlays already showing.
        CloseOverlays();
        OverlayFonts fonts;
        try
        {
            fonts = _fonts ??= OverlayFonts.Load(_fontsFolder);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.IdentifyOverlayFailed(_logger, "fonts", exception);
            return;
        }

        foreach (var spec in specs)
        {
            var overlay = new IdentifyOverlayWindow(spec, fonts);
            try
            {
                overlay.Present();
                _overlays.Add(overlay);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                HostLog.IdentifyOverlayFailed(_logger, spec.Display.Id, exception);
                overlay.Close();
            }
        }
    }

    /// <summary>Facts about each open window (UI thread).</summary>
    public IReadOnlyList<WindowFacts> DescribeWindows()
    {
        var facts = new List<WindowFacts>();
        foreach (var window in new[] { _operator, _guest })
        {
            if (window is not null && window.Handle != IntPtr.Zero)
            {
                facts.Add(new WindowFacts(window.Role, window.GetPhysicalBounds(), DisplayIdOf(window)));
            }
        }

        return facts;
    }

    /// <summary>
    /// Sizes a role's window so its client area (the page viewport) is <paramref name="width"/> × <paramref name="height"/>
    /// physical pixels, keeping its outer top-left (UI thread). Returns the window's new outer bounds.
    /// </summary>
    public WindowFacts? SetWindowSize(Role role, int width, int height)
    {
        if (WindowFor(role) is not { } window || window.Handle == IntPtr.Zero)
        {
            return null;
        }

        window.SetClientSize(width, height);
        return new WindowFacts(role, window.GetPhysicalBounds(), DisplayIdOf(window));
    }

    /// <summary>Closes the Guest window, if open (UI thread; also used at shutdown).</summary>
    public void CloseGuest()
    {
        if (_guest is not { } guest)
        {
            return;
        }

        _guest = null;
        _guestOpen = false;
        _services?.Guard.Detach();
        guest.CloseByHost();
        HostLog.GuestScreenClosed(_logger);
    }

    private void OpenGuest(DisplayInfo display)
    {
        CloseGuest();
        if (_services is not { } services)
        {
            return;
        }

        // The display is used as given (it may be virtual): no lookup in the adapter.
        var window = new BoothWindow(Role.Guest, services)
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Topmost = HostBuild.GuestWindowTopmost,
            ShowActivated = false,
        };
        window.PinBounds(display.Bounds);
        _guest = window;
        _guestOpen = true;
        window.Closed += (_, _) =>
        {
            // Closed by something other than CloseGuest (no Event runs, so the closing handler let it go): forget it.
            if (ReferenceEquals(_guest, window))
            {
                _guest = null;
                _guestOpen = false;
                services.Guard.Detach();
            }
        };
        window.Show();
        window.PinBounds(display.Bounds);
        if (!services.Guard.Attach(window.EnsureHandle()))
        {
            HostLog.InputGuardUnavailable(_logger, services.Guard.LastError);
        }

        var windowBounds = window.GetPhysicalBounds();
        HostLog.GuestScreenOpened(_logger, display.Name, display.Id, display.Bounds, display.Scale, windowBounds);
        _ = LoadGuestAsync(window);
    }

    private async Task LoadGuestAsync(BoothWindow window)
    {
        try
        {
            await window.LoadWebViewAsync();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.GuestOpenFailed(_logger, exception);
            if (ReferenceEquals(_guest, window))
            {
                CloseGuest();
            }
        }
    }

    private void UpdateOperatorDisplayId()
    {
        if (_operator is { } window && window.Handle != IntPtr.Zero)
        {
            _operatorDisplayId = DisplayIdOf(window);
        }
    }

    private string? DisplayIdOf(BoothWindow window) =>
        _displays.IdForMonitor(NativeMethods.MonitorFromWindow(window.Handle, NativeMethods.MonitorDefaultToNearest));

}
