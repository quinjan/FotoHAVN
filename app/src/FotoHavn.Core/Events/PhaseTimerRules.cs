using FotoHavn.Core.Storage;
using FotoHavn.Protocol;

namespace FotoHavn.Core.Events;

/// <summary>
/// The Phase Timer table (plan section 7, Phase timers): each phase's default, range, and whether it can be off.
/// A value of 0 means off, and is allowed only where <see cref="CanBeOff"/> says so.
/// </summary>
public static class PhaseTimerRules
{
    /// <summary>The warning shows in the last 20 seconds of every timed phase except the Keepsake (which has no warning).</summary>
    public const int WarningSeconds = 20;

    private const int OffableMinimum = 20;
    private const int OffableMaximum = 300;
    private const int KeepsakeMinimum = 5;
    private const int KeepsakeMaximum = 60;

    private static readonly PhaseTimerPhase[] Phases =
    [
        PhaseTimerPhase.Template,
        PhaseTimerPhase.PhotographsCapture,
        PhaseTimerPhase.PhotographsReview,
        PhaseTimerPhase.Filters,
        PhaseTimerPhase.Keepsake,
        PhaseTimerPhase.GuestGallery,
    ];

    /// <summary>Every phase, in the order Event setup lists them.</summary>
    public static IReadOnlyList<PhaseTimerPhase> All => Phases;

    public static int Default(PhaseTimerPhase phase) => phase switch
    {
        PhaseTimerPhase.Template => 30,
        PhaseTimerPhase.PhotographsCapture => 30,
        PhaseTimerPhase.PhotographsReview => 45,
        PhaseTimerPhase.Filters => 30,
        PhaseTimerPhase.Keepsake => 12,
        PhaseTimerPhase.GuestGallery => 60,
        _ => throw NotAPhase(phase),
    };

    /// <summary>The shortest allowed time when the timer is on.</summary>
    public static int Min(PhaseTimerPhase phase) => phase switch
    {
        PhaseTimerPhase.Keepsake => KeepsakeMinimum,
        _ when Enum.IsDefined(phase) => OffableMinimum,
        _ => throw NotAPhase(phase),
    };

    /// <summary>The longest allowed time.</summary>
    public static int Max(PhaseTimerPhase phase) => phase switch
    {
        PhaseTimerPhase.Keepsake => KeepsakeMaximum,
        _ when Enum.IsDefined(phase) => OffableMaximum,
        _ => throw NotAPhase(phase),
    };

    /// <summary>Whether 0 (no timer) is allowed. The Keepsake and the guest gallery are never off.</summary>
    public static bool CanBeOff(PhaseTimerPhase phase) => phase switch
    {
        PhaseTimerPhase.Template or PhaseTimerPhase.PhotographsCapture or PhaseTimerPhase.PhotographsReview or PhaseTimerPhase.Filters => true,
        PhaseTimerPhase.Keepsake or PhaseTimerPhase.GuestGallery => false,
        _ => throw NotAPhase(phase),
    };

    /// <summary>Whether <paramref name="seconds"/> is allowed: 0 only where the phase can be off, otherwise within <see cref="Min"/> and <see cref="Max"/>.</summary>
    public static bool IsValid(PhaseTimerPhase phase, int seconds) =>
        seconds == 0 ? CanBeOff(phase) : seconds >= Min(phase) && seconds <= Max(phase);

    /// <summary>Whether the phase shows the warning with <c>MORE TIME</c>: every timed phase except the Keepsake.</summary>
    public static bool HasWarning(PhaseTimerPhase phase) => phase switch
    {
        PhaseTimerPhase.Keepsake => false,
        _ when Enum.IsDefined(phase) => true,
        _ => throw NotAPhase(phase),
    };

    /// <summary>A new Event's Phase Timers: every phase at its default.</summary>
    public static IReadOnlyList<PhaseTimerSetting> Defaults() => [.. Phases.Select(phase => new PhaseTimerSetting(phase, Default(phase)))];

    /// <summary>The wire phase for a storage phase.</summary>
    public static PhaseTimer ToProtocol(PhaseTimerPhase phase) => phase switch
    {
        PhaseTimerPhase.Template => PhaseTimer.Template,
        PhaseTimerPhase.PhotographsCapture => PhaseTimer.PhotographsCapture,
        PhaseTimerPhase.PhotographsReview => PhaseTimer.PhotographsReview,
        PhaseTimerPhase.Filters => PhaseTimer.Filters,
        PhaseTimerPhase.Keepsake => PhaseTimer.Keepsake,
        PhaseTimerPhase.GuestGallery => PhaseTimer.GuestGallery,
        _ => throw NotAPhase(phase),
    };

    /// <summary>The storage phase for a wire phase.</summary>
    public static PhaseTimerPhase FromProtocol(PhaseTimer phase) => phase switch
    {
        PhaseTimer.Template => PhaseTimerPhase.Template,
        PhaseTimer.PhotographsCapture => PhaseTimerPhase.PhotographsCapture,
        PhaseTimer.PhotographsReview => PhaseTimerPhase.PhotographsReview,
        PhaseTimer.Filters => PhaseTimerPhase.Filters,
        PhaseTimer.Keepsake => PhaseTimerPhase.Keepsake,
        PhaseTimer.GuestGallery => PhaseTimerPhase.GuestGallery,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Not a Phase Timer."),
    };

    private static ArgumentOutOfRangeException NotAPhase(PhaseTimerPhase phase) =>
        new(nameof(phase), phase, "Not a Phase Timer phase.");
}
