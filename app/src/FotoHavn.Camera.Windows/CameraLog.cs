using FotoHavn.Protocol.Cameras;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Camera.Windows;

internal static partial class CameraLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Camera {DeviceId}: format {Width}x{Height} @ {Fps} fps {Subtype}.")]
    public static partial void FormatChosen(ILogger logger, string deviceId, int width, int height, double fps, string subtype);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "The frame reader cannot convert to BGRA8 itself; converting each frame instead.")]
    public static partial void ReaderConversionFallback(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Opening camera {DeviceId} failed with HRESULT {HResult}, classified as {Kind}.")]
    public static partial void OpenFailed(ILogger logger, Exception exception, string deviceId, string hresult, CameraFailureKind kind);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Stopping the frame reader during cleanup failed.")]
    public static partial void CleanupStopFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Camera {DeviceId}: a frame could not be processed.")]
    public static partial void FrameFailed(ILogger logger, Exception exception, string deviceId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Camera {DeviceId}: MediaCapture.Failed {HResult} ({Kind}): {Message}")]
    public static partial void CaptureFailed(ILogger logger, string deviceId, string hresult, CameraFailureKind kind, string message);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Camera {DeviceId}: exclusive control was lost to another app.")]
    public static partial void ExclusiveControlLost(ILogger logger, string deviceId);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning, Message = "Camera {DeviceId}: releasing the device after a failure threw.")]
    public static partial void TeardownThrew(ILogger logger, Exception exception, string deviceId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "Camera {DeviceId}: a Failed handler threw.")]
    public static partial void FailedHandlerThrew(ILogger logger, Exception exception, string deviceId);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "Camera {DeviceId}: stopping the frame reader failed.")]
    public static partial void ReaderStopFailed(ILogger logger, Exception exception, string deviceId);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning, Message = "A preview frame could not be produced.")]
    public static partial void PreviewFrameFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "A CamerasChanged handler threw.")]
    public static partial void CamerasChangedHandlerThrew(ILogger logger, Exception exception);
}
