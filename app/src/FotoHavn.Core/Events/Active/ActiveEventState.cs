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

    public bool Succeeded => Failure is null;

    public static StartEventResult Failed(StartFailureCode code, CameraFailureKind? cameraReason = null) => new(code, cameraReason);
}
