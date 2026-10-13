namespace FotoHavn.Core.Fakes;

/// <summary>A manual clock for tests: time moves only with <see cref="Advance"/>, which fires due callbacks in due order.</summary>
public sealed class FakeClock : IClock
{
    private readonly object _gate = new();
    private readonly List<Timer> _timers = [];
    private DateTimeOffset _now;
    private long _sequence;

    public FakeClock()
        : this(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero))
    {
    }

    public FakeClock(DateTimeOffset start)
    {
        _now = start;
    }

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_gate)
            {
                return _now;
            }
        }
    }

    /// <summary>The number of scheduled callbacks that haven't fired or been cancelled.</summary>
    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count;
            }
        }
    }

    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        lock (_gate)
        {
            var timer = new Timer(this, _now + delay, _sequence++, callback);
            _timers.Add(timer);
            return timer;
        }
    }

    /// <summary>Moves time forward by <paramref name="delta"/>, firing each callback that falls due, at its due time, on this thread.</summary>
    public void Advance(TimeSpan delta)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delta, TimeSpan.Zero);
        DateTimeOffset target;
        lock (_gate)
        {
            target = _now + delta;
        }

        while (TryTakeDue(target, out var timer))
        {
            timer.Callback();
        }

        lock (_gate)
        {
            _now = target;
        }
    }

    private bool TryTakeDue(DateTimeOffset target, out Timer timer)
    {
        lock (_gate)
        {
            timer = _timers
                .Where(candidate => candidate.Due <= target)
                .OrderBy(candidate => candidate.Due)
                .ThenBy(candidate => candidate.Sequence)
                .FirstOrDefault()!;
            if (timer is null)
            {
                return false;
            }

            _timers.Remove(timer);
            _now = timer.Due;
            return true;
        }
    }

    private void Cancel(Timer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class Timer(FakeClock clock, DateTimeOffset due, long sequence, Action callback) : IDisposable
    {
        public DateTimeOffset Due => due;

        public long Sequence => sequence;

        public Action Callback => callback;

        public void Dispose() => clock.Cancel(this);
    }
}
