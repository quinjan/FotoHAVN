using FotoHavn.Camera.Windows;
using FotoHavn.Core.Camera;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Host.Windows.Cameras;

/// <summary>
/// TEMPORARY (M5 wave A, until wave B3's MediaCapture adapter replaces it): lists the cameras with the M1
/// <see cref="VideoCaptureDeviceList"/> and opens nothing. It never raises <see cref="CamerasChanged"/>.
/// </summary>
internal sealed class TemporaryCameraAdapter : ICameraAdapter
{
    public event EventHandler? CamerasChanged
    {
        add { }
        remove { }
    }

    public async Task<IReadOnlyList<AvailableCamera>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var devices = await VideoCaptureDeviceList.ListAsync().ConfigureAwait(false);
        return [.. devices.Select(device => new AvailableCamera(device.Id, device.Name))];
    }

    public Task<ICameraSession> OpenAsync(string deviceId, CancellationToken cancellationToken) =>
        Task.FromException<ICameraSession>(new CameraException(CameraFailureKind.NotFound));
}
