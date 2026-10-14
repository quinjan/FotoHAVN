using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Camera.Windows.Tests;

/// <summary>
/// A stand-in for the Core policies so the adapter's logic is tested on its own, with deterministic choices. It follows
/// the contract's shape (largest usable format wins, ≥ eligibility) but is not the contract.
/// </summary>
internal sealed class TestPolicies : ICameraPolicySet
{
    public static TestPolicies Instance { get; } = new();

    public CameraFormat? ChooseFormat(IReadOnlyList<CameraFormat> formats) =>
        formats
            .Where(format => format.Width >= 1280 && format.Height >= 720)
            .OrderByDescending(format => format.Width)
            .ThenByDescending(format => format.Subtype == "NV12")
            .ThenByDescending(format => format.FramesPerSecond)
            .Cast<CameraFormat?>()
            .FirstOrDefault();

    public bool IsEligible(TimeSpan frameTimestamp, TimeSpan notBefore) => frameTimestamp >= notBefore;

    public CameraFailureKind Classify(int hresult, CameraOperation operation) => hresult switch
    {
        CameraHResults.AccessDenied => CameraFailureKind.PrivacyDenied,
        CameraHResults.SharingViolation or CameraHResults.VideoDeviceLocked or CameraHResults.VideoRecordingDevicePreempted =>
            operation == CameraOperation.Open ? CameraFailureKind.InUseByAnotherApp : CameraFailureKind.ExclusiveLost,
        CameraHResults.HwMftFailedStartStreaming => operation == CameraOperation.Open ? CameraFailureKind.InUseByAnotherApp : CameraFailureKind.Failed,
        CameraHResults.VideoRecordingDeviceInvalidated or CameraHResults.DeviceNotConnected or CameraHResults.DeviceRemoved =>
            operation == CameraOperation.Open ? CameraFailureKind.NotFound : CameraFailureKind.Removed,
        CameraHResults.FileNotFound or CameraHResults.NotFound or CameraHResults.MediaFoundationNotFound =>
            operation == CameraOperation.Open ? CameraFailureKind.NotFound : CameraFailureKind.Failed,
        _ => operation == CameraOperation.Open ? CameraFailureKind.Unknown : CameraFailureKind.Failed,
    };

    public CameraFailureKind Classify(Exception exception, CameraOperation operation) => exception switch
    {
        CameraException camera => camera.Kind,
        UnauthorizedAccessException => CameraFailureKind.PrivacyDenied,
        _ => Classify(exception.HResult, operation),
    };
}

/// <summary>A preview channel that records what the adapter pushes; it accepts everything unless told otherwise.</summary>
internal sealed class RecordingPreviewChannel : IPreviewChannel
{
    private readonly object _gate = new();
    private readonly Dictionary<PreviewTarget, int> _posted = [];
    private readonly Dictionary<PreviewTarget, int> _refused = [];
    private PreviewFrame? _lastFrame;
    private byte[] _lastPixels = [];

    public bool Accept { get; set; } = true;

    public bool ThrowOnPost { get; set; }

    public int Posted(PreviewTarget target)
    {
        lock (_gate)
        {
            return _posted.GetValueOrDefault(target);
        }
    }

    public int Refused(PreviewTarget target)
    {
        lock (_gate)
        {
            return _refused.GetValueOrDefault(target);
        }
    }

    public PreviewFrame? LastFrame
    {
        get
        {
            lock (_gate)
            {
                return _lastFrame;
            }
        }
    }

    public byte[] LastPixels
    {
        get
        {
            lock (_gate)
            {
                return _lastPixels;
            }
        }
    }

    public bool TryPost(PreviewTarget target, PreviewFrame frame)
    {
        if (ThrowOnPost)
        {
            throw new InvalidOperationException("The channel is broken.");
        }

        lock (_gate)
        {
            if (!Accept)
            {
                _refused[target] = _refused.GetValueOrDefault(target) + 1;
                return false;
            }

            _posted[target] = _posted.GetValueOrDefault(target) + 1;
            _lastFrame = frame;
            _lastPixels = frame.Rgba.ToArray();
            return true;
        }
    }

    public PreviewFrameStats GetStats(PreviewTarget target) => default;
}

/// <summary>A BGRA frame of one colour; counts how often its pixels were copied.</summary>
internal sealed class SolidFrame(int width, int height, byte blue, byte green, byte red, byte alpha = 255, int strideBytes = 0) : IBgraFrame
{
    private readonly int _stride = strideBytes == 0 ? width * 4 : strideBytes;

    public int Width { get; } = width;

    public int Height { get; } = height;

    public int CopyCount { get; private set; }

    public int CopyTo(PinnedBuffer destination)
    {
        CopyCount++;
        destination.EnsureCapacity(_stride * Height);
        var span = destination.AsSpan(_stride * Height);
        span.Clear();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var offset = (y * _stride) + (x * 4);
                span[offset] = blue;
                span[offset + 1] = green;
                span[offset + 2] = red;
                span[offset + 3] = alpha;
            }
        }

        return _stride;
    }
}
