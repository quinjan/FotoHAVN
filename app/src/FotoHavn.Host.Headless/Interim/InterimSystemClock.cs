using FotoHavn.Core;

namespace FotoHavn.Host.Headless.Interim;

// TEMPORARY (M2 wave C): replace with FotoHavn.Core.SystemClock, which the Core agent delivers in wave B, and delete
// this file.

/// <summary>Wall-clock time and one-shot timers from <see cref="TimeProvider.System"/>.</summary>
internal sealed class InterimSystemClock : IClock
{
    public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow();

    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return TimeProvider.System.CreateTimer(_ => callback(), null, delay, Timeout.InfiniteTimeSpan);
    }
}
