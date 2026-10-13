using FotoHavn.Core.Displays;

namespace FotoHavn.Host.Windows.Displays;

/// <summary>One active display path from <c>QueryDisplayConfig</c>: its GDI source name and its monitor's identity.</summary>
/// <param name="GdiDeviceName">The source's GDI device name (<c>\\.\DISPLAY1</c>), which joins it to its monitor.</param>
/// <param name="MonitorDevicePath">The target's monitor device path: the display's stable identity. May be empty.</param>
/// <param name="FriendlyName">The target's monitor friendly name. May be empty.</param>
internal sealed record DisplayPathRecord(string GdiDeviceName, string MonitorDevicePath, string FriendlyName);

/// <summary>One monitor from <c>EnumDisplayMonitors</c>/<c>GetMonitorInfo</c>/<c>GetDpiForMonitor</c>, in physical pixels.</summary>
internal sealed record MonitorRecord(string GdiDeviceName, DisplayRect Bounds, DisplayRect WorkArea, uint Dpi, bool IsPrimary);

/// <summary>
/// The displays the adapter reports, joined from display-configuration paths and monitors. Immutable, so the brain can
/// read it from any thread. This is fact mapping only; display rules live in <c>FotoHavn.Core</c>.
/// </summary>
internal sealed class DisplaySnapshot
{
    private readonly Dictionary<string, string> _idByGdiDeviceName;

    private DisplaySnapshot(IReadOnlyList<DisplayInfo> displays, Dictionary<string, string> idByGdiDeviceName)
    {
        Displays = displays;
        _idByGdiDeviceName = idByGdiDeviceName;
    }

    public static DisplaySnapshot Empty { get; } = new([], new(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyList<DisplayInfo> Displays { get; }

    /// <summary>
    /// Joins each monitor to the first path with the same GDI device name (cloned displays share a source, so the first
    /// target names it). <see cref="DisplayInfo.Id"/> is the monitor device path, or the GDI device name when Windows
    /// reports no path (for example a remote session); the name falls back the same way. Displays are ordered by
    /// position, left to right then top to bottom, for stable logs only: nothing matches by order.
    /// </summary>
    public static DisplaySnapshot Build(IEnumerable<DisplayPathRecord> paths, IEnumerable<MonitorRecord> monitors)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(monitors);

        var pathByGdiName = new Dictionary<string, DisplayPathRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            pathByGdiName.TryAdd(path.GdiDeviceName, path);
        }

        var displays = new List<DisplayInfo>();
        var idByGdiName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var monitor in monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y))
        {
            if (!idByGdiName.TryAdd(monitor.GdiDeviceName, string.Empty))
            {
                continue;
            }

            pathByGdiName.TryGetValue(monitor.GdiDeviceName, out var path);
            var id = string.IsNullOrWhiteSpace(path?.MonitorDevicePath) ? monitor.GdiDeviceName : path.MonitorDevicePath;
            var name = string.IsNullOrWhiteSpace(path?.FriendlyName) ? ShortGdiName(monitor.GdiDeviceName) : path.FriendlyName;
            var scale = monitor.Dpi == 0 ? 1.0 : monitor.Dpi / 96.0;

            idByGdiName[monitor.GdiDeviceName] = id;
            displays.Add(new DisplayInfo(id, name, monitor.Bounds, monitor.WorkArea, scale, monitor.IsPrimary));
        }

        return new DisplaySnapshot(displays, idByGdiName);
    }

    /// <summary>The display id for a monitor's GDI device name, or null when this snapshot doesn't have it.</summary>
    public string? IdForGdiDeviceName(string? gdiDeviceName) =>
        gdiDeviceName is not null && _idByGdiDeviceName.TryGetValue(gdiDeviceName, out var id) ? id : null;

    /// <summary>Whether both snapshots report the same displays in the same order.</summary>
    public bool HasSameDisplays(DisplaySnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Displays.SequenceEqual(other.Displays);
    }

    /// <summary>One line per display, for the log.</summary>
    public override string ToString() =>
        string.Join("; ", Displays.Select(d => FormattableString.Invariant(
            $"{d.Name} [{d.Id}] {d.Bounds.Width}x{d.Bounds.Height} at {d.Bounds.X},{d.Bounds.Y}, work area {d.WorkArea.X},{d.WorkArea.Y} {d.WorkArea.Width}x{d.WorkArea.Height}, scale {d.Scale}{(d.IsPrimary ? ", primary" : string.Empty)}")));

    private static string ShortGdiName(string gdiDeviceName) =>
        gdiDeviceName.StartsWith(@"\\.\", StringComparison.Ordinal) ? gdiDeviceName[4..] : gdiDeviceName;
}
