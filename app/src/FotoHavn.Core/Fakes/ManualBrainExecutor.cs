namespace FotoHavn.Core.Fakes;

/// <summary>
/// A deterministic executor for tests: <see cref="Post"/> only queues; <see cref="RunPending"/> runs the queue on the
/// caller's thread, including work posted while it runs, until it is empty.
/// </summary>
public sealed class ManualBrainExecutor : IBrainExecutor
{
    private readonly object _gate = new();
    private readonly Queue<Action> _queue = new();
    private readonly Action<Exception>? _onError;
    private int _runningThreadId;

    /// <param name="onError">
    /// Receives exceptions thrown by work items, after which the run goes on (as the real executor does). When null, the
    /// exception propagates out of <see cref="RunPending"/> so a test sees the brain bug.
    /// </param>
    public ManualBrainExecutor(Action<Exception>? onError = null)
    {
        _onError = onError;
    }

    public bool IsCurrentThread => Volatile.Read(ref _runningThreadId) == Environment.CurrentManagedThreadId;

    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        lock (_gate)
        {
            _queue.Enqueue(work);
        }
    }

    /// <summary>Runs every queued work item in order, then returns how many ran.</summary>
    public int RunPending()
    {
        var ran = 0;
        Volatile.Write(ref _runningThreadId, Environment.CurrentManagedThreadId);
        try
        {
            while (TryTake(out var work))
            {
                ran++;
                Run(work);
            }
        }
        finally
        {
            Volatile.Write(ref _runningThreadId, 0);
        }

        return ran;
    }

    private void Run(Action work)
    {
        if (_onError is null)
        {
            work();
            return;
        }

        try
        {
            work();
        }
#pragma warning disable CA1031 // Mirrors the real executor, which reports a faulty work item and keeps running.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _onError(exception);
        }
    }

    private bool TryTake(out Action work)
    {
        lock (_gate)
        {
            return _queue.TryDequeue(out work!);
        }
    }
}
