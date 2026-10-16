using FotoHavn.Core.Hardening;

namespace FotoHavn.Host.Windows.Hardening;

/// <summary>
/// One FOTOHAVN per Data folder (M10). The first launch owns the named mutex of its Data folder and listens on the folder's activation
/// event; a second launch on the same folder signals that event and exits. The names come from <see cref="SingleInstancePolicy"/>, so
/// launches on different Data folders (the end-to-end pool) never meet. Both objects live in the user's session (<c>Local\</c>).
/// </summary>
/// <remarks>
/// A mutex belongs to the thread that took it: <see cref="Acquire"/> and <see cref="Dispose"/> belong on the UI thread (OnStartup and
/// OnExit). A process that dies releases it, and a later launch that finds it abandoned counts as the owner. Another process never
/// owns the mutex, but a second guard on the same thread would (Windows mutexes are recursive), so only one guard per process is made.
/// </remarks>
internal sealed class SingleInstanceGuard : IDisposable
{
    private static readonly TimeSpan SignalRetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _activation;
    private readonly bool _owned;
    private RegisteredWaitHandle? _registration;
    private bool _disposed;

    private SingleInstanceGuard(SingleInstanceNames names, Mutex mutex, EventWaitHandle? activation, bool owned)
    {
        Names = names;
        _mutex = mutex;
        _activation = activation;
        _owned = owned;
    }

    public SingleInstanceNames Names { get; }

    public SingleInstanceDecision Decision => SingleInstancePolicy.Decide(_owned);

    /// <summary>Takes the Data folder's mutex if nobody holds it. The first launch also creates the activation event, so a second launch can find it at once.</summary>
    public static SingleInstanceGuard Acquire(SingleInstanceNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var mutex = new Mutex(initiallyOwned: true, names.MutexName, out var createdNew);
        var owned = createdNew || TryTakeExisting(mutex);
        var activation = owned ? new EventWaitHandle(false, EventResetMode.AutoReset, names.ActivationEventName) : null;
        return new SingleInstanceGuard(names, mutex, activation, owned);
    }

    /// <summary>
    /// Tells the running instance on <paramref name="names"/>' Data folder to bring its window forward. The event may not exist yet if the
    /// first launch is still starting, so it retries until <paramref name="patience"/> has passed. Returns whether it signalled.
    /// </summary>
    public static bool SignalExisting(SingleInstanceNames names, TimeSpan patience)
    {
        ArgumentNullException.ThrowIfNull(names);
        var deadline = DateTime.UtcNow + patience;
        while (true)
        {
            if (EventWaitHandle.TryOpenExisting(names.ActivationEventName, out var existing))
            {
                using (existing)
                {
                    return existing.Set();
                }
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(SignalRetryDelay);
        }
    }

    /// <summary>Calls <paramref name="onActivated"/> on a pool thread each time a second launch signals. Only the owner listens.</summary>
    public void ListenForActivation(Action onActivated)
    {
        ArgumentNullException.ThrowIfNull(onActivated);
        if (_activation is null || _registration is not null || _disposed)
        {
            return;
        }

        _registration = ThreadPool.RegisterWaitForSingleObject(_activation, (_, _) => onActivated(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registration?.Unregister(null);
        _activation?.Dispose();
        if (_owned)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Released from another thread than the one that took it: the handle closes below and the process exit frees it.
            }
        }

        _mutex.Dispose();
    }

    private static bool TryTakeExisting(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }
}
