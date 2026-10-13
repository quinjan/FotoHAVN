using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Camera;

/// <summary>
/// One open camera: a single long-lived frame stream that feeds the preview and every Capture. Disposing it releases the
/// camera. A session that failed is dead: the brain disposes it and opens a new one to retry.
/// </summary>
/// <remarks>
/// Threading: <see cref="Failed"/> and the preview pushes happen on the adapter's own threads; <see cref="Now"/> and
/// <see cref="LatestFrame"/> may be read from any thread. The brain reads them on its executor and treats a read as a
/// snapshot.
/// </remarks>
public interface ICameraSession : IAsyncDisposable
{
    /// <summary>
    /// Raised at most once, from any thread, when the session fails after opening: the capture session failed, the
    /// device was removed, or exclusive control was lost. Frame staleness is not reported here; the brain detects it with
    /// <c>FrameFreshness</c>. The handler must return at once.
    /// </summary>
    event EventHandler<CameraFailure>? Failed;

    /// <summary>The device ID this session was opened with.</summary>
    string DeviceId { get; }

    /// <summary>The format the stream runs at.</summary>
    CameraFormat Format { get; }

    /// <summary>The session's monotonic clock. <see cref="CameraFrameInfo.Timestamp"/> and <see cref="CaptureAsync"/>'s <c>notBefore</c> are on it.</summary>
    TimeSpan Now { get; }

    /// <summary>The newest frame received, or null before the first one. It keeps its last value when frames stop.</summary>
    CameraFrameInfo? LatestFrame { get; }

    /// <summary>
    /// Chooses which page receives downsized preview frames. The adapter pushes them to its <see cref="IPreviewChannel"/>
    /// for that target only, and stops for <see cref="PreviewTarget.None"/>. A new session starts with
    /// <see cref="PreviewTarget.None"/>. It returns at once.
    /// </summary>
    void SetPreviewTarget(PreviewTarget target);

    /// <summary>
    /// Returns the first frame whose timestamp is at or after <paramref name="notBefore"/>, never an earlier one, as the
    /// full-size BGRA8 frame. The brain passes <see cref="Now"/> at the moment its countdown ends. It waits for a frame
    /// when none qualifies yet and has no timeout of its own: the caller cancels it.
    /// </summary>
    /// <exception cref="CameraException">The session failed before a frame arrived.</exception>
    /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<CapturedFrame> CaptureAsync(TimeSpan notBefore, CancellationToken cancellationToken);
}
