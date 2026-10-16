using FotoHavn.Core.Hardening;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// Runtime hardening (M10; contract m10-contract.md).
//   - The wake lock and the "an Event is held" flag follow the Event: EventHeld is called at Start Event step 7 and EventReleased on
//     every path that lets the Event go (End Event, the guest PIN exit, a failed start's rollback, and Dispose).
//   - RequestClose is the host's way in for a close request that is not a UI command: CloseRequestRules.Decide picks close now, ask to
//     end the Event (the closeApp confirmation), or end the Event and close.
//   - ConfirmCloseApp and CancelCloseApp answer the "End the running Event and close FOTOHAVN?" dialog.
// The PIN, Event names, and guest data never appear in a log or a diagnostic from here (LogRedaction is the allow-list).
public sealed partial class Brain
{
    // The Event is held from Start Event step 7 to the end of the End Event sequence or the rollback of a failed start. Written on
    // the executor and by Dispose; read by any thread through IsEventActive.
    private int _eventHeld;

    /// <summary>How long closing the app waits for the store writes the Event's end queued before it tells the host to close (and the host waits again before it disposes the store).</summary>
    public static readonly TimeSpan ShutdownStoreDrainBound = TimeSpan.FromSeconds(10);

    /// <summary>The same wait when Windows is ending the session: the host gives the brain 5 s in all, and the wait must end inside it.</summary>
    public static readonly TimeSpan SessionEndStoreDrainBound = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Whether an Event is held (starting from step 7 on, or running). Safe to read from any thread: the Windows host reads it in the
    /// Guest window's closing handler, which has to answer at once, to apply <see cref="CloseRequestRules.Decide"/> to a close attempt.
    /// </summary>
    public bool IsEventActive => Volatile.Read(ref _eventHeld) != 0;

    /// <summary>Start Event step 7: the Event is held, so the system is kept awake until <see cref="EventReleased"/>.</summary>
    private void EventHeld(string eventId)
    {
        _dependencies.WakeLock.Acquire(WakeLockRules.ReasonFor(eventId));
        Volatile.Write(ref _eventHeld, 1);
    }

    /// <summary>The Event is no longer held (it ended, or its start rolled back): the system may sleep again. Harmless when nothing is held.</summary>
    private void EventReleased()
    {
        Volatile.Write(ref _eventHeld, 0);
        _dependencies.WakeLock.Release();
    }

    /// <summary>The brain is being disposed with an Event still held (the process is exiting): the wake lock must not outlive it.</summary>
    private void ReleaseHoldsOnDispose()
    {
        if (IsEventActive)
        {
            EventReleased();
        }
    }

    /// <summary>
    /// A request to close FOTOHAVN that is not a UI command: the Operator window's close, or Windows ending the session. Callable from
    /// any thread (it posts to the executor). The task completes with the outcome once the brain has acted on it: for
    /// <see cref="CloseRequestOutcome.EndEventAndClose"/> that is after the Event ended and <see cref="IAppLifetime.Shutdown"/> was
    /// called, so the host can wait on it, with a limit, from the session-ending handler.
    /// </summary>
    /// <remarks>
    /// The Guest window's close is not asked here: the host applies <see cref="CloseRequestRules.Decide"/> with <see cref="IsEventActive"/>
    /// itself, because a window's closing handler cannot wait for the executor.
    /// </remarks>
    public Task<CloseRequestOutcome> RequestClose(CloseSource source)
    {
        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Not a close source.");
        }

        // The Guest window's close is decided by the host at once; the brain never acts on it.
        if (source == CloseSource.GuestWindow)
        {
            return Task.FromResult(CloseRequestRules.Decide(IsEventActive, source));
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            return Task.FromResult(CloseRequestOutcome.CloseNow);
        }

        var completion = new TaskCompletionSource<CloseRequestOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                CloseOnExecutor(source, completion);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
                throw;
            }
        });
        return completion.Task;
    }

    /// <summary>
    /// Acts on a close request on the executor, deciding from the state it finds there (an Event may have ended since the request was made).
    /// Closing waits for the store writes the Event's end queued (<see cref="ShutdownAfterStoreWrites"/>), so the request completes after the app was told to close.
    /// </summary>
    private void CloseOnExecutor(CloseSource source, TaskCompletionSource<CloseRequestOutcome> completion)
    {
        var outcome = CloseRequestRules.Decide(IsEventActive, source);
        BrainLog.CloseRequested(_logger, source, outcome);
        if (outcome == CloseRequestOutcome.AskToEndEvent)
        {
            SetConfirmation(new ConfirmationOverlay(ConfirmationKind.CloseApp, null, null, null));
            PublishChanges();
            completion.SetResult(outcome);
            return;
        }

        if (outcome == CloseRequestOutcome.EndEventAndClose)
        {
            EndActiveEvent();
            PublishChanges();
        }

        ShutdownAfterStoreWrites(source == CloseSource.SessionEnd ? SessionEndStoreDrainBound : ShutdownStoreDrainBound, completion, outcome);
    }

    /// <summary><c>ConfirmCloseApp</c>: end the Event if one is held (the End Event sequence), then close the app once this command has been answered and the store has the Event's end.</summary>
    private CommandResultMessage ConfirmCloseApp(string id)
    {
        if (_confirmation?.Kind != ConfirmationKind.CloseApp)
        {
            return NotAvailable(id);
        }

        EndActiveEvent();
        _confirmation = null;
        Post(() => ShutdownAfterStoreWrites(ShutdownStoreDrainBound, null, CloseRequestOutcome.CloseNow));
        return CommandResultMessage.Accepted(id);
    }

    /// <summary>
    /// A completed Capture commit, Guest Cycle end, or Photo Strip commit is durable before the process goes: the store is disposed right after the app
    /// shuts down, and a write that arrives later fails against a closed connection (an <c>operator_exit</c> cycle would stay open until the next start).
    /// Waits for every write the brain started off the executor (bounded), then tells the host to close. Called on the executor; it never blocks it.
    /// </summary>
    private void ShutdownAfterStoreWrites(TimeSpan bound, TaskCompletionSource<CloseRequestOutcome>? completion, CloseRequestOutcome outcome)
    {
        if (_offExecutorWork == 0)
        {
            CloseApp(completion, outcome);
            return;
        }

        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RegisterStoreSettleWaiter(settled);
        _ = SettledWithinAsync(settled.Task, bound).ContinueWith(
            _ => Post(() => CloseApp(completion, outcome)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void CloseApp(TaskCompletionSource<CloseRequestOutcome>? completion, CloseRequestOutcome outcome)
    {
        try
        {
            _dependencies.AppLifetime.Shutdown();
        }
        catch (Exception exception)
        {
            completion?.TrySetException(exception);
            throw;
        }

        completion?.TrySetResult(outcome);
    }

    /// <summary>
    /// Completes when every store and compositor call the brain started off the executor has finished and been handled (the Event's end, its Guest Cycle's end,
    /// and the writes queued before it), or after <paramref name="bound"/> of real time, which is logged. Callable from any thread; the wait never blocks the
    /// executor. The host calls it before it disposes the brain and the store, so no write meets a closed store.
    /// </summary>
    public Task WhenStoreWritesSettledAsync(TimeSpan bound)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bound, TimeSpan.Zero);
        if (Volatile.Read(ref _disposed) != 0)
        {
            return Task.CompletedTask;
        }

        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() => RegisterStoreSettleWaiter(settled));
        return SettledWithinAsync(settled.Task, bound);
    }

    // Executor only: the waiters are released when the off-executor work count reaches zero.
    private readonly List<TaskCompletionSource> _storeSettleWaiters = [];

    private void RegisterStoreSettleWaiter(TaskCompletionSource settled)
    {
        if (_offExecutorWork == 0)
        {
            settled.TrySetResult();
        }
        else
        {
            _storeSettleWaiters.Add(settled);
        }
    }

    private void ReleaseStoreSettleWaiters()
    {
        foreach (var waiter in _storeSettleWaiters)
        {
            waiter.TrySetResult();
        }

        _storeSettleWaiters.Clear();
    }

    private async Task SettledWithinAsync(Task settled, TimeSpan bound)
    {
        try
        {
            await settled.WaitAsync(bound, TimeProvider.System).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            BrainLog.StoreWritesNotSettled(_dependencies.Logger, (long)bound.TotalMilliseconds, OffExecutorWork);
        }
    }

    /// <summary><c>CancelCloseApp</c>: close the dialog; the Event keeps running.</summary>
    private CommandResultMessage CancelCloseApp(string id)
    {
        if (_confirmation?.Kind != ConfirmationKind.CloseApp)
        {
            return NotAvailable(id);
        }

        _confirmation = null;
        return CommandResultMessage.Accepted(id);
    }

    private WakeLockDiagnostics WakeLockDiagnosticsNow() => new(_dependencies.WakeLock.IsHeld, _dependencies.WakeLock.Reason);

    /// <summary>The Event and Guest Cycle that log lines belong to right now (<see cref="EventScopedLogger"/>); null where there is none.</summary>
    internal (string? EventId, string? GuestCycleId) LogScopeIds() => (HeldEventId, CurrentRun?.Id);

    private InstanceDiagnostics InstanceDiagnosticsNow() => new(_dependencies.SingleInstance?.MutexName);
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "A close request ({Reason}) was decided: {Outcome}.")]
    public static partial void CloseRequested(ILogger logger, CloseSource reason, CloseRequestOutcome outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Closing without waiting further: {Pending} store or compositor calls were still pending after {BoundMs} ms.")]
    public static partial void StoreWritesNotSettled(ILogger logger, long boundMs, int pending);
}
