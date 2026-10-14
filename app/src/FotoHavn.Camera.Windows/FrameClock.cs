using System.Diagnostics;

namespace FotoHavn.Camera.Windows;

/// <summary>
/// The session's monotonic clock. <see cref="Now"/> and every frame <see cref="Stamp"/> come from the same time base, so
/// <c>Now - LatestFrame.Timestamp</c> is the frame age.
/// </summary>
internal sealed class FrameClock
{
    /// <summary>A system-relative time further behind the arrival than this is not on our time base: stamp by arrival.</summary>
    internal static readonly TimeSpan MaximumPlausibleLatency = TimeSpan.FromSeconds(5);

    private readonly Func<TimeSpan> _now;
    private TimeSpan _last = TimeSpan.MinValue;
    private long _cameraTimeStamps;
    private long _arrivalStamps;

    public FrameClock()
        : this(StopwatchNow)
    {
    }

    public FrameClock(Func<TimeSpan> now)
    {
        _now = now ?? throw new ArgumentNullException(nameof(now));
    }

    public TimeSpan Now => _now();

    /// <summary>Frames stamped from the camera's system-relative time (diagnostic counters, written by the frame thread).</summary>
    public long CameraTimeStamps => Interlocked.Read(ref _cameraTimeStamps);

    /// <summary>Frames stamped by their arrival because the camera's time was absent or not on our time base.</summary>
    public long ArrivalStamps => Interlocked.Read(ref _arrivalStamps);

    /// <summary>
    /// Stamps a frame. The camera's system-relative time (the QueryPerformanceCounter time base) is used when it is a plausible
    /// capture time (not after the arrival, not older than <see cref="MaximumPlausibleLatency"/>); otherwise the arrival time.
    /// Stamps never go backwards. Call from the single frame thread.
    /// </summary>
    public TimeSpan Stamp(TimeSpan? systemRelativeTime)
    {
        var arrival = _now();
        var stamp = arrival;
        var fromCamera = false;
        if (systemRelativeTime is { } captured)
        {
            var latency = arrival - captured;
            if (latency >= TimeSpan.Zero && latency <= MaximumPlausibleLatency)
            {
                stamp = captured;
                fromCamera = true;
            }
        }

        Interlocked.Increment(ref fromCamera ? ref _cameraTimeStamps : ref _arrivalStamps);

        if (stamp < _last)
        {
            stamp = _last;
        }

        _last = stamp;
        return stamp;
    }

    private static TimeSpan StopwatchNow()
    {
        var ticks = Stopwatch.GetTimestamp();
        var seconds = ticks / Stopwatch.Frequency;
        var remainder = ticks % Stopwatch.Frequency;
        return TimeSpan.FromTicks((seconds * TimeSpan.TicksPerSecond) + (remainder * TimeSpan.TicksPerSecond / Stopwatch.Frequency));
    }
}
