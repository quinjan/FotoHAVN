using FotoHavn.Core.Camera;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Fakes;

/// <summary>
/// An open <see cref="FakeCameraAdapter"/> camera. Created by <see cref="FakeCameraAdapter.OpenAsync"/>. Frames follow the
/// adapter's clock; preview frames go to the adapter's <see cref="IPreviewChannel"/> for the current target only, and
/// only while a target is set the pattern is rendered at all.
/// </summary>
public sealed class FakeCameraSession : ICameraSession
{
    private readonly object _gate = new();
    private readonly FakeCameraAdapter _adapter;
    private readonly List<PendingCapture> _pending = [];
    private readonly TimeSpan _interval;
    private byte[]? _previewBuffer;
    private IDisposable? _timer;
    private CameraFrameInfo? _latest;
    private CameraFailure? _failure;
    private PreviewTarget _target;
    private long _sequence;
    private int _captures;
    private bool _disposed;

    internal FakeCameraSession(FakeCameraAdapter adapter, string deviceId)
    {
        _adapter = adapter;
        DeviceId = deviceId;
        _interval = TimeSpan.FromTicks((long)(TimeSpan.TicksPerSecond / adapter.FramesPerSecond));
    }

    public event EventHandler<CameraFailure>? Failed;

    public string DeviceId { get; }

    public CameraFormat Format => _adapter.Format;

    public TimeSpan Now => _adapter.Now;

    public CameraFrameInfo? LatestFrame
    {
        get
        {
            lock (_gate)
            {
                return _latest;
            }
        }
    }

    /// <summary>The preview target now.</summary>
    public PreviewTarget PreviewTarget
    {
        get
        {
            lock (_gate)
            {
                return _target;
            }
        }
    }

    /// <summary>How many frames the session has produced.</summary>
    public long FramesProduced
    {
        get
        {
            lock (_gate)
            {
                return _sequence;
            }
        }
    }

    public void SetPreviewTarget(PreviewTarget target)
    {
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "Not a preview target.");
        }

        lock (_gate)
        {
            _target = target;
        }
    }

    public Task<CapturedFrame> CaptureAsync(TimeSpan notBefore, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<CapturedFrame>(cancellationToken);
        }

        PendingCapture pending;
        lock (_gate)
        {
            if (_disposed)
            {
                return Task.FromException<CapturedFrame>(new ObjectDisposedException(nameof(FakeCameraSession)));
            }

            if (_failure is { } failure)
            {
                return Task.FromException<CapturedFrame>(new CameraException(failure.Kind, failure.Detail));
            }

            if (_latest is { } latest && latest.Timestamp >= notBefore)
            {
                return Task.FromResult(BuildCapture(latest.Timestamp, latest.Sequence));
            }

            pending = new PendingCapture(notBefore);
            _pending.Add(pending);
        }

        pending.Registration = cancellationToken.Register(() =>
        {
            lock (_gate)
            {
                _pending.Remove(pending);
            }

            pending.Completion.TrySetCanceled(cancellationToken);
        });
        return pending.Completion.Task;
    }

    public ValueTask DisposeAsync()
    {
        PendingCapture[] pending;
        lock (_gate)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            _target = PreviewTarget.None;
            _timer?.Dispose();
            pending = [.. _pending];
            _pending.Clear();
        }

        _adapter.Release(DeviceId, this);
        foreach (var capture in pending)
        {
            capture.Fail(new ObjectDisposedException(nameof(FakeCameraSession)));
        }

        return ValueTask.CompletedTask;
    }

    internal void Start()
    {
        lock (_gate)
        {
            _timer = _adapter.Clock.Schedule(_interval, Tick);
        }
    }

    /// <summary>Fails the session: it stops, releases the device, fails pending captures, and raises <see cref="Failed"/> once.</summary>
    internal void Break(CameraFailure failure)
    {
        PendingCapture[] pending;
        lock (_gate)
        {
            if (_disposed || _failure is not null)
            {
                return;
            }

            _failure = failure;
            _target = PreviewTarget.None;
            _timer?.Dispose();
            pending = [.. _pending];
            _pending.Clear();
        }

        _adapter.Release(DeviceId, this);
        foreach (var capture in pending)
        {
            capture.Fail(new CameraException(failure.Kind, failure.Detail));
        }

        Failed?.Invoke(this, failure);
    }

    private void Tick()
    {
        PreviewTarget target = PreviewTarget.None;
        CameraFrameInfo? frame = null;
        PendingCapture[] ready = [];
        lock (_gate)
        {
            if (_disposed || _failure is not null)
            {
                return;
            }

            if (!_adapter.IsStalled)
            {
                frame = new CameraFrameInfo(++_sequence, Now, FakeCameraAdapter.FrameWidth, FakeCameraAdapter.FrameHeight);
                _latest = frame;
                target = _target;
                ready = [.. _pending.Where(capture => capture.NotBefore <= frame.Timestamp)];
                _pending.RemoveAll(capture => capture.NotBefore <= frame.Timestamp);
            }
        }

        try
        {
            if (frame is not null)
            {
                Deliver(frame, target, ready);
            }
        }
        finally
        {
            lock (_gate)
            {
                if (!_disposed && _failure is null)
                {
                    _timer = _adapter.Clock.Schedule(_interval, Tick);
                }
            }
        }
    }

    private void Deliver(CameraFrameInfo frame, PreviewTarget target, PendingCapture[] ready)
    {
        if (target != PreviewTarget.None)
        {
            _previewBuffer ??= new byte[FakeCameraAdapter.FrameWidth * FakeCameraAdapter.FrameHeight * 4];
            FakeTestPattern.Render(_previewBuffer, frame.Width, frame.Height, frame.Sequence, rgba: true);
            _adapter.Preview.TryPost(target, new PreviewFrame(frame.Sequence, frame.Width, frame.Height, _previewBuffer, frame.Timestamp));
        }

        foreach (var capture in ready)
        {
            capture.Complete(BuildCapture(frame.Timestamp, frame.Sequence));
        }
    }

    private CapturedFrame BuildCapture(TimeSpan timestamp, long sequence)
    {
        int index;
        lock (_gate)
        {
            index = _captures++;
        }

        var photographs = _adapter.Photographs;
        if (photographs.Count > 0)
        {
            var photograph = photographs[index % photographs.Count];
            return new CapturedFrame(photograph.Bgra.ToArray(), photograph.Width, photograph.Height, photograph.Width * 4, timestamp);
        }

        var pixels = new byte[FakeCameraAdapter.FrameWidth * FakeCameraAdapter.FrameHeight * 4];
        FakeTestPattern.Render(pixels, FakeCameraAdapter.FrameWidth, FakeCameraAdapter.FrameHeight, sequence, rgba: false);
        return new CapturedFrame(pixels, FakeCameraAdapter.FrameWidth, FakeCameraAdapter.FrameHeight, FakeCameraAdapter.FrameWidth * 4, timestamp);
    }

    private sealed class PendingCapture(TimeSpan notBefore)
    {
        public TimeSpan NotBefore => notBefore;

        public TaskCompletionSource<CapturedFrame> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenRegistration Registration { get; set; }

        public void Complete(CapturedFrame frame)
        {
            Registration.Dispose();
            Completion.TrySetResult(frame);
        }

        public void Fail(Exception exception)
        {
            Registration.Dispose();
            Completion.TrySetException(exception);
        }
    }
}
