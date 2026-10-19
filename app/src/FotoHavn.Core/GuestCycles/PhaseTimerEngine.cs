using FotoHavn.Core.Storage;

namespace FotoHavn.Core.GuestCycles;

/// <summary>
/// The Phase Timer as a pure state machine on an injected time (plan section 7, Phase timers; D18). The brain feeds <c>now</c>
/// from its clock and, while it <see cref="IsRunning"/>, schedules a wake-up from <see cref="Remaining"/>; nothing here reads a clock or starts a timer. It is
/// immutable: each operation returns the new state. The protocol's <c>PhaseTimer</c> enum is a different type, so this is the engine.
/// </summary>
/// <remarks>
/// A timer is <em>active</em> from <see cref="Start"/> until <see cref="Stop"/>. While active it is running or paused; paused time
/// doesn't count. A timer started with 0 seconds is off: it never expires.
/// </remarks>
public sealed class PhaseTimerEngine
{
    // The time counted before the current run, so paused intervals are never added.
    private readonly TimeSpan _elapsedBeforeRun;

    // When the current run began; null while paused.
    private readonly DateTimeOffset? _runningSince;

    private PhaseTimerEngine(PhaseTimerPhase? phase, TimeSpan total, TimeSpan elapsedBeforeRun, DateTimeOffset? runningSince)
    {
        Phase = phase;
        Total = total;
        _elapsedBeforeRun = elapsedBeforeRun;
        _runningSince = runningSince;
    }

    /// <summary>No timer is active.</summary>
    public static PhaseTimerEngine Stopped { get; } = new(null, TimeSpan.Zero, TimeSpan.Zero, null);

    /// <summary>The phase being timed, or null while stopped.</summary>
    public PhaseTimerPhase? Phase { get; }

    /// <summary>The phase's full time; zero when the timer is off or stopped.</summary>
    public TimeSpan Total { get; }

    /// <summary>Whether a phase is being timed (running or paused).</summary>
    public bool IsActive => Phase is not null;

    /// <summary>Whether the phase's timer is off (0 seconds): active but never expiring.</summary>
    public bool IsOff => IsActive && Total == TimeSpan.Zero;

    /// <summary>Whether time is counting now.</summary>
    public bool IsRunning => _runningSince is not null;

    /// <summary>Whether the timer is active but not counting: paused during a capture or Operator Assistance.</summary>
    public bool IsPaused => IsActive && !IsRunning;

    /// <summary>Starts timing <paramref name="phase"/> for <paramref name="seconds"/> (0: off) from <paramref name="now"/>, running.</summary>
    public static PhaseTimerEngine Start(PhaseTimerPhase phase, int seconds, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "Not a Phase Timer phase.");
        }

        return new PhaseTimerEngine(phase, TimeSpan.FromSeconds(seconds), TimeSpan.Zero, now);
    }

    /// <summary>Stops counting; paused time doesn't count. A stopped or already paused timer is returned as it is.</summary>
    public PhaseTimerEngine Pause(DateTimeOffset now) =>
        _runningSince is { } since ? new PhaseTimerEngine(Phase, Total, Elapsed(since, now), null) : this;

    /// <summary>Counts again from <paramref name="now"/>. A stopped or running timer is returned as it is.</summary>
    public PhaseTimerEngine Resume(DateTimeOffset now) =>
        IsPaused ? new PhaseTimerEngine(Phase, Total, _elapsedBeforeRun, now) : this;

    /// <summary>
    /// Restarts the phase's full time (the Camera tab appearing again, or an expiry whose outcome couldn't change the phase). A running
    /// timer runs from <paramref name="now"/>; a paused one stays paused with the full time left. A stopped timer stays stopped.
    /// </summary>
    public PhaseTimerEngine Restart(DateTimeOffset now) =>
        !IsActive ? this : new PhaseTimerEngine(Phase, Total, TimeSpan.Zero, IsRunning ? now : null);

    /// <summary>Stops timing.</summary>
    public PhaseTimerEngine Stop() => IsActive ? Stopped : this;

    /// <summary>The time left, never negative; null when the timer is off or stopped.</summary>
    public TimeSpan? Remaining(DateTimeOffset now)
    {
        if (!IsActive || IsOff)
        {
            return null;
        }

        var left = Total - ElapsedAt(now);
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }

    /// <summary>Whether the time is used up. Never true for an off or stopped timer.</summary>
    public bool IsExpired(DateTimeOffset now) => Remaining(now) is { } left && left <= TimeSpan.Zero;

    private TimeSpan ElapsedAt(DateTimeOffset now) => _runningSince is { } since ? Elapsed(since, now) : _elapsedBeforeRun;

    private TimeSpan Elapsed(DateTimeOffset since, DateTimeOffset now) => _elapsedBeforeRun + (now > since ? now - since : TimeSpan.Zero);
}
