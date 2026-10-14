namespace FotoHavn.Core;

/// <summary>A clock whose speed can change at run time (E2E builds only, through <c>test.setClockScale</c>).</summary>
public interface IScalableClock : IClock
{
    /// <summary>How many times faster than the wrapped clock this one runs; 1 is real time.</summary>
    double Factor { get; }

    /// <summary>
    /// Runs the clock <paramref name="factor"/> times faster from now on. Time doesn't jump: it advances at the new speed from the
    /// instant of the change, and every callback already scheduled keeps its scaled due time (it is rescheduled).
    /// </summary>
    void SetFactor(double factor);
}

/// <summary>
/// Wraps an <see cref="IClock"/> and runs it <see cref="Factor"/> times faster, so E2E scenarios with 30-second Phase Timers and a
/// 30-second PIN lockout finish quickly. Both <see cref="UtcNow"/> and scheduled delays are scaled; the camera (frames, freshness
/// against its own clock) stays real-time. The brain's code never knows: it asks this clock for time and timers as usual. The
/// field build never creates one.
/// </summary>
public sealed class ScaledClock : IScalableClock
{
    public const double MinimumFactor = 1;
    public const double MaximumFactor = 100;

    private readonly IClock _inner;
    private readonly object _gate = new();
    private readonly List<ScaledTimer> _timers = [];
    private double _factor = 1;
    private DateTimeOffset _realAnchor;
    private DateTimeOffset _scaledAnchor;

    public ScaledClock(IClock inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _realAnchor = inner.UtcNow;
        _scaledAnchor = _realAnchor;
    }

    public double Factor
    {
        get
        {
            lock (_gate)
            {
                return _factor;
            }
        }
    }

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_gate)
            {
                return ScaledNow();
            }
        }
    }

    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        lock (_gate)
        {
            var timer = new ScaledTimer(this, ScaledNow() + delay, callback);
            _timers.Add(timer);
            timer.Arm();
            return timer;
        }
    }

    public void SetFactor(double factor)
    {
        if (double.IsNaN(factor) || factor < MinimumFactor || factor > MaximumFactor)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, $"The factor is {MinimumFactor} to {MaximumFactor}.");
        }

        lock (_gate)
        {
            _scaledAnchor = ScaledNow();
            _realAnchor = _inner.UtcNow;
            _factor = factor;
            foreach (var timer in _timers.ToArray())
            {
                timer.Arm();
            }
        }
    }

    // Callers hold _gate.
    private DateTimeOffset ScaledNow() => _scaledAnchor + ((_inner.UtcNow - _realAnchor) * _factor);

    private sealed class ScaledTimer(ScaledClock clock, DateTimeOffset due, Action callback) : IDisposable
    {
        private IDisposable? _real;
        private int _generation;
        private bool _finished;

        /// <summary>(Re)schedules the wrapped timer for what is left at the current speed. The caller holds the clock's gate.</summary>
        public void Arm()
        {
            _real?.Dispose();
            var remaining = due - clock.ScaledNow();
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            var generation = ++_generation;
            _real = clock._inner.Schedule(TimeSpan.FromTicks((long)(remaining.Ticks / clock._factor)), () => Fire(generation));
        }

        public void Dispose()
        {
            lock (clock._gate)
            {
                _finished = true;
                _generation++;
                _real?.Dispose();
                _real = null;
                clock._timers.Remove(this);
            }
        }

        private void Fire(int generation)
        {
            lock (clock._gate)
            {
                if (_finished || generation != _generation)
                {
                    return;
                }

                _finished = true;
                clock._timers.Remove(this);
            }

            callback();
        }
    }
}
