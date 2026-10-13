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

        services.OpenFolder(@"C:\Data\Events\event-1");
        Assert.Throws<ArgumentNullException>(() => services.OpenGuestScreen(null!));
        Assert.Throws<ArgumentNullException>(() => services.OpenFolder(null!));
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
