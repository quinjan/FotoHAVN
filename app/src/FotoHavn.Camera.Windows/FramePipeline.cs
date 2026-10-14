using FotoHavn.Core.Camera;
using FotoHavn.Protocol.Cameras;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Camera.Windows;

/// <summary>One decoded camera frame waiting on the frame thread; its pixels are copied out only when somebody needs them.</summary>
internal interface IBgraFrame
{
    int Width { get; }

    int Height { get; }

    /// <summary>Copies the BGRA pixels into <paramref name="destination"/> (growing it) and returns the row stride in bytes.</summary>
    int CopyTo(PinnedBuffer destination);
}

/// <summary>
/// The frame loop's logic, free of Windows APIs: sequence numbers, <see cref="LatestFrame"/>, the preview push with its
/// back-pressure drops, and <see cref="CaptureAsync"/> waiting for the first eligible frame. <see cref="Process"/> is called
/// from one frame thread at a time; everything else may be called from any thread.
/// </summary>
internal sealed class FramePipeline
{
    private readonly IPreviewChannel _preview;
    private readonly ICameraPolicySet _policies;
    private readonly ILogger _logger;
    private readonly PinnedBuffer _frameBuffer = new();
    private readonly PreviewScaler _scaler = new();
    private readonly Lock _gate = new();
    private readonly List<PendingCapture> _pending = [];
    private Exception? _terminal;
    private CameraFrameInfo? _latest;
    private int _target;
    private long _sequence;
    private long _previewPosted;
    private long _previewDropped;

    public FramePipeline(IPreviewChannel preview, ICameraPolicySet policies, ILogger logger)
    {
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public CameraFrameInfo? LatestFrame => Volatile.Read(ref _latest);

    public PreviewTarget Target => (PreviewTarget)Volatile.Read(ref _target);

    /// <summary>Frames the preview channel accepted (this adapter's own count; the channel keeps its delivered count).</summary>
    public long PreviewPosted => Interlocked.Read(ref _previewPosted);

    /// <summary>Frames the channel refused (back-pressure or no page connected) or that failed to convert.</summary>
    public long PreviewDropped => Interlocked.Read(ref _previewDropped);

    public int PreviewBufferAllocations => _scaler.Allocations;

    public void SetPreviewTarget(PreviewTarget target)
    {
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target));
        }

        Volatile.Write(ref _target, (int)target);
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
            if (_terminal is not null)
            {
                return Task.FromException<CapturedFrame>(_terminal);
            }

            pending = new PendingCapture(notBefore);
            _pending.Add(pending);
        }

        if (cancellationToken.CanBeCanceled)
        {
            pending.Registration = cancellationToken.Register(static state =>
            {
                var (owner, request, token) = ((FramePipeline, PendingCapture, CancellationToken))state!;
                owner.Cancel(request, token);
            }, (this, pending, cancellationToken));
        }

        return pending.Completion.Task;
    }

    /// <summary>Ends the pipeline: pending and later captures fault with <paramref name="reason"/>.</summary>
    public void Terminate(Exception reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        PendingCapture[] pending;
        lock (_gate)
        {
            _terminal ??= reason;
            pending = [.. _pending];
            _pending.Clear();
        }

        foreach (var request in pending)
        {
            request.Registration.Dispose();
            request.Completion.TrySetException(reason);
        }
    }

    public void Process(TimeSpan timestamp, IBgraFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var sequence = ++_sequence;
        Volatile.Write(ref _latest, new CameraFrameInfo(sequence, timestamp, frame.Width, frame.Height));

        PendingCapture[] ready = [];
        lock (_gate)
        {
            if (_pending.Count > 0)
            {
                ready = [.. _pending.Where(request => _policies.IsEligible(timestamp, request.NotBefore))];
                foreach (var request in ready)
                {
                    _pending.Remove(request);
                }
            }
        }

        var target = Target;
        if (ready.Length == 0 && target == PreviewTarget.None)
        {
            return;
        }

        var stride = frame.CopyTo(_frameBuffer);
        var byteCount = checked(stride * frame.Height);
        foreach (var request in ready)
        {
            request.Registration.Dispose();
            var bgra = _frameBuffer.AsSpan(byteCount).ToArray();
            request.Completion.TrySetResult(new CapturedFrame(bgra, frame.Width, frame.Height, stride, timestamp));
        }

        if (target != PreviewTarget.None)
        {
            PushPreview(target, sequence, timestamp, frame.Width, frame.Height, stride);
        }
    }

    private void PushPreview(PreviewTarget target, long sequence, TimeSpan timestamp, int width, int height, int stride)
    {
        bool accepted;
        try
        {
            var (rgba, previewWidth, previewHeight) = _scaler.Scale(_frameBuffer, width, height, stride);
            accepted = _preview.TryPost(target, new PreviewFrame(sequence, previewWidth, previewHeight, rgba, timestamp));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            CameraLog.PreviewFrameFailed(_logger, exception);
            accepted = false;
        }

        if (accepted)
        {
            Interlocked.Increment(ref _previewPosted);
        }
        else
        {
            Interlocked.Increment(ref _previewDropped);
        }
    }

    private void Cancel(PendingCapture request, CancellationToken token)
    {
        lock (_gate)
        {
            _pending.Remove(request);
        }

        request.Completion.TrySetCanceled(token);
    }

    private sealed class PendingCapture(TimeSpan notBefore)
    {
        public TimeSpan NotBefore { get; } = notBefore;

        public TaskCompletionSource<CapturedFrame> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenRegistration Registration { get; set; }
    }
}
