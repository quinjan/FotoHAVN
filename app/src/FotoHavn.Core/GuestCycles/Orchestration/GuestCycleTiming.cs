namespace FotoHavn.Core.GuestCycles.Orchestration;

/// <summary>
/// The timings the Guest Cycle's orchestration uses (plan section 7, Capture sequence and Phase timers). Constants only: the brain
/// schedules them on its own clock and executor.
/// </summary>
internal static class GuestCycleTiming
{
    /// <summary>The pause after each flash before the next countdown or the review (capture sequence step 6).</summary>
    public static readonly TimeSpan PostCapturePause = TimeSpan.FromMilliseconds(800);

    /// <summary>The countdown number, the Phase Timer warning's seconds, and Operator Assistance's busy state all push once a second.</summary>
    public static readonly TimeSpan SecondTick = TimeSpan.FromSeconds(1);
}
