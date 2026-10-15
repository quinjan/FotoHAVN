using FotoHavn.Core.Displays;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Events.Active;

/// <summary>
/// The Active Event: the one Event that is running (plan section 8, Start Event). Brain state, touched only on the executor.
/// Wave A defined the facts every owner reads; M6 wave B1b adds what the running Event needs (the exit-hold and PIN dialog
/// state, the lockout) here or beside it in this folder.
/// </summary>
internal sealed class ActiveEventState(EventRecord eventRecord, DateTimeOffset startedAt, DisplayInfo guestDisplay, bool singleScreen)
{
    /// <summary>The Event as it was saved when it started. The running Event can't be edited, so it never changes.</summary>
    public EventRecord Event => eventRecord;

    /// <summary>When it started, on the brain's clock.</summary>
    public DateTimeOffset StartedAt => startedAt;

    /// <summary>The display the Guest Screen opened on.</summary>
    public DisplayInfo GuestDisplay => guestDisplay;

    /// <summary>Whether the Guest Screen covers the Operator Screen (D38).</summary>
    public bool SingleScreen => singleScreen;

    /// <summary>Completed Guest Cycles of this Event (the number the list and the panel show); M8 keeps it current.</summary>
    public int CompletedGuestCycles { get; set; }

    /// <summary>The guest step the panel names; M6 is always <see cref="RunningEventStep.Start"/>.</summary>
    public RunningEventStep Step { get; set; } = RunningEventStep.Start;

    /// <summary>The Operator Assistance alert, or null; M8 sets it.</summary>
    public RunningEventAssistance? Assistance { get; set; }

    /// <summary>When the exit hold began on the brain's clock; null while no hold is in progress.</summary>
    public DateTimeOffset? ExitHoldBegan { get; set; }

    /// <summary>Whether the Guest Screen shows the PIN dialog.</summary>
    public bool PinDialogOpen { get; set; }

    /// <summary>Whether the last PIN attempt in this dialog was wrong (the dialog shows its error until the lockout ends).</summary>
    public bool LastPinWrong { get; set; }

    /// <summary>Whether a PIN check is running off the executor (one at a time).</summary>
    public bool PinCheckInFlight { get; set; }

    /// <summary>The wrong-PIN lockout. It outlives the dialog: closing the dialog doesn't end a lockout.</summary>
    public PinLockout Lockout { get; } = new();

    /// <summary>The once-a-second tick that keeps the locked countdown current; running only while the dialog is open and locked.</summary>
    public IDisposable? LockoutTick { get; set; }

    /// <summary>The timer that reopens the camera while it isn't available, so Start unavailable recovers by itself.</summary>
    public IDisposable? CameraRecovery { get; set; }

    /// <summary>Whether storage stopped being usable while the Event runs (M8 and the tests report it; Start unavailable shows it).</summary>
    public bool StorageFailed { get; set; }

    /// <summary>Stops every timer this Event owns. The Event is ending or its start failed.</summary>
    public void StopTimers()
    {
        LockoutTick?.Dispose();
        LockoutTick = null;
        CameraRecovery?.Dispose();
        CameraRecovery = null;
    }
}

/// <summary>
/// How a Start Event attempt ended (plan section 8, Start Event steps 1 to 9): <see cref="Failure"/> is null when the Event is
/// running, otherwise the step that stopped it. Nothing is left half-started on a failure: the camera and wake lock are released
/// and no Guest Screen is open.
/// </summary>
/// <param name="Failure">The failed step, or null on success.</param>
/// <param name="CameraReason">Why the camera wasn't eligible; set only with <see cref="StartFailureCode.CameraNotEligible"/>.</param>
internal sealed record StartEventResult(StartFailureCode? Failure, CameraFailureKind? CameraReason)
{
    /// <summary>The Event started.</summary>
    public static StartEventResult Started { get; } = new(null, null);

    /// <summary>The operator ended the Event while it was still starting (the Guest Screen's handshake was pending): nothing runs and nothing failed.</summary>
    public static StartEventResult Ended { get; } = new(null, null) { WasEnded = true };

    /// <summary>True for <see cref="Ended"/>: the start never finished because the operator ended it, which is neither a success nor a failure to report.</summary>
    public bool WasEnded { get; init; }

    public bool Succeeded => Failure is null && !WasEnded;

    public static StartEventResult Failed(StartFailureCode code, CameraFailureKind? cameraReason = null) => new(code, cameraReason);
}
