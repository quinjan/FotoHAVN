using FotoHavn.Core.Hardening;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core;

// Runtime hardening (M10; contract m10-contract.md). Owner: M10 wave B1. Wave A defined the seams and left the bodies that need a
// decision with the rest of the brain as stubs that keep today's behaviour:
//   - The wake lock and the "an Event is held" flag follow the Event: EventHeld is called at Start Event step 7 and EventReleased on
//     every path that lets the Event go (End Event, the guest PIN exit, a failed start's rollback, and Dispose). Done in wave A.
//   - RequestClose is the host's way in for a close request that is not a UI command. The stub shuts down at once; B1 replaces it with
//     CloseRequestRules.Decide (ask, end then close, or close now) and the close confirmation below.
//   - ConfirmCloseApp and CancelCloseApp answer the "End the running Event and close FOTOHAVN?" dialog. The stubs are not available;
//     B1 fills them.
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
    /// called, so the host can wait on it, with a limit, from the session-ending handler. Wave A stub: it shuts down at once.
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

        var completion = new TaskCompletionSource<CloseRequestOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (Volatile.Read(ref _disposed) != 0)
        {
            completion.SetResult(CloseRequestOutcome.CloseNow);
            return completion.Task;
        }

        Post(() =>
        {
            _dependencies.AppLifetime.Shutdown();
            completion.SetResult(CloseRequestOutcome.CloseNow);
        });
        return completion.Task;
    }

    /// <summary><c>ConfirmCloseApp</c>: end the running Event cleanly, then close the app. Wave A stub: not available.</summary>
    private static CommandResultMessage ConfirmCloseApp(string id) => NotAvailable(id);

    /// <summary><c>CancelCloseApp</c>: close the dialog; the Event keeps running. Wave A stub: not available.</summary>
    private static CommandResultMessage CancelCloseApp(string id) => NotAvailable(id);

    private WakeLockDiagnostics WakeLockDiagnosticsNow() => new(_dependencies.WakeLock.IsHeld, _dependencies.WakeLock.Reason);

    private InstanceDiagnostics InstanceDiagnosticsNow() => new(_dependencies.SingleInstance?.MutexName);
}
