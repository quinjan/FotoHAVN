using FotoHavn.Core.Fakes;

namespace FotoHavn.Core.Tests;

public sealed class ScaledClockTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void At_factor_one_it_is_the_wrapped_clock()
    {
        var inner = new FakeClock(Start);
        var clock = new ScaledClock(inner);
        var fired = 0;

        clock.Schedule(TimeSpan.FromSeconds(10), () => fired++);
        inner.Advance(TimeSpan.FromSeconds(9));

        Assert.Equal(1, clock.Factor);
        Assert.Equal(Start + TimeSpan.FromSeconds(9), clock.UtcNow);
        Assert.Equal(0, fired);

        inner.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Time_and_timers_run_faster_by_the_factor()
    {
        var inner = new FakeClock(Start);
        var clock = new ScaledClock(inner);
        clock.SetFactor(10);
        var fired = 0;

        clock.Schedule(TimeSpan.FromSeconds(30), () => fired++);
        inner.Advance(TimeSpan.FromSeconds(2.9));
        Assert.Equal(0, fired);
        Assert.Equal(Start + TimeSpan.FromSeconds(29), clock.UtcNow);

        inner.Advance(TimeSpan.FromSeconds(0.1));
        Assert.Equal(1, fired);
        Assert.Equal(Start + TimeSpan.FromSeconds(30), clock.UtcNow);
    }

    [Fact]
    public void A_change_does_not_make_time_jump_and_reschedules_pending_timers()
    {
        var inner = new FakeClock(Start);
        var clock = new ScaledClock(inner);
        var fired = 0;
        clock.Schedule(TimeSpan.FromSeconds(100), () => fired++);
        inner.Advance(TimeSpan.FromSeconds(10));

        clock.SetFactor(10);

        Assert.Equal(Start + TimeSpan.FromSeconds(10), clock.UtcNow);
        inner.Advance(TimeSpan.FromSeconds(8.9));
        Assert.Equal(0, fired);
        Assert.Equal(Start + TimeSpan.FromSeconds(99), clock.UtcNow);
        inner.Advance(TimeSpan.FromSeconds(0.1));
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Slowing_down_again_stretches_what_is_left()
    {
        var inner = new FakeClock(Start);
        var clock = new ScaledClock(inner);
        clock.SetFactor(10);
        var fired = 0;
        clock.Schedule(TimeSpan.FromSeconds(60), () => fired++);
        inner.Advance(TimeSpan.FromSeconds(3));

        clock.SetFactor(1);

        inner.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(0, fired);
        inner.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, fired);
        Assert.Equal(Start + TimeSpan.FromSeconds(60), clock.UtcNow);
    }

    [Fact]
    public void A_disposed_timer_never_fires_and_a_fired_one_fires_once()
    {
        var inner = new FakeClock(Start);
        var clock = new ScaledClock(inner);
        var cancelled = 0;
        var once = 0;
        clock.Schedule(TimeSpan.FromSeconds(5), () => cancelled++).Dispose();
        clock.Schedule(TimeSpan.FromSeconds(5), () => once++);

        inner.Advance(TimeSpan.FromSeconds(5));
        clock.SetFactor(5);
        inner.Advance(TimeSpan.FromSeconds(50));

        Assert.Equal(0, cancelled);
        Assert.Equal(1, once);
        Assert.Equal(0, inner.PendingCount);
    }

    [Fact]
    public void A_timer_cancelled_before_a_change_stays_cancelled()
    {
        var inner = new FakeClock(Start);
        var clock = new ScaledClock(inner);
        var fired = 0;
        var timer = clock.Schedule(TimeSpan.FromSeconds(5), () => fired++);

        timer.Dispose();
        clock.SetFactor(2);
        inner.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(0, fired);
        Assert.Equal(0, inner.PendingCount);
    }

    [Fact]
    public void A_zero_delay_fires_at_once_and_a_late_timer_is_due_immediately_after_a_change()
    {
        var inner = new FakeClock(Start);
        var clock = new ScaledClock(inner);
        var fired = 0;
        clock.Schedule(TimeSpan.Zero, () => fired++);
        clock.Schedule(TimeSpan.FromSeconds(1), () => fired++);

        inner.Advance(TimeSpan.Zero);
        Assert.Equal(1, fired);
        inner.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, fired);
    }

    [Fact]
    public void A_timer_whose_due_time_passed_before_a_change_is_rearmed_to_fire_now()
    {
        var late = new LateClock(new FakeClock(Start));
        var clock = new ScaledClock(late);
        var fired = 0;
        clock.Schedule(TimeSpan.FromSeconds(1), () => fired++);
        var cancelled = clock.Schedule(TimeSpan.FromSeconds(1), () => fired += 100);
        cancelled.Dispose();

        late.Skip(TimeSpan.FromSeconds(5));
        clock.SetFactor(2);
        late.Fire();

        Assert.Equal(1, fired);
    }
    [Theory]
    [InlineData(0.5)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void The_factor_is_1_to_100(double factor)
    {
        var clock = new ScaledClock(new FakeClock(Start));

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetFactor(factor));
        Assert.Equal(1, clock.Factor);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void The_edges_of_the_range_are_allowed(double factor)
    {
        var clock = new ScaledClock(new FakeClock(Start));

        clock.SetFactor(factor);

        Assert.Equal(factor, clock.Factor);
    }

    [Fact]
    public void It_checks_its_arguments()
    {
        var clock = new ScaledClock(new FakeClock(Start));

        Assert.Throws<ArgumentNullException>(() => new ScaledClock(null!));
        Assert.Throws<ArgumentNullException>(() => clock.Schedule(TimeSpan.Zero, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Schedule(TimeSpan.FromSeconds(-1), () => { }));
    }

    [Fact]
    public void It_runs_on_the_real_clock_too()
    {
        var clock = new ScaledClock(SystemClock.Instance);
        clock.SetFactor(100);
        using var fired = new ManualResetEventSlim();

        clock.Schedule(TimeSpan.FromSeconds(5), fired.Set);

        Assert.True(fired.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken), "A 5-second timer at 100x fires in about 50 ms.");
    }

    [Fact]
    public void The_no_op_wake_lock_only_tracks_what_the_brain_asked()
    {
        var wakeLock = new NoOpWakeLock();
        Assert.False(wakeLock.IsHeld);

        wakeLock.Release();
        Assert.False(wakeLock.IsHeld);

        wakeLock.Acquire("first");
        wakeLock.Acquire("second");
        Assert.True(wakeLock.IsHeld);

        wakeLock.Release();
        Assert.False(wakeLock.IsHeld);
    }

    /// <summary>A wrapped clock that holds its timers until <see cref="Fire"/>, to test a callback that is due late.</summary>
    private sealed class LateClock(FakeClock inner) : IClock
    {
        private readonly List<Action> _callbacks = [];
        private TimeSpan _skipped;

        public DateTimeOffset UtcNow => inner.UtcNow + _skipped;

        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            _callbacks.Add(callback);
            return new Handle();
        }

        public void Skip(TimeSpan delta) => _skipped += delta;

        public void Fire()
        {
            foreach (var callback in _callbacks.ToArray())
            {
                callback();
            }
        }

        private sealed class Handle : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
