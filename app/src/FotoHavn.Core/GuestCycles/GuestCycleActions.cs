namespace FotoHavn.Core.GuestCycles;

/// <summary>
/// One action on the Guest Cycle model, named after the website reducer's actions (<c>session.ts</c>). The brain turns each guest
/// command or timer into one; <see cref="GuestCycleRules.Apply"/> decides whether it counts. Only this assembly derives from it.
/// </summary>
public abstract record GuestCycleAction
{
    internal GuestCycleAction()
    {
    }
}

/// <summary>Chooses a Template (website <c>template</c>/<c>layout</c>) on the Template step, before any Capture: the windows are the new Template's, all empty.</summary>
public sealed record SelectTemplateAction(string TemplateId, int WindowCount) : GuestCycleAction;

/// <summary>Starts a capture sequence for the empty windows (website <c>begin</c> with mode sequence).</summary>
public sealed record BeginSequenceAction(long OperationId) : GuestCycleAction;

/// <summary>Starts a Retake of one occupied window (website <c>begin</c> with mode retake).</summary>
public sealed record BeginRetakeAction(long OperationId, int Target) : GuestCycleAction;

/// <summary>A Capture the store committed for <paramref name="WindowIndex"/> (website <c>photo</c>). A stale operation id or a wrong window is ignored.</summary>
public sealed record CaptureCommittedAction(long OperationId, int WindowIndex, KeptCapture Capture) : GuestCycleAction;

/// <summary>A capture sequence ended normally (website <c>finish</c>). A stale operation id is ignored.</summary>
public sealed record FinishSequenceAction(long OperationId) : GuestCycleAction;

/// <summary>Cancels the running operation and any Move (website <c>cancel</c>). A Retake's pending candidate is discarded.</summary>
public sealed record CancelAction : GuestCycleAction;

/// <summary>Accepts the Retake's candidate: it takes the window and the original is discarded (website <c>accept</c>).</summary>
public sealed record AcceptRetakeAction : GuestCycleAction;

/// <summary>Discards only the Retake's candidate so another can be taken (website <c>try-again</c>).</summary>
public sealed record TryAgainAction : GuestCycleAction;

/// <summary>Cancels the Retake and keeps the original (website <c>cancel</c> during a Retake).</summary>
public sealed record KeepOriginalAction : GuestCycleAction;

/// <summary>Selects a window (website <c>select</c>).</summary>
public sealed record SelectWindowAction(int Index) : GuestCycleAction;

/// <summary>Begins a Move from an occupied window (website <c>move-start</c> from a slot).</summary>
public sealed record MoveStartAction(int Index) : GuestCycleAction;

/// <summary>Places the Move at a window (website <c>move-to</c>); an occupied window swaps.</summary>
public sealed record MoveToAction(int Index) : GuestCycleAction;

/// <summary>A drag release (website <c>move-position</c>): ignored unless the arrangement revision and the dragged Capture still match.</summary>
public sealed record DragPlaceAction(int SourceIndex, string CaptureId, long Revision, int TargetIndex) : GuestCycleAction;

/// <summary>Cancels a Move (website <c>move-cancel</c>).</summary>
public sealed record MoveCancelAction : GuestCycleAction;

/// <summary>Chooses a Look (website <c>look</c>).</summary>
public sealed record SelectLookAction(string LookId) : GuestCycleAction;

/// <summary>Goes on to a later step (website <c>stage</c>, forward only: guests never go back). Filters and Keepsake need <see cref="GuestCycleModel.CanContinue"/>.</summary>
public sealed record GoToStepAction(GuestCycleStep Step) : GuestCycleAction;

/// <summary>Shows a Photographs tab.</summary>
public sealed record ShowTabAction(PhotographsTab Tab) : GuestCycleAction;
