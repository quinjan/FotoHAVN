using FotoHavn.Core.Events;
using FotoHavn.Core.Storage;

namespace FotoHavn.Core.GuestCycles;

/// <summary>
/// The Phase Timer as a pure state machine on an injected time (plan section 7, Phase timers; D18). The brain feeds <c>now</c>
/// from its clock and schedules a wake-up at <see cref="NextDeadline"/>; nothing here reads a clock or starts a timer. It is
/// immutable: each operation returns the new state. The protocol's <c>PhaseTimer</c> enum is a different type, so this is the engine.
/// </summary>
/// <remarks>
/// A timer is <em>active</em> from <see cref="Start"/> until <see cref="Stop"/>. While active it is running or paused; paused time
/// doesn't count. A timer started with 0 seconds is off: it never expires and never warns.
/// </remarks>
public sealed class PhaseTimerEngine
{
    private static readonly TimeSpan WarningWindow = TimeSpan.FromSeconds(PhaseTimerRules.WarningSeconds);

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

    /// <summary>Whether the phase's timer is off (0 seconds): active but never expiring and never warning.</summary>
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
    /// Restarts the phase's full time (<c>MORE TIME</c>, going back, entering the phase again, or stopping a capture). A running timer
    /// runs from <paramref name="now"/>; a paused one stays paused with the full time left. A stopped timer stays stopped.
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

    /// <summary>
    /// Whether the warning with <c>MORE TIME</c> shows: the last <see cref="PhaseTimerRules.WarningSeconds"/> seconds before expiry, for
    /// a phase that has one (never the Keepsake). It ends at expiry, when the phase's outcome takes over.
    /// </summary>
    public bool IsWarning(DateTimeOffset now) =>
        Phase is { } phase && PhaseTimerRules.HasWarning(phase) && Remaining(now) is { } left && left > TimeSpan.Zero && left <= WarningWindow;

    /// <summary>
    /// When the brain should next wake for this timer: the warning's start while it hasn't begun, otherwise the expiry. Null when
    /// nothing is due: stopped, off, or paused (the brain asks again after <see cref="Resume"/>). It may be at or before
    /// <paramref name="now"/>, which means act now.
    /// </summary>
    public DateTimeOffset? NextDeadline(DateTimeOffset now)
    {
        if (_runningSince is not { } since || IsOff)
        {
            return null;
        }

        var expiry = since + (Total - _elapsedBeforeRun);
        var warningStart = expiry - WarningWindow;
        return PhaseTimerRules.HasWarning(Phase!.Value) && now < warningStart ? warningStart : expiry;
    }

    private TimeSpan ElapsedAt(DateTimeOffset now) => _runningSince is { } since ? Elapsed(since, now) : _elapsedBeforeRun;

    private TimeSpan Elapsed(DateTimeOffset since, DateTimeOffset now) => _elapsedBeforeRun + (now > since ? now - since : TimeSpan.Zero);
}
