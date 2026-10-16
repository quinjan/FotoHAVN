using FotoHavn.Core.Hardening;
using FotoHavn.Host.Windows.Hardening;

namespace FotoHavn.Host.Windows.Tests;

/// <summary><c>--smoke</c> (M10): the Operator UI becoming ready exits success, the time running out exits the timeout code, and only the first counts.</summary>
public sealed class SmokeRunTests
{
    [Fact]
    public void The_Operator_UI_becoming_ready_exits_with_the_success_code_once()
    {
        var exits = new List<int>();
        using var smoke = new SmokeRun(exits.Add, TimeSpan.FromMinutes(5));

        smoke.OperatorReady();
        smoke.OperatorReady();

        Assert.Equal([HostExitCodes.Success], exits);
    }

    [Fact]
    public void The_time_running_out_exits_with_the_timeout_code()
    {
        using var exited = new ManualResetEventSlim();
        var exits = new List<int>();
        using var smoke = new SmokeRun(
            code =>
            {
                exits.Add(code);
                exited.Set();
            },
            TimeSpan.FromMilliseconds(50));

        Assert.True(exited.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        smoke.OperatorReady();

        Assert.Equal([HostExitCodes.SmokeTimeout], exits);
    }

    [Fact]
    public void Being_ready_cancels_the_timeout()
    {
        var exits = new List<int>();
        using var smoke = new SmokeRun(exits.Add, TimeSpan.FromMilliseconds(100));

        smoke.OperatorReady();
        Thread.Sleep(300);

        Assert.Equal([HostExitCodes.Success], exits);
        Assert.Equal(TimeSpan.FromSeconds(60), SmokeRun.DefaultTimeout);
        Assert.Throws<ArgumentNullException>(() => new SmokeRun(null!, TimeSpan.FromSeconds(1)));
    }
}

/// <summary>Bringing the Operator window forward for a second launch (M10): restore first, take the foreground, raise it briefly when Windows refuses.</summary>
public sealed class ForegroundActivatorTests
{
    private sealed class FakeWindow(bool minimized, bool foregroundAllowed) : IForegroundWindowApi
    {
        public List<string> Calls { get; } = [];

        public bool IsMinimized => minimized;

        public void Restore() => Calls.Add("restore");

        public bool SetForeground()
        {
            Calls.Add("foreground");
            return foregroundAllowed;
        }

        public void RaiseBriefly() => Calls.Add("raise");
    }

    [Fact]
    public void A_window_in_the_background_is_made_the_foreground_window()
    {
        var window = new FakeWindow(minimized: false, foregroundAllowed: true);

        Assert.True(ForegroundActivator.Bring(window));

        Assert.Equal(["foreground"], window.Calls);
    }

    [Fact]
    public void A_minimized_window_is_restored_first()
    {
        var window = new FakeWindow(minimized: true, foregroundAllowed: true);

        Assert.True(ForegroundActivator.Bring(window));

        Assert.Equal(["restore", "foreground"], window.Calls);
    }

    [Fact]
    public void When_Windows_refuses_the_foreground_the_window_is_raised_instead()
    {
        var window = new FakeWindow(minimized: true, foregroundAllowed: false);

        Assert.False(ForegroundActivator.Bring(window));

        Assert.Equal(["restore", "foreground", "raise"], window.Calls);
        Assert.Throws<ArgumentNullException>(() => ForegroundActivator.Bring(null!));
    }
}
