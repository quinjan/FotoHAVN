namespace FotoHavn.Camera.Windows.Tests;

/// <summary>Hardware tests: they run against real cameras and report "not run" (skipped) without one.</summary>
public sealed class VideoCaptureDeviceListTests
{
    [Fact]
    public async Task Lists_connected_cameras_with_id_and_name()
    {
        var cameras = await VideoCaptureDeviceList.ListAsync();

        if (cameras.Count == 0)
        {
            Assert.Skip("not run: no camera");
        }

        Assert.All(cameras, camera =>
        {
            Assert.False(string.IsNullOrWhiteSpace(camera.Id));
            Assert.False(string.IsNullOrWhiteSpace(camera.Name));
        });
        Assert.Equal(cameras.Count, cameras.Select(camera => camera.Id).Distinct(StringComparer.Ordinal).Count());
    }
}
