namespace FotoHavn.Camera.Windows.Tests;

public sealed class FrameClockTests
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    [Fact]
    public void Now_reads_the_supplied_clock()
    {
        var now = TimeSpan.FromSeconds(10);
        var clock = new FrameClock(() => now);

        Assert.Equal(TimeSpan.FromSeconds(10), clock.Now);
        now = TimeSpan.FromSeconds(11);
        Assert.Equal(TimeSpan.FromSeconds(11), clock.Now);
    }

    [Fact]
    public void Default_clock_advances_monotonically()
    {
        var clock = new FrameClock();
        var first = clock.Now;
        Thread.Sleep(20);
        var second = clock.Now;

        Assert.True(second > first);
        Assert.True(second - first < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_rejects_a_null_clock() =>
        Assert.Throws<ArgumentNullException>(() => new FrameClock(null!));

    [Fact]
    public void Stamp_uses_the_cameras_system_relative_time_when_it_is_a_recent_past_capture_time()
    {
        var now = TimeSpan.FromSeconds(100);
        var clock = new FrameClock(() => now);

        Assert.Equal(TimeSpan.FromSeconds(99.97), clock.Stamp(TimeSpan.FromSeconds(99.97)));
    }

    [Fact]
    public void Stamp_uses_the_arrival_time_without_a_system_relative_time()
    {
        var clock = new FrameClock(() => TimeSpan.FromSeconds(100));

        Assert.Equal(TimeSpan.FromSeconds(100), clock.Stamp(null));
    }

    [Fact]
    public void Stamp_uses_the_arrival_time_when_the_camera_time_is_in_the_future()
    {
        var clock = new FrameClock(() => TimeSpan.FromSeconds(100));

        Assert.Equal(TimeSpan.FromSeconds(100), clock.Stamp(TimeSpan.FromSeconds(100.5)));
    }

    [Fact]
    public void Stamp_uses_the_arrival_time_when_the_camera_time_is_on_another_time_base()
    {
        var clock = new FrameClock(() => TimeSpan.FromSeconds(100));

        Assert.Equal(TimeSpan.FromSeconds(100), clock.Stamp(TimeSpan.FromSeconds(100) - FrameClock.MaximumPlausibleLatency - TimeSpan.FromTicks(1)));
        Assert.Equal(TimeSpan.FromSeconds(100), clock.Stamp(TimeSpan.FromHours(-3)));
    }

    [Fact]
    public void Stamp_accepts_the_oldest_plausible_latency_and_an_instant_frame()
    {
        var now = TimeSpan.FromSeconds(100);
        var clock = new FrameClock(() => now);

        Assert.Equal(now - FrameClock.MaximumPlausibleLatency, clock.Stamp(now - FrameClock.MaximumPlausibleLatency));
        now += Second;
        Assert.Equal(now, clock.Stamp(now));
    }

    [Fact]
    public void Stamps_never_go_backwards()
    {
        var now = TimeSpan.FromSeconds(100);
        var clock = new FrameClock(() => now);

        var first = clock.Stamp(TimeSpan.FromSeconds(99.9));
        now = TimeSpan.FromSeconds(100.1);
        var second = clock.Stamp(TimeSpan.FromSeconds(99.5));
        var third = clock.Stamp(TimeSpan.FromSeconds(100.05));

        Assert.Equal(TimeSpan.FromSeconds(99.9), first);
        Assert.Equal(first, second);
        Assert.Equal(TimeSpan.FromSeconds(100.05), third);
    }

    [Fact]
    public void Counts_which_time_base_each_frame_was_stamped_from()
    {
        var clock = new FrameClock(() => TimeSpan.FromSeconds(100));

        clock.Stamp(TimeSpan.FromSeconds(99.9));
        clock.Stamp(TimeSpan.FromSeconds(99.8));
        clock.Stamp(null);
        clock.Stamp(TimeSpan.FromHours(5));

        Assert.Equal(2, clock.CameraTimeStamps);
        Assert.Equal(2, clock.ArrivalStamps);
    }

    [Fact]
    public void Frame_age_is_now_minus_the_stamp()
    {
        var now = TimeSpan.FromSeconds(100);
        var clock = new FrameClock(() => now);
        var stamp = clock.Stamp(TimeSpan.FromSeconds(99.95));
        now = TimeSpan.FromSeconds(100.45);

        Assert.Equal(TimeSpan.FromMilliseconds(500), clock.Now - stamp);
    }
}
