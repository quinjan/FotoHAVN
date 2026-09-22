using FotoHavn.Core;
using Windows.Media.Capture;

namespace FotoHavn.App;

internal static class CameraOpenPolicy
{
    public static MediaCaptureInitializationSettings CreateSettings(CameraDeviceId exactDeviceId) =>
        new()
        {
            VideoDeviceId = exactDeviceId.Value,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            SharingMode = MediaCaptureSharingMode.ExclusiveControl,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu,
        };
}

internal static class CameraFailureMapper
{
    private const int DeviceInUseHResult = unchecked((int)0xC00D3704);
    private const int DeviceNotAvailableHResult = unchecked((int)0xC00D36D5);

    public static CameraOpenResult Map(Exception exception) => exception switch
    {
        UnauthorizedAccessException => CameraOpenResult.AccessDenied,
        _ when exception.HResult == DeviceInUseHResult => CameraOpenResult.InUse,
        _ when exception.HResult == DeviceNotAvailableHResult => CameraOpenResult.Disconnected,
        _ => CameraOpenResult.Unavailable,
    };
}
