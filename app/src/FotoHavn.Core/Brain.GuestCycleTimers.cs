using FotoHavn.Core.Events;
using FotoHavn.Core.GuestCycles;
using FotoHavn.Core.GuestCycles.Orchestration;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;

namespace FotoHavn.Core;

// The Phase Timer (plan section 7, Phase timers, D18). One PhaseTimerEngine times the phase the guest is in; the brain feeds it the clock
// and keeps exactly one wake-up scheduled, at the engine's next deadline or, while the warning shows, at the next whole second. The
// timer is paused during a capture sequence and Operator Assistance, and restarted only by MORE TIME, going back or entering a phase,
// showing the Camera tab, and stopping a sequence. It never deletes a kept Capture.
public sealed partial class Brain
{
    private static int SecondsFor(GuestCycleRun run, PhaseTimerPhase phase) =>
        run.Active.Event.PhaseTimers.FirstOrDefault(setting => setting.Phase == phase)?.Seconds ?? PhaseTimerRules.Default(phase);

    /// <summary>
    /// Makes the timer fit the cycle: a new phase starts a new timer, a sequence or Operator Assistance pauses it, and anything else lets it
    /// run. Then the single wake-up is scheduled again.
    /// </summary>
    private void SyncPhaseTimer(GuestCycleRun run)
    {
        var now = _dependencies.Clock.UtcNow;
        if (GuestCyclePlan.PhaseFor(run.Model, run.Finishing) is not { } phase)
        {
            run.Timer = PhaseTimerEngine.Stopped;
        }
        else if (run.Timer.Phase != phase)
        {
            run.Timer = PhaseTimerEngine.Start(phase, SecondsFor(run, phase), now);
        }

        run.Timer = run.Sequence is not null || _assistance is not null ? run.Timer.Pause(now) : run.Timer.Resume(now);
        RescheduleTimer(run);
    }

    private void RescheduleTimer(GuestCycleRun run)
    {
        run.TimerWake?.Dispose();
        run.TimerWake = null;
        var generation = ++run.TimerGeneration;
        var now = _dependencies.Clock.UtcNow;
        if (run.Timer.NextDeadline(now) is not { } deadline)
        {
            return;
        }

        var delay = GuestCyclePlan.WakeDelay(deadline - now, run.Timer.Remaining(now)!.Value, run.Timer.IsWarning(now));
        run.TimerWake = _dependencies.Clock.Schedule(delay, () => Post(() => OnTimerWake(run, generation)));
    }

    /// <summary>The timer's wake-up: the warning began, a second of it passed, or the time ran out.</summary>
    private void OnTimerWake(GuestCycleRun run, int generation)
    {
        if (run.Ended || generation != run.TimerGeneration)
        {
            return;
        }

        if (run.Timer.IsExpired(_dependencies.Clock.UtcNow))
        {
            HandleExpiry(run);
        }
        else
        {
            RescheduleTimer(run);
        }

        PublishChanges();
    }

    /// <summary>
    /// The phase ran out (plan section 7, Phase timers): the cycle carries on with what the guest chose, the way it would have if they had
    /// pressed the action themselves. An open Start over confirmation closes as KEEP GOING first.
    /// </summary>
    private void HandleExpiry(GuestCycleRun run)
    {
        run.StartOverOpen = false;
        switch (run.Timer.Phase)
        {
            case PhaseTimerPhase.Template:
                ApplyModel(run, new GoToStepAction(GuestCycleStep.Photographs));
                break;
            case PhaseTimerPhase.PhotographsCapture:
                // A Retake left waiting (its candidate, or after TRY AGAIN) blocks the sequence: it ends as KEEP ORIGINAL, as on the
                // review timer, so the empty windows still fill and an untouched cycle reaches its Photo Strip.
                if (run.Model.Operation is { Mode: OperationMode.Retake } && run.Sequence is null)
                {
                    ApplyModel(run, new CancelAction());
                }

                TryBeginSequence(run);
                break;
            case PhaseTimerPhase.PhotographsReview:
                // A pending Retake candidate is discarded (the original stays) and a pending Move is cancelled, then the arrangement goes on.
                ApplyModel(run, new CancelAction());
                ContinueToFilters(run);
                break;
            case PhaseTimerPhase.Filters:
                BeginFinalCompose(run);
                break;
            default:
                ReturnToStart(run, null);
                return;
        }

        var now = _dependencies.Clock.UtcNow;
        if (run.Timer.IsExpired(now))
        {
            // The outcome changed nothing the timer follows (the camera wasn't ready, say): it runs again rather than spinning on its own expiry.
            run.Timer = run.Timer.Restart(now);
        }

        AfterChange(run);
    }

    /// <summary><c>MoreTime</c>: restarts the phase's full time while its warning shows.</summary>
    private CommandResultMessage MoreTime(string id)
    {
        var now = _dependencies.Clock.UtcNow;
        if (ActionableRun is not { } run || !run.Timer.IsRunning || !run.Timer.IsWarning(now))
        {
            return MoreTimeInGallery(id);
        }

        run.Timer = run.Timer.Restart(now);
        AfterChange(run);
        return Accepted(id);
    }
}
