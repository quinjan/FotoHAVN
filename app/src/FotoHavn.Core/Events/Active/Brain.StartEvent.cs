using FotoHavn.Core.Displays;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// Start Event (plan section 8, Start Event steps 1 to 9; M6 contract section 9.5). Owner: M6 wave B1b. The sequence runs on the
// executor and never waits: every step that needs I/O or time starts it and posts the next step back. A failure at any step
// rolls back what the earlier steps did, in reverse, and completes the task with the step's failure code.
public sealed partial class Brain
{
    /// <summary>How long Start Event waits for the bound camera's fresh frame (step 4) before the camera isn't eligible.</summary>
    internal static readonly TimeSpan StartCameraTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How long Start Event waits for the blank previews (step 6); they warm a cache, so a slow one never stops the start.</summary>
    internal static readonly TimeSpan StartPreviewTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How long Start Event waits for the Guest Screen's UI handshake (step 8).</summary>
    internal static readonly TimeSpan GuestScreenReadyTimeout = TimeSpan.FromSeconds(10);

    /// <summary>How often the running Event reopens a camera that isn't available, so Start unavailable recovers by itself.</summary>
    internal static readonly TimeSpan CameraRecoveryInterval = TimeSpan.FromSeconds(3);

    // The start in progress; null when none is. Only one runs at a time, and none starts while an Event runs.
    private StartSequence? _start;

    /// <summary>
    /// Runs Start Event steps 1 to 9 for the saved Event <paramref name="eventId"/> and completes with how it ended. Called on the
    /// executor and returns at once; the task completes on any thread, and the caller posts what it does with the result back to the
    /// executor (it never touches brain state from a continuation). B1a's <c>SaveAndStartEvent</c> calls it after a successful
    /// save; <c>StartEvent</c> calls it for the Event list's Start. On success the Event is running (<see cref="ActiveEventId"/>
    /// is set, the camera, wake lock, and Guest Screen are held); on failure nothing is left held.
    /// </summary>
    internal Task<StartEventResult> TryStartEventAsync(string eventId)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventId);
        if (_activeEvent is not null || _start is not null)
        {
            return Task.FromResult(StartEventResult.Failed(StartFailureCode.EventRunning));
        }

        var sequence = new StartSequence(this, eventId);
        _start = sequence;
        sequence.Begin();
        return sequence.Result;
    }

    /// <summary>
    /// A guest connection completed its handshake and got its <c>welcome</c>: the brain treats that as "Guest Screen ready" (Start
    /// Event step 8). The headless host's guest client does the same. Idempotent: it only matters while a start waits for it.
    /// </summary>
    private void OnGuestScreenReady() => _start?.GuestScreenReady();

    private bool GuestConnected() => _connections.Exists(connection => connection.Role == Role.Guest && connection.Handshaken);

    /// <summary>Reopens the bound camera every <see cref="CameraRecoveryInterval"/> while it isn't available; its fresh frames then make Start available again.</summary>
    private void StartCameraRecovery(ActiveEventState active)
    {
        active.CameraRecovery?.Dispose();
        active.CameraRecovery = _dependencies.Clock.Schedule(CameraRecoveryInterval, () => Post(() => RecoverCamera(active)));
    }

    private void RecoverCamera(ActiveEventState active)
    {
        if (!ReferenceEquals(_activeEvent, active))
        {
            return;
        }

        if ((_cameraState is CameraState.NotAvailable or CameraState.InUseByAnotherApp) && active.Event.CameraDeviceId is { } deviceId)
        {
            OpenCameraFor(deviceId, DesiredPreviewTarget());
            PublishChanges();
        }

        StartCameraRecovery(active);
    }

    /// <summary>One run of the sequence. Brain state is touched only on the executor, in the handlers it posts.</summary>
    private sealed class StartSequence(Brain brain, string eventId)
    {
        private readonly TaskCompletionSource<StartEventResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private EventRecord? _record;
        private DisplayInfo? _display;
        private ActiveEventState? _state;
        private IDisposable? _timeout;
        private Action? _cameraObserver;
        private bool _finished;
        private bool _cameraTaken;
        private bool _awaitingCamera;
        private bool _previewsDone;
        private bool _wakeLockTaken;
        private bool _guestScreenOpened;
        private bool _awaitingGuest;
        private int _completedGuestCycles;

        public Task<StartEventResult> Result => _completion.Task;

        /// <summary>The Event this sequence starts; it is held from the first step on.</summary>
        public string EventId => eventId;

        /// <summary>The bound camera device, known once step 2 has loaded the Event.</summary>
        public string? CameraDeviceId => _record?.CameraDeviceId;

        /// <summary>
        /// The operator ended the Event while the sequence waited for the Guest Screen (step 8). The caller releases what the steps took,
        /// exactly once, through the End Event sequence; this only stops the waits and completes the task as ended. Later callbacks
        /// (a late handshake, the handshake timeout, a slow store answer) find the sequence finished and do nothing.
        /// </summary>
        public void Cancel()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            _awaitingGuest = false;
            EndCameraWait();
            StopTimeout();
            brain._start = null;
            _completion.TrySetResult(StartEventResult.Ended);
        }

        /// <summary>Step 2 starts: the saved Event is loaded (step 1, no other Event running, was checked by the caller).</summary>
        public void Begin()
        {
            if (!brain._storageResolved || brain._storageError is not null)
            {
                Fail(StartFailureCode.StorageNotWritable);
                return;
            }

            Task<EventRecord?> load;
            try
            {
                load = brain._dependencies.Store.GetEventAsync(eventId, CancellationToken.None);
            }
#pragma warning disable CA1031 // A store that throws instead of faulting its task is handled like one that faults.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                load = Task.FromException<EventRecord?>(exception);
            }

            Continue(load, OnLoaded);
        }

        public void GuestScreenReady()
        {
            if (_awaitingGuest && !_finished)
            {
                _awaitingGuest = false;
                Succeed();
            }
        }

        // Step 2: validate the saved setup and its PIN. Step 3: the bound display.
        private void OnLoaded(Task<EventRecord?> load)
        {
            if (!load.IsCompletedSuccessfully)
            {
                BrainLog.StartStorageFailed(brain._logger, load.Exception?.GetBaseException());
                Fail(StartFailureCode.StorageNotWritable);
                return;
            }

            if (load.Result is not { } record || SavedEventRules.FirstViolation(record, brain._dependencies.Catalog) is not null)
            {
                Fail(StartFailureCode.InvalidSetup);
                return;
            }

            if (!record.HasPin)
            {
                Fail(StartFailureCode.PinMissing);
                return;
            }

            _record = record;
            if (DisplayMatching.Find(brain.CurrentDisplays, record.GuestDisplayId) is not { } display)
            {
                Fail(StartFailureCode.GuestDisplayMissing);
                return;
            }

            _display = display;
            OpenCamera(record);
        }

        // Step 4: open the bound camera and wait for its fresh frame (the brain's camera runtime proves it; this adds a timeout).
        private void OpenCamera(EventRecord record)
        {
            _cameraTaken = true;
            _awaitingCamera = true;
            _cameraObserver = () => brain.Post(OnCameraChanged);
            brain.CameraStateChanged += _cameraObserver;
            _timeout = brain._dependencies.Clock.Schedule(StartCameraTimeout, () => brain.Post(OnCameraTimeout));
            brain.OpenCameraFor(record.CameraDeviceId!, PreviewTarget.None);
        }

        private void OnCameraChanged()
        {
            if (_finished || !_awaitingCamera)
            {
                return;
            }

            switch (brain._cameraState)
            {
                case CameraState.Ready:
                    EndCameraWait();
                    CheckStorage();
                    break;
                case CameraState.Checking:
                    break;
                default:
                    EndCameraWait();
                    Fail(StartFailureCode.CameraNotEligible, brain._cameraReason ?? CameraFailureKind.Unknown);
                    break;
            }
        }

        private void OnCameraTimeout()
        {
            if (_finished || !_awaitingCamera)
            {
                return;
            }

            _timeout = null;
            EndCameraWait();
            Fail(StartFailureCode.CameraNotEligible, brain._cameraReason ?? CameraFailureKind.Stale);
        }

        private void EndCameraWait()
        {
            _awaitingCamera = false;
            StopTimeout();
            if (_cameraObserver is { } observer)
            {
                brain.CameraStateChanged -= observer;
                _cameraObserver = null;
            }
        }

        // Step 5: storage accepts a write. The probe (a flushed write and delete under the Data folder) proves it, then the same listing the
        // Event list uses brings the completed Guest Cycle count the panel shows and refreshes the list.
        private void CheckStorage()
        {
            Task probe;
            try
            {
                probe = brain._dependencies.Store.ProbeWritableAsync(CancellationToken.None);
            }
#pragma warning disable CA1031 // A store that throws instead of faulting its task is handled like one that faults.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                probe = Task.FromException(exception);
            }

            Continue(probe, OnStorageProbed);
        }

        private void OnStorageProbed(Task probe)
        {
            if (!probe.IsCompletedSuccessfully)
            {
                BrainLog.StartStorageFailed(brain._logger, probe.Exception?.GetBaseException());
                Fail(StartFailureCode.StorageNotWritable);
                return;
            }

            Task<IReadOnlyList<EventSummary>> list;
            try
            {
                list = brain._dependencies.Store.ListEventsAsync(CancellationToken.None);
            }
#pragma warning disable CA1031 // A store that throws instead of faulting its task is handled like one that faults.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                list = Task.FromException<IReadOnlyList<EventSummary>>(exception);
            }

            Continue(list, OnStorageChecked);
        }

        private void OnStorageChecked(Task<IReadOnlyList<EventSummary>> list)
        {
            if (!list.IsCompletedSuccessfully)
            {
                BrainLog.StartStorageFailed(brain._logger, list.Exception?.GetBaseException());
                Fail(StartFailureCode.StorageNotWritable);
                return;
            }

            brain._eventSummaries = [.. list.Result];
            _completedGuestCycles = list.Result.FirstOrDefault(summary => string.Equals(summary.Id, eventId, StringComparison.Ordinal))?.CompletedGuestCycles ?? 0;
            WarmPreviews();
        }

        // Step 6: the blank Template previews. Never a failure: a missing preview is just missing.
        private void WarmPreviews()
        {
            var warming = _record!.OfferedTemplateIds
                .Select(templateId => brain.ComposeBlankPreviewAsync(templateId, CancellationToken.None))
                .ToArray();
            _timeout = brain._dependencies.Clock.Schedule(StartPreviewTimeout, () => brain.Post(OnPreviewsDone));
            Task.WhenAll(warming).ContinueWith(
                _ => brain.Post(OnPreviewsDone),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void OnPreviewsDone()
        {
            if (_finished || _previewsDone)
            {
                return;
            }

            _previewsDone = true;
            StopTimeout();
            AcquireWakeLock();
        }

        // Step 7.
        private void AcquireWakeLock()
        {
            brain.EventHeld(eventId);
            _wakeLockTaken = true;
            OpenGuestScreen();
        }

        // Step 8: open the Guest Screen and wait for its UI handshake. The display is looked up again: steps 4 to 7 took time.
        private void OpenGuestScreen()
        {
            if (DisplayMatching.Find(brain.CurrentDisplays, _record!.GuestDisplayId) is not { } display)
            {
                Fail(StartFailureCode.GuestDisplayMissing);
                return;
            }

            _display = display;
            var singleScreen = DisplayRules.IsSingleScreen(brain.CurrentDisplays, display.Id, brain.OperatorDisplayId());
            _state = new ActiveEventState(_record, brain._dependencies.Clock.UtcNow, display, singleScreen) { CompletedGuestCycles = _completedGuestCycles };
            brain._activeEvent = _state;
            BrainLog.GuestScreenOpening(brain._logger, display.Id, singleScreen);
            try
            {
                brain._dependencies.Host.OpenGuestScreen(display);
            }
#pragma warning disable CA1031 // A host that can't open the window is a failed step, whatever it threw.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                BrainLog.StartGuestScreenFailed(brain._logger, exception);
                Fail(StartFailureCode.GuestScreenNotReady);
                return;
            }

            _guestScreenOpened = true;
            brain._guestScreenDisplay = display;
            if (brain.GuestConnected())
            {
                Succeed();
                return;
            }

            _awaitingGuest = true;
            _timeout = brain._dependencies.Clock.Schedule(GuestScreenReadyTimeout, () => brain.Post(OnGuestTimeout));
            brain.PublishChanges();
        }

        private void OnGuestTimeout()
        {
            if (_finished || !_awaitingGuest)
            {
                return;
            }

            _timeout = null;
            _awaitingGuest = false;
            BrainLog.StartGuestScreenTimedOut(brain._logger);
            Fail(StartFailureCode.GuestScreenNotReady);
        }

        // Step 9: the Event is running; the operator shows the Event list with the panel (the presentation follows from the state).
        private void Succeed()
        {
            StopTimeout();
            _finished = true;
            brain._start = null;
            brain.SetPreviewTarget(PreviewTarget.Guest);
            brain.StartCameraRecovery(_state!);
            BrainLog.EventStarted(brain._logger, eventId);
            brain.PublishChanges();
            _completion.TrySetResult(StartEventResult.Started);
        }

        /// <summary>Undoes what the steps before this one did, in reverse, and completes with <paramref name="code"/>. Nothing stays held.</summary>
        private void Fail(StartFailureCode code, CameraFailureKind? cameraReason = null)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            EndCameraWait();
            if (_guestScreenOpened)
            {
                brain._dependencies.Host.CloseGuestScreen();
                brain._guestScreenDisplay = null;
            }

            if (_wakeLockTaken)
            {
                brain.EventReleased();
            }

            if (_cameraTaken)
            {
                brain.ReleaseCamera();
            }

            if (_state is not null && ReferenceEquals(brain._activeEvent, _state))
            {
                brain._activeEvent = null;
            }

            if (brain._confirmation?.Kind == ConfirmationKind.CloseApp)
            {
                // The Event this dialog was about is gone.
                brain._confirmation = null;
            }

            brain._start = null;
            BrainLog.StartFailed(brain._logger, code);
            brain.PublishChanges();
            _completion.TrySetResult(StartEventResult.Failed(code, cameraReason));
        }

        private void StopTimeout()
        {
            _timeout?.Dispose();
            _timeout = null;
        }

        /// <summary>Posts <paramref name="next"/> back to the executor when <paramref name="task"/> completes, unless the sequence has ended meanwhile.</summary>
        private void Continue<TTask>(TTask task, Action<TTask> next)
            where TTask : Task => task.ContinueWith(
            completed => brain.Post(() =>
            {
                if (!_finished)
                {
                    next(task);
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
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Event {EventId} started.")]
    public static partial void EventStarted(ILogger logger, string eventId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting the Event stopped: {Code}.")]
    public static partial void StartFailed(ILogger logger, StartFailureCode code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Starting the Event: storage didn't answer.")]
    public static partial void StartStorageFailed(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Starting the Event: the host couldn't open the Guest Screen.")]
    public static partial void StartGuestScreenFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Starting the Event: the Guest Screen's UI never finished its handshake.")]
    public static partial void StartGuestScreenTimedOut(ILogger logger);
}
