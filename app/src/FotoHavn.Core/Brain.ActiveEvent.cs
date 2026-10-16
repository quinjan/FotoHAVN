using FotoHavn.Core.Displays;
using FotoHavn.Core.Events;
using FotoHavn.Core.Events.Active;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// The running Event, End Event, and the Guest Screen's shell, exit hold, and PIN (plan sections 7 and 8; M6 contract sections 4
// and 10). Owner: M6 wave B1b. Start Event is in Events/Active/Brain.StartEvent.cs.
//
// The brain treats a guest `welcome` (the Guest Screen's UI finished its handshake) as "Guest Screen ready" for Start Event step 8.
// The camera is driven only through the camera slot (Brain.Camera.cs); the wake lock only through `_dependencies.WakeLock`; the
// guest window only through `_dependencies.Host`. The PIN digits live only in the SubmitPin payload and the store call: never in a
// presentation, a log, or the state.
public sealed partial class Brain
{
    /// <summary>The wrong-PIN lockout's countdown updates this often while the PIN dialog is locked.</summary>
    internal static readonly TimeSpan LockoutTickInterval = TimeSpan.FromSeconds(1);

    // The running Event; null while none runs. Start Event sets it and the End Event sequence clears it.
    private ActiveEventState? _activeEvent;

    // The display the Guest Screen is on, or null while none is open (the test.openGuestScreen stand-in sets it too).
    private DisplayInfo? _guestScreenDisplay;

    /// <summary>
    /// Keeps the brain consistent with the current display list. M2 stand-in for D37 (M6 and M8 complete it): a Guest
    /// Screen whose display is gone is closed. The caller publishes the presentations afterwards.
    /// </summary>
    private void ApplyDisplayChange()
    {
        if (_guestScreenDisplay is { } display && DisplayMatching.Find(CurrentDisplays, display.Id) is null)
        {
            BrainLog.GuestDisplayLost(_logger, display.Id);
            _dependencies.Host.CloseGuestScreen();
            _guestScreenDisplay = null;

            // M8: the bound display going away is Operator Assistance at any time the Event runs (a start still waiting for the Guest Screen fails on its own).
            EnterAssistance(AssistanceReason.GuestDisplay);
        }
    }

    /// <summary>The display containing the Operator window, for D38: the virtual primary while displays are virtual.</summary>
    private string? OperatorDisplayId() => _virtualDisplays is { } virtualDisplays
        ? virtualDisplays.PrimaryId
        : _dependencies.Host.GetOperatorDisplayId();

    /// <summary>The running Event's ID, or null while none runs. Event setup and the Event list read it to lock the running Event.</summary>
    internal string? ActiveEventId => _activeEvent?.Event.Id;

    /// <summary>The running Event's bound camera device, or null while none runs. Event setup shows "In use by the running Event" for it.</summary>
    internal string? ActiveEventCameraDeviceId => _activeEvent?.Event.CameraDeviceId;

    /// <summary>
    /// The Event that is held: the running Event, or the one whose Start Event sequence is in progress (steps 1 to 8), else null.
    /// Every lock reads this, so a starting Event can't be edited, saved, deleted, or started again, exactly like a running one.
    /// </summary>
    internal string? HeldEventId => _activeEvent?.Event.Id ?? _start?.EventId;

    /// <summary>The held Event's bound camera device (null before the start has loaded the Event, or while none is held).</summary>
    internal string? HeldEventCameraDeviceId => _activeEvent?.Event.CameraDeviceId ?? _start?.CameraDeviceId;

    /// <summary>True while an Event runs or is starting: the one camera session belongs to it, and Event setup must not open, replace, or release it.</summary>
    internal bool IsCameraHeld => _activeEvent is not null || _start is not null;

    /// <summary>
    /// Makes <paramref name="state"/> the running Event (null: none runs) and publishes. M6 wave A's stand-in so the UI's fixtures and
    /// the render tests reach the running states from a real brain; B1b's Start and End Event sequences set it themselves.
    /// </summary>
    internal void ShowActiveEvent(ActiveEventState? state) =>
        Post(() =>
        {
            if (!ReferenceEquals(_activeEvent, state))
            {
                _activeEvent?.StopTimers();
            }

            _activeEvent = state;
            PublishChanges();
        });

    /// <summary>
    /// Records that storage stopped being usable (or is usable again) while the Event runs, so the Guest Screen shows Start unavailable
    /// (storage) until it recovers. M8 reports it from its Guest Cycle writes; the tests report it directly. Called on the executor;
    /// the caller publishes.
    /// </summary>
    internal void SetActiveStorageFailed(bool failed)
    {
        if (_activeEvent is { } active)
        {
            active.StorageFailed = failed;
        }
    }

    /// <summary>The running-Event panel, or null while no Event runs. Rendered on every operator surface, so any view can show an Operator Assistance alert.</summary>
    private RunningEventPanel? RenderRunningEventPanel()
    {
        if (_activeEvent is not { } active)
        {
            return null;
        }

        var connected = DisplayMatching.Find(CurrentDisplays, active.GuestDisplay.Id) is not null;
        return new RunningEventPanel(
            active.Event.Id,
            active.Event.Name,
            new RunningEventDisplay(active.GuestDisplay.Name, connected ? RunningDisplayState.Connected : RunningDisplayState.Disconnected),
            new RunningEventCamera(_cameraState, _cameraReason),
            PanelStep(active),
            active.CompletedGuestCycles,
            active.StartedAt,
            PanelAssistance(active),
            active.SingleScreen);
    }

    /// <summary>
    /// The Guest Screen's presentation (the fatal case is handled by the caller): Start, Start unavailable, and the overlays of the exit
    /// hold and PIN dialog. While no Event runs it is the Start placeholder, because a guest client may connect at any time.
    /// </summary>
    private GuestPresentation RenderGuestShell()
    {
        if (_activeEvent is not { } active)
        {
            return new GuestPresentation(GuestSurface.Start, new StartView(StepInsideAvailable: false, GuestGalleryLinkVisible: false), null, GuestOverlays.None, null, null, null, null, null, null, null, null, null);
        }

        var overlays = RenderGuestOverlays(active);

        // A running Guest Cycle, or Operator Assistance, renders its own step (Brain.GuestCycle.cs); null means the guest is on Start or the guest gallery.
        if (RenderGuestCycle(active, overlays) is { } cycle)
        {
            return cycle;
        }

        // The guest gallery and its strip detail (Brain.GuestGallery.cs); null means Start. Outside a Guest Cycle an ineligible camera or unwritable storage is Start unavailable, never Operator Assistance.
        if (RenderGuestGallery(active, overlays) is { } gallery)
        {
            return gallery;
        }

        var galleryLink = GuestGalleryLinkShown(active);
        if (_cameraState != CameraState.Ready)
        {
            return new GuestPresentation(GuestSurface.StartUnavailable, null, new StartUnavailableView(StartUnavailableReason.Camera, galleryLink), overlays, null, null, null, null, null, null, null, null, null);
        }

        return !_storageResolved || _storageError is not null || active.StorageFailed
            ? new GuestPresentation(GuestSurface.StartUnavailable, null, new StartUnavailableView(StartUnavailableReason.Storage, galleryLink), overlays, null, null, null, null, null, null, null, null, null)
            : new GuestPresentation(GuestSurface.Start, new StartView(StepInsideAvailable: true, galleryLink), null, overlays, null, null, null, null, null, null, null, null, null);
    }

    private GuestOverlays RenderGuestOverlays(ActiveEventState active)
    {
        var exitHold = active.ExitHoldBegan is null ? null : new ExitHoldOverlay(Active: true);
        PinDialogOverlay? pinDialog = null;
        if (active.PinDialogOpen)
        {
            var now = _dependencies.Clock.UtcNow;
            var attemptsRemaining = active.Lockout.AttemptsRemaining(now);
            pinDialog = active.Lockout.IsLocked(now)
                ? new PinDialogOverlay(PinDialogError.WrongPin, active.Lockout.RemainingSeconds(now), null)
                : new PinDialogOverlay(active.LastPinWrong && attemptsRemaining < PinRules.MaxWrongAttempts ? PinDialogError.WrongPin : null, null, attemptsRemaining);
        }

        return new GuestOverlays(exitHold, pinDialog, null, null, null);
    }

    private GuestPresentation RenderGuest() => _fatalReason is { } reason
        ? new GuestPresentation(GuestSurface.Fatal, null, null, GuestOverlays.None, new FatalView(reason), null, null, null, null, null, null, null, null)
        : RenderGuestShell();

    // End Event (plan section 8; contract section 10.8)

    /// <summary>
    /// Ends the running Event: ends any unfinished Guest Cycle (<c>operator_exit</c>; a completed one stays completed) and Operator Assistance, closes the Guest Screen, releases the camera and
    /// the wake lock, and clears the Event, its overlays, the exit-hold state, and the lockout. Called on the executor; the caller
    /// publishes. The operator's surface is left as it is: it shows the Event list again because the Event is gone.
    /// </summary>
    private void EndActiveEvent()
    {
        if (_activeEvent is not { } active)
        {
            return;
        }

        // Ending while the start still waits for the Guest Screen's handshake: the start is over (no failure to report); the releases below are its rollback.
        _start?.Cancel();
        EndGuestCycleForEvent();
        DropGuestGallery();
        active.StopTimers();
        _dependencies.Host.CloseGuestScreen();
        _guestScreenDisplay = null;
        ReleaseCamera();
        _dependencies.WakeLock.Release();
        _activeEvent = null;
        RefreshSetupCamera();
        if (_confirmation?.Kind == ConfirmationKind.EndEvent)
        {
            _confirmation = null;
        }

        BrainLog.EventEnded(_logger, active.Event.Id);
        StartSummaryRefresh();
    }

    /// <summary>Re-lists the Events off the executor and posts the result back, so the Event list shows what the finished Event changed.</summary>
    private void StartSummaryRefresh()
    {
        if (!_storageResolved || _storageError is not null)
        {
            return;
        }

        Task<IReadOnlyList<Storage.EventSummary>> list;
        try
        {
            list = _dependencies.Store.ListEventsAsync(CancellationToken.None);
        }
#pragma warning disable CA1031 // A store that throws instead of faulting its task is handled like one that faults.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            list = Task.FromException<IReadOnlyList<Storage.EventSummary>>(exception);
        }

        list.ContinueWith(
            completed => Post(() =>
            {
                if (completed.IsCompletedSuccessfully)
                {
                    _eventSummaries = [.. completed.Result];
                    PublishChanges();
                }
                else
                {
                    _ = completed.Exception;
                }
            }),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary><c>StartEvent</c> from the Event list: Start Event for the saved Event; a failed precondition opens Event setup showing why.</summary>
    private CommandResultMessage StartEvent(string id, StartEventPayload payload)
    {
        if (_activeEvent is not null || _start is not null)
        {
            return CommandResultMessage.Rejected(id, RejectReason.EventRunning);
        }

        if (!_storageResolved || _storageError is not null || !_eventSummaries.Any(summary => string.Equals(summary.Id, payload.EventId, StringComparison.Ordinal)))
        {
            return NotAvailable(id);
        }

        var eventId = payload.EventId;
        TryStartEventAsync(eventId).ContinueWith(
            completed => Post(() =>
            {
                if (completed.Result is { Failure: { } failure } result)
                {
                    OpenEventSetupWithStartFailure(eventId, new EventSetupStartFailure(failure, result.CameraReason));
                }

                PublishChanges();
            }),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return CommandResultMessage.Accepted(id);
    }

    /// <summary><c>EndEvent</c>: asks "End this Event?"; the Event keeps running until <c>ConfirmEndEvent</c>.</summary>
    private CommandResultMessage EndEvent(string id)
    {
        if (_activeEvent is null)
        {
            return NotAvailable(id);
        }

        SetConfirmation(new ConfirmationOverlay(ConfirmationKind.EndEvent, null, null, null));
        return CommandResultMessage.Accepted(id);
    }

    private CommandResultMessage ConfirmEndEvent(string id)
    {
        if (_activeEvent is null || _confirmation?.Kind != ConfirmationKind.EndEvent)
        {
            return NotAvailable(id);
        }

        EndActiveEvent();
        return CommandResultMessage.Accepted(id);
    }

    private CommandResultMessage KeepRunning(string id)
    {
        if (_activeEvent is null || _confirmation?.Kind != ConfirmationKind.EndEvent)
        {
            return NotAvailable(id);
        }

        _confirmation = null;
        return CommandResultMessage.Accepted(id);
    }

    // The exit hold and the PIN dialog (plan section 8, End an Active Event, From the Guest Screen)

    /// <summary><c>BeginExitHold</c>: the hold starts on the brain's clock; the UI draws the 3-second progress.</summary>
    private CommandResultMessage BeginExitHold(string id)
    {
        if (_activeEvent is not { } active || active.PinDialogOpen || active.ExitHoldBegan is not null)
        {
            return NotAvailable(id);
        }

        active.ExitHoldBegan = _dependencies.Clock.UtcNow;
        return CommandResultMessage.Accepted(id);
    }

    /// <summary><c>CompleteExitHold</c>: opens the PIN dialog when the hold lasted long enough on the brain's clock; a shorter one is rejected and the hold clears.</summary>
    private CommandResultMessage CompleteExitHold(string id)
    {
        if (_activeEvent is not { ExitHoldBegan: { } began } active)
        {
            return NotAvailable(id);
        }

        var now = _dependencies.Clock.UtcNow;
        active.ExitHoldBegan = null;
        if (!ExitHoldRules.IsComplete(now - began))
        {
            return NotAvailable(id);
        }

        active.PinDialogOpen = true;
        active.LastPinWrong = false;
        ScheduleLockoutTick(active);
        return CommandResultMessage.Accepted(id);
    }

    /// <summary>
    /// <c>SubmitPin</c>: the check runs off the executor and its outcome is posted back, so the command is accepted either way and
    /// the presentation carries the result. A malformed PIN is a wrong attempt, never an invalid payload.
    /// </summary>
    private CommandResultMessage SubmitPin(string id, SubmitPinPayload payload)
    {
        if (_activeEvent is not { PinDialogOpen: true } active)
        {
            return NotAvailable(id);
        }

        var now = _dependencies.Clock.UtcNow;
        if (active.Lockout.IsLocked(now))
        {
            return CommandResultMessage.Rejected(id, RejectReason.PinLocked);
        }

        if (active.PinCheckInFlight)
        {
            return NotAvailable(id);
        }

        if (!PinRules.IsValidFormat(payload.Digits))
        {
            RegisterWrongPin(active, now);
            return CommandResultMessage.Accepted(id);
        }

        active.PinCheckInFlight = true;
        Task<bool> check;
        try
        {
            check = _dependencies.Store.VerifyPinAsync(active.Event.Id, payload.Digits, CancellationToken.None);
        }
#pragma warning disable CA1031 // A store that throws instead of faulting its task is handled like one that faults: the PIN isn't verified.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            check = Task.FromException<bool>(exception);
        }

        check.ContinueWith(
            completed => Post(() =>
            {
                var verified = completed.IsCompletedSuccessfully && completed.Result;
                _ = completed.Exception;
                CompletePinCheck(active, verified);
                PublishChanges();
            }),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return CommandResultMessage.Accepted(id);
    }

    private void CompletePinCheck(ActiveEventState active, bool verified)
    {
        active.PinCheckInFlight = false;
        if (!ReferenceEquals(_activeEvent, active) || !active.PinDialogOpen)
        {
            return;
        }

        if (verified)
        {
            active.Lockout.RegisterCorrect();
            EndActiveEvent();
        }
        else
        {
            RegisterWrongPin(active, _dependencies.Clock.UtcNow);
        }
    }

    private void RegisterWrongPin(ActiveEventState active, DateTimeOffset now)
    {
        active.LastPinWrong = true;
        if (active.Lockout.RegisterWrong(now))
        {
            BrainLog.PinLocked(_logger);
        }

        ScheduleLockoutTick(active);
    }

    /// <summary>Keeps the locked countdown current: one tick a second while the dialog is open and locked, and one more when the lockout has ended.</summary>
    private void ScheduleLockoutTick(ActiveEventState active)
    {
        active.LockoutTick?.Dispose();
        active.LockoutTick = null;
        if (!active.PinDialogOpen || !active.Lockout.IsLocked(_dependencies.Clock.UtcNow))
        {
            return;
        }

        active.LockoutTick = _dependencies.Clock.Schedule(LockoutTickInterval, () => Post(() =>
        {
            if (!ReferenceEquals(_activeEvent, active))
            {
                return;
            }

            ScheduleLockoutTick(active);
            PublishChanges();
        }));
    }

    /// <summary><c>CancelExit</c>: closes the hold or the PIN dialog. A running lockout keeps running.</summary>
    private CommandResultMessage CancelExit(string id)
    {
        if (_activeEvent is not { } active)
        {
            return NotAvailable(id);
        }

        if (active.PinDialogOpen)
        {
            active.PinDialogOpen = false;
            active.LastPinWrong = false;
            active.LockoutTick?.Dispose();
            active.LockoutTick = null;
            return CommandResultMessage.Accepted(id);
        }

        if (active.ExitHoldBegan is null)
        {
            return NotAvailable(id);
        }

        active.ExitHoldBegan = null;
        return CommandResultMessage.Accepted(id);
    }
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Event {EventId} ended.")]
    public static partial void EventEnded(ILogger logger, string eventId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Five wrong PIN attempts: the PIN dialog is locked.")]
    public static partial void PinLocked(ILogger logger);
}
