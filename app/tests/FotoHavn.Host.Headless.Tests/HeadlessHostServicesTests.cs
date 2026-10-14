using FotoHavn.Core;
using FotoHavn.Core.Displays;
using Microsoft.Extensions.Logging.Abstractions;

namespace FotoHavn.Host.Headless.Tests;

public sealed class HeadlessHostServicesTests
{
    private static readonly DisplayInfo Primary = HeadlessSettings.DefaultDisplays[0];
    private static readonly DisplayInfo Guest = HeadlessSettings.DefaultDisplays[1];

    [Fact]
    public void The_operator_display_is_the_primary_display()
    {
        Assert.Equal("fake-primary", Services(Guest, Primary).GetOperatorDisplayId());
        Assert.Null(Services(Guest).GetOperatorDisplayId());
    }

    [Fact]
    public void Opening_and_closing_the_guest_screen_is_only_recorded()
    {
        var services = Services(Primary, Guest);
        Assert.Null(services.GuestScreen);

        services.OpenGuestScreen(Guest);
        Assert.Same(Guest, services.GuestScreen);

        services.CloseGuestScreen();
        Assert.Null(services.GuestScreen);

        // Closing again, with no Guest Screen open, is a no-op.
        services.CloseGuestScreen();
        Assert.Null(services.GuestScreen);

        services.OpenFolder(@"C:\Data\Events\event-1");
        Assert.Throws<ArgumentNullException>(() => services.OpenGuestScreen(null!));
        Assert.Throws<ArgumentNullException>(() => services.OpenFolder(null!));
    }

    [Fact]
    public void Identify_overlays_are_only_recorded_and_a_new_show_replaces_them()
    {
        var services = Services(Primary, Guest);
        Assert.Empty(services.IdentifyOverlays);

        services.ShowIdentifyOverlays([new IdentifyOverlaySpec(Primary, 1, Primary.Name), new IdentifyOverlaySpec(Guest, 2, Guest.Name)]);
        Assert.Equal([1, 2], services.IdentifyOverlays.Select(overlay => overlay.Number));

        services.ShowIdentifyOverlays([new IdentifyOverlaySpec(Guest, 1, Guest.Name)]);
        Assert.Equal(["fake-guest"], services.IdentifyOverlays.Select(overlay => overlay.Display.Id));

        services.HideIdentifyOverlays();
        Assert.Empty(services.IdentifyOverlays);
        Assert.Throws<ArgumentNullException>(() => services.ShowIdentifyOverlays(null!));
    }

    [Fact]
    public void Only_an_E2E_build_scales_the_brain_clock()
    {
        Assert.Same(SystemClock.Instance, ClockComposition.Compose(SystemClock.Instance, scaled: false));
        Assert.IsAssignableFrom<IScalableClock>(ClockComposition.Compose(SystemClock.Instance, scaled: true));
        Assert.Throws<ArgumentNullException>(() => ClockComposition.Compose(null!, scaled: true));

        var clock = ClockComposition.ForBuild(SystemClock.Instance);
#if E2E_HOOKS
        Assert.IsAssignableFrom<IScalableClock>(clock);
#else
        Assert.Same(SystemClock.Instance, clock);
#endif
    }

    private static HeadlessHostServices Services(params DisplayInfo[] displays) =>
        new(new FixedDisplays(displays), NullLogger.Instance);

    private sealed class FixedDisplays(DisplayInfo[] displays) : IDisplayAdapter
    {
        public event EventHandler? DisplaysChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<DisplayInfo> GetDisplays() => displays;
    }
}
