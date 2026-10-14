namespace FotoHavn.Core.Events;

/// <summary>
/// The Operator PIN rules (plan section 8): 4 to 6 ASCII digits, entered twice when set, and the lockout after wrong
/// attempts at the Guest Screen's PIN dialog. The PIN keeps guests out; it isn't a security boundary.
/// </summary>
public static class PinRules
{
    public const int MinDigits = 4;
    public const int MaxDigits = 6;

    /// <summary>Five wrong attempts lock the dialog.</summary>
    public const int MaxWrongAttempts = 5;

    /// <summary>How long the dialog stays locked after the fifth wrong attempt.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);

    /// <summary>Whether <paramref name="pin"/> is 4 to 6 characters, each an ASCII <c>0</c> to <c>9</c> (no spaces, no other digits).</summary>
    public static bool IsValidFormat(string? pin) =>
        pin is { Length: >= MinDigits and <= MaxDigits } && pin.All(character => character is >= '0' and <= '9');

    /// <summary>
    /// Checks a PIN and its confirmation as Event setup receives them. Both empty is <see cref="EventSetupError.PinRequired"/>
    /// (the caller decides whether an Event that already has a PIN may leave them empty); a bad format is
    /// <see cref="EventSetupError.PinFormat"/>; two different values are <see cref="EventSetupError.PinMismatch"/>. Null when valid.
    /// </summary>
    public static EventSetupError? Check(string? pin, string? confirmation)
    {
        if (string.IsNullOrEmpty(pin) && string.IsNullOrEmpty(confirmation))
        {
            return EventSetupError.PinRequired;
        }

        if (!IsValidFormat(pin))
        {
            return EventSetupError.PinFormat;
        }

        return string.Equals(pin, confirmation, StringComparison.Ordinal) ? null : EventSetupError.PinMismatch;
    }
}

/// <summary>
/// The exit hold's length (plan section 8, End an Active Event): a 3-second press-and-hold on the Guest Screen's
/// discreet control. The UI draws the progress; the brain only accepts <c>CompleteExitHold</c> after enough time.
/// </summary>
public static class ExitHoldRules
{
    /// <summary>The hold the guest-facing control asks for.</summary>
    public static readonly TimeSpan RequiredHold = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The shortest hold the brain accepts, measured on its own clock from <c>BeginExitHold</c> to <c>CompleteExitHold</c>.
    /// Below <see cref="RequiredHold"/> to absorb the bridge's latency; far above a tap.
    /// </summary>
    public static readonly TimeSpan MinimumAccepted = TimeSpan.FromSeconds(2.5);

    public static bool IsComplete(TimeSpan held) => held >= MinimumAccepted;
}

/// <summary>
/// The wrong-PIN lockout, a pure state machine on injected time. <see cref="PinRules.MaxWrongAttempts"/> wrong attempts
/// lock it for <see cref="PinRules.LockoutDuration"/> from the last one; when the lockout ends the count starts again.
/// A correct PIN clears the count. Wrong attempts while locked change nothing (they don't extend the lockout).
/// </summary>
public sealed class PinLockout
{
    private int _wrongAttempts;
    private DateTimeOffset? _lockedUntil;

    /// <summary>Wrong attempts since the last correct PIN or the end of the last lockout, as of the last call that recorded one.</summary>
    public int WrongAttempts => _wrongAttempts;

    /// <summary>Whether the dialog is locked at <paramref name="now"/>.</summary>
    public bool IsLocked(DateTimeOffset now) => _lockedUntil is { } until && now < until;

    /// <summary>Whole seconds left in the lockout at <paramref name="now"/>, rounded up (so it never shows 0 while locked); 0 when not locked.</summary>
    public int RemainingSeconds(DateTimeOffset now) =>
        _lockedUntil is { } until && now < until ? (int)Math.Ceiling((until - now).TotalSeconds) : 0;

    /// <summary>Attempts left before a lockout at <paramref name="now"/>; the full allowance once a finished lockout is behind us.</summary>
    public int AttemptsRemaining(DateTimeOffset now) =>
        HasExpired(now) ? PinRules.MaxWrongAttempts : Math.Max(0, PinRules.MaxWrongAttempts - _wrongAttempts);

    /// <summary>
    /// Records a wrong PIN. Returns true when this attempt locked the dialog. While locked it records nothing and returns false.
    /// </summary>
    public bool RegisterWrong(DateTimeOffset now)
    {
        if (HasExpired(now))
        {
            Reset();
        }

        if (IsLocked(now))
        {
            return false;
        }

        _wrongAttempts++;
        if (_wrongAttempts < PinRules.MaxWrongAttempts)
        {
            return false;
        }

        _lockedUntil = now + PinRules.LockoutDuration;
        return true;
    }

    /// <summary>Records the right PIN: the count and any finished lockout are cleared.</summary>
    public void RegisterCorrect() => Reset();

    private bool HasExpired(DateTimeOffset now) => _lockedUntil is { } until && now >= until;

    private void Reset()
    {
        _wrongAttempts = 0;
        _lockedUntil = null;
    }
}
