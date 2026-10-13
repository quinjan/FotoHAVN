using FotoHavn.Core.Displays;
using FotoHavn.Host.Windows.Displays;
using Microsoft.Extensions.Logging.Abstractions;

namespace FotoHavn.Host.Windows.Tests;

public sealed class DisplaySnapshotTests
{
    // A laptop with a docked 4K monitor at 150% to its left, in the shapes QueryDisplayConfig/DisplayConfigGetDeviceInfo
    // and GetMonitorInfo report (the laptop panel has no friendly name, as on the recorded machine below).
    private const string LaptopPath = @"\\?\DISPLAY#BOE0A1C#4&1f2e3d4c&0&UID8388688#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string MonitorPath = @"\\?\DISPLAY#DELA0F4#5&2b3c4d5e&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    private static readonly DisplayPathRecord[] Paths =
    [
        new(@"\\.\DISPLAY1", LaptopPath, string.Empty),
        new(@"\\.\DISPLAY2", MonitorPath, "DELL U2720Q"),
    ];

    private static readonly MonitorRecord[] Monitors =
    [
        new(@"\\.\DISPLAY1", new DisplayRect(0, 0, 1920, 1200), new DisplayRect(0, 0, 1920, 1152), 120, IsPrimary: true),
        new(@"\\.\DISPLAY2", new DisplayRect(-3840, -960, 3840, 2160), new DisplayRect(-3840, -960, 3840, 2088), 144, IsPrimary: false),
    ];

    [Fact]
    public void Joins_paths_and_monitors_by_GDI_device_name()
    {
        var snapshot = DisplaySnapshot.Build(Paths, Monitors);

        Assert.Equal(
            [
                new DisplayInfo(MonitorPath, "DELL U2720Q", new(-3840, -960, 3840, 2160), new(-3840, -960, 3840, 2088), 1.5, IsPrimary: false),
                new DisplayInfo(LaptopPath, "DISPLAY1", new(0, 0, 1920, 1200), new(0, 0, 1920, 1152), 1.25, IsPrimary: true),
            ],
            snapshot.Displays);
    }

    [Fact]
    public void Maps_the_recorded_single_laptop_panel()
    {
        // Recorded on the development laptop (one internal panel, no friendly name, 100%).
        const string panel = @"\\?\DISPLAY#CSW164A#4&18a747&0&UID8388688#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

        var snapshot = DisplaySnapshot.Build(
            [new DisplayPathRecord(@"\\.\DISPLAY1", panel, string.Empty)],
            [new MonitorRecord(@"\\.\DISPLAY1", new DisplayRect(0, 0, 1920, 1200), new DisplayRect(0, 0, 1920, 1152), 96, IsPrimary: true)]);

        Assert.Equal(
            new DisplayInfo(panel, "DISPLAY1", new(0, 0, 1920, 1200), new(0, 0, 1920, 1152), 1.0, IsPrimary: true),
            Assert.Single(snapshot.Displays));
    }

    [Fact]
    public void Maps_GDI_device_names_to_display_ids_case_insensitively()
    {
        var snapshot = DisplaySnapshot.Build(Paths, Monitors);

        Assert.Equal(LaptopPath, snapshot.IdForGdiDeviceName(@"\\.\display1"));
        Assert.Equal(MonitorPath, snapshot.IdForGdiDeviceName(@"\\.\DISPLAY2"));
        Assert.Null(snapshot.IdForGdiDeviceName(@"\\.\DISPLAY3"));
        Assert.Null(snapshot.IdForGdiDeviceName(null));
    }

    [Fact]
    public void Falls_back_to_the_GDI_name_without_a_device_path()
    {
        var snapshot = DisplaySnapshot.Build([], [Monitors[0]]);

        var display = Assert.Single(snapshot.Displays);
        Assert.Equal(@"\\.\DISPLAY1", display.Id);
        Assert.Equal("DISPLAY1", display.Name);
        Assert.Equal(@"\\.\DISPLAY1", snapshot.IdForGdiDeviceName(@"\\.\DISPLAY1"));
    }

    [Fact]
    public void Cloned_displays_take_the_first_target_of_their_source()
    {
        var clone = new DisplayPathRecord(@"\\.\DISPLAY1", @"\\?\DISPLAY#PROJ001#1&1&0&UID1#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", "Projector");

        var snapshot = DisplaySnapshot.Build([Paths[0], clone], [Monitors[0]]);

        Assert.Equal(LaptopPath, Assert.Single(snapshot.Displays).Id);
    }

    [Fact]
    public void Ignores_a_repeated_monitor_and_treats_zero_DPI_as_100_percent()
    {
        var monitor = Monitors[0] with { Dpi = 0 };

        var snapshot = DisplaySnapshot.Build(Paths, [monitor, monitor]);

        Assert.Equal(1.0, Assert.Single(snapshot.Displays).Scale);
    }

    [Fact]
    public void Compares_snapshots_by_their_displays()
    {
        var first = DisplaySnapshot.Build(Paths, Monitors);
        var same = DisplaySnapshot.Build(Enumerable.Reverse(Paths), Enumerable.Reverse(Monitors));
        var moved = DisplaySnapshot.Build(Paths, [Monitors[0], Monitors[1] with { Bounds = new(1920, 0, 3840, 2160) }]);

        Assert.True(first.HasSameDisplays(same));
        Assert.False(first.HasSameDisplays(moved));
        Assert.False(first.HasSameDisplays(DisplaySnapshot.Empty));
        Assert.Empty(DisplaySnapshot.Empty.Displays);
    }

    [Fact]
    public void Describes_each_display_for_the_log()
    {
        var text = DisplaySnapshot.Build(Paths, Monitors).ToString();

        Assert.Contains("DELL U2720Q [" + MonitorPath + "] 3840x2160 at -3840,-960", text, StringComparison.Ordinal);
        Assert.Contains("scale 1.25, primary", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_real_displays_of_this_machine()
    {
        // Exercises the Win32 struct layouts. Headless machines may have no display; then the list is empty.
        var snapshot = WindowsDisplayAdapter.Read(DisplaySnapshot.Empty, NullLogger.Instance);

        Assert.True(snapshot.Displays.Count(display => display.IsPrimary) <= 1);
        Assert.All(snapshot.Displays, display =>
        {
            Assert.False(string.IsNullOrWhiteSpace(display.Id));
            Assert.False(string.IsNullOrWhiteSpace(display.Name));
            Assert.True(display.Bounds.Width > 0 && display.Bounds.Height > 0);
            Assert.True(display.Scale >= 1.0);
        });
        Assert.Equal(snapshot.Displays.Count, snapshot.Displays.Select(display => display.Id).Distinct(StringComparer.Ordinal).Count());
    }
}
