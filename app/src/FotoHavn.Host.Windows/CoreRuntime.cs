using System.Collections.Concurrent;
using FotoHavn.Core;

namespace FotoHavn.Host.Windows;

// Stand-ins until the wave-B merge: FotoHavn.Core delivers DedicatedThreadBrainExecutor and SystemClock (contract §7.1).
// At the merge, replace the two factory bodies with `new DedicatedThreadBrainExecutor(onError)` and `new SystemClock()`
// and delete the two classes below.
internal static class CoreRuntime
{
    public static HostBrainExecutor CreateExecutor(Action<Exception> onError) => new(onError);

    public static IClock CreateClock() => new HostSystemClock();
}

/// <summary>A FIFO executor on one background thread; a throwing work item is reported and the loop keeps running.</summary>
internal sealed class HostBrainExecutor : IBrainExecutor, IDisposable
{
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Action<Exception> _onError;
    private readonly Thread _thread;

    public HostBrainExecutor(Action<Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(onError);
        _onError = onError;
        _thread = new Thread(Run) { IsBackground = true, Name = "FOTOHAVN brain" };
        _thread.Start();
    }

    public bool IsCurrentThread => Thread.CurrentThread == _thread;

    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        try
        {
            _queue.Add(work);
        }
        catch (InvalidOperationException)
        {
            // Disposed: the app is shutting down.
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        if (!IsCurrentThread && _thread.Join(TimeSpan.FromSeconds(2)))
        {
            _queue.Dispose();
        }
    }

    private void Run()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
        {
            try
            {
                work();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                _onError(exception);
            }
        }
    }
}

/// <summary>Wall-clock time and one-shot timers.</summary>
internal sealed class HostSystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return new Timer(_ => callback(), null, delay < TimeSpan.Zero ? TimeSpan.Zero : delay, Timeout.InfiniteTimeSpan);
    }
}
