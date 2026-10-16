namespace FotoHavn.Core.Fakes;

/// <summary>One call a <see cref="FakeWakeLock"/> received.</summary>
/// <param name="Acquired">True for <c>Acquire</c>, false for <c>Release</c>.</param>
/// <param name="Reason">The reason given to <c>Acquire</c>; null for <c>Release</c>.</param>
/// <param name="WasHeld">Whether the lock was held when the call arrived (a repeated Acquire, or a Release of nothing, is harmless but recorded).</param>
public sealed record WakeLockCall(bool Acquired, string? Reason, bool WasHeld);

/// <summary>
/// A wake lock for tests (the "fake OS seam"): every call is recorded in order, so a test can assert the lock is held exactly
/// around an Event and released once on every end path. Behaves like the real one: the first reason stays while held. Thread-safe.
/// </summary>
public sealed class FakeWakeLock : IWakeLock
{
    private readonly Lock _gate = new();
    private readonly List<WakeLockCall> _calls = [];
    private string? _reason;

    public bool IsHeld
    {
        get
        {
            lock (_gate)
            {
                return _reason is not null;
            }
        }
    }

    public string? Reason
    {
        get
        {
            lock (_gate)
            {
                return _reason;
            }
        }
    }

    /// <summary>Every call so far, oldest first.</summary>
    public IReadOnlyList<WakeLockCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>How many <c>Acquire</c> calls arrived while the lock was not held (the times it really became held).</summary>
    public int Acquisitions => Calls.Count(call => call.Acquired && !call.WasHeld);

    /// <summary>How many <c>Release</c> calls arrived while the lock was held (the times it really let go).</summary>
    public int Releases => Calls.Count(call => !call.Acquired && call.WasHeld);

    public void Acquire(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_gate)
        {
            _calls.Add(new WakeLockCall(true, reason, _reason is not null));
            _reason ??= reason;
        }
    }

    public void Release()
    {
        lock (_gate)
        {
            _calls.Add(new WakeLockCall(false, null, _reason is not null));
            _reason = null;
        }
    }
}
