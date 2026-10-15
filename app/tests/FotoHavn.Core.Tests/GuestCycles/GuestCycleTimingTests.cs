using FotoHavn.Core.GuestCycles.Orchestration;

namespace FotoHavn.Core.Tests.GuestCycles;

public sealed class GuestCycleTimingTests
{
    [Fact]
    public void The_pause_after_a_flash_is_800_ms_and_the_pushes_are_once_a_second()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(800), GuestCycleTiming.PostCapturePause);
        Assert.Equal(TimeSpan.FromSeconds(1), GuestCycleTiming.SecondTick);
    }
}
