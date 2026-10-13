using Windows.Devices.Enumeration;

namespace FotoHavn.Camera.Windows;

/// <summary>
/// Thin enumeration of video capture devices for the M1 camera precondition. M5 replaces it with the
/// MediaCapture adapter and device watcher.
/// </summary>
public static class VideoCaptureDeviceList
{
    public static async Task<IReadOnlyList<VideoCaptureDevice>> ListAsync()
    {
        var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        return devices.Select(device => new VideoCaptureDevice(device.Id, device.Name)).ToArray();
    }
}
