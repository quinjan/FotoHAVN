using FotoHavn.Core;
using FotoHavn.Core.Fakes;

namespace FotoHavn.Host.Windows.Tests;

public sealed class ClockCompositionTests
{
    [Fact]
    public void A_scaled_composition_wraps_the_system_clock_and_runs_at_the_factor()
    {
        var system = new FakeClock(DateTimeOffset.Parse("2026-10-14T09:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture));

        var clock = ClockComposition.Compose(system, scaled: true);

        var scalable = Assert.IsAssignableFrom<IScalableClock>(clock);
        Assert.Equal(1, scalable.Factor);
        scalable.SetFactor(10);
        system.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(10, scalable.Factor);
        Assert.Equal(system.UtcNow.AddSeconds(27), clock.UtcNow);
    }

    [Fact]
    public void An_unscaled_composition_is_the_system_clock_itself()
    {
        Assert.Same(SystemClock.Instance, ClockComposition.Compose(SystemClock.Instance, scaled: false));
        Assert.Throws<ArgumentNullException>(() => ClockComposition.Compose(null!, scaled: true));
    }

    [Fact]
    public void Only_an_E2E_build_scales_the_brain_clock()
    {
        var clock = ClockComposition.ForBuild(SystemClock.Instance);

#if E2E_HOOKS
        Assert.IsAssignableFrom<IScalableClock>(clock);
#else
        Assert.Same(SystemClock.Instance, clock);
        Assert.False(clock is IScalableClock);
#endif
    }
}
