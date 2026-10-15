using FotoHavn.Core.Displays;
using FotoHavn.Core.Events;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Events.Setup;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// Event setup (plan section 8, Event setup; M6 contract sections 4 and 10). Owner: M6 wave B1a. The draft (a class under Events/Setup/)
// lives in the brain; every Set* command updates it and the form is rendered from it, so the UI never decides a rule. The camera is
// driven only through the camera slot in Brain.Camera.cs; Start goes through TryStartEventAsync (Brain.ActiveEvent.cs, owner B1b).
public sealed partial class Brain
{
    // The open draft, or null. It is dropped when setup closes.
    private EventSetupDraft? _setupDraft;

    // True from EditEvent (or a failed StartEvent) until the saved Event arrives, so a second open can't start meanwhile.
    private bool _setupLoading;

    // Bumped whenever setup opens or closes: a blank preview or a save that arrives for an earlier setup is dropped.
    private int _setupRequest;
    private int _setupLoadRequest;
    private CancellationTokenSource? _setupCancellation;
    private readonly Dictionary<string, string> _blankPreviewKeys = new(StringComparer.Ordinal);

    // Start Event for SaveAndStartEvent; tests replace it to reach the outcomes B1b's sequence produces.
    private Func<string, Task<StartEventResult>>? _startEventRunner;

    /// <summary>Replaces how <c>SaveAndStartEvent</c> starts the saved Event (tests only); null restores <see cref="TryStartEventAsync"/>.</summary>
    internal void UseStartEventRunner(Func<string, Task<StartEventResult>>? runner) => _startEventRunner = runner;

    /// <summary>
    /// The Event setup form for the open draft, or null when none is open (then the operator shows the Event list). Only asked
    /// while <c>_operatorSurface</c> is <see cref="OperatorSurface.EventSetup"/>.
    /// </summary>
    private EventSetupView? RenderEventSetup()
    {
        if (_setupDraft is not { } draft)
        {
            return null;
        }

        var running = IsCameraHeld;
        return new EventSetupView(
            draft.EventId,
            draft.IsNew,
            draft.IsDirty,
            new EventSetupFieldValues(draft.Name, draft.Date, draft.Venue, draft.Customer, draft.Notes),
            [.. EventSetupValidator.Validate(draft).Select(error => error.ToProtocol())],
            draft.ShowErrors,
            RenderSetupCamera(draft),
            RenderSetupGuestDisplay(draft),
            draft.Mirror,
            draft.CountdownSeconds,
            PrinterChoice.None,
            RenderSetupTemplates(draft),
            RenderSetupLooks(draft),
            [.. PhaseTimerRules.All.Select(phase => new PhaseTimerOption(
                PhaseTimerRules.ToProtocol(phase),
                draft.TimerSeconds(phase),
                PhaseTimerRules.Default(phase),
                PhaseTimerRules.Min(phase),
                PhaseTimerRules.Max(phase),
                PhaseTimerRules.CanBeOff(phase)))],
            draft.GuestGalleryEnabled,
            new EventSetupPinView(draft.HasSavedPin, draft.HasPendingPin),
            !running,
            running ? StartDisabledReason.EventRunning : null,
            draft.StartFailure,
            draft.Saving);
    }

    private IReadOnlyList<EventSetupTemplateOption> RenderSetupTemplates(EventSetupDraft draft) =>
        [.. _dependencies.Catalog.Templates.Select(template => new EventSetupTemplateOption(
            template.Id,
            template.Name,
            template.Description,
            template.Width,
            template.Height,
            template.IsLandscape,
            draft.OfferedTemplateIds.Contains(template.Id, StringComparer.Ordinal),
            string.Equals(draft.DefaultTemplateId, template.Id, StringComparison.Ordinal),
            _blankPreviewKeys.GetValueOrDefault(template.Id)))];

    private IReadOnlyList<EventSetupLookOption> RenderSetupLooks(EventSetupDraft draft) =>
        [.. _dependencies.Catalog.Looks.Select(look => new EventSetupLookOption(
            look.Id,
            look.Name,
            look.Description,
            draft.OfferedLookIds.Contains(look.Id, StringComparer.Ordinal),
            string.Equals(draft.DefaultLookId, look.Id, StringComparison.Ordinal)))];

    // The camera field (plan section 8, Camera). The binding is the draft's and is never swapped: a bound camera that isn't listed is
    // Missing and keeps its saved name. The brain has one camera session, so while an Event runs it belongs to that Event: the same
    // device is "in use by the running Event" and any other isn't checked until the Event ends.
    private EventSetupCameraView RenderSetupCamera(EventSetupDraft draft)
    {
        var options = _cameras.Select(camera => new CameraOption(camera.DeviceId, camera.Name)).ToList();
        if (draft.CameraDeviceId is not { } bound)
        {
            return new EventSetupCameraView(options, null, null, EventSetupCameraState.None, null, false, draft.Mirror);
        }

        var listed = _cameras.FirstOrDefault(camera => string.Equals(camera.DeviceId, bound, StringComparison.Ordinal));
        var (state, reason, previewActive) = SetupCameraState(bound, listed is not null);
        return new EventSetupCameraView(options, bound, listed?.Name ?? draft.CameraName, state, reason, previewActive, draft.Mirror);
    }

    private (EventSetupCameraState State, CameraFailureKind? Reason, bool PreviewActive) SetupCameraState(string bound, bool listed)
    {
        if (IsCameraHeld)
        {
            if (string.Equals(bound, HeldEventCameraDeviceId, StringComparison.Ordinal))
            {
                return (EventSetupCameraState.InUseByRunningEvent, null, false);
            }

            return (listed ? EventSetupCameraState.NotChecked : EventSetupCameraState.Missing, null, false);
        }

        if (!listed)
        {
            return (EventSetupCameraState.Missing, null, false);
        }

        if (!string.Equals(_selectedCameraId, bound, StringComparison.Ordinal))
        {
            return (EventSetupCameraState.NotChecked, null, false);
        }

        var preview = _previewTarget == PreviewTarget.Operator;
        return _cameraState switch
        {
            CameraState.Checking => (EventSetupCameraState.Checking, null, preview),
            CameraState.Ready => (EventSetupCameraState.Ready, null, preview),
            CameraState.NotAvailable => (EventSetupCameraState.NotAvailable, _cameraReason, false),
            CameraState.InUseByAnotherApp => (EventSetupCameraState.InUseByAnotherApp, _cameraReason, false),
            _ => (EventSetupCameraState.NotChecked, null, false),
        };
    }

    // The guest display field (plan section 8, Guest display): the display list in IDENTIFY DISPLAYS order, the Operator Screen
    // marked, and the binding never swapped for another display.
    private EventSetupGuestDisplayView RenderSetupGuestDisplay(EventSetupDraft draft)
    {
        var displays = CurrentDisplays;
        var operatorId = OperatorDisplayId() ?? DisplayMatching.FindPrimary(displays)?.Id;
        var options = DisplayRules.NumberingOrder(displays)
            .Select((display, index) => new GuestDisplayOption(
                display.Id,
                display.Name,
                display.Bounds.X,
                display.Bounds.Y,
                display.Bounds.Width,
                display.Bounds.Height,
                index + 1,
                string.Equals(display.Id, operatorId, StringComparison.Ordinal)))
            .ToList();
        if (draft.GuestDisplayId is not { } bound)
        {
            return new EventSetupGuestDisplayView(options, null, null, GuestDisplayState.None, false);
        }

        return DisplayMatching.Find(displays, bound) is { } connected
            ? new EventSetupGuestDisplayView(options, bound, connected.Name, GuestDisplayState.Connected, DisplayRules.IsSingleScreen(displays, bound, OperatorDisplayId()))
            : new EventSetupGuestDisplayView(options, bound, draft.GuestDisplayName, GuestDisplayState.Missing, false);
    }

    // Opening and closing

    /// <summary>A draft whose surface was left without closing it is dropped; an open or loading one blocks a second.</summary>
    private bool SetupInUse()
    {
        DropStaleSetup();
        return _setupLoading || _setupDraft is not null;
    }

    private void DropStaleSetup()
    {
        if (_setupDraft is not null && _operatorSurface != OperatorSurface.EventSetup)
        {
            CloseSetup();
        }
    }

    /// <summary>Shows <paramref name="draft"/> as Event setup: composes the blank Template previews and opens the bound camera, if any.</summary>
    private void OpenSetup(EventSetupDraft draft)
    {
        CancelSetupWork();
        _setupDraft = draft;
        _blankPreviewKeys.Clear();
        _operatorSurface = OperatorSurface.EventSetup;
        var request = ++_setupRequest;
        _setupCancellation = new CancellationTokenSource();
        var token = _setupCancellation.Token;
        foreach (var template in _dependencies.Catalog.Templates)
        {
            StartBlankPreview(template.Id, request, token);
        }

        RefreshSetupCamera();
    }

    /// <summary>Closes Event setup: drops the draft, goes back to the Event list, hides the identify overlays, and releases the camera unless the running Event holds it.</summary>
    private void CloseSetup()
    {
        _setupRequest++;
        CancelSetupWork();
        _setupDraft = null;
        _blankPreviewKeys.Clear();
        _operatorSurface = OperatorSurface.EventList;
        if (_confirmation is { Kind: ConfirmationKind.LeaveSetup })
        {
            _confirmation = null;
        }

        HideIdentifyDisplays();
        if (!IsCameraHeld && _selectedCameraId is not null)
        {
            ReleaseCamera();
        }

        OpenPendingStartFailure();
    }

    // Shows a start failure that waited for the operator to leave another draft: Event setup for the failed Event, with the reason.
    private void OpenPendingStartFailure()
    {
        if (_pendingStartFailure is not { } pending || _setupDraft is not null)
        {
            return;
        }

        _pendingStartFailure = null;
        BeginLoadSetup(pending.EventId, pending.Failure);
    }

    private void CancelSetupWork()
    {
        _setupCancellation?.Cancel();
        _setupCancellation?.Dispose();
        _setupCancellation = null;
    }

    /// <summary>
    /// Opens the draft's bound camera for the Operator preview when it is listed, no Event runs, and the slot doesn't already hold it.
    /// Setup calls it when it opens and after a failed start; the running Event's end calls it too (the camera was not checked meanwhile).
    /// </summary>
    internal void RefreshSetupCamera()
    {
        if (_setupDraft is not { CameraDeviceId: { } bound } || IsCameraHeld || _cameraDiagnosticsOpen || _cameraDiagnosticsOverride is not null)
        {
            return;
        }

        var listed = _cameras.Any(camera => string.Equals(camera.DeviceId, bound, StringComparison.Ordinal));
        var held = string.Equals(_selectedCameraId, bound, StringComparison.Ordinal) && _cameraState != CameraState.Idle;
        if (listed && !held)
        {
            OpenCameraFor(bound, PreviewTarget.Operator);
        }
    }

    private void StartBlankPreview(string templateId, int request, CancellationToken token) =>
        ComposeBlankPreviewAsync(templateId, token).ContinueWith(
            completed => Post(() =>
            {
                if (request == _setupRequest && _setupDraft is not null && completed.IsCompletedSuccessfully && completed.Result is { } key)
                {
                    _blankPreviewKeys[templateId] = key;
                    PublishChanges();
                }
            }),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private void BeginLoadSetup(string eventId, EventSetupStartFailure? failure)
    {
        _setupLoading = true;
        var request = ++_setupLoadRequest;
        LoadEventAsync(eventId, _storageCancellation.Token).ContinueWith(
            completed => Post(() => CompleteLoadSetup(request, completed.Result, failure)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>The saved Event, or null when it is gone or can't be read (logged). It never throws.</summary>
    private async Task<EventRecord?> LoadEventAsync(string eventId, CancellationToken ct)
    {
        try
        {
            return await _dependencies.Store.GetEventAsync(eventId, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // An Event that can't be read is reported as not found; the operator gets a notice and a fresh list.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BrainLog.EventLoadFailed(_logger, exception.GetType().Name);
            return null;
        }
    }

    private void CompleteLoadSetup(int request, EventRecord? record, EventSetupStartFailure? failure)
    {
        if (request != _setupLoadRequest)
        {
            return;
        }

        _setupLoading = false;
        if (record is null)
        {
            ShowNotice(OperatorNoticeCode.EventNotFound);
            RefreshEventList();
        }
        else
        {
            var draft = EventSetupDraft.FromRecord(_dependencies.Catalog, record);
            draft.StartFailure = failure;
            OpenSetup(draft);
        }

        PublishChanges();
    }

    /// <summary>
    /// Opens Event setup for the saved Event <paramref name="eventId"/> showing <paramref name="failure"/>: how a failed
    /// <c>StartEvent</c> from the Event list ends (plan section 8, Start Event: "stay in Event setup with the reason"). Called by
    /// <c>StartEvent</c> (B1b) on the executor; the Event is loaded off the executor, so the setup appears, and publishes, when it arrives.
    /// </summary>
    internal void OpenEventSetupWithStartFailure(string eventId, EventSetupStartFailure failure)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventId);
        ArgumentNullException.ThrowIfNull(failure);
        DropStaleSetup();
        if (_setupDraft is { } open)
        {
            // The operator opened another draft while the start ran. A draft of the same Event shows the failure now. For any other
            // Event the failure waits (the protocol has no notice code for it) and Event setup for that Event opens with it as soon
            // as the open draft closes; the operator's work is never replaced.
            if (string.Equals(open.EventId, eventId, StringComparison.Ordinal))
            {
                open.StartFailure = failure;
                RefreshSetupCamera();
            }
            else
            {
                _pendingStartFailure = (eventId, failure);
            }

            return;
        }

        BeginLoadSetup(eventId, failure);
    }

    // A start failure that arrived while another draft was open or loading: shown when that draft closes (see CloseSetup).
    private (string EventId, EventSetupStartFailure Failure)? _pendingStartFailure;

    // Commands that edit the draft

    /// <summary>Applies one edit to the open draft. Not available without a draft or while a save is in flight; a successful edit clears the last start failure.</summary>
    private CommandResultMessage Edit(string id, Func<EventSetupDraft, bool> change)
    {
        if (_setupDraft is not { Saving: false } draft || !change(draft))
        {
            return NotAvailable(id);
        }

        draft.StartFailure = null;
        return CommandResultMessage.Accepted(id);
    }

    private CommandResultMessage ChangeEventField(string id, ChangeEventFieldPayload payload) =>
        Edit(id, draft =>
        {
            draft.SetField(payload.Field, payload.Value);
            return true;
        });

    /// <summary><c>SelectCamera</c>: binds the camera and, unless an Event runs, opens it for the Operator preview.</summary>
    private CommandResultMessage SelectCamera(string id, SelectCameraPayload payload) =>
        Edit(id, draft =>
        {
            if (_cameras.FirstOrDefault(camera => string.Equals(camera.DeviceId, payload.DeviceId, StringComparison.Ordinal)) is not { } camera)
            {
                return false;
            }

            draft.BindCamera(camera.DeviceId, camera.Name);
            if (!IsCameraHeld)
            {
                OpenCameraFor(camera.DeviceId, PreviewTarget.Operator);
            }

            return true;
        });

    /// <summary><c>SelectGuestDisplay</c>: binds the display; the brain never substitutes another one.</summary>
    private CommandResultMessage SelectGuestDisplay(string id, SelectGuestDisplayPayload payload) =>
        Edit(id, draft =>
        {
            if (DisplayMatching.Find(CurrentDisplays, payload.DisplayId) is not { } display)
            {
                return false;
            }

            draft.BindGuestDisplay(display.Id, display.Name);
            return true;
        });

    /// <summary>
    /// <c>IdentifyDisplays</c>: allowed on the Event setup surface. Wave A implemented it: <see cref="ShowIdentifyDisplays"/>
    /// (Brain.Operator.cs) runs the sequence and the 3-second timer; B1a hides it with <see cref="HideIdentifyDisplays"/> when leaving setup.
    /// </summary>
    private CommandResultMessage IdentifyDisplays(string id)
    {
        if (_operatorSurface != OperatorSurface.EventSetup)
        {
            return NotAvailable(id);
        }

        ShowIdentifyDisplays();
        return CommandResultMessage.Accepted(id);
    }

    private CommandResultMessage SetMirror(string id, SetMirrorPayload payload) =>
        Edit(id, draft =>
        {
            draft.SetMirror(payload.Enabled);
            return true;
        });

    /// <summary><c>SetCountdown</c>: 3, 5, or 10 seconds; any other value is <c>validationFailed</c> and changes nothing.</summary>
    private CommandResultMessage SetCountdown(string id, SetCountdownPayload payload)
    {
        if (_setupDraft is { Saving: false } && !EventSetupLimits.IsValidCountdown(payload.Seconds))
        {
            return CommandResultMessage.Rejected(id, RejectReason.ValidationFailed);
        }

        return Edit(id, draft =>
        {
            draft.SetCountdown(payload.Seconds);
            return true;
        });
    }

    private CommandResultMessage SetOfferedTemplates(string id, SetOfferedTemplatesPayload payload) =>
        Edit(id, draft =>
        {
            draft.SetOfferedTemplates(payload.TemplateIds);
            return true;
        });

    private CommandResultMessage SetDefaultTemplate(string id, SetDefaultTemplatePayload payload) =>
        Edit(id, draft => draft.SetDefaultTemplate(payload.TemplateId));

    private CommandResultMessage SetOfferedLooks(string id, SetOfferedLooksPayload payload) =>
        Edit(id, draft =>
        {
            draft.SetOfferedLooks(payload.LookIds);
            return true;
        });

    private CommandResultMessage SetDefaultLook(string id, SetDefaultLookPayload payload) =>
        Edit(id, draft => draft.SetDefaultLook(payload.LookId));

    /// <summary><c>SetPhaseTimer</c>: any value is kept so a half-typed number stays visible; a value out of range is reported as <c>timers.&lt;phase&gt;</c>.</summary>
    private CommandResultMessage SetPhaseTimer(string id, SetPhaseTimerPayload payload) =>
        Edit(id, draft =>
        {
            draft.SetTimer(PhaseTimerRules.FromProtocol(payload.Phase), payload.Seconds);
            return true;
        });

    private CommandResultMessage SetGuestGallery(string id, SetGuestGalleryPayload payload) =>
        Edit(id, draft =>
        {
            draft.SetGuestGallery(payload.Enabled);
            return true;
        });

    /// <summary><c>SetPin</c>: the PIN and its confirmation are held in the draft only until saved, never echoed, logged, or kept in a presentation.</summary>
    private CommandResultMessage SetPin(string id, SetPinPayload payload) =>
        Edit(id, draft =>
        {
            draft.SetPin(payload.Pin, payload.Confirmation);
            return true;
        });

    /// <summary><c>SaveEvent</c>: validates, then writes the Event (and a pending PIN) off the executor and returns to the Event list.</summary>
    private CommandResultMessage SaveEvent(string id) =>
        _setupDraft is { Saving: false } draft ? BeginSave(id, draft, startAfterSave: false) : NotAvailable(id);

    /// <summary><c>SaveAndStartEvent</c>: as save, but a PIN is needed, and the saved Event is then started through <see cref="TryStartEventAsync"/> (B1b).</summary>
    private CommandResultMessage SaveAndStartEvent(string id)
    {
        if (IsCameraHeld)
        {
            return CommandResultMessage.Rejected(id, RejectReason.EventRunning);
        }

        return _setupDraft is { Saving: false } draft ? BeginSave(id, draft, startAfterSave: true) : NotAvailable(id);
    }

    private CommandResultMessage BeginSave(string id, EventSetupDraft draft, bool startAfterSave)
    {
        if (draft.EventId is { } savedId && IsHeld(savedId))
        {
            return CommandResultMessage.Rejected(id, RejectReason.EventRunning);
        }

        draft.ShowErrors = true;
        var errors = EventSetupValidator.Validate(draft);
        if (!(startAfterSave ? EventSetupValidator.CanStart(errors) : EventSetupValidator.CanSave(errors)))
        {
            return CommandResultMessage.Rejected(id, RejectReason.ValidationFailed);
        }

        draft.StartFailure = null;
        draft.Saving = true;
        var request = _setupRequest;
        var create = draft.IsNew;
        var eventId = draft.EventId ?? StorageIds.NewId();
        var record = draft.ToRecord(eventId);
        var pin = draft.PendingPin;
        var listRequest = ++_eventListRequest;
        SaveEventAsync(record, create, pin, _storageCancellation.Token).ContinueWith(
            completed => Post(() => CompleteSave(request, draft, listRequest, eventId, pin is not null, completed.Result, startAfterSave)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return CommandResultMessage.Accepted(id);
    }

    // Writes the Event row, then the pending PIN, then re-lists. The PIN is only an argument here: nothing logs it, and a failure
    // is logged by its type and kind, never its message.
    private async Task<StoreOutcome> SaveEventAsync(EventRecord record, bool create, string? pin, CancellationToken ct)
    {
        var persisted = false;
        try
        {
            if (create)
            {
                await _dependencies.Store.CreateEventAsync(record, ct).ConfigureAwait(false);
            }
            else
            {
                await _dependencies.Store.UpdateEventAsync(record, ct).ConfigureAwait(false);
            }

            persisted = true;
            if (pin is not null)
            {
                await _dependencies.Store.SetPinAsync(record.Id, pin, ct).ConfigureAwait(false);
            }
        }
        catch (StorageException exception)
        {
            BrainLog.EventSaveFailed(_logger, exception.GetType().Name, exception.Kind);
            return new StoreOutcome(false, exception.Kind, persisted && create, await ListQuietlyAsync(ct).ConfigureAwait(false));
        }
#pragma warning disable CA1031 // Whatever the store throws, the operator gets the save notice and keeps the draft.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BrainLog.EventSaveFailed(_logger, exception.GetType().Name, StorageErrorKind.Unknown);
            return new StoreOutcome(false, StorageErrorKind.Unknown, persisted && create, await ListQuietlyAsync(ct).ConfigureAwait(false));
        }

        return new StoreOutcome(true, null, create, await ListQuietlyAsync(ct).ConfigureAwait(false));
    }

    private void CompleteSave(int request, EventSetupDraft draft, int listRequest, string eventId, bool pinWasPending, StoreOutcome outcome, bool startAfterSave)
    {
        ApplySummaries(listRequest, outcome.Summaries);
        if (request != _setupRequest || !ReferenceEquals(_setupDraft, draft))
        {
            PublishChanges();
            return;
        }

        if (!outcome.Succeeded)
        {
            draft.Saving = false;
            if (outcome.RecordPersisted)
            {
                // The row exists but a later step failed: the next save updates it instead of creating it again.
                draft.MarkCreated(eventId);
            }

            ShowNotice(outcome.Kind == StorageErrorKind.NotFound ? OperatorNoticeCode.EventNotFound : OperatorNoticeCode.SaveFailed);
            PublishChanges();
            return;
        }

        draft.MarkSaved(eventId, draft.HasSavedPin || pinWasPending);
        if (startAfterSave)
        {
            StartSavedEvent(request, draft, eventId);
        }
        else
        {
            CloseSetup();
        }

        PublishChanges();
    }

    // SAVE AND START: the saved Event starts through Start Event (B1b). Success closes setup (the Event now holds the camera); a failure
    // keeps the draft open with the reason, and the camera is checked again because the failed start released it.
    private void StartSavedEvent(int request, EventSetupDraft draft, string eventId) =>
        (_startEventRunner ?? TryStartEventAsync)(eventId).ContinueWith(
            completed => Post(() =>
            {
                if (request != _setupRequest || !ReferenceEquals(_setupDraft, draft))
                {
                    _ = completed.Exception;
                    return;
                }

                if (completed.IsCompletedSuccessfully && completed.Result.Succeeded)
                {
                    CloseSetup();
                }
                else
                {
                    var result = completed.IsCompletedSuccessfully ? completed.Result : StartEventResult.Failed(StartFailureCode.InvalidSetup);
                    draft.Saving = false;

                    // An end by the operator is not a failure: the draft stays as it was, with nothing to report.
                    if (!result.WasEnded)
                    {
                        draft.StartFailure = new EventSetupStartFailure(result.Failure ?? StartFailureCode.InvalidSetup, result.CameraReason);
                    }

                    RefreshSetupCamera();
                }

                PublishChanges();
            }),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    /// <summary><c>LeaveEventSetup</c>: straight back to the Event list when nothing changed, otherwise the <c>leaveSetup</c> confirmation.</summary>
    private CommandResultMessage LeaveEventSetup(string id)
    {
        if (_setupDraft is not { Saving: false } draft)
        {
            return NotAvailable(id);
        }

        if (draft.IsDirty)
        {
            SetConfirmation(new ConfirmationOverlay(ConfirmationKind.LeaveSetup, EventSetupLimits.NormalizeOptional(draft.Name), null));
        }
        else
        {
            CloseSetup();
        }

        return CommandResultMessage.Accepted(id);
    }

    /// <summary><c>ConfirmLeave</c>: <c>save</c> is <c>SaveEvent</c>, <c>discardChanges</c> closes setup, <c>keepEditing</c> closes the dialog.</summary>
    private CommandResultMessage ConfirmLeave(string id, ConfirmLeavePayload payload)
    {
        if (_confirmation is not { Kind: ConfirmationKind.LeaveSetup } || _setupDraft is not { Saving: false } draft)
        {
            return NotAvailable(id);
        }

        _confirmation = null;
        switch (payload.Choice)
        {
            case LeaveChoice.KeepEditing:
                break;
            case LeaveChoice.DiscardChanges:
                CloseSetup();
                break;
            default:
                return BeginSave(id, draft, startAfterSave: false);
        }

        return CommandResultMessage.Accepted(id);
    }
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading an Event for Event setup failed ({ErrorType}).")]
    public static partial void EventLoadFailed(ILogger logger, string errorType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Saving an Event failed ({ErrorType}, {Kind}).")]
    public static partial void EventSaveFailed(ILogger logger, string errorType, StorageErrorKind kind);
}

