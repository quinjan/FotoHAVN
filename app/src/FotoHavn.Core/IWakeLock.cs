namespace FotoHavn.Core;

/// <summary>
/// Keeps Windows awake while an Event runs (plan section 8, Start Event step 7; M10). The brain decides and calls it only on its
/// executor, but the host may read <see cref="IsHeld"/> and the brain releases it from <c>Dispose</c>, so an implementation must
/// be thread-safe. It is held exactly while an Event is held (from Start Event step 7 until the End Event sequence, or the rollback
/// of a failed start, completes) and never otherwise; the Windows host holds a display-required and system-required power request.
/// </summary>
public interface IWakeLock
{
    /// <summary>Whether the lock is held. Acquiring twice, or releasing without holding, is harmless.</summary>
    bool IsHeld { get; }

    /// <summary>Why the lock is held (shown to the OS, to <c>powercfg /requests</c>, and in <c>test.getDiagnostics</c>), or null while it is not held.</summary>
    string? Reason { get; }

    /// <summary>Starts keeping the system awake. While already held, the first reason stays.</summary>
    /// <param name="reason">Why: an Event ID, never an Event name (see <see cref="Hardening.WakeLockRules.ReasonFor"/>).</param>
    void Acquire(string reason);

    /// <summary>Stops keeping the system awake.</summary>
    void Release();
}

/// <summary>
/// The wake lock for hosts that keep nothing awake (the headless host, tests): it only tracks whether the brain asked, so
/// <see cref="IsHeld"/> (and the <c>test.getDiagnostics</c> <c>wakeLock</c> field) still show the Start and End Event sequence. Thread-safe.
/// </summary>
public sealed class NoOpWakeLock : IWakeLock
{
    private readonly Lock _gate = new();
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

    public void Acquire(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_gate)
        {
            _reason ??= reason;
        }
    }

    public void Release()
    {
        lock (_gate)
        {
            _reason = null;
        }
    }
}
