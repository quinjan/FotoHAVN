using FotoHavn.Protocol;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core;

// The clock in the brain (plan section 14, `test.setClockScale`; M6 contract section 8). Owner: M6 wave B1b. Wave A implemented the
// hook: it scales the clock the host passed in BrainDependencies.Clock when that clock is a ScaledClock (the E2E hosts wrap
// their real clock in one); the camera runs on its own clock and stays real-time.
public sealed partial class Brain
{
    /// <summary>The clock's speed factor for diagnostics: 1 unless the host passed a scalable clock and the hook changed it.</summary>
    private double ClockScaleNow => _dependencies.Clock is IScalableClock scalable ? scalable.Factor : 1;

    private HookOutcome SetClockScale(double factor)
    {
        if (double.IsNaN(factor) || factor < ScaledClock.MinimumFactor || factor > ScaledClock.MaximumFactor)
        {
            return HookOutcome.Fail(TestHookError.InvalidArgs);
        }

        if (_dependencies.Clock is not IScalableClock scalable)
        {
            return HookOutcome.Fail(TestHookError.NotSupported);
        }

        scalable.SetFactor(factor);
        return HookOutcome.Ok(EmptyHookResult.Instance);
    }
}
