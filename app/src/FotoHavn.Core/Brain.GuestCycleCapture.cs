using FotoHavn.Core.Camera;
using FotoHavn.Core.GuestCycles;
using FotoHavn.Core.GuestCycles.Orchestration;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// The capture sequence (plan section 7, Capture sequence). Per window: the countdown (one push a second on the brain's clock), the first frame
// after the countdown's end, the flash, the Event's mirror setting, the JPEG, the commit, an 800 ms pause, then the next window or the
// review. A Retake is one window and stops at the candidate. Every result posts back to the executor and is judged by the model: a Capture
// the model refuses is set discarded in the store, so nothing stays kept that no cycle will use.
public sealed partial class Brain
{
    /// <summary><c>TakePhotographs</c>: the sequence fills every empty window in order.</summary>
    private CommandResultMessage TakePhotographs(string id) =>
        ActionableRun is { Model.Tab: PhotographsTab.Camera } run && TryBeginSequence(run) ? Accepted(id) : NotAvailable(id);

    /// <summary>Starts a sequence for every empty window (<c>TAKE PHOTOGRAPHS</c> and the capture timer's outcome). False when none can start now.</summary>
    private bool TryBeginSequence(GuestCycleRun run)
    {
        if (!CanTake(run))
        {
            return false;
        }

        var operationId = ++run.NextOperationId;
        ApplyModel(run, new BeginSequenceAction(operationId));
        LaunchSequence(run, new CaptureSequence(operationId, OperationMode.Sequence, run.Model.EmptyWindows.Count, run.Model.NextEmptyWindow!.Value));
        return true;
    }

    private CommandResultMessage BeginRetake(string id, BeginRetakePayload payload)
    {
        if (ActionableRun is not { Model.Step: GuestCycleStep.Photographs, Sequence: null } run || _cameraState != CameraState.Ready)
        {
            return NotAvailable(id);
        }

        if (!InRange(run, payload.Index))
        {
            return Invalid(id);
        }

        var operationId = ++run.NextOperationId;
        if (!ApplyModel(run, new BeginRetakeAction(operationId, payload.Index)).Applied)
        {
            return NotAvailable(id);
        }

        LaunchSequence(run, new CaptureSequence(operationId, OperationMode.Retake, 1, payload.Index));
        return Accepted(id);
    }

    /// <summary><c>TryAgain</c>: the candidate is discarded and the same window counts down again, in the same operation.</summary>
    private CommandResultMessage TryAgain(string id)
    {
        if (ActionableRun is not { Model.Operation: { Mode: OperationMode.Retake, Candidate: not null, Target: { } target } operation } run || _cameraState != CameraState.Ready)
        {
            return NotAvailable(id);
        }

        ApplyModel(run, new TryAgainAction());
        LaunchSequence(run, new CaptureSequence(operation.Id, OperationMode.Retake, 1, target));
        return Accepted(id);
    }

    /// <summary><c>StopCapture</c>: the remaining countdown is cancelled, the Captures saved stay, and the capture timer starts afresh.</summary>
    private CommandResultMessage StopCapture(string id)
    {
        if (ActionableRun is not { Sequence: { } sequence } run)
        {
            return NotAvailable(id);
        }

        sequence.RestartsTimer = true;
        if (sequence.Step is SequenceStep.Capturing or SequenceStep.Saving)
        {
            // The frame is already taken: that Capture is saved first, then the sequence ends.
            sequence.StopRequested = true;
        }
        else
        {
            EndSequence(run, sequence);
        }

        AfterChange(run);
        return Accepted(id);
    }

    private void LaunchSequence(GuestCycleRun run, CaptureSequence sequence)
    {
        run.Sequence = sequence;
        BeginCountdown(run, sequence);
        AfterChange(run);
    }

    private void BeginCountdown(GuestCycleRun run, CaptureSequence sequence)
    {
        sequence.Step = SequenceStep.Countdown;
        sequence.CountdownRemaining = run.Active.Event.CountdownSeconds;
        sequence.Wake = _dependencies.Clock.Schedule(GuestCycleTiming.SecondTick, () => Post(() => OnCountdownTick(run, sequence)));
    }

    private void OnCountdownTick(GuestCycleRun run, CaptureSequence sequence)
    {
        if (run.Ended || !ReferenceEquals(run.Sequence, sequence))
        {
            return;
        }

        sequence.CountdownRemaining--;
        if (sequence.CountdownRemaining > 0)
        {
            sequence.Wake = _dependencies.Clock.Schedule(GuestCycleTiming.SecondTick, () => Post(() => OnCountdownTick(run, sequence)));
        }
        else
        {
            TakeFrame(run, sequence);
        }

        PublishChanges();
    }

    /// <summary>The countdown reached zero: the camera is asked for the first frame at or after this moment on its own clock.</summary>
    private void TakeFrame(GuestCycleRun run, CaptureSequence sequence)
    {
        sequence.CountdownRemaining = null;
        sequence.TakenAt = _dependencies.Clock.UtcNow;
        if (_cameraState != CameraState.Ready || _cameraSession is not { } session)
        {
            CaptureFailed(run, sequence, null);
            return;
        }

        sequence.Step = SequenceStep.Capturing;
        var cancellation = new CancellationTokenSource();
        sequence.CaptureCancellation = cancellation;
        sequence.CaptureTimeout = _dependencies.Clock.Schedule(CaptureTimeout, () => Post(() => OnCaptureTimeout(run, sequence)));
        Task<CapturedFrame> capture;
        try
        {
            capture = session.CaptureAsync(session.Now, cancellation.Token);
        }
#pragma warning disable CA1031 // A camera session that throws instead of faulting its task is handled like one that faults.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            capture = Task.FromException<CapturedFrame>(exception);
        }

        var mirrored = run.Active.Event.Mirror;

        // The mirror, the JPEG, and the post back run on the pool; they count as off-executor work from the moment the camera answers, so a test settles on them.
        capture.ContinueWith(
            completed =>
            {
                Interlocked.Increment(ref _offExecutorWork);
                _ = Task.Run(() =>
                {
                    try
                    {
                        ProcessFrame(run, sequence, completed, mirrored);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _offExecutorWork);
                    }
                });
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void OnCaptureTimeout(GuestCycleRun run, CaptureSequence sequence)
    {
        if (!run.Ended && sequence.Step == SequenceStep.Capturing)
        {
            sequence.CaptureCancellation?.Cancel();
        }
    }

    /// <summary>
    /// Runs off the executor when the camera answered: tells the brain the frame is taken (the flash), then mirrors and encodes it and
    /// posts the JPEG back for the commit.
    /// </summary>
    private void ProcessFrame(GuestCycleRun run, CaptureSequence sequence, Task<CapturedFrame> captured, bool mirrored)
    {
        if (!captured.IsCompletedSuccessfully)
        {
            var failure = FailureOf(captured);
            Post(() =>
            {
                CaptureFailed(run, sequence, failure);
                PublishChanges();
            });
            return;
        }

        Post(() =>
        {
            FrameTaken(run, sequence);
            PublishChanges();
        });
        try
        {
            var frame = MirrorWhenAsked(captured.Result, mirrored) ?? throw new InvalidOperationException("The frame could not be mirrored.");
            var encoder = _dependencies.CaptureEncoder ?? throw new InvalidOperationException("The host gave the brain no Capture encoder.");
            var jpeg = encoder.EncodeJpeg(frame);
            Post(() =>
            {
                StartCommit(run, sequence, jpeg, frame.Width, frame.Height, mirrored);
                PublishChanges();
            });
        }
#pragma warning disable CA1031 // Whatever stops a Capture being encoded, it can't be saved: Operator Assistance for storage.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Post(() =>
            {
                SaveFailed(run, sequence, exception);
                PublishChanges();
            });
        }
    }

    /// <summary>The frame is in hand: one flash, counted now.</summary>
    private static void FrameTaken(GuestCycleRun run, CaptureSequence sequence)
    {
        if (run.Ended)
        {
            return;
        }

        run.FlashSequence++;
        sequence.Step = SequenceStep.Saving;
        sequence.CaptureTimeout?.Dispose();
        sequence.CaptureTimeout = null;
    }

    private void StartCommit(GuestCycleRun run, CaptureSequence sequence, byte[] jpeg, int width, int height, bool mirrored)
    {
        if (run.Ended)
        {
            return;
        }

        var commit = new CaptureCommit(run.Id, run.NextCaptureNumber++, jpeg, width, height, mirrored, sequence.TakenAt);
        var write = InOrder(run, () => _dependencies.Store.CommitCaptureAsync(commit, CancellationToken.None));
        if (sequence.Mode == OperationMode.Retake)
        {
            run.RetakeCommit = write;
        }

        Off(() => write, committed => CaptureCommitted(run, sequence, committed, jpeg, width, height));
    }

    private void CaptureCommitted(GuestCycleRun run, CaptureSequence sequence, Task<CaptureRecord> committed, byte[] jpeg, int width, int height)
    {
        if (sequence.Mode == OperationMode.Retake)
        {
            run.RetakeCommit = null;
        }

        if (run.Ended)
        {
            // The cycle ended meanwhile: a Sequence's Capture, if it was saved, stays with the Event; a Retake's was discarded when the cycle ended.
            return;
        }

        if (!committed.IsCompletedSuccessfully)
        {
            SaveFailed(run, sequence, FailureOf(committed));
            return;
        }

        var record = committed.Result;
        var kept = new KeptCapture(record.Id, record.Sequence, GuestCyclePlan.CaptureUrl(run.Active.Event.Id, run.Id, record.Sequence, record.Sha256), width, height);
        if (!ApplyModel(run, new CaptureCommittedAction(sequence.OperationId, sequence.TargetWindow, kept)).Applied)
        {
            // A late Capture no cycle will use: it must not stay kept in the store.
            BrainLog.GuestCycleLateCapture(_logger, record.Id);
            DiscardInStore(run, record.Id);
            EndSequence(run, sequence);
            AfterChange(run);
            return;
        }

        run.Jpegs[record.Id] = jpeg;
        sequence.Captured++;
        if (sequence.Mode == OperationMode.Retake)
        {
            // A Retake stops at the candidate; the guest decides on the Arrange tab.
            EndSequence(run, sequence);
        }
        else
        {
            sequence.Step = SequenceStep.Pausing;
            sequence.Wake = _dependencies.Clock.Schedule(GuestCycleTiming.PostCapturePause, () => Post(() => OnPauseDone(run, sequence)));
        }

        AfterChange(run);
    }

    private void OnPauseDone(GuestCycleRun run, CaptureSequence sequence)
    {
        if (run.Ended || !ReferenceEquals(run.Sequence, sequence))
        {
            return;
        }

        if (!sequence.StopRequested && run.Model.NextEmptyWindow is { } next)
        {
            sequence.TargetWindow = next;
            BeginCountdown(run, sequence);
        }
        else
        {
            EndSequence(run, sequence);
        }

        AfterChange(run);
        PublishChanges();
    }

    /// <summary>The camera failed during a Capture: the sequence ends, committed Captures stay, and Operator Assistance begins.</summary>
    private void CaptureFailed(GuestCycleRun run, CaptureSequence sequence, Exception? failure)
    {
        if (run.Ended)
        {
            return;
        }

        BrainLog.GuestCycleCaptureFailed(_logger, failure);
        EndSequence(run, sequence);
        EnterAssistance(AssistanceReason.Camera);
        AfterChange(run);
    }

    /// <summary>A Capture could not be saved (encoding or the store): the uncommitted Capture is discarded, committed ones stay, and Operator Assistance begins.</summary>
    private void SaveFailed(GuestCycleRun run, CaptureSequence sequence, Exception? failure)
    {
        if (run.Ended)
        {
            return;
        }

        BrainLog.GuestCycleSaveFailed(_logger, failure);
        EndSequence(run, sequence);
        EnterAssistance(AssistanceReason.Storage);
        AfterChange(run);
    }

    /// <summary>Ends a sequence that hasn't taken its frame yet (the countdown or the pause). False while a frame is being taken or saved.</summary>
    private bool StopIfIdle(GuestCycleRun run, CaptureSequence sequence)
    {
        if (sequence.Step is SequenceStep.Capturing or SequenceStep.Saving)
        {
            return false;
        }

        EndSequence(run, sequence);
        return true;
    }

    /// <summary>
    /// Takes the sequence out of the cycle: a Sequence finishes in the model (the Arrange tab shows once every window is filled); a Retake
    /// that took no candidate is cancelled, the original staying; a Retake whose candidate waits keeps its operation. The caller syncs.
    /// </summary>
    private void EndSequence(GuestCycleRun run, CaptureSequence sequence)
    {
        sequence.Release();
        run.Sequence = null;
        if (sequence.Mode == OperationMode.Sequence)
        {
            ApplyModel(run, new FinishSequenceAction(sequence.OperationId));
        }
        else if (run.Model.Operation is { Candidate: null })
        {
            ApplyModel(run, new CancelAction());
        }

        if (sequence.RestartsTimer)
        {
            run.Timer = run.Timer.Restart(_dependencies.Clock.UtcNow);
        }
    }

    /// <summary>
    /// Operator Assistance begins: a sequence that hasn't taken its frame ends now, and one that has finishes saving it first so no
    /// photograph is lost.
    /// </summary>
    private void InterruptSequence(GuestCycleRun run, CaptureSequence sequence)
    {
        if (sequence.Step is SequenceStep.Capturing or SequenceStep.Saving)
        {
            sequence.StopRequested = true;
        }
        else
        {
            EndSequence(run, sequence);
        }
    }
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "A Capture's frame was never taken.")]
    public static partial void GuestCycleCaptureFailed(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A Capture could not be saved.")]
    public static partial void GuestCycleSaveFailed(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Capture {CaptureId} arrived after the cycle stopped using its operation; it is discarded.")]
    public static partial void GuestCycleLateCapture(ILogger logger, string captureId);
}
