using FotoHavn.Core.Events;
using FotoHavn.Core.GuestCycles;
using FotoHavn.Core.Storage;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace FotoHavn.Core.Tests.GuestCycles;

/// <summary>The Phase Timer engine (plan section 7, Phase timers; D18) on explicit times: pause and resume around the warning boundary, many pause cycles, restarts, and off.</summary>
public sealed class PhaseTimerEngineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(double seconds) => T0.AddSeconds(seconds);

    private static PhaseTimerEngine Template(int seconds = 30) => PhaseTimerEngine.Start(PhaseTimerPhase.Template, seconds, T0);

    [Fact]
    public void A_stopped_timer_has_nothing_to_report()
    {
        var timer = PhaseTimerEngine.Stopped;

        Assert.False(timer.IsActive);
        Assert.False(timer.IsRunning);
        Assert.False(timer.IsPaused);
        Assert.False(timer.IsOff);
        Assert.Null(timer.Phase);
        Assert.Null(timer.Remaining(T0));
        Assert.False(timer.IsExpired(T0));
        Assert.False(timer.IsWarning(T0));
        Assert.Null(timer.NextDeadline(T0));
    }

    [Fact]
    public void Starting_runs_the_phase_for_its_full_time()
    {
        var timer = Template(30);

        Assert.True(timer.IsActive);
        Assert.True(timer.IsRunning);
        Assert.Equal(PhaseTimerPhase.Template, timer.Phase);
        Assert.Equal(TimeSpan.FromSeconds(30), timer.Total);
        Assert.Equal(TimeSpan.FromSeconds(30), timer.Remaining(T0));
        Assert.Equal(TimeSpan.FromSeconds(12.5), timer.Remaining(At(17.5)));
        Assert.False(timer.IsExpired(At(29.999)));
        Assert.True(timer.IsExpired(At(30)));
        Assert.True(timer.IsExpired(At(300)));
        Assert.Equal(TimeSpan.Zero, timer.Remaining(At(300)));
    }

    [Fact]
    public void A_time_before_the_start_counts_as_no_time_elapsed()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), Template().Remaining(At(-5)));
    }

    [Fact]
    public void Starting_with_a_negative_time_or_an_unknown_phase_is_a_programming_error()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseTimerEngine.Start(PhaseTimerPhase.Template, -1, T0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseTimerEngine.Start((PhaseTimerPhase)99, 30, T0));
    }

    [Fact]
    public void The_warning_covers_the_last_twenty_seconds_and_ends_at_expiry()
    {
        var timer = Template(30);

        Assert.False(timer.IsWarning(At(0)));
        Assert.False(timer.IsWarning(At(9.999)));
        Assert.True(timer.IsWarning(At(10)));
        Assert.True(timer.IsWarning(At(29.999)));
        Assert.False(timer.IsWarning(At(30)));
    }

    [Fact]
    public void A_twenty_second_phase_warns_from_the_start()
    {
        Assert.True(Template(20).IsWarning(T0));
    }

    [Theory]
    [InlineData(PhaseTimerPhase.Template, true)]
    [InlineData(PhaseTimerPhase.PhotographsCapture, true)]
    [InlineData(PhaseTimerPhase.PhotographsReview, true)]
    [InlineData(PhaseTimerPhase.Filters, true)]
    [InlineData(PhaseTimerPhase.GuestGallery, true)]
    [InlineData(PhaseTimerPhase.Keepsake, false)]
    public void Every_timed_phase_warns_except_the_Keepsake(PhaseTimerPhase phase, bool warns)
    {
        var timer = PhaseTimerEngine.Start(phase, 25, T0);

        Assert.Equal(warns, timer.IsWarning(At(20)));
        Assert.True(timer.IsExpired(At(25)));
    }

    [Fact]
    public void The_next_deadline_is_the_warning_start_and_then_the_expiry()
    {
        var timer = Template(30);

        Assert.Equal(At(10), timer.NextDeadline(T0));
        Assert.Equal(At(10), timer.NextDeadline(At(9.999)));
        Assert.Equal(At(30), timer.NextDeadline(At(10)));
        Assert.Equal(At(30), timer.NextDeadline(At(25)));
        Assert.Equal(At(30), timer.NextDeadline(At(40)));
    }

    [Fact]
    public void The_Keepsake_has_only_an_expiry_deadline()
    {
        var timer = PhaseTimerEngine.Start(PhaseTimerPhase.Keepsake, 12, T0);

        Assert.Equal(At(12), timer.NextDeadline(T0));
        Assert.Equal(At(12), timer.NextDeadline(At(5)));
    }

    [Fact]
    public void An_off_timer_never_expires_warns_or_wakes_the_brain()
    {
        var timer = PhaseTimerEngine.Start(PhaseTimerPhase.Filters, 0, T0);

        Assert.True(timer.IsActive);
        Assert.True(timer.IsOff);
        Assert.True(timer.IsRunning);
        Assert.Null(timer.Remaining(At(100_000)));
        Assert.False(timer.IsExpired(At(100_000)));
        Assert.False(timer.IsWarning(At(100_000)));
        Assert.Null(timer.NextDeadline(T0));
        Assert.False(timer.Pause(At(1)).Resume(At(5)).IsExpired(At(100_000)));
        Assert.True(timer.Restart(At(3)).IsOff);
    }

    [Fact]
    public void Paused_time_does_not_count()
    {
        var paused = Template(30).Pause(At(10));

        Assert.True(paused.IsPaused);
        Assert.False(paused.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(20), paused.Remaining(At(10)));
        Assert.Equal(TimeSpan.FromSeconds(20), paused.Remaining(At(5000)));
        Assert.False(paused.IsExpired(At(5000)));
        Assert.Null(paused.NextDeadline(At(500)));

        var resumed = paused.Resume(At(100));
        Assert.True(resumed.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(20), resumed.Remaining(At(100)));
        Assert.Equal(TimeSpan.FromSeconds(5), resumed.Remaining(At(115)));
        Assert.Equal(At(120), resumed.NextDeadline(At(100)));
    }

    [Fact]
    public void Pausing_and_resuming_around_the_warning_boundary_moves_the_warning_by_the_paused_time()
    {
        var timer = Template(30).Pause(At(9));

        Assert.False(timer.IsWarning(At(1000)));
        timer = timer.Resume(At(50));
        Assert.False(timer.IsWarning(At(50.5)));
        Assert.Equal(At(51), timer.NextDeadline(At(50)));
        Assert.True(timer.IsWarning(At(51)));
        Assert.Equal(At(71), timer.NextDeadline(At(51)));

        var inWarning = timer.Pause(At(60));
        Assert.True(inWarning.IsWarning(At(9999)));
        Assert.Equal(TimeSpan.FromSeconds(11), inWarning.Remaining(At(9999)));
        Assert.Equal(At(10_071), inWarning.Resume(At(10_060)).NextDeadline(At(10_060)));
    }

    [Fact]
    public void Many_pause_cycles_count_only_the_running_intervals()
    {
        var timer = Template(300);
        var now = 0.0;
        var running = 0.0;
        for (var cycle = 0; cycle < 200; cycle++)
        {
            now += 0.5;
            running += 0.5;
            timer = timer.Pause(At(now));
            now += 3;
            timer = timer.Resume(At(now));
        }

        Assert.Equal(TimeSpan.FromSeconds(300 - running), timer.Remaining(At(now)));
        Assert.False(timer.IsExpired(At(now)));
    }

    [Fact]
    public void Pausing_a_paused_timer_and_resuming_a_running_one_change_nothing()
    {
        var running = Template(30);
        var paused = running.Pause(At(4));

        Assert.Same(running, running.Resume(At(8)));
        Assert.Same(paused, paused.Pause(At(8)));
        Assert.Same(PhaseTimerEngine.Stopped, PhaseTimerEngine.Stopped.Pause(At(1)));
        Assert.Same(PhaseTimerEngine.Stopped, PhaseTimerEngine.Stopped.Resume(At(1)));
    }

    [Fact]
    public void Restarting_gives_the_phase_its_full_time_again()
    {
        var restarted = Template(30).Restart(At(25));

        Assert.True(restarted.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(30), restarted.Remaining(At(25)));
        Assert.Equal(TimeSpan.FromSeconds(10), restarted.Remaining(At(45)));
        Assert.False(restarted.IsWarning(At(30)));
        Assert.Equal(At(35), restarted.NextDeadline(At(25)));
    }

    [Fact]
    public void Restarting_an_expired_timer_runs_the_phase_again()
    {
        var timer = Template(30);

        Assert.True(timer.IsExpired(At(31)));
        Assert.False(timer.Restart(At(31)).IsExpired(At(31)));
    }

    [Fact]
    public void Restarting_a_paused_timer_keeps_it_paused_with_the_full_time_left()
    {
        var restarted = Template(30).Pause(At(12)).Restart(At(20));

        Assert.True(restarted.IsPaused);
        Assert.Equal(TimeSpan.FromSeconds(30), restarted.Remaining(At(500)));
        Assert.Equal(TimeSpan.FromSeconds(30), restarted.Resume(At(500)).Remaining(At(500)));
    }

    [Fact]
    public void Restarting_a_stopped_timer_leaves_it_stopped_and_stopping_stops_a_timer()
    {
        Assert.Same(PhaseTimerEngine.Stopped, PhaseTimerEngine.Stopped.Restart(At(1)));
        Assert.Same(PhaseTimerEngine.Stopped, PhaseTimerEngine.Stopped.Stop());
        Assert.Same(PhaseTimerEngine.Stopped, Template().Stop());
        Assert.Same(PhaseTimerEngine.Stopped, Template().Pause(At(3)).Stop());
    }

    [Fact]
    public void Starting_another_phase_replaces_the_timer()
    {
        var next = PhaseTimerEngine.Start(PhaseTimerPhase.PhotographsCapture, 45, At(30));

        Assert.Equal(PhaseTimerPhase.PhotographsCapture, next.Phase);
        Assert.Equal(TimeSpan.FromSeconds(45), next.Remaining(At(30)));
    }

    [Fact]
    public void A_timer_paused_at_expiry_stays_expired()
    {
        var paused = Template(30).Pause(At(30));

        Assert.True(paused.IsExpired(At(31)));
        Assert.False(paused.IsWarning(At(31)));
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = 300)]
    public bool Remaining_is_the_total_minus_the_sum_of_the_running_intervals(Pattern pattern)
    {
        var timer = PhaseTimerEngine.Start(PhaseTimerPhase.Template, pattern.Seconds, T0);
        var now = T0;
        var running = TimeSpan.Zero;
        foreach (var (runFor, pauseFor) in pattern.Cycles)
        {
            now += runFor;
            running += runFor;
            timer = timer.Pause(now);
            now += pauseFor;
            timer = timer.Resume(now);
        }

        var left = TimeSpan.FromSeconds(pattern.Seconds) - running;
        var expected = left < TimeSpan.Zero ? TimeSpan.Zero : left;
        return timer.Remaining(now) == expected
            && timer.IsExpired(now) == (expected == TimeSpan.Zero)
            && timer.IsWarning(now) == (expected > TimeSpan.Zero && expected <= TimeSpan.FromSeconds(PhaseTimerRulesWarning));
    }

    private const int PhaseTimerRulesWarning = PhaseTimerRules.WarningSeconds;

    /// <summary>A start time in seconds and run/pause intervals (the running interval is counted, the paused one is not).</summary>
    public sealed record Pattern(int Seconds, IReadOnlyList<(TimeSpan Run, TimeSpan Pause)> Cycles);

    public static class Generators
    {
        public static Arbitrary<Pattern> Patterns()
        {
            var interval = Gen.Choose(0, 40_000).Select(milliseconds => TimeSpan.FromMilliseconds(milliseconds));
            var cycle = interval.SelectMany(run => interval.Select(pause => (run, pause)));
            return Gen.Choose(20, 300).SelectMany(seconds => Gen.ArrayOf(cycle, 12).Select(cycles => new Pattern(seconds, cycles))).ToArbitrary();
        }
    }
}
