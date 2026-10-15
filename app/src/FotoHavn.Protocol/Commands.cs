namespace FotoHavn.Protocol;

// Command payload records, one per command in plan section 9. A command's wire name is its record name without
// "Payload". Commands without fields have an empty record and send `{}`. Range and state checks are the brain's job;
// these records only fix the shape.

/// <summary>Marks a command payload record. Every one is listed in <see cref="CommandRegistry"/>.</summary>
#pragma warning disable CA1040 // Marker interface: the registry and the parser need a common payload type.
public interface ICommandPayload;
#pragma warning restore CA1040

/// <summary>An Event Setup text field changed by <see cref="ChangeEventFieldPayload"/>.</summary>
public enum EventField
{
    Name,

    /// <summary>The Event's local date, sent as <c>yyyy-MM-dd</c>.</summary>
    Date,
    Venue,

    /// <summary>The Customer or host.</summary>
    Customer,
    Notes,
}

/// <summary>A Phase Timer set by <see cref="SetPhaseTimerPayload"/> (plan section 7, Phase timers).</summary>
public enum PhaseTimer
{
    Template,
    PhotographsCapture,
    PhotographsReview,
    Filters,
    Keepsake,
    GuestGallery,
}

/// <summary>The answer to the leave-with-changes confirmation in Event setup.</summary>
public enum LeaveChoice
{
    Save,
    DiscardChanges,
    KeepEditing,
}

/// <summary>A Guest Cycle step a guest can go to with the step progress.</summary>
public enum GuestStep
{
    Template,
    Photographs,
    Filters,
}

// Event list (operator)
public sealed record OpenNewEventPayload : ICommandPayload;

public sealed record EditEventPayload(string EventId) : ICommandPayload;

public sealed record StartEventPayload(string EventId) : ICommandPayload;

public sealed record DeleteEventPayload(string EventId) : ICommandPayload;

public sealed record ConfirmDeleteEventPayload : ICommandPayload;

public sealed record CancelDeleteEventPayload : ICommandPayload;

public sealed record OpenEventFolderPayload(string EventId) : ICommandPayload;

/// <summary>Operator-only: closes the operator notice (<c>overlays.notice</c>).</summary>
public sealed record DismissNoticePayload : ICommandPayload;

// Event setup (operator)
public sealed record ChangeEventFieldPayload(EventField Field, string Value) : ICommandPayload;

public sealed record SelectCameraPayload(string DeviceId) : ICommandPayload;

public sealed record SelectGuestDisplayPayload(string DisplayId) : ICommandPayload;

public sealed record IdentifyDisplaysPayload : ICommandPayload;

public sealed record SetMirrorPayload(bool Enabled) : ICommandPayload;

public sealed record SetCountdownPayload(int Seconds) : ICommandPayload;

public sealed record SetOfferedTemplatesPayload(IReadOnlyList<string> TemplateIds) : ICommandPayload;

public sealed record SetDefaultTemplatePayload(string TemplateId) : ICommandPayload;

public sealed record SetOfferedLooksPayload(IReadOnlyList<string> LookIds) : ICommandPayload;

public sealed record SetDefaultLookPayload(string LookId) : ICommandPayload;

/// <summary><paramref name="Seconds"/> is 0 to turn a timer off, where the phase allows it.</summary>
public sealed record SetPhaseTimerPayload(PhaseTimer Phase, int Seconds) : ICommandPayload;

public sealed record SetGuestGalleryPayload(bool Enabled) : ICommandPayload;

public sealed record SetPinPayload(string Pin, string Confirmation) : ICommandPayload;

public sealed record SaveEventPayload : ICommandPayload;

public sealed record SaveAndStartEventPayload : ICommandPayload;

public sealed record LeaveEventSetupPayload : ICommandPayload;

public sealed record ConfirmLeavePayload(LeaveChoice Choice) : ICommandPayload;

// Event Gallery (operator)
public sealed record OpenGalleryPayload(string EventId) : ICommandPayload;

public sealed record LoadMoreGuestCyclesPayload(string Cursor) : ICommandPayload;

public sealed record OpenGuestCyclePayload(string GuestCycleId) : ICommandPayload;

public sealed record CloseGuestCyclePayload : ICommandPayload;

public sealed record OpenGuestCycleFolderPayload(string GuestCycleId) : ICommandPayload;

public sealed record DeleteGuestCyclePayload(string GuestCycleId) : ICommandPayload;

public sealed record ConfirmDeleteGuestCyclePayload : ICommandPayload;

public sealed record CancelDeleteGuestCyclePayload : ICommandPayload;

public sealed record CloseGalleryPayload : ICommandPayload;

// Running Event (operator)
public sealed record EndEventPayload : ICommandPayload;

public sealed record ConfirmEndEventPayload : ICommandPayload;

public sealed record KeepRunningPayload : ICommandPayload;

/// <summary>Allowed from both roles: the running-Event panel and the Guest Screen's Operator Assistance.</summary>
public sealed record RetryAssistancePayload : ICommandPayload;

// Storage (operator)

/// <summary>Operator-only: try opening storage again from the storage error surface (plan section 10).</summary>
public sealed record RetryStoragePayload : ICommandPayload;

// Guest Cycle (guest)
public sealed record BeginGuestCyclePayload : ICommandPayload;

public sealed record SelectTemplatePayload(string TemplateId) : ICommandPayload;

/// <summary>
/// Goes to a Guest Cycle step: it is both <c>CONTINUE</c> (to the next step) and <c>BACK</c> (to an earlier one, from the step progress). The brain refuses
/// Filters until the arrangement is complete, and the step the guest is already on. Keepsake has no command: the brain moves there itself after the Photo Strip is committed.
/// </summary>
public sealed record GoToStepPayload(GuestStep Step) : ICommandPayload;

public sealed record ShowCameraTabPayload : ICommandPayload;

public sealed record ShowArrangeTabPayload : ICommandPayload;

public sealed record TakePhotographsPayload : ICommandPayload;

public sealed record StopCapturePayload : ICommandPayload;

public sealed record SelectPositionPayload(int Index) : ICommandPayload;

public sealed record BeginRetakePayload(int Index) : ICommandPayload;

public sealed record AcceptRetakePayload : ICommandPayload;

public sealed record TryAgainPayload : ICommandPayload;

public sealed record KeepOriginalPayload : ICommandPayload;

public sealed record BeginMovePayload(int Index) : ICommandPayload;

public sealed record MoveToPayload(int Index) : ICommandPayload;

public sealed record CancelMovePayload : ICommandPayload;

/// <summary><paramref name="Revision"/> is the arrangement revision the drag started from; a stale one is ignored.</summary>
public sealed record DragPlacePayload(int SourceIndex, string CaptureId, long Revision, int TargetIndex) : ICommandPayload;

public sealed record SelectLookPayload(string LookId) : ICommandPayload;

public sealed record FinishStripPayload : ICommandPayload;

/// <summary><c>TRY AGAIN</c> after a composition failed on Filters: the brain composes again what failed (a Look's preview or the final Photo Strip). Nothing is lost.</summary>
public sealed record RetryCompositionPayload : ICommandPayload;

public sealed record MoreTimePayload : ICommandPayload;

public sealed record StartOverPayload : ICommandPayload;

public sealed record ConfirmStartOverPayload : ICommandPayload;

public sealed record KeepGoingPayload : ICommandPayload;

// Guest Screen (guest)
public sealed record BeginExitHoldPayload : ICommandPayload;

public sealed record CompleteExitHoldPayload : ICommandPayload;

public sealed record SubmitPinPayload(string Digits) : ICommandPayload;

public sealed record CancelExitPayload : ICommandPayload;

public sealed record OpenGuestGalleryPayload : ICommandPayload;

public sealed record LoadMoreGuestStripsPayload(string Cursor) : ICommandPayload;

public sealed record OpenGuestStripPayload(string GuestCycleId) : ICommandPayload;

public sealed record CloseGuestStripPayload : ICommandPayload;

public sealed record BackToStartPayload : ICommandPayload;
