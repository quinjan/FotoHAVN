namespace FotoHavn.Core;

/// <summary>
/// The brain's executor for both hosts: one background thread runs posted work items one at a time, in the order posted.
/// A work item that throws is reported to the <c>onError</c> callback (the host logs it) and the thread keeps running.
/// </summary>
public sealed class DedicatedThreadBrainExecutor : IBrainExecutor, IDisposable
{
    private readonly object _gate = new();
    private readonly Queue<Action> _queue = new();
    private readonly Action<Exception> _onError;
    private readonly Thread _thread;
    private bool _stopping;

    /// <param name="onError">Receives every exception a work item throws, on the executor thread. It must not throw.</param>
    /// <param name="threadName">The thread's name, for debuggers and logs.</param>
    public DedicatedThreadBrainExecutor(Action<Exception> onError, string threadName = "FOTOHAVN brain")
    {
        ArgumentNullException.ThrowIfNull(onError);
        _onError = onError;
        _thread = new Thread(Run) { IsBackground = true, Name = threadName };
        _thread.Start();
    }

    public bool IsCurrentThread => Thread.CurrentThread == _thread;

    /// <summary>Queues <paramref name="work"/>. After <see cref="Dispose"/> it is dropped.</summary>
    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            _queue.Enqueue(work);
            Monitor.Pulse(_gate);
        }
    }

    /// <summary>
    /// Stops taking work, runs what is already queued, and waits for the thread to end (unless called from a work item,
    /// which can't wait for itself).
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            _stopping = true;
            Monitor.PulseAll(_gate);
        }

        if (!IsCurrentThread)
        {
            _thread.Join();
        }
    }

    private void Run()
    {
        while (TryTake(out var work))
        {
            try
            {
                work();
            }
#pragma warning disable CA1031 // The executor outlives a faulty work item by design; the host logs the bug.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                _onError(exception);
            }
        }
    }

    private bool TryTake(out Action work)
    {
        lock (_gate)
        {
            while (_queue.Count == 0 && !_stopping)
            {
                Monitor.Wait(_gate);
            }

            return _queue.TryDequeue(out work!);
        }
    }
}

/// <summary>The real clock: <see cref="TimeProvider.System"/>. Callbacks run on a thread-pool thread.</summary>
public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();

    public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow();

    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        return TimeProvider.System.CreateTimer(_ => callback(), null, delay, Timeout.InfiniteTimeSpan);
    }
}
