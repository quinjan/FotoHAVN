using FotoHavn.Core;

namespace FotoHavn.Host.Windows;

/// <summary>Which clock the brain runs on: E2E builds scale the system clock (<c>test.setClockScale</c>); field builds never create a <see cref="ScaledClock"/>.</summary>
internal static class ClockComposition
{
    /// <summary>The brain's clock for this build.</summary>
    public static IClock ForBuild(IClock system) => Compose(system, HostBuild.IsE2E);

    internal static IClock Compose(IClock system, bool scaled)
    {
        ArgumentNullException.ThrowIfNull(system);
        return scaled ? new ScaledClock(system) : system;
    }
}
