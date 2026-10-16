using FotoHavn.Core.Camera.Policies;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.GuestCycles.Orchestration;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// Operator Assistance (plan section 7, Failures inside a Guest Cycle). A camera, storage, or guest-display failure stops the guest on "A little
// interruption." and the running-Event panel shows the alert; the Phase Timer pauses, committed Captures stay, and RETRY (guest or operator)
// re-proves what failed: with a cycle the camera (a fresh frame), storage, and the bound display (reopening the Guest Screen if it was
// closed); without one only storage and the display. A failed check leaves the assistance showing; a passed one returns to the step the cycle
// was on, or to Start.
public sealed partial class Brain
{
    /// <summary>How often the brain looks for the reopened Guest Screen's handshake.</summary>
    internal static readonly TimeSpan GuestScreenPollInterval = TimeSpan.FromMilliseconds(250);

    private AssistanceState? _assistance;

    /// <summary>
    /// Begins Operator Assistance for <paramref name="reason"/> (the first reason stands while it lasts): a sequence that hasn't taken its
    /// frame ends, one that has finishes saving it, the Phase Timer pauses, and the preview stops. Called on the executor; the caller publishes.
    /// </summary>
    private void EnterAssistance(AssistanceReason reason)
    {
        if (_activeEvent is null || _start is not null || _assistance is not null)
        {
            return;
        }

        _assistance = new AssistanceState(reason);
        BrainLog.AssistanceEntered(_logger, reason);
        if (CurrentRun is { } run)
        {
            run.StartOverOpen = false;
            if (run.Sequence is { } sequence)
            {
                InterruptSequence(run, sequence);
            }

            AfterChange(run);
        }
        else
        {
            SyncPreviewTarget();
        }
    }

    /// <summary>The camera became unusable while a cycle runs (a completed cycle on its Keepsake needs no camera).</summary>
    private void OnCameraChangedDuringCycle()
    {
        if (CurrentRun is { Completed: false } && _cameraState is CameraState.NotAvailable or CameraState.InUseByAnotherApp)
        {
            EnterAssistance(AssistanceReason.Camera);
        }
    }

    /// <summary>Drops the assistance and anything its retry waits on (the Event is ending).</summary>
    private void ClearAssistance()
    {
        if (_assistance is { } assistance)
        {
            EndCameraWait(assistance);
            assistance.Wake?.Dispose();
            assistance.Wake = null;
        }

        _assistance = null;
    }

    /// <summary><c>RetryAssistance</c> (both roles): checks what failed; the answer is at once, the outcome follows.</summary>
    private CommandResultMessage RetryAssistance(string id)
    {
        if (_assistance is not { Retrying: false } assistance || _activeEvent is not { } active)
        {
            return NotAvailable(id);
        }

        assistance.Retrying = true;
        if (CurrentRun is { Completed: false })
        {
            RetryCamera(assistance, active);
        }
        else
        {
            RetryStorageProbe(assistance, active);
        }

        return Accepted(id);
    }

    private bool IsRetrying(AssistanceState assistance) => ReferenceEquals(_assistance, assistance) && assistance.Retrying;

    /// <summary>The camera: a fresh frame proves it; otherwise it is reopened and given <see cref="StartCameraTimeout"/> to deliver one.</summary>
    private void RetryCamera(AssistanceState assistance, ActiveEventState active)
    {
        if (_cameraSession is { } session && _cameraState == CameraState.Ready && FrameFreshness.IsFresh(session.Now, session.LatestFrame, FrameFreshness.EligibleMaxAge))
        {
            RetryStorageProbe(assistance, active);
            return;
        }

        assistance.CameraObserver = () => Post(() =>
        {
            OnRetryCameraChanged(assistance, active);
            PublishChanges();
        });
        CameraStateChanged += assistance.CameraObserver;
        assistance.Wake = _dependencies.Clock.Schedule(StartCameraTimeout, () => Post(() =>
        {
            if (IsRetrying(assistance) && assistance.CameraObserver is not null)
            {
                EndCameraWait(assistance);
                FailRetry(assistance);
            }

            PublishChanges();
        }));
        OpenCameraFor(active.Event.CameraDeviceId!, PreviewTarget.None);
    }

    private void OnRetryCameraChanged(AssistanceState assistance, ActiveEventState active)
    {
        if (!IsRetrying(assistance) || assistance.CameraObserver is null)
        {
            return;
        }

        switch (_cameraState)
        {
            case CameraState.Ready:
                EndCameraWait(assistance);
                RetryStorageProbe(assistance, active);
                break;
            case CameraState.Checking:
                break;
            default:
                EndCameraWait(assistance);
                FailRetry(assistance);
                break;
        }
    }

    private void EndCameraWait(AssistanceState assistance)
    {
        if (assistance.CameraObserver is { } observer)
        {
            CameraStateChanged -= observer;
            assistance.CameraObserver = null;
            assistance.Wake?.Dispose();
            assistance.Wake = null;
        }
    }

    /// <summary>Storage: a flushed write and delete under the Data folder.</summary>
    private void RetryStorageProbe(AssistanceState assistance, ActiveEventState active) =>
        OffVoid(
            () => _dependencies.Store.ProbeWritableAsync(CancellationToken.None),
            probe =>
            {
                if (!IsRetrying(assistance))
                {
                    return;
                }

                if (probe.IsCompletedSuccessfully)
                {
                    RetryDisplay(assistance, active);
                }
                else
                {
                    BrainLog.AssistanceRetryFailed(_logger, FailureOf(probe), "storage");
                    FailRetry(assistance);
                }
            });

    /// <summary>The bound display: it must be connected, and its Guest Screen open and showing (reopened here when the loss closed it).</summary>
    private void RetryDisplay(AssistanceState assistance, ActiveEventState active)
    {
        if (DisplayMatching.Find(CurrentDisplays, active.Event.GuestDisplayId) is not { } display)
        {
            BrainLog.AssistanceRetryFailed(_logger, null, "guest display");
            FailRetry(assistance);
            return;
        }

        if (_guestScreenDisplay is null)
        {
            try
            {
                _dependencies.Host.OpenGuestScreen(display);
                _guestScreenDisplay = display;
            }
#pragma warning disable CA1031 // A host that can't open the window is a failed check, whatever it threw.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                BrainLog.AssistanceRetryFailed(_logger, exception, "guest screen");
                FailRetry(assistance);
                return;
            }
        }

        WaitForGuestScreen(assistance, _dependencies.Clock.UtcNow + GuestScreenReadyTimeout);
    }

    private void WaitForGuestScreen(AssistanceState assistance, DateTimeOffset deadline)
    {
        if (!IsRetrying(assistance))
        {
            return;
        }

        if (GuestConnected())
        {
            RecoverFromAssistance(assistance);
        }
        else if (_dependencies.Clock.UtcNow >= deadline)
        {
            BrainLog.AssistanceRetryFailed(_logger, null, "guest screen handshake");
            FailRetry(assistance);
        }
        else
        {
            assistance.Wake = _dependencies.Clock.Schedule(GuestScreenPollInterval, () => Post(() =>
            {
                WaitForGuestScreen(assistance, deadline);
                PublishChanges();
            }));
        }
    }

    private static void FailRetry(AssistanceState assistance)
    {
        assistance.Wake?.Dispose();
        assistance.Wake = null;
        assistance.Retrying = false;
    }

    /// <summary>Every check passed: back to the step the cycle was on (its timer resumes), or to Start.</summary>
    private void RecoverFromAssistance(AssistanceState assistance)
    {
        assistance.Wake?.Dispose();
        _assistance = null;
        SetActiveStorageFailed(false);
        BrainLog.AssistanceRecovered(_logger, assistance.Reason);
        if (CurrentRun is { } run)
        {
            AfterChange(run);
        }
        else
        {
            SyncPreviewTarget();
        }
    }
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Operator Assistance begins: {Reason}.")]
    public static partial void AssistanceEntered(ILogger logger, AssistanceReason reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Operator Assistance ({Reason}) is over: the checks passed.")]
    public static partial void AssistanceRecovered(ILogger logger, AssistanceReason reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Retrying Operator Assistance: the {Check} check failed.")]
    public static partial void AssistanceRetryFailed(ILogger logger, Exception? exception, string check);
}
