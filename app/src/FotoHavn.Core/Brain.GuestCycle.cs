using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Events;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.GuestCycles;
using FotoHavn.Core.GuestCycles.Orchestration;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// The Guest Cycle's commands, presentation, and diagnostics (plan sections 7 and 9; M8 contract). The rules live in the pure model
// (GuestCycles/GuestCycleRules.cs) and the timer engine (GuestCycles/PhaseTimerEngine.cs); this partial and its siblings only orchestrate them:
//   Brain.GuestCycle.cs                                    commands, lifecycle, presentation, diagnostics   (this file)
//   Brain.GuestCycleCapture.cs                             countdown, flash, capture, commit, sequence, Retake
//   Brain.GuestCycleTimers.cs                              the Phase Timer's scheduling, pause and resume, warning, and expiry outcomes
//   GuestCycles/Orchestration/Brain.GuestCycleFilters.cs   Look thumbnails, strip previews, the final Photo Strip, the Keepsake
//   Brain.Assistance.cs                                    Operator Assistance: entry, RetryAssistance, recovery
// Every handler runs on the executor, answers at once, and starts store, camera, and compositor work off it; the outcome is posted back
// carrying what it needs to be judged stale (the run, the operation id, the composition request).
public sealed partial class Brain
{
    private GuestCycleRun? _cycle;
    private bool _cycleStarting;
    private Action? _cycleCameraObserver;

    /// <summary>The Event record of the running Event, or null while none runs. The Guest Cycle reads the Event's Templates, Looks, countdown, mirror, and Phase Timers from it.</summary>
    internal Storage.EventRecord? ActiveEventRecord => _activeEvent?.Event;

    /// <summary>The open camera session, or null while the camera isn't open. Valid only on the executor; a Capture takes its frame through it (never off the executor beyond the awaited call).</summary>
    internal Camera.ICameraSession? CameraSlotSession => _cameraSession;

    /// <summary>The running Guest Cycle of the running Event, or null (none, ended, or left over from another Event).</summary>
    private GuestCycleRun? CurrentRun => _cycle is { Ended: false } run && ReferenceEquals(run.Active, _activeEvent) ? run : null;

    /// <summary>The cycle when the guest can act on it now: no Operator Assistance, no Start over confirmation, and no Photo Strip being made.</summary>
    private GuestCycleRun? ActionableRun => CurrentRun is { StartOverOpen: false, Finishing: false } run && _assistance is null ? run : null;

    private static CommandResultMessage Accepted(string id) => CommandResultMessage.Accepted(id);

    private static CommandResultMessage Invalid(string id) => CommandResultMessage.Rejected(id, RejectReason.InvalidPayload);

    private static Exception? FailureOf(Task task) => task.Exception?.GetBaseException();

    // Off-executor work (store, compositor): start it, and post the outcome back to the executor, where it is published.

    /// <summary>The store and compositor calls started and not yet handled back on the executor; tests settle on it.</summary>
    internal int OffExecutorWork => Volatile.Read(ref _offExecutorWork);

    private int _offExecutorWork;

    private void Off<T>(Func<Task<T>> work, Action<Task<T>> complete)
    {
        Interlocked.Increment(ref _offExecutorWork);
        Task.Run(work).ContinueWith(
            completed =>
            {
                _ = completed.Exception;
                Post(() =>
                {
                    complete(completed);
                    PublishChanges();
                    Interlocked.Decrement(ref _offExecutorWork);
                });
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void OffVoid(Func<Task> work, Action<Task> complete) =>
        Off(
            async () =>
            {
                await work().ConfigureAwait(false);
                return true;
            },
            completed => complete(completed));

    private TemplateDefinition TemplateOf(string templateId) => _dependencies.Catalog.FindTemplate(templateId)!;

    // Starting a cycle (plan section 7, Start → Template)

    /// <summary><c>BeginGuestCycle</c> (<c>STEP INSIDE</c>): the store creates the cycle off the executor; a storage failure there is Operator Assistance.</summary>
    private CommandResultMessage BeginGuestCycle(string id)
    {
        if (_activeEvent is not { } active
            || _start is not null
            || _cycle is not null
            || _cycleStarting
            || _assistance is not null
            || active.PinDialogOpen
            || _cameraState != CameraState.Ready
            || !_storageResolved
            || _storageError is not null
            || active.StorageFailed)
        {
            return NotAvailable(id);
        }

        _cycleStarting = true;
        Off(() => _dependencies.Store.StartGuestCycleAsync(active.Event.Id, CancellationToken.None), started => CompleteBeginGuestCycle(active, started));
        return Accepted(id);
    }

    private void CompleteBeginGuestCycle(ActiveEventState active, Task<GuestCycleRecord> started)
    {
        _cycleStarting = false;
        if (!ReferenceEquals(_activeEvent, active))
        {
            // The Event ended while the store created the cycle: close it so it isn't left open.
            if (started.IsCompletedSuccessfully)
            {
                EndInStore(started.Result.Id, GuestCycleEndReason.OperatorExit);
            }

            return;
        }

        if (!started.IsCompletedSuccessfully)
        {
            BrainLog.GuestCycleStartFailed(_logger, FailureOf(started));
            EnterAssistance(AssistanceReason.Storage);
            return;
        }

        var record = active.Event;
        var templateSkipped = record.OfferedTemplateIds.Count == 1;
        var template = TemplateOf(record.DefaultTemplateId);
        var run = new GuestCycleRun(active, started.Result.Id, GuestCycleModel.Create(template.Id, template.WindowCount, record.DefaultLookId), templateSkipped, record.OfferedLookIds.Count == 1);
        if (templateSkipped)
        {
            run.Model = GuestCycleRules.Apply(run.Model, new GoToStepAction(GuestCycleStep.Photographs)).Model;
        }

        _cycle = run;
        _cycleCameraObserver = () => Post(() =>
        {
            OnCameraChangedDuringCycle();
            PublishChanges();
        });
        CameraStateChanged += _cycleCameraObserver;
        BrainLog.GuestCycleStarted(_logger, run.Id);
        AfterChange(run);
    }

    // Template

    private CommandResultMessage SelectTemplate(string id, SelectTemplatePayload payload)
    {
        if (ActionableRun is not { Model.Step: GuestCycleStep.Template } run)
        {
            return NotAvailable(id);
        }

        if (!run.Active.Event.OfferedTemplateIds.Contains(payload.TemplateId, StringComparer.Ordinal))
        {
            return Invalid(id);
        }

        var outcome = ApplyModel(run, new SelectTemplateAction(payload.TemplateId, TemplateOf(payload.TemplateId).WindowCount));
        AfterChange(run);

        // Choosing the Template already selected is a harmless tap.
        return outcome.Applied || run.Model.TemplateId == payload.TemplateId ? Accepted(id) : NotAvailable(id);
    }

    // Steps

    private CommandResultMessage GoToStep(string id, GoToStepPayload payload)
    {
        if (ActionableRun is not { } run)
        {
            return NotAvailable(id);
        }

        var target = payload.Step switch
        {
            GuestStep.Template => GuestCycleStep.Template,
            GuestStep.Photographs => GuestCycleStep.Photographs,
            _ => GuestCycleStep.Filters,
        };
        var model = run.Model;
        if (target == model.Step || model.Step == GuestCycleStep.Keepsake || (target == GuestCycleStep.Template && run.TemplateSkipped))
        {
            return NotAvailable(id);
        }

        // Going on or back during a countdown cancels it; once a frame is taken the Capture finishes first, so the guest tries again.
        if (run.Sequence is { } sequence && !StopIfIdle(run, sequence))
        {
            return NotAvailable(id);
        }

        if (target == GuestCycleStep.Filters)
        {
            if (!run.Model.CanContinue)
            {
                return NotAvailable(id);
            }

            ContinueToFilters(run);
        }
        else
        {
            if (!ApplyModel(run, new GoToStepAction(target)).Applied)
            {
                return NotAvailable(id);
            }

            LeaveFilters(run);
        }

        AfterChange(run);
        return Accepted(id);
    }

    /// <summary>Leaves Photographs for Filters. With one Look offered Filters is skipped: the Photo Strip is made with that Look at once.</summary>
    private void ContinueToFilters(GuestCycleRun run)
    {
        if (run.FiltersSkipped)
        {
            BeginFinalCompose(run);
        }
        else if (ApplyModel(run, new GoToStepAction(GuestCycleStep.Filters)).Applied)
        {
            EnterFilters(run);
        }
    }

    /// <summary>Whatever Filters was composing no longer matters once the guest leaves it.</summary>
    private static void LeaveFilters(GuestCycleRun run)
    {
        run.CompositionRequest++;
        run.Composing = false;
        run.CompositionFailed = false;
    }

    private CommandResultMessage ShowCameraTab(string id)
    {
        var accepted = ShowTab(id, PhotographsTab.Camera, out var run);
        if (run is not null)
        {
            // The Camera tab appearing starts the capture timer afresh.
            run.Timer = run.Timer.Restart(_dependencies.Clock.UtcNow);
            AfterChange(run);
        }

        return accepted;
    }

    private CommandResultMessage ShowArrangeTab(string id)
    {
        var accepted = ShowTab(id, PhotographsTab.Arrange, out var run);
        if (run is not null)
        {
            AfterChange(run);
        }

        return accepted;
    }

    /// <summary>Applies the tab change; <paramref name="changed"/> is the run when it took effect.</summary>
    private CommandResultMessage ShowTab(string id, PhotographsTab tab, out GuestCycleRun? changed)
    {
        changed = null;
        if (ActionableRun is not { Model.Step: GuestCycleStep.Photographs } run || !ApplyModel(run, new ShowTabAction(tab)).Applied)
        {
            return NotAvailable(id);
        }

        changed = run;
        return Accepted(id);
    }

    // Arrangement: each command is one model action; a refusal is notAvailable (a stale drag is the one quiet no-op).

    private CommandResultMessage SelectPosition(string id, SelectPositionPayload payload) =>
        Arrange(id, run => InRange(run, payload.Index), new SelectWindowAction(payload.Index));

    private CommandResultMessage BeginMove(string id, BeginMovePayload payload) =>
        Arrange(id, run => InRange(run, payload.Index), new MoveStartAction(payload.Index));

    private CommandResultMessage MoveTo(string id, MoveToPayload payload) =>
        Arrange(id, run => InRange(run, payload.Index), new MoveToAction(payload.Index));

    private CommandResultMessage CancelMove(string id) => Arrange(id, _ => true, new MoveCancelAction());

    private CommandResultMessage AcceptRetake(string id) => Arrange(id, _ => true, new AcceptRetakeAction());

    private CommandResultMessage DragPlace(string id, DragPlacePayload payload) =>
        Arrange(id, run => InRange(run, payload.SourceIndex) && InRange(run, payload.TargetIndex), new DragPlaceAction(payload.SourceIndex, payload.CaptureId, payload.Revision, payload.TargetIndex), quietNoOp: true);

    private CommandResultMessage KeepOriginal(string id)
    {
        if (ActionableRun is { Sequence: { Mode: OperationMode.Retake } sequence } run)
        {
            // A Retake still counting down: keeping the original cancels it.
            if (!StopIfIdle(run, sequence))
            {
                return NotAvailable(id);
            }

            AfterChange(run);
            return Accepted(id);
        }

        return Arrange(id, _ => true, new KeepOriginalAction());
    }

    private static bool InRange(GuestCycleRun run, int index) => index >= 0 && index < run.Model.WindowCount;

    private CommandResultMessage Arrange(string id, Func<GuestCycleRun, bool> payloadIsValid, GuestCycleAction action, bool quietNoOp = false)
    {
        if (ActionableRun is not { Model.Step: GuestCycleStep.Photographs } run)
        {
            return NotAvailable(id);
        }

        if (!payloadIsValid(run))
        {
            return Invalid(id);
        }

        if (!ApplyModel(run, action).Applied)
        {
            return quietNoOp ? Accepted(id) : NotAvailable(id);
        }

        AfterChange(run);
        return Accepted(id);
    }

    /// <summary>Applies <paramref name="action"/> to the model; what it drops from the cycle is discarded in the store and forgotten here.</summary>
    private GuestCycleOutcome ApplyModel(GuestCycleRun run, GuestCycleAction action)
    {
        var outcome = GuestCycleRules.Apply(run.Model, action);
        if (outcome.Applied)
        {
            run.Model = outcome.Model;
            foreach (var captureId in outcome.DiscardedCaptureIds)
            {
                run.Jpegs.Remove(captureId);
                DiscardInStore(run, captureId);
            }
        }

        return outcome;
    }

    /// <summary>
    /// Queues a store write behind every earlier one of the cycle and returns its outcome. The store deletes a discarded Capture's file only when the cycle
    /// ends or the Photo Strip commits, and refuses a write after either, so the writes must reach it in the order the brain decided them. Called on the
    /// executor; the write itself runs off it.
    /// </summary>
    private static Task<T> InOrder<T>(GuestCycleRun run, Func<Task<T>> write)
    {
        var queued = run.StoreWrites.ContinueWith(_ => write(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
        run.StoreWrites = queued;
        return queued;
    }

    private void DiscardInStore(GuestCycleRun run, string captureId)
    {
        var write = InOrder(run, async () =>
        {
            await _dependencies.Store.SetCaptureStateAsync(captureId, CaptureState.Discarded, CancellationToken.None).ConfigureAwait(false);
            return true;
        });
        OffVoid(
            () => write,
            completed =>
            {
                if (!completed.IsCompletedSuccessfully)
                {
                    BrainLog.GuestCycleStoreFailed(_logger, FailureOf(completed), "discard a Capture");
                    if (!run.Ended)
                    {
                        EnterAssistance(AssistanceReason.Storage);
                    }
                }
            });
    }

    /// <summary>Discards a Retake's Capture whose commit is still in the store's queue when the cycle ends: the discard is queued right behind that commit, so it lands before the end.</summary>
    private void DiscardCommitted(GuestCycleRun run, Task<CaptureRecord> commit)
    {
        var write = InOrder(run, async () =>
        {
            if (commit.IsCompletedSuccessfully)
            {
                await _dependencies.Store.SetCaptureStateAsync(commit.Result.Id, CaptureState.Discarded, CancellationToken.None).ConfigureAwait(false);
            }

            return true;
        });
        OffVoid(() => write, completed => LogEndFailure(completed, "discard a Capture"));
    }

    /// <summary>Ends the cycle in the store. Whatever the cycle still holds as a Retake candidate is discarded first, so no candidate stays kept.</summary>
    private void EndInStore(GuestCycleRun run, GuestCycleEndReason reason)
    {
        if (run.Model.Operation?.Candidate is { } candidate)
        {
            DiscardInStore(run, candidate);
        }

        if (run.RetakeCommit is { } late)
        {
            run.RetakeCommit = null;
            DiscardCommitted(run, late);
        }

        var write = InOrder(run, async () =>
        {
            await _dependencies.Store.EndGuestCycleAsync(run.Id, reason, CancellationToken.None).ConfigureAwait(false);
            return true;
        });
        OffVoid(() => write, completed =>
        {
            LogEndFailure(completed, "end the Guest Cycle");
            if (completed.IsCompletedSuccessfully)
            {
                OnGuestCycleEnded(run.Id, reason);
            }
        });
    }

    /// <summary>Ends a cycle the brain never held (the Event ended while the store created it).</summary>
    private void EndInStore(string cycleId, GuestCycleEndReason reason) =>
        OffVoid(() => _dependencies.Store.EndGuestCycleAsync(cycleId, reason, CancellationToken.None), completed => LogEndFailure(completed, "end the Guest Cycle"));

    private void LogEndFailure(Task completed, string what)
    {
        if (!completed.IsCompletedSuccessfully)
        {
            BrainLog.GuestCycleStoreFailed(_logger, FailureOf(completed), what);
        }
    }

    // Start over (plan section 7, D21)

    private CommandResultMessage StartOver(string id)
    {
        if (CurrentRun is not { StartOverOpen: false, Finishing: false } run || _assistance is not null || run.Model.Step == GuestCycleStep.Keepsake)
        {
            return NotAvailable(id);
        }

        run.StartOverOpen = true;
        return Accepted(id);
    }

    private CommandResultMessage KeepGoing(string id)
    {
        if (CurrentRun is not { StartOverOpen: true } run)
        {
            return NotAvailable(id);
        }

        run.StartOverOpen = false;
        return Accepted(id);
    }

    private CommandResultMessage ConfirmStartOver(string id)
    {
        if (CurrentRun is not { StartOverOpen: true } run)
        {
            return NotAvailable(id);
        }

        ReturnToStart(run, GuestCycleEndReason.StartedOver);
        return Accepted(id);
    }

    /// <summary>Ends the cycle in the brain (and in the store with <paramref name="endReason"/>, unless it is already completed) and shows Start.</summary>
    private void ReturnToStart(GuestCycleRun run, GuestCycleEndReason? endReason)
    {
        if (endReason is { } reason)
        {
            EndInStore(run, reason);
        }

        TearDownCycle(run);
        SyncPreviewTarget();
    }

    /// <summary>Forgets the cycle: no timer, no sequence, no previews, no photographs in memory, and every late result is dropped.</summary>
    private void TearDownCycle(GuestCycleRun run)
    {
        run.Ended = true;
        run.Timer = PhaseTimerEngine.Stopped;
        run.TimerWake?.Dispose();
        run.TimerWake = null;
        run.Sequence?.Release();
        run.Sequence = null;
        foreach (var key in run.OwnedKeys)
        {
            _dependencies.Previews.Remove(key);
        }

        run.OwnedKeys.Clear();
        run.Jpegs.Clear();
        CameraStateChanged -= _cycleCameraObserver;
        _cycleCameraObserver = null;
        _cycle = null;
    }

    /// <summary>The Event is ending (END EVENT, or the guest's PIN exit): an unfinished cycle ends as <c>operator_exit</c> with its Captures kept; a completed one stays completed. Called by <c>EndActiveEvent</c>.</summary>
    private void EndGuestCycleForEvent()
    {
        ClearAssistance();
        if (CurrentRun is { } run)
        {
            // A Photo Strip commit in flight completes the cycle itself.
            ReturnToStart(run, run.Completed || run.CommitInFlight ? null : GuestCycleEndReason.OperatorExit);
        }
    }

    // Presentation

    /// <summary>
    /// The Guest Screen's presentation while a Guest Cycle runs, or while Operator Assistance shows with or without one; null when the guest
    /// is on Start, Start unavailable, or the guest gallery, so <see cref="RenderGuestShell"/> renders those. <paramref name="overlays"/> holds
    /// the exit hold and PIN dialog M6 owns; the cycle adds <c>phaseTimerWarning</c> and <c>startOverConfirmation</c> with
    /// <c>overlays with { ... }</c>. Called on the executor.
    /// </summary>
    private GuestPresentation? RenderGuestCycle(ActiveEventState active, GuestOverlays overlays)
    {
        var run = _cycle is { Ended: false } current && ReferenceEquals(current.Active, active) ? current : null;
        if (_assistance is { } assistance)
        {
            return new GuestPresentation(
                GuestSurface.OperatorAssistance,
                null,
                null,
                overlays,
                null,
                run is null ? null : CycleView(run),
                null,
                null,
                null,
                null,
                new OperatorAssistanceView(assistance.Reason, assistance.Retrying),
                null,
                null);
        }

        if (run is null)
        {
            return null;
        }

        var shown = overlays with { PhaseTimerWarning = WarningOverlay(run), StartOverConfirmation = run.StartOverOpen ? new StartOverConfirmationOverlay() : null };
        var cycle = CycleView(run);
        return run.Model.Step switch
        {
            GuestCycleStep.Template => new GuestPresentation(GuestSurface.Template, null, null, shown, null, cycle, RenderTemplate(run), null, null, null, null, null, null),
            GuestCycleStep.Photographs => new GuestPresentation(GuestSurface.Photographs, null, null, shown, null, cycle, null, RenderPhotographs(run), null, null, null, null, null),
            GuestCycleStep.Filters => new GuestPresentation(GuestSurface.Filters, null, null, shown, null, cycle, null, null, RenderFilters(run), null, null, null, null),
            _ => new GuestPresentation(GuestSurface.Keepsake, null, null, shown, null, cycle, null, null, null, run.Keepsake, null, null, null),
        };
    }

    private static GuestCycleView CycleView(GuestCycleRun run) => new(
        run.Id,
        GuestCyclePlan.ToName(run.Model.Step),
        GuestCyclePlan.Steps(run.Model.Step, run.TemplateSkipped, run.FiltersSkipped),
        run.Model.Step != GuestCycleStep.Keepsake);

    private PhaseTimerWarningOverlay? WarningOverlay(GuestCycleRun run)
    {
        var now = _dependencies.Clock.UtcNow;
        return run.Timer.IsRunning && run.Timer.IsWarning(now)
            ? new PhaseTimerWarningOverlay(PhaseTimerRules.ToProtocol(run.Timer.Phase!.Value), GuestCyclePlan.WholeSecondsLeft(run.Timer.Remaining(now)!.Value))
            : null;
    }

    private TemplateView RenderTemplate(GuestCycleRun run) => new(
        [.. run.Active.Event.OfferedTemplateIds.Select(templateId =>
        {
            var template = TemplateOf(templateId);
            return new TemplateOptionView(template.Id, template.Name, template.Description, template.Width, template.Height, template.IsLandscape, WindowViews(template), BlankKey(template.Id));
        })],
        run.Model.TemplateId,
        CanContinue: true);

    private static IReadOnlyList<TemplateWindowView> WindowViews(TemplateDefinition template) =>
        [.. template.Windows.Select(window => new TemplateWindowView(window.X, window.Y, window.Width, window.Height))];

    /// <summary>The blank preview's key once it is in the cache (Start Event composes them), else null.</summary>
    private string? BlankKey(string templateId)
    {
        var key = BlankPreviewKeyFor(templateId);
        return _dependencies.Previews.Contains(key) ? key : null;
    }

    private PhotographsView RenderPhotographs(GuestCycleRun run)
    {
        var model = run.Model;
        var template = TemplateOf(model.TemplateId);
        var sequence = run.Sequence;
        var tab = GuestCyclePlan.EffectiveTab(model);
        var windows = model.Windows
            .Select((captureId, index) => new PhotographsWindowView(index, captureId, captureId is null ? null : model.KeptCaptures[captureId].ImageUrl))
            .ToArray();
        var cropWindow = template.Windows[sequence?.TargetWindow ?? model.NextEmptyWindow ?? model.Selected ?? 0];
        return new PhotographsView(
            tab == PhotographsTab.Camera ? PhotographsTabName.Camera : PhotographsTabName.Arrange,
            new PhotographsTemplateView(template.Id, template.Width, template.Height, template.IsLandscape, WindowViews(template), BlankKey(template.Id)),
            windows,
            model.RetainedPool.Count,
            new CaptureSequenceView(
                sequence is not null,
                sequence?.TargetWindow,
                sequence?.CountdownRemaining,
                run.FlashSequence,
                sequence?.Captured ?? 0,
                sequence?.Total ?? 0,
                sequence?.Step == SequenceStep.Pausing),
            run.Active.Event.Mirror,
            (double)cropWindow.Width / cropWindow.Height,
            SelectionView(model, sequence),
            model.Operation is { Candidate: { } candidate, Target: { } retakeTarget } ? new RetakeCandidateView(retakeTarget, model.KeptCaptures[candidate].ImageUrl) : null,
            model.Revision,
            !run.Finishing && CanTake(run),
            !run.Finishing && sequence is null && windows.Any(window => window.CaptureId is not null),
            !run.Finishing && model.CanContinue);
    }

    /// <summary>Whether <c>TAKE PHOTOGRAPHS</c> works: an empty window, nothing running, and a camera that is ready.</summary>
    private bool CanTake(GuestCycleRun run) =>
        run.Model.Step == GuestCycleStep.Photographs
        && run.Sequence is null
        && run.Model.Operation is null
        && run.Model.NextEmptyWindow is not null
        && _cameraState == CameraState.Ready;

    private static PhotographSelectionView SelectionView(GuestCycleModel model, CaptureSequence? sequence)
    {
        if (model.Operation is { Mode: OperationMode.Retake, Target: { } target })
        {
            return new PhotographSelectionView(target, PhotographSelectionMode.Retaking);
        }

        if (model.Move is { } move)
        {
            return new PhotographSelectionView(move.SourceIndex ?? model.Selected, PhotographSelectionMode.Moving);
        }

        return sequence is null && model.Selected is { } selected
            ? new PhotographSelectionView(selected, PhotographSelectionMode.Selected)
            : new PhotographSelectionView(null, PhotographSelectionMode.None);
    }

    /// <summary>The running Guest Cycle for <c>test.getDiagnostics</c>, or null while none runs.</summary>
    private GuestCycleDiagnostics? GuestCycleDiagnosticsNow()
    {
        if (CurrentRun is not { } run)
        {
            return null;
        }

        var now = _dependencies.Clock.UtcNow;
        var model = run.Model;
        var timer = run.Timer;
        var remaining = timer.Remaining(now);
        PhotographsTabName? tab = model.Step == GuestCycleStep.Photographs
            ? (GuestCyclePlan.EffectiveTab(model) == PhotographsTab.Camera ? PhotographsTabName.Camera : PhotographsTabName.Arrange)
            : null;
        return new GuestCycleDiagnostics(
            run.Id,
            GuestCyclePlan.ToName(model.Step),
            tab,
            model.Windows.Count(captureId => captureId is not null),
            model.WindowCount,
            run.Sequence is not null,
            run.Sequence?.CountdownRemaining,
            new GuestCycleTimerDiagnostics(
                remaining is not null ? PhaseTimerRules.ToProtocol(timer.Phase!.Value) : null,
                remaining is { } left ? (long)left.TotalMilliseconds : null,
                timer.IsPaused,
                timer.IsWarning(now)));
    }

    // The panel on the Operator Screen: which step the guest is on, or the assistance alert.

    private RunningEventStep PanelStep(ActiveEventState active) => _assistance is not null
        ? RunningEventStep.OperatorAssistance
        : _cycle is { Ended: false } run && ReferenceEquals(run.Active, active) ? GuestCyclePlan.ToRunning(run.Model.Step) : active.Step;

    private RunningEventAssistance? PanelAssistance(ActiveEventState active) =>
        _assistance is { } assistance ? new RunningEventAssistance(assistance.Reason) : active.Assistance;

    // Keeping the camera's preview where the guest needs it: on Start and on the Camera tab only.

    private PreviewTarget DesiredPreviewTarget() => _assistance is not null
        ? PreviewTarget.None
        : CurrentRun is not { } run
            ? PreviewTarget.Guest
            : run.Model.Step == GuestCycleStep.Photographs && !run.Finishing && GuestCyclePlan.EffectiveTab(run.Model) == PhotographsTab.Camera
                ? PreviewTarget.Guest
                : PreviewTarget.None;

    private void SyncPreviewTarget()
    {
        if (_activeEvent is not null && DesiredPreviewTarget() is var desired && desired != _previewTarget)
        {
            SetPreviewTarget(desired);
        }
    }

    /// <summary>What every change to a cycle ends with: the Phase Timer follows the phase, and the preview follows the step.</summary>
    private void AfterChange(GuestCycleRun run)
    {
        SyncPhaseTimer(run);
        SyncPreviewTarget();
    }
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Guest Cycle {GuestCycleId} started.")]
    public static partial void GuestCycleStarted(ILogger logger, string guestCycleId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Creating the Guest Cycle failed.")]
    public static partial void GuestCycleStartFailed(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The store failed to {Operation} for the Guest Cycle.")]
    public static partial void GuestCycleStoreFailed(ILogger logger, Exception? exception, string operation);
}
