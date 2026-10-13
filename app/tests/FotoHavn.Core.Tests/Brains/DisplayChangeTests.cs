using FotoHavn.Core.Fakes;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Testing;
using static FotoHavn.Core.Tests.Support.TestDisplays;

namespace FotoHavn.Core.Tests.Brains;

public sealed class DisplayChangeTests
{
    [Fact]
    public void Losing_the_Guest_Screen_display_closes_the_Guest_Screen()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);
        client.Test(TestHookNames.OpenGuestScreen, """{"displayId":"fake-guest"}""");
        harness.Run();

        harness.Displays.Disconnect("fake-guest");
        harness.Run();

        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], harness.Host.Calls);
        Assert.Contains(harness.Logger.Entries, entry => entry.Message.Contains("fake-guest is gone", StringComparison.Ordinal));
        client.Clear();
        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();
        Assert.False(Diagnostics(client).GuestScreenOpen);
    }

    [Fact]
    public void Other_display_changes_keep_the_Guest_Screen()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);
        client.Test(TestHookNames.OpenGuestScreen, """{"displayId":"fake-guest"}""");
        harness.Run();

        harness.Displays.Connect(Display("third", x: 3840));
        harness.Displays.Connect(Display("fake-guest", x: 1920, width: 2560, height: 1440));
        harness.Run();

        Assert.Equal(["OpenGuestScreen(fake-guest)"], harness.Host.Calls);
    }

    [Fact]
    public void Identity_is_the_device_path_not_the_position()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);
        client.Test(TestHookNames.OpenGuestScreen, """{"displayId":"fake-guest"}""");
        harness.Run();

        // Another monitor takes the guest display's place: same bounds, another device path.
        harness.Displays.SetDisplays([FakeDisplayAdapter.DefaultPrimary, Display("other-monitor", x: 1920)]);
        harness.Run();

        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], harness.Host.Calls);
    }

    [Fact]
    public void Display_changes_are_read_on_the_executor_and_listed_in_diagnostics()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);

        harness.Displays.Connect(Display("third", x: 3840, scale: 1.5));
        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        var displays = Diagnostics(client).Displays;
        Assert.False(displays.Virtual);
        Assert.Equal(["fake-primary", "fake-guest", "third"], displays.Displays.Select(display => display.Id));
        Assert.Equal(new TestDisplay("third", "third", new PixelRect(3840, 0, 1920, 1080), new PixelRect(3840, 0, 1920, 1080), 1.5, false), displays.Displays[2]);
    }

    [Fact]
    public void A_display_change_after_the_brain_is_disposed_is_ignored()
    {
        var harness = BrainHarness.WithHooks();
        harness.Dispose();

        harness.Displays.Disconnect("fake-guest");

        Assert.Equal(0, harness.Executor.PendingCount);
    }

    private static DiagnosticsResult Diagnostics(TestClient client) =>
        Protocol.ProtocolJson.Deserialize<DiagnosticsResult>(Assert.IsType<TestResultMessage>(client.Messages[^1]).Result!.Value);
}
