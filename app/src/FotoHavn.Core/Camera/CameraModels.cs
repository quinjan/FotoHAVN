using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Camera;

// The camera port (plan section 11, ADR 0004). The brain owns every camera rule; an adapter (FotoHavn.Camera.Windows, or
// the FakeCameraAdapter) only performs device I/O and reports facts. Preview frames never pass through the brain: the
// adapter pushes them to the host's IPreviewChannel, and the brain only chooses the target with SetPreviewTarget.

/// <summary>A camera Windows lists, identified by its device ID.</summary>
/// <param name="DeviceId">The device ID: the camera's identity and what the Camera Binding stores.</param>
/// <param name="Name">The friendly name, for display only.</param>
public sealed record AvailableCamera(string DeviceId, string Name);

/// <summary>One stream format a camera offers.</summary>
/// <param name="Width">Frame width in pixels.</param>
/// <param name="Height">Frame height in pixels.</param>
/// <param name="FramesPerSecond">The nominal frame rate (30000/1001 is 29.97).</param>
/// <param name="Subtype">The media subtype name as Windows reports it (<c>NV12</c>, <c>YUY2</c>, <c>MJPG</c>, <c>RGB32</c>, …), uppercase.</param>
public sealed record CameraFormat(int Width, int Height, double FramesPerSecond, string Subtype);

/// <summary>The metadata of the newest frame a session received, without its pixels.</summary>
/// <param name="Sequence">A count that rises by one per frame the session received, starting at 1.</param>
/// <param name="Timestamp">When the frame was captured, on the session's monotonic clock, comparable with <see cref="ICameraSession.Now"/>.</param>
/// <param name="Width">The frame width in pixels.</param>
/// <param name="Height">The frame height in pixels.</param>
public sealed record CameraFrameInfo(long Sequence, TimeSpan Timestamp, int Width, int Height);

/// <summary>A full-size frame taken for a Capture, converted to BGRA8 (four bytes per pixel in B, G, R, A order, alpha 255).</summary>
/// <param name="Bgra">The pixels. The adapter gives the caller its own copy, so the caller may mirror it in place.</param>
/// <param name="Width">The frame width in pixels.</param>
/// <param name="Height">The frame height in pixels.</param>
/// <param name="Stride">Bytes per row (at least <c>Width × 4</c>).</param>
/// <param name="Timestamp">When the frame was captured, on the session's monotonic clock.</param>
public sealed record CapturedFrame(ReadOnlyMemory<byte> Bgra, int Width, int Height, int Stride, TimeSpan Timestamp);

/// <summary>A failure a session reports on its own, after it opened.</summary>
/// <param name="Kind">The classified cause.</param>
/// <param name="Detail">Diagnostic text for logs. Never shown to a guest or the operator.</param>
public sealed record CameraFailure(CameraFailureKind Kind, string Detail);

/// <summary>An adapter operation failed with a classified cause.</summary>
public sealed class CameraException : Exception
{
    public CameraException()
        : this(CameraFailureKind.Unknown)
    {
    }

    public CameraException(string message)
        : base(message)
    {
        Kind = CameraFailureKind.Unknown;
    }

    public CameraException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = CameraFailureKind.Unknown;
    }

    public CameraException(CameraFailureKind kind)
        : base($"The camera failed: {kind}.")
    {
        Kind = kind;
    }

    public CameraException(CameraFailureKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public CameraException(CameraFailureKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>The classified cause.</summary>
    public CameraFailureKind Kind { get; }
}
