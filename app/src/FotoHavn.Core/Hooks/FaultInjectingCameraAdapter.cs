using FotoHavn.Core.Camera;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Hooks;

/// <summary>
/// The E2E test hooks' handle on the camera: <c>test.injectCameraFault</c> and <c>test.clearCameraFault</c> drive it. The
/// <see cref="FaultInjectingCameraAdapter"/> implements it; E2E hosts pass the same object as
/// <c>BrainDependencies.CameraFaults</c> (E2E builds only) and as <see cref="BrainDependencies.Camera"/>.
/// </summary>
public interface ICameraFaultControl
{
    /// <summary>Makes the camera fail the way <paramref name="kind"/> says, until <see cref="ClearFault"/>.</summary>
    void InjectFault(CameraFaultKind kind);

    /// <summary>Removes the injected fault: frames flow again, a removed camera is listed again, and opens succeed.</summary>
    void ClearFault();
}

/// <summary>
/// Wraps a real or fake <see cref="ICameraAdapter"/> so E2E tests can make its sessions fail at the session level, which is
/// where the brain observes failures: <c>stale</c> stops frames (the session's <see cref="ICameraSession.LatestFrame"/> stops
/// advancing while <see cref="ICameraSession.Now"/> does not, and preview stops), <c>failed</c> and <c>exclusiveLost</c> raise
/// <see cref="ICameraSession.Failed"/>, and <c>removed</c> raises <c>Failed(Removed)</c> and hides the device from
/// <see cref="ListAsync"/>. Like the storage fault decorator it is always compiled and does nothing until a hook calls it.
/// </summary>
/// <remarks>
/// While a fault is injected, new opens behave as the real camera would: <c>failed</c>, <c>exclusiveLost</c>, and
/// <c>removed</c> fail the open with <see cref="CameraException"/> (<c>Failed</c>, <c>ExclusiveLost</c>, <c>NotFound</c>), and
/// <c>stale</c> opens a session that delivers no frames. <see cref="ClearFault"/> undoes all of it; a session that already
/// failed stays dead, so the brain opens a new one.
/// </remarks>
public sealed class FaultInjectingCameraAdapter : ICameraAdapter, ICameraFaultControl
{
    private readonly ICameraAdapter _inner;
    private readonly object _gate = new();
    private readonly List<FaultSession> _sessions = [];
    private CameraFaultKind? _fault;
    private string? _lastDeviceId;
    private string? _hiddenDeviceId;

    public FaultInjectingCameraAdapter(ICameraAdapter inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _inner.CamerasChanged += (_, _) => CamerasChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CamerasChanged;

    /// <summary>The fault in force, or null.</summary>
    public CameraFaultKind? ActiveFault
    {
        get
        {
            lock (_gate)
            {
                return _fault;
            }
        }
    }

    public async Task<IReadOnlyList<AvailableCamera>> ListAsync(CancellationToken cancellationToken)
    {
        var cameras = await _inner.ListAsync(cancellationToken).ConfigureAwait(false);
        string? hidden;
        lock (_gate)
        {
            hidden = _hiddenDeviceId;
        }

        return hidden is null ? cameras : [.. cameras.Where(camera => !string.Equals(camera.DeviceId, hidden, StringComparison.Ordinal))];
    }

    public async Task<ICameraSession> OpenAsync(string deviceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ThrowIfOpenFails(deviceId);
        var inner = await _inner.OpenAsync(deviceId, cancellationToken).ConfigureAwait(false);
        FaultSession? session = null;
        CameraException? failure;
        lock (_gate)
        {
            failure = OpenFailure(deviceId);
            if (failure is null)
            {
                // A fault injected while the open was running applies to the new session too.
                session = new FaultSession(this, inner, frozen: _fault == CameraFaultKind.Stale);
                _sessions.Add(session);
                _lastDeviceId = deviceId;
            }
        }

        if (failure is not null)
        {
            await ReleaseQuietlyAsync(inner).ConfigureAwait(false);
            throw failure;
        }

        return session!;
    }

    public void InjectFault(CameraFaultKind kind)
    {
        FaultSession[] live;
        var listChanged = false;
        lock (_gate)
        {
            _fault = kind;
            if (kind == CameraFaultKind.Removed && _hiddenDeviceId is null && _lastDeviceId is not null)
            {
                _hiddenDeviceId = _lastDeviceId;
                listChanged = true;
            }

            live = [.. _sessions];
        }

        foreach (var session in live)
        {
            switch (kind)
            {
                case CameraFaultKind.Stale:
                    session.Freeze();
                    break;
                case CameraFaultKind.Failed:
                    session.Kill(CameraFailureKind.Failed);
                    break;
                case CameraFaultKind.Removed:
                    session.Kill(CameraFailureKind.Removed);
                    break;
                default:
                    session.Kill(CameraFailureKind.ExclusiveLost);
                    break;
            }
        }

        if (listChanged)
        {
            CamerasChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearFault()
    {
        FaultSession[] live;
        bool listChanged;
        lock (_gate)
        {
            _fault = null;
            listChanged = _hiddenDeviceId is not null;
            _hiddenDeviceId = null;
            live = [.. _sessions];
        }

        foreach (var session in live)
        {
            session.Thaw();
        }

        if (listChanged)
        {
            CamerasChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ThrowIfOpenFails(string deviceId)
    {
        lock (_gate)
        {
            if (OpenFailure(deviceId) is { } failure)
            {
                throw failure;
            }
        }
    }

    // Callers hold the gate.
    private CameraException? OpenFailure(string deviceId) => _fault switch
    {
        CameraFaultKind.Failed => new CameraException(CameraFailureKind.Failed, "Injected fault: the camera session failed."),
        CameraFaultKind.ExclusiveLost => new CameraException(CameraFailureKind.ExclusiveLost, "Injected fault: exclusive control was lost."),
        CameraFaultKind.Removed when string.Equals(deviceId, _hiddenDeviceId, StringComparison.Ordinal) =>
            new CameraException(CameraFailureKind.NotFound, "Injected fault: the camera was removed."),
        _ => null,
    };

    private void Forget(FaultSession session)
    {
        lock (_gate)
        {
            _sessions.Remove(session);
        }
    }

    private static async Task ReleaseQuietlyAsync(ICameraSession session)
    {
        try
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Releasing a session the caller never saw must not fail the open that is already failing.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <summary>A session that can be frozen (no frames) or killed (Failed) on demand.</summary>
    private sealed class FaultSession : ICameraSession
    {
        private readonly FaultInjectingCameraAdapter _owner;
        private readonly ICameraSession _inner;
        private readonly object _gate = new();
        private TaskCompletionSource _thaw = NewThaw();
        private CameraFrameInfo? _frozenFrame;
        private PreviewTarget _target = PreviewTarget.None;
        private CameraFailureKind? _deadKind;
        private bool _frozen;
        private bool _failedRaised;
        private bool _disposed;

        public FaultSession(FaultInjectingCameraAdapter owner, ICameraSession inner, bool frozen)
        {
            _owner = owner;
            _inner = inner;
            _frozen = frozen;
            _inner.Failed += OnInnerFailed;
            if (frozen)
            {
                _inner.SetPreviewTarget(PreviewTarget.None);
            }
        }

        public event EventHandler<CameraFailure>? Failed;

        public string DeviceId => _inner.DeviceId;

        public CameraFormat Format => _inner.Format;

        public TimeSpan Now => _inner.Now;

        public CameraFrameInfo? LatestFrame
        {
            get
            {
                lock (_gate)
                {
                    return _frozen ? _frozenFrame : _inner.LatestFrame;
                }
            }
        }

        public void SetPreviewTarget(PreviewTarget target)
        {
            lock (_gate)
            {
                _target = target;
                if (_frozen || _deadKind is not null || _disposed)
                {
                    return;
                }
            }

            _inner.SetPreviewTarget(target);
        }

        public async Task<CapturedFrame> CaptureAsync(TimeSpan notBefore, CancellationToken cancellationToken)
        {
            while (true)
            {
                Task wait;
                lock (_gate)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    if (_deadKind is { } kind)
                    {
                        throw new CameraException(kind, "The injected fault ended the camera session.");
                    }

                    if (!_frozen)
                    {
                        break;
                    }

                    wait = _thaw.Task;
                }

                await wait.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return await _inner.CaptureAsync(notBefore, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            TaskCompletionSource thaw;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                thaw = _thaw;
            }

            _inner.Failed -= OnInnerFailed;
            _owner.Forget(this);
            thaw.TrySetResult();
            await _inner.DisposeAsync().ConfigureAwait(false);
        }

        /// <summary>Stops frames: <see cref="LatestFrame"/> keeps its value and the inner preview stops.</summary>
        public void Freeze()
        {
            lock (_gate)
            {
                if (_frozen || _deadKind is not null || _disposed)
                {
                    return;
                }

                _frozenFrame = _inner.LatestFrame;
                _frozen = true;
                _thaw = NewThaw();
            }

            _inner.SetPreviewTarget(PreviewTarget.None);
        }

        /// <summary>Resumes frames and the preview of the last target.</summary>
        public void Thaw()
        {
            PreviewTarget target;
            TaskCompletionSource thaw;
            lock (_gate)
            {
                if (!_frozen)
                {
                    return;
                }

                _frozen = false;
                _frozenFrame = null;
                target = _target;
                thaw = _thaw;
            }

            if (_deadKind is null && !_disposed)
            {
                _inner.SetPreviewTarget(target);
            }

            thaw.TrySetResult();
        }

        /// <summary>Raises <see cref="Failed"/> once and ends the session, releasing the camera like a real failed session.</summary>
        public void Kill(CameraFailureKind kind)
        {
            if (!MarkDead(kind))
            {
                return;
            }

            Failed?.Invoke(this, new CameraFailure(kind, "Injected fault."));
            _ = ReleaseQuietlyAsync(_inner);
        }

        private static TaskCompletionSource NewThaw() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private void OnInnerFailed(object? sender, CameraFailure failure)
        {
            if (MarkDead(failure.Kind))
            {
                Failed?.Invoke(this, failure);
            }
        }

        /// <summary>Marks the session dead and frees its pending captures; false when it already failed or was disposed.</summary>
        private bool MarkDead(CameraFailureKind kind)
        {
            TaskCompletionSource thaw;
            lock (_gate)
            {
                if (_failedRaised || _disposed)
                {
                    return false;
                }

                _failedRaised = true;
                _deadKind = kind;
                thaw = _thaw;
            }

            thaw.TrySetResult();
            return true;
        }
    }
}
