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
                completion.SetResult(CloseOnExecutor(source));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
                throw;
            }
        });
        return completion.Task;
    }

    /// <summary>Acts on a close request on the executor, deciding from the state it finds there (an Event may have ended since the request was made).</summary>
    private CloseRequestOutcome CloseOnExecutor(CloseSource source)
    {
        var outcome = CloseRequestRules.Decide(IsEventActive, source);
        BrainLog.CloseRequested(_logger, source, outcome);
        if (outcome == CloseRequestOutcome.AskToEndEvent)
        {
            SetConfirmation(new ConfirmationOverlay(ConfirmationKind.CloseApp, null, null, null));
            PublishChanges();
            return outcome;
        }

        if (outcome == CloseRequestOutcome.EndEventAndClose)
        {
            EndActiveEvent();
            PublishChanges();
        }

        _dependencies.AppLifetime.Shutdown();
        return outcome;
    }

    /// <summary><c>ConfirmCloseApp</c>: end the running Event cleanly (the End Event sequence), then close the app once this command has been answered.</summary>
    private CommandResultMessage ConfirmCloseApp(string id)
    {
        if (_confirmation?.Kind != ConfirmationKind.CloseApp || !IsEventActive)
        {
            return NotAvailable(id);
        }

        EndActiveEvent();
        Post(_dependencies.AppLifetime.Shutdown);
        return CommandResultMessage.Accepted(id);
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
}
