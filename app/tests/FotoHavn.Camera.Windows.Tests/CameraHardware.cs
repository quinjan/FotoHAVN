using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Protocol.Cameras;
using Microsoft.Extensions.Logging.Abstractions;

namespace FotoHavn.Camera.Windows.Tests;

/// <summary>Helpers for the tests that open the real camera. Only one process can hold it, so collisions are waited out.</summary>
internal static class CameraHardware
{
    public static readonly TimeSpan CollisionPatience = TimeSpan.FromSeconds(90);

    public static void Log(string message) => TestContext.Current.TestOutputHelper?.WriteLine(message);

    public static WindowsCameraAdapter CreateAdapter(IPreviewChannel? channel = null, bool watchDevices = false) =>
        new(channel ?? new RecordingPreviewChannel(), NullLogger.Instance, CoreCameraPolicies.Instance, watchDevices);

    public static async Task<AvailableCamera> RequireCameraAsync(ICameraAdapter adapter)
    {
        var cameras = await adapter.ListAsync(TestContext.Current.CancellationToken);
        if (cameras.Count == 0)
        {
            Assert.Skip("not run: no camera");
        }

        return cameras.FirstOrDefault(camera => camera.Name.Contains("Integrated", StringComparison.OrdinalIgnoreCase)) ?? cameras[0];
    }

    /// <summary>Opens the camera, retrying while another process (another agent's test run, the Camera app) holds it.</summary>
    public static async Task<ICameraSession> OpenWithRetryAsync(ICameraAdapter adapter, string deviceId)
    {
        var deadline = DateTime.UtcNow + CollisionPatience;
        var collisions = 0;
        while (true)
        {
            try
            {
                var session = await adapter.OpenAsync(deviceId, TestContext.Current.CancellationToken);
                if (collisions > 0)
                {
                    Log($"Camera was held by another process; opened after {collisions} collision(s).");
                }

                return session;
            }
            catch (CameraException exception) when (exception.Kind is CameraFailureKind.InUseByAnotherApp && DateTime.UtcNow < deadline)
            {
                collisions++;
                await Task.Delay(TimeSpan.FromMilliseconds(750), TestContext.Current.CancellationToken);
            }
        }
    }

    public static async Task WaitForFirstFrameAsync(ICameraSession session, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (session.LatestFrame is null)
        {
            Assert.True(DateTime.UtcNow < deadline, "No frame arrived.");
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }
}
