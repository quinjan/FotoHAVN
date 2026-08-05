using FotoHavn.Core;

namespace FotoHavn.App;

internal sealed class CameraSessionOwner<T> where T : IAsyncDisposable
{
    private T? resource;

    public CameraDeviceId? DeviceId { get; private set; }

    public string? StreamId { get; private set; }

    public bool IsOwnedDevice(CameraDeviceId deviceId) => DeviceId == deviceId;

    public async Task AdoptAsync(CameraDeviceId deviceId, string streamId, T ownedResource)
    {
        await ReleaseAsync().ConfigureAwait(false);
        DeviceId = deviceId;
        StreamId = streamId;
        resource = ownedResource;
    }

    public async Task ReleaseAsync()
    {
        var previous = resource;
        resource = default;
        DeviceId = null;
        StreamId = null;
        if (previous is not null)
        {
            await previous.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<bool> RemoveAsync(CameraDeviceId deviceId)
    {
        if (!IsOwnedDevice(deviceId))
        {
            return false;
        }

        await ReleaseAsync().ConfigureAwait(false);
        return true;
    }
}
