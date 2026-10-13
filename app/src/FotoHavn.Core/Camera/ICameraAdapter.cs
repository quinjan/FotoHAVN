namespace FotoHavn.Core.Camera;

/// <summary>
/// Discovers cameras and opens them. One adapter lives for the app's lifetime; the host constructs it (with the host's
/// <see cref="IPreviewChannel"/>) and passes it to the brain in <see cref="BrainDependencies.Camera"/>.
/// </summary>
/// <remarks>
/// Threading: <see cref="CamerasChanged"/> may be raised on any thread; the brain only posts a work item to its executor
/// from the handler. The methods may be called from the brain's executor thread and must not block it: they return a
/// task and do their I/O elsewhere.
/// </remarks>
public interface ICameraAdapter
{
    /// <summary>
    /// Raised when a camera is added, removed, or renamed (the Windows device watcher; the fake's controls). The handler
    /// must return at once. The args carry nothing: the brain calls <see cref="ListAsync"/> again.
    /// </summary>
    event EventHandler? CamerasChanged;

    /// <summary>The Available Cameras now, in the order Windows lists them. An empty list is not an error.</summary>
    Task<IReadOnlyList<AvailableCamera>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Opens the camera with exclusive control, picks its format, and starts its one long-lived frame stream. It
    /// completes when frames can flow, not when the first one arrives (the brain proves freshness with
    /// <see cref="ICameraSession.LatestFrame"/>). Opening a device that already has a live session fails with
    /// <see cref="CameraFailureKind.InUseByAnotherApp"/>.
    /// </summary>
    /// <exception cref="CameraException">The open failed. <see cref="CameraException.Kind"/> says why:
    /// <see cref="CameraFailureKind.NotFound"/>, <see cref="CameraFailureKind.InUseByAnotherApp"/>,
    /// <see cref="CameraFailureKind.PrivacyDenied"/>, <see cref="CameraFailureKind.NoSupportedFormat"/>, or
    /// <see cref="CameraFailureKind.Unknown"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled; nothing stays open.</exception>
    Task<ICameraSession> OpenAsync(string deviceId, CancellationToken cancellationToken);
}
