using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using FotoHavn.Core.Displays;
using FotoHavn.Host.Windows.Logging;
using Microsoft.Extensions.Logging;
using static FotoHavn.Host.Windows.Interop.NativeMethods;

namespace FotoHavn.Host.Windows.Displays;

/// <summary>
/// The real display adapter: <c>QueryDisplayConfig</c> + <c>DisplayConfigGetDeviceInfo</c> for each active display's
/// monitor device path and friendly name, and <c>EnumDisplayMonitors</c>/<c>GetMonitorInfo</c>/<c>GetDpiForMonitor</c>
/// for bounds, work area, scale, and the primary flag. A hidden top-level window receives <c>WM_DISPLAYCHANGE</c>,
/// <c>WM_SETTINGCHANGE</c> (work area), <c>WM_DPICHANGED</c>, and <c>WM_DEVICECHANGE</c>; each starts a short debounce,
/// then the adapter re-reads and raises <see cref="DisplaysChanged"/> only if the list changed.
/// </summary>
/// <remarks>
/// Create, refresh, and dispose on the UI thread. <see cref="GetDisplays"/> returns the cached snapshot from any thread.
/// </remarks>
internal sealed class WindowsDisplayAdapter : IDisplayAdapter, IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(250);

    private readonly ILogger _logger;
    private readonly HwndSource _watcher;
    private readonly DispatcherTimer _debounce;
    private volatile DisplaySnapshot _snapshot;

    public WindowsDisplayAdapter(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _snapshot = Read(DisplaySnapshot.Empty, logger);
        LogDisplays(_logger, _snapshot);

        _debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = Debounce };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Refresh();
        };

        // A hidden top-level window (never shown): WM_DISPLAYCHANGE and the device-change broadcast reach only top-level windows.
        _watcher = new HwndSource(new HwndSourceParameters("FOTOHAVN display watcher")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000), // WS_POPUP, without WS_VISIBLE
        });
        _watcher.AddHook(WatchMessages);
    }

    public event EventHandler? DisplaysChanged;

    /// <summary>Raised on the UI thread after the snapshot changed and before <see cref="DisplaysChanged"/>.</summary>
    public event EventHandler? SnapshotUpdated;

    public DisplaySnapshot Snapshot => _snapshot;

    public IReadOnlyList<DisplayInfo> GetDisplays() => _snapshot.Displays;

    /// <summary>The id of the display a monitor handle names, from the current snapshot; null when unknown.</summary>
    public string? IdForMonitor(IntPtr monitor) => _snapshot.IdForGdiDeviceName(GdiDeviceName(monitor));

    /// <summary>Re-reads the displays now (UI thread) and raises the events if they changed.</summary>
    public void Refresh()
    {
        var previous = _snapshot;
        var next = Read(previous, _logger);
        if (next.HasSameDisplays(previous))
        {
            return;
        }

        _snapshot = next;
        LogDisplays(_logger, next);
        SnapshotUpdated?.Invoke(this, EventArgs.Empty);
        DisplaysChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _debounce.Stop();
        _watcher.RemoveHook(WatchMessages);
        _watcher.Dispose();
    }

    /// <summary>
    /// Reads paths and monitors. If <c>QueryDisplayConfig</c> fails, keeps <paramref name="previous"/> when it has
    /// displays, so ids never silently switch to GDI names; at start-up it falls back to GDI names.
    /// </summary>
    internal static DisplaySnapshot Read(DisplaySnapshot previous, ILogger logger)
    {
        IReadOnlyList<DisplayPathRecord> paths;
        try
        {
            paths = ReadPaths();
        }
        catch (Win32Exception exception)
        {
            HostLog.DisplayConfigFailed(logger, exception);
            if (previous.Displays.Count > 0)
            {
                return previous;
            }

            paths = [];
        }

        return DisplaySnapshot.Build(paths, ReadMonitors());
    }

    internal static string? GdiDeviceName(IntPtr monitor)
    {
        if (monitor == IntPtr.Zero)
        {
            return null;
        }

        var info = new MonitorInfoEx { Size = (uint)Marshal.SizeOf<MonitorInfoEx>(), Device = string.Empty };
        return GetMonitorInfo(monitor, ref info) ? info.Device : null;
    }

    private static List<DisplayPathRecord> ReadPaths()
    {
        DisplayConfigPathInfo[] paths;
        uint pathCount;
        int result;
        var attempts = 0;
        do
        {
            result = GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out pathCount, out var modeCount);
            if (result != ErrorSuccess)
            {
                throw new Win32Exception(result);
            }

            paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[modeCount];
            result = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
        }
        while (result == ErrorInsufficientBuffer && ++attempts < 5);

        if (result != ErrorSuccess)
        {
            throw new Win32Exception(result);
        }

        var records = new List<DisplayPathRecord>((int)pathCount);
        for (var i = 0; i < pathCount; i++)
        {
            var path = paths[i];
            var source = new DisplayConfigSourceDeviceName
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = DisplayConfigDeviceInfoGetSourceName,
                    Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                    AdapterId = path.SourceInfo.AdapterId,
                    Id = path.SourceInfo.Id,
                },
                ViewGdiDeviceName = string.Empty,
            };
            if (DisplayConfigGetDeviceInfo(ref source) != ErrorSuccess)
            {
                continue;
            }

            var target = new DisplayConfigTargetDeviceName
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = DisplayConfigDeviceInfoGetTargetName,
                    Size = (uint)Marshal.SizeOf<DisplayConfigTargetDeviceName>(),
                    AdapterId = path.TargetInfo.AdapterId,
                    Id = path.TargetInfo.Id,
                },
                MonitorFriendlyDeviceName = string.Empty,
                MonitorDevicePath = string.Empty,
            };
            var hasTarget = DisplayConfigGetDeviceInfo(ref target) == ErrorSuccess;
            records.Add(new DisplayPathRecord(
                source.ViewGdiDeviceName ?? string.Empty,
                hasTarget ? target.MonitorDevicePath ?? string.Empty : string.Empty,
                hasTarget ? target.MonitorFriendlyDeviceName ?? string.Empty : string.Empty));
        }

        return records;
    }

    private static List<MonitorRecord> ReadMonitors()
    {
        var monitors = new List<MonitorRecord>();
        MonitorEnumProc callback = (IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data) =>
        {
            var info = new MonitorInfoEx { Size = (uint)Marshal.SizeOf<MonitorInfoEx>(), Device = string.Empty };
            if (GetMonitorInfo(monitor, ref info))
            {
                var dpi = GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) == 0 ? dpiX : 96u;
                monitors.Add(new MonitorRecord(
                    info.Device,
                    ToDisplayRect(info.Monitor),
                    ToDisplayRect(info.Work),
                    dpi,
                    (info.Flags & MonitorInfoPrimary) != 0));
            }

            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return monitors;
    }

    internal static DisplayRect ToDisplayRect(Rect rect) => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    private static void LogDisplays(ILogger logger, DisplaySnapshot snapshot) =>
        HostLog.DisplaysRead(logger, snapshot.Displays.Count, snapshot);

    private IntPtr WatchMessages(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message is WmDisplayChange or WmSettingChange or WmDeviceChange or WmDpiChanged)
        {
            _debounce.Stop();
            _debounce.Start();
        }

        return IntPtr.Zero;
    }
}
