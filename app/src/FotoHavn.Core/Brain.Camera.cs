using System.Runtime.InteropServices;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Core.Hooks;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// The camera in the brain (plan section 11, ADR 0004, M5 contract sections 3 and 6): the brain owns the one camera session,
// opens and closes it through the adapter without ever waiting for it on the executor, and decides the camera's state from
// what the session reports: a fresh frame proves the device (ready), a Failed event or a frame older than the staleness
// limit ends it. Adapter events (CamerasChanged, Failed) only post a work item; frames never pass through the brain.
public sealed partial class Brain
{
    /// <summary>How often the brain looks at the latest frame's age.</summary>
    internal static readonly TimeSpan FreshnessPollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>How long a diagnostics Capture may wait for its frame before it fails.</summary>
    internal static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);

    private IReadOnlyList<AvailableCamera> _cameras = [];
    private int _cameraListRequest;
    private bool _cameraDiagnosticsOpen;
    private string? _selectedCameraId;
    private CameraState _cameraState = CameraState.Idle;
    private CameraFailureKind? _cameraReason;
    private PreviewTarget _previewTarget = PreviewTarget.None;
    private bool _mirrored;
    private CameraCaptureView? _lastCapture;

    // The session, its Failed handler, the freshness timer, and the open's token are also taken by Dispose from another
    // thread, so each is exchanged atomically and whoever takes it disposes it.
    private ICameraSession? _cameraSession;
    private IDisposable? _freshnessTimer;
    private CancellationTokenSource? _cameraOpenCancellation;
    private EventHandler<CameraFailure>? _sessionFailedHandler;

    // Each release or new open bumps these, so events, timer ticks, and open results of an earlier session are ignored.
    private int _cameraGeneration;
    private int _cameraOpenRequest;
    private bool _cameraOpenInFlight;
    private string? _queuedCameraId;
    private TimeSpan _cameraOpenedAt;
    private Task _cameraDisposal = Task.CompletedTask;

    private void StartCameraRuntime()
    {
        _dependencies.Camera.CamerasChanged += OnCamerasChanged;
        Post(StartCameraList);
    }

    private void OnCamerasChanged(object? sender, EventArgs e) => Post(StartCameraList);

    /// <summary>The diagnostics view of the camera runtime, or null while the surface is not open.</summary>
    private CameraDiagnosticsView? RenderCameraDiagnostics()
    {
        if (_cameraDiagnosticsOverride is { } overridden)
        {
            return overridden;
        }

        if (!_cameraDiagnosticsOpen)
        {
            return null;
        }

        var format = _cameraSession is { } session ? ToFormatView(session.Format) : null;
        return new CameraDiagnosticsView(
            [.. _cameras.Select(camera => new CameraOption(camera.DeviceId, camera.Name))],
            _selectedCameraId,
            _cameraState,
            _cameraReason,
            format,
            _mirrored,
            _lastCapture);
    }

    private static CameraFormatView ToFormatView(CameraFormat format) => new(format.Width, format.Height, format.FramesPerSecond);

    /// <summary>The selected camera as <c>test.getDiagnostics</c> reports it; frame figures are null when unknown.</summary>
    private CameraDiagnostics CameraDiagnosticsNow()
    {
        if (_cameraSession is not { } session)
        {
            return new CameraDiagnostics(_cameraState, _selectedCameraId, null, null, null, null, PreviewTarget.None);
        }

        int? ageMs = session.LatestFrame is { } latest
            ? (int)Math.Max(0, (session.Now - latest.Timestamp).TotalMilliseconds)
            : null;
        double? fps = null;
        long? dropped = null;
        if (_previewTarget != PreviewTarget.None && _dependencies.PreviewStats is { } stats)
        {
            var counters = stats.GetStats(_previewTarget);
            fps = counters.DeliveredPerSecond;
            dropped = counters.Dropped;
        }

        return new CameraDiagnostics(_cameraState, _selectedCameraId, ToFormatView(session.Format), ageMs, fps, dropped, _previewTarget);
    }

    // The camera list

    private void StartCameraList()
    {
        var request = ++_cameraListRequest;
        ListCameras().ContinueWith(
            completed => Post(() =>
            {
                CompleteCameraList(request, completed);
                PublishChanges();
            }),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private Task<IReadOnlyList<AvailableCamera>> ListCameras()
    {
        try
        {
            return _dependencies.Camera.ListAsync(CancellationToken.None);
        }
#pragma warning disable CA1031 // A camera adapter that throws instead of faulting its task is handled like one that faults.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return Task.FromException<IReadOnlyList<AvailableCamera>>(exception);
        }
    }

    /// <summary>Takes a listing as the camera list when it answers the latest request, then applies it to the selected camera.</summary>
    private void CompleteCameraList(int request, Task<IReadOnlyList<AvailableCamera>> completed)
    {
        if (!completed.IsCompletedSuccessfully)
        {
            if (completed.Exception?.GetBaseException() is { } exception)
            {
                BrainLog.CameraListFailed(_logger, exception);
            }

            return;
        }

        if (request != _cameraListRequest)
        {
            return;
        }

        _cameras = [.. completed.Result];
        if (_selectedCameraId is not { } selected)
        {
            return;
        }

        if (!_cameras.Any(camera => string.Equals(camera.DeviceId, selected, StringComparison.Ordinal)))
        {
            // The camera left the list: it is not available, and no other camera takes its place.
            if (_cameraState != CameraState.NotAvailable || _cameraReason != CameraFailureKind.Removed)
            {
                FailCamera(CameraFailureKind.Removed);
            }
        }
        else if (_cameraState == CameraState.NotAvailable && _cameraReason is CameraFailureKind.Removed or CameraFailureKind.NotFound)
        {
            // It came back: a new session, then a fresh frame, then ready.
            StartCameraOpen(selected);
        }
    }

    // Opening and closing

    /// <summary>Selects <paramref name="deviceId"/>: any open session is closed, the camera is opened, and it is checking until a fresh frame arrives.</summary>
    private void StartCameraOpen(string deviceId)
    {
        _selectedCameraId = deviceId;
        ReleaseCameraSession();
        _cameraState = CameraState.Checking;
        _cameraReason = null;
        if (_cameraOpenInFlight)
        {
            // One open at a time: the running one is cancelled and its session, if it got one, is closed on arrival.
            _queuedCameraId = deviceId;
            return;
        }

        BeginCameraOpen(deviceId);
    }

    private void BeginCameraOpen(string deviceId)
    {
        _cameraOpenInFlight = true;
        _queuedCameraId = null;
        var request = _cameraOpenRequest;
        var cancellation = new CancellationTokenSource();
        Volatile.Write(ref _cameraOpenCancellation, cancellation);

        // The adapter opens only after the previous session finished disposing, so a quick reselect doesn't see "in use".
        var open = _cameraDisposal.IsCompleted
            ? OpenCamera(deviceId, cancellation.Token)
            : _cameraDisposal.ContinueWith(_ => OpenCamera(deviceId, cancellation.Token), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();

        // Raw executor post: the result must be handled (a session closed) even when the brain was disposed meanwhile.
        open.ContinueWith(
            completed => _dependencies.Executor.Post(() => CompleteCameraOpen(deviceId, request, completed)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private Task<ICameraSession> OpenCamera(string deviceId, CancellationToken cancellationToken)
    {
        try
        {
            return _dependencies.Camera.OpenAsync(deviceId, cancellationToken);
        }
#pragma warning disable CA1031 // A camera adapter that throws instead of faulting its task is handled like one that faults.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return Task.FromException<ICameraSession>(exception);
        }
    }

    private void CompleteCameraOpen(string deviceId, int request, Task<ICameraSession> completed)
    {
        _cameraOpenInFlight = false;
        Interlocked.Exchange(ref _cameraOpenCancellation, null)?.Dispose();
        var session = completed.IsCompletedSuccessfully ? completed.Result : null;
        if (!completed.IsCompletedSuccessfully)
        {
            _ = completed.Exception;
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            DisposeInBackground(session);
            return;
        }

        if (_queuedCameraId is { } queued)
        {
            DisposeInBackground(session);
            BeginCameraOpen(queued);
            return;
        }

        if (request != _cameraOpenRequest)
        {
            DisposeInBackground(session);
            return;
        }

        if (session is null)
        {
            var kind = OpenFailureKind(completed);
            BrainLog.CameraOpenFailed(_logger, completed.Exception?.GetBaseException(), deviceId, kind);
            FailCamera(kind);
        }
        else
        {
            AttachCameraSession(session);
        }

        PublishChanges();
    }

    private static CameraFailureKind OpenFailureKind(Task completed) => completed.Exception?.GetBaseException() switch
    {
        null => CameraFailureKind.Unknown,
        CameraException cameraException => cameraException.Kind,
        var exception => CameraFailureClassifier.Classify(exception, CameraOperation.Open),
    };

    private void AttachCameraSession(ICameraSession session)
    {
        var generation = _cameraGeneration;
        EventHandler<CameraFailure> handler = (_, failure) => Post(() => OnCameraSessionFailed(generation, failure));
        _sessionFailedHandler = handler;
        session.Failed += handler;
        Volatile.Write(ref _cameraSession, session);
        if (Volatile.Read(ref _disposed) != 0)
        {
            ShutdownCamera();
            return;
        }

        _cameraOpenedAt = session.Now;
        session.SetPreviewTarget(_previewTarget);
        BrainLog.CameraOpened(_logger, session.DeviceId, session.Format.Width, session.Format.Height, session.Format.FramesPerSecond);
        ScheduleFreshnessCheck(generation);
    }

    private void OnCameraSessionFailed(int generation, CameraFailure failure)
    {
        if (generation != _cameraGeneration || _cameraSession is null)
        {
            return;
        }

        BrainLog.CameraSessionFailed(_logger, failure.Kind, failure.Detail);
        FailCamera(failure.Kind);
        PublishChanges();
    }

    /// <summary>Ends the session (if any) and moves the camera to the state its failure kind maps to.</summary>
    private void FailCamera(CameraFailureKind kind)
    {
        ReleaseCameraSession();
        (_cameraState, var reason) = CameraStateMapping.For(kind);
        _cameraReason = reason;
        BrainLog.CameraStateChanged(_logger, _cameraState, reason);
    }

    /// <summary>
    /// Stops the freshness timer, ignores the open in flight and the session's later events, and closes the session in the
    /// background. The camera's state is the caller's to set.
    /// </summary>
    private void ReleaseCameraSession()
    {
        _cameraGeneration++;
        _cameraOpenRequest++;
        Volatile.Read(ref _cameraOpenCancellation)?.Cancel();
        Interlocked.Exchange(ref _freshnessTimer, null)?.Dispose();
        if (Interlocked.Exchange(ref _cameraSession, null) is { } session)
        {
            if (_sessionFailedHandler is { } handler)
            {
                session.Failed -= handler;
            }

            _cameraDisposal = Task.WhenAll(_cameraDisposal, DisposeInBackground(session));
        }

        _sessionFailedHandler = null;
    }

    /// <summary>
    /// Stops all camera work for good, from any thread (<see cref="Dispose"/>): no more events, no timer, no open in flight, and
    /// the session closed.
    /// </summary>
    private void ShutdownCamera()
    {
        _dependencies.Camera.CamerasChanged -= OnCamerasChanged;
        Volatile.Read(ref _cameraOpenCancellation)?.Cancel();
        Interlocked.Exchange(ref _freshnessTimer, null)?.Dispose();
        if (Interlocked.Exchange(ref _cameraSession, null) is { } session)
        {
            if (_sessionFailedHandler is { } handler)
            {
                session.Failed -= handler;
            }

            _ = DisposeInBackground(session);
        }
    }

    /// <summary>Closes a session without waiting for it; the returned task always completes successfully.</summary>
    private Task DisposeInBackground(ICameraSession? session)
    {
        if (session is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            var disposal = session.DisposeAsync();
            return disposal.IsCompletedSuccessfully ? Task.CompletedTask : ObserveDisposalAsync(disposal);
        }
#pragma warning disable CA1031 // A session that fails to close can't be retried; the brain logs it and carries on.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BrainLog.CameraCloseFailed(_logger, exception);
            return Task.CompletedTask;
        }
    }

    private async Task ObserveDisposalAsync(ValueTask disposal)
    {
        try
        {
            await disposal.ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A session that fails to close can't be retried; the brain logs it and carries on.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BrainLog.CameraCloseFailed(_logger, exception);
        }
    }

    // Freshness

    private void ScheduleFreshnessCheck(int generation)
    {
        var timer = _dependencies.Clock.Schedule(FreshnessPollInterval, () => Post(() => CheckFreshness(generation)));
        if (Interlocked.Exchange(ref _freshnessTimer, timer) is { } previous)
        {
            previous.Dispose();
        }
    }

    /// <summary>
    /// Looks at the latest frame's age: while checking, a frame no older than the eligibility limit makes the camera ready (a
    /// device proves itself by delivering), and no such frame within the staleness limit of opening fails it; once ready, a
    /// frame older than the staleness limit fails it.
    /// </summary>
    private void CheckFreshness(int generation)
    {
        if (generation != _cameraGeneration || _cameraSession is not { } session)
        {
            return;
        }

        var now = session.Now;
        var latest = session.LatestFrame;
        if (_cameraState == CameraState.Checking)
        {
            if (FrameFreshness.IsFresh(now, latest, FrameFreshness.EligibleMaxAge))
            {
                _cameraState = CameraState.Ready;
                _cameraReason = null;
                BrainLog.CameraStateChanged(_logger, _cameraState, null);
            }
            else if (now - _cameraOpenedAt > FrameFreshness.StaleAfter)
            {
                FailCamera(CameraFailureKind.Stale);
                PublishChanges();
                return;
            }
        }
        else if (!FrameFreshness.IsFresh(now, latest, FrameFreshness.StaleAfter))
        {
            FailCamera(CameraFailureKind.Stale);
            PublishChanges();
            return;
        }

        ScheduleFreshnessCheck(generation);
        PublishChanges();
    }

    // Diagnostics capture

    /// <summary>
    /// <c>test.captureDiagnostics</c>: captures the first frame at or after now, mirrors it when the diagnostics mirror is on, has
    /// the host save it, and shows it as the last capture. The brain never waits: each step posts the next.
    /// </summary>
    private void CaptureDiagnosticsFrame(Connection connection, string id)
    {
        if (_hostHooks is not { } hooks)
        {
            Reply(connection, id, HookOutcome.Fail(TestHookError.NotSupported));
            return;
        }

        if (_cameraState != CameraState.Ready || _cameraSession is not { } session)
        {
            Reply(connection, id, HookOutcome.Fail(TestHookError.HookFailed));
            return;
        }

        var mirrored = _mirrored;
        var cancellation = new CancellationTokenSource();
        var timeout = _dependencies.Clock.Schedule(CaptureTimeout, cancellation.Cancel);
        Task<CapturedFrame> capture;
        try
        {
            capture = session.CaptureAsync(session.Now, cancellation.Token);
        }
#pragma warning disable CA1031 // Any failure becomes the hook's error (contract section 8).
        catch (Exception)
#pragma warning restore CA1031
        {
            timeout.Dispose();
            cancellation.Dispose();
            Reply(connection, id, HookOutcome.Fail(TestHookError.HookFailed));
            return;
        }

        capture.ContinueWith(
            completed =>
            {
                timeout.Dispose();
                cancellation.Dispose();
                var frame = completed.IsCompletedSuccessfully ? MirrorWhenAsked(completed.Result, mirrored) : null;
                _ = completed.Exception;
                Post(() => SaveDiagnosticsCapture(connection, id, hooks, frame, mirrored));
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    /// <summary>Mirrors the frame's own bytes (the caller owns them) on the thread that completed the capture, not the executor. Null when that fails.</summary>
    private CapturedFrame? MirrorWhenAsked(CapturedFrame frame, bool mirrored)
    {
        if (!mirrored)
        {
            return frame;
        }

        try
        {
            MirrorTransform.MirrorBgraInPlace(MemoryMarshal.AsMemory(frame.Bgra).Span, frame.Width, frame.Height, frame.Stride);
            return frame;
        }
#pragma warning disable CA1031 // A frame that can't be mirrored fails the hook.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BrainLog.CameraMirrorFailed(_logger, exception);
            return null;
        }
    }

    private void SaveDiagnosticsCapture(Connection connection, string id, IHostHookPort hooks, CapturedFrame? frame, bool mirrored)
    {
        if (frame is null)
        {
            Reply(connection, id, HookOutcome.Fail(TestHookError.HookFailed));
            return;
        }

        RunTask(
            connection,
            id,
            () => hooks.SaveDiagnosticsCaptureAsync(frame, mirrored),
            saved =>
            {
                _lastCapture = saved;
                PublishChanges();
                return saved;
            },
            hooks.Classify);
    }
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Listing the cameras failed.")]
    public static partial void CameraListFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Opened camera {DeviceId} at {Width}x{Height} {Fps} fps.")]
    public static partial void CameraOpened(ILogger logger, string deviceId, int width, int height, double fps);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Opening camera {DeviceId} failed ({Kind}).")]
    public static partial void CameraOpenFailed(ILogger logger, Exception? exception, string deviceId, CameraFailureKind kind);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The camera session failed ({Kind}): {Detail}")]
    public static partial void CameraSessionFailed(ILogger logger, CameraFailureKind kind, string detail);

    [LoggerMessage(Level = LogLevel.Information, Message = "The camera is {State} ({Reason}).")]
    public static partial void CameraStateChanged(ILogger logger, CameraState state, CameraFailureKind? reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Closing a camera session failed.")]
    public static partial void CameraCloseFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mirroring a diagnostics capture failed.")]
    public static partial void CameraMirrorFailed(ILogger logger, Exception exception);
}
