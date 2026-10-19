using System.Collections.Immutable;

namespace FotoHavn.Core.GuestCycles;

/// <summary>
/// The Guest Cycle's arrangement rules, a pure port of the website's <c>sessionReducer</c> (plan section 7, Retake and arrangement
/// rules). <see cref="Apply"/> never throws on a bad action: a stale, out-of-range, or wrong-state action is ignored and returns
/// the same model instance. The brain runs it on the executor and persists what the outcome says (discarded Captures).
/// </summary>
public static class GuestCycleRules
{
    /// <summary>Applies <paramref name="action"/> to <paramref name="model"/>. A completed cycle (the Keepsake step) ignores everything.</summary>
    public static GuestCycleOutcome Apply(GuestCycleModel model, GuestCycleAction action)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(action);
        if (model.Step == GuestCycleStep.Keepsake)
        {
            return Ignored(model);
        }

        return action switch
        {
            SelectTemplateAction a => SelectTemplate(model, a),
            BeginSequenceAction a => BeginSequence(model, a),
            BeginRetakeAction a => BeginRetake(model, a),
            CaptureCommittedAction a => CaptureCommitted(model, a),
            FinishSequenceAction a => FinishSequence(model, a),
            CancelAction => Cancel(model),
            AcceptRetakeAction => AcceptRetake(model),
            TryAgainAction => TryAgain(model),
            KeepOriginalAction => KeepOriginal(model),
            SelectWindowAction a => SelectWindow(model, a),
            MoveStartAction a => MoveStart(model, a),
            MoveToAction a => MoveTo(model, a),
            DragPlaceAction a => DragPlace(model, a),
            MoveCancelAction => MoveCancel(model),
            SelectLookAction a => SelectLook(model, a),
            GoToStepAction a => GoToStep(model, a),
            ShowTabAction a => ShowTab(model, a),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Not a Guest Cycle action."),
        };
    }

    private static GuestCycleOutcome Ignored(GuestCycleModel model) => new(model, false, []);

    private static GuestCycleOutcome Changed(GuestCycleModel model, params string[] discarded) => new(model, true, discarded);

    private static bool InRange(GuestCycleModel model, int index) => index >= 0 && index < model.WindowCount;

    private static ImmutableDictionary<string, KeptCapture> Kept(GuestCycleModel model) => model.KeptCaptures.ToImmutableDictionary();

    private static GuestCycleOutcome SelectTemplate(GuestCycleModel model, SelectTemplateAction action)
    {
        // The Template is chosen before any photograph is taken, and guests never come back to it: the new Template's windows are all empty.
        if (model.Step != GuestCycleStep.Template || action.TemplateId == model.TemplateId || action.WindowCount < 1)
        {
            return Ignored(model);
        }

        return Changed(model with
        {
            TemplateId = action.TemplateId,
            WindowCount = action.WindowCount,
            Windows = new string?[action.WindowCount],
            Revision = model.Revision + 1,
        });
    }

    private static GuestCycleOutcome BeginSequence(GuestCycleModel model, BeginSequenceAction action)
    {
        if (model.Step != GuestCycleStep.Photographs || model.Operation is not null || model.NextEmptyWindow is null)
        {
            return Ignored(model);
        }

        return Changed(model with
        {
            Move = null,
            Tab = PhotographsTab.Camera,
            Operation = new GuestOperation(action.OperationId, OperationMode.Sequence, null, null),
        });
    }

    private static GuestCycleOutcome BeginRetake(GuestCycleModel model, BeginRetakeAction action)
    {
        if (model.Step != GuestCycleStep.Photographs || model.Operation is not null || !InRange(model, action.Target) || model.Windows[action.Target] is null)
        {
            return Ignored(model);
        }

        return Changed(model with
        {
            Move = null,
            Selected = action.Target,
            Tab = PhotographsTab.Camera,
            Operation = new GuestOperation(action.OperationId, OperationMode.Retake, action.Target, null),
        });
    }

    private static GuestCycleOutcome CaptureCommitted(GuestCycleModel model, CaptureCommittedAction action)
    {
        if (model.Operation is not { } operation
            || operation.Id != action.OperationId
            || !InRange(model, action.WindowIndex)
            || model.KeptCaptures.ContainsKey(action.Capture.Id))
        {
            return Ignored(model);
        }

        var kept = Kept(model).Add(action.Capture.Id, action.Capture);
        if (operation.Mode == OperationMode.Retake)
        {
            // The candidate waits beside the original until the guest accepts; a second candidate is ignored.
            return operation.Target != action.WindowIndex || operation.Candidate is not null
                ? Ignored(model)
                : Changed(model with { KeptCaptures = kept, Operation = operation with { Candidate = action.Capture.Id } });
        }

        if (model.Windows[action.WindowIndex] is not null)
        {
            return Ignored(model);
        }

        string?[] windows = [.. model.Windows];
        windows[action.WindowIndex] = action.Capture.Id;
        return Changed(model with { Windows = windows, KeptCaptures = kept, Selected = action.WindowIndex, Revision = model.Revision + 1 });
    }

    private static GuestCycleOutcome FinishSequence(GuestCycleModel model, FinishSequenceAction action)
    {
        if (model.Operation is not { Mode: OperationMode.Sequence } operation || operation.Id != action.OperationId)
        {
            return Ignored(model);
        }

        var finished = model with { Operation = null };
        return Changed(finished with { Tab = finished.IsComplete ? PhotographsTab.Arrange : PhotographsTab.Camera });
    }

    private static GuestCycleOutcome Cancel(GuestCycleModel model) =>
        model.Operation is null && model.Move is null ? Ignored(model) : CancelOperationAndMove(model);

    /// <summary>Drops the operation and the Move. A Retake's pending candidate leaves the cycle, so the caller discards it; a cancelled Retake returns to Arrange.</summary>
    private static GuestCycleOutcome CancelOperationAndMove(GuestCycleModel model)
    {
        var candidate = model.Operation?.Candidate;
        var kept = candidate is null ? Kept(model) : Kept(model).Remove(candidate);
        var tab = model.Operation is { Mode: OperationMode.Retake } ? PhotographsTab.Arrange : model.Tab;
        return Changed(model with { Operation = null, Move = null, KeptCaptures = kept, Tab = tab }, [.. new[] { candidate }.OfType<string>()]);
    }

    private static GuestCycleOutcome AcceptRetake(GuestCycleModel model)
    {
        if (model.Operation is not { Candidate: { } candidate, Target: { } target })
        {
            return Ignored(model);
        }

        string?[] windows = [.. model.Windows];
        var original = windows[target];
        windows[target] = candidate;
        var kept = original is null ? Kept(model) : Kept(model).Remove(original);
        return Changed(
            model with
            {
                Windows = windows,
                KeptCaptures = kept,
                Selected = target,
                Operation = null,
                Tab = PhotographsTab.Arrange,
                Revision = model.Revision + 1,
            },
            [.. new[] { original }.OfType<string>()]);
    }

    private static GuestCycleOutcome TryAgain(GuestCycleModel model)
    {
        if (model.Operation is not { Candidate: { } candidate } operation)
        {
            return Ignored(model);
        }

        return Changed(model with { KeptCaptures = Kept(model).Remove(candidate), Operation = operation with { Candidate = null } }, candidate);
    }

    private static GuestCycleOutcome KeepOriginal(GuestCycleModel model) =>
        model.Operation is { Mode: OperationMode.Retake } ? CancelOperationAndMove(model) : Ignored(model);

    private static GuestCycleOutcome SelectWindow(GuestCycleModel model, SelectWindowAction action) =>
        model.Operation is not null || !InRange(model, action.Index) ? Ignored(model) : Changed(model with { Selected = action.Index });

    private static GuestCycleOutcome MoveStart(GuestCycleModel model, MoveStartAction action) =>
        model.Operation is not null || !InRange(model, action.Index) || model.Windows[action.Index] is not { } captureId
            ? Ignored(model)
            : Changed(model with { Move = new GuestMove(action.Index, captureId, model.Revision) });

    private static GuestCycleOutcome MoveTo(GuestCycleModel model, MoveToAction action) =>
        model.Move is { } move ? Place(model, move, action.Index) : Ignored(model);

    private static GuestCycleOutcome DragPlace(GuestCycleModel model, DragPlaceAction action)
    {
        // A pointer gesture owns a snapshot of the arrangement it began on, so a late release after a Retake or another placement is refused.
        if (model.Step != GuestCycleStep.Photographs
            || model.Operation is not null
            || model.Move is not null
            || model.Revision != action.Revision
            || !InRange(model, action.SourceIndex)
            || model.Windows[action.SourceIndex] != action.CaptureId)
        {
            return Ignored(model);
        }

        return Place(model, new GuestMove(action.SourceIndex, action.CaptureId, action.Revision), action.TargetIndex);
    }

    private static GuestCycleOutcome Place(GuestCycleModel model, GuestMove move, int index)
    {
        if (model.Operation is not null || !InRange(model, index))
        {
            return Ignored(model);
        }

        var source = move.SourceIndex;
        if (source == index)
        {
            // Dropping a photograph where it is changes nothing but the selection, so the arrangement revision stays.
            return Changed(model with { Selected = index, Move = null });
        }

        string?[] windows = [.. model.Windows];
        (windows[source], windows[index]) = (windows[index], windows[source]);
        return Changed(model with { Windows = windows, Selected = index, Move = null, Revision = model.Revision + 1 });
    }

    private static GuestCycleOutcome MoveCancel(GuestCycleModel model) =>
        model.Move is null ? Ignored(model) : Changed(model with { Move = null });

    private static GuestCycleOutcome SelectLook(GuestCycleModel model, SelectLookAction action) =>
        model.Operation is not null || action.LookId == model.LookId ? Ignored(model) : Changed(model with { LookId = action.LookId });

    private static GuestCycleOutcome GoToStep(GuestCycleModel model, GoToStepAction action)
    {
        // Guests only go on. Filters and the Keepsake need a complete arrangement with no operation and no Move, so leaving a step never
        // cancels anything, and the Photographs step opens on the Camera tab because no photograph is taken before it.
        if (action.Step <= model.Step
            || (action.Step is GuestCycleStep.Filters or GuestCycleStep.Keepsake && !model.CanContinue)
            || (action.Step == GuestCycleStep.Keepsake && model.Step != GuestCycleStep.Filters))
        {
            return Ignored(model);
        }

        return Changed(model with { Step = action.Step });
    }

    private static GuestCycleOutcome ShowTab(GuestCycleModel model, ShowTabAction action)
    {
        if (model.Step != GuestCycleStep.Photographs
            || model.Operation is not null
            || action.Tab == model.Tab
            || (action.Tab == PhotographsTab.Arrange && model.Windows.All(id => id is null)))
        {
            return Ignored(model);
        }

        return Changed(model with { Tab = action.Tab });
    }
}
