using System.Collections.Concurrent;
using FotoHavn.Core;

namespace FotoHavn.Host.Headless.Interim;

// TEMPORARY (M2 wave C): replace with FotoHavn.Core.DedicatedThreadBrainExecutor, which the Core agent delivers in
// wave B, and delete this file. It exists only so the headless host composes and runs before that merge.

/// <summary>Runs posted work one item at a time, in order, on one background thread.</summary>
internal sealed class InterimBrainExecutor : IBrainExecutor, IDisposable
{
    private readonly BlockingCollection<Action> _work = [];
    private readonly Action<Exception> _onError;
    private readonly Thread _thread;

    public InterimBrainExecutor(Action<Exception> onError)
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
            _work.Add(work);
        }
        catch (InvalidOperationException)
        {
            // Disposed: late work (a socket closing during shutdown) is dropped.
        }
    }

    public void Dispose()
    {
        _work.CompleteAdding();
        if (!IsCurrentThread)
        {
            _thread.Join(TimeSpan.FromSeconds(5));
        }
    }

    private void Run()
    {
        foreach (var work in _work.GetConsumingEnumerable())
        {
            try
            {
                work();
            }
#pragma warning disable CA1031 // A failing work item is a brain bug; the executor reports it and keeps running.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                _onError(exception);
            }
        }
    }
}
