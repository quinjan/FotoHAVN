using System.Diagnostics;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Camera.Windows.Tests;

/// <summary>
/// The "in use by another app" facts, with the real Windows Camera app as the other app. It opens a window and takes the camera,
/// so it only runs when <c>FOTOHAVN_CAMERA_APP_TEST=1</c>; the contract's HRESULT table was researched from headers and this is
/// where it meets a real capture.
/// </summary>
[Collection("camera")]
public sealed class WindowsCameraAppCollisionTests
{
    private const string OptIn = "FOTOHAVN_CAMERA_APP_TEST";

    [Fact]
    public async Task Opening_while_the_windows_camera_app_holds_the_camera_reports_in_use_with_a_recorded_hresult()
    {
        RequireOptIn();
        using var adapter = CameraHardware.CreateAdapter();
        var camera = await CameraHardware.RequireCameraAsync(adapter);
        var launched = await LaunchCameraAppAsync();
        try
        {
            CameraException? failure = null;
            ICameraSession? session = null;
            try
            {
                session = await adapter.OpenAsync(camera.DeviceId, TestContext.Current.CancellationToken);
            }
            catch (CameraException exception)
            {
                failure = exception;
            }

            if (session is not null)
            {
                CameraHardware.Log("OBSERVED: the Windows Camera app did NOT stop an exclusive open (the open succeeded).");
                await session.DisposeAsync();
                Assert.Fail("The open succeeded while the Camera app was running; check that the app really holds the camera.");
            }

            Assert.NotNull(failure);
            var inner = failure.InnerException;
            // Windows accepts the exclusive InitializeAsync and answers through MediaFrameReader.StartAsync (no HRESULT is thrown).
            CameraHardware.Log($"OBSERVED in-use open failure: kind {failure.Kind}; inner exception {(inner is null ? "none (no HRESULT)" : $"{inner.GetType().Name} 0x{inner.HResult:X8}")}; detail: {failure.Message}");
            Assert.Equal(CameraFailureKind.InUseByAnotherApp, failure.Kind);
            Assert.Equal(0, adapter.HeldDeviceCount);
        }
        finally
        {
            Close(launched);
        }
    }

    [Fact]
    public async Task Starting_the_windows_camera_app_while_we_stream_leaves_our_session_streaming_and_reports_what_happens()
    {
        RequireOptIn();
        using var adapter = CameraHardware.CreateAdapter();
        var camera = await CameraHardware.RequireCameraAsync(adapter);
        await using var session = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);
        CameraFailure? failure = null;
        session.Failed += (_, args) => failure = args;
        await CameraHardware.WaitForFirstFrameAsync(session, TimeSpan.FromSeconds(10));
        var launched = await LaunchCameraAppAsync();
        try
        {
            await Task.Delay(5000, TestContext.Current.CancellationToken);

            var age = session.Now - session.LatestFrame!.Timestamp;
            CameraHardware.Log($"OBSERVED with the Camera app started after us: Failed raised = {failure is not null} ({failure?.Kind}: {failure?.Detail}); frame age {age.TotalMilliseconds:0} ms.");
            Assert.Null(failure);
            Assert.True(age < TimeSpan.FromSeconds(2));
        }
        finally
        {
            Close(launched);
        }
    }

    private static void RequireOptIn()
    {
        if (Environment.GetEnvironmentVariable(OptIn) != "1")
        {
            Assert.Skip($"not run: set {OptIn}=1 (it starts the Windows Camera app)");
        }
    }

    private static async Task<int[]> LaunchCameraAppAsync()
    {
        var before = Process.GetProcessesByName("WindowsCamera").Select(process => process.Id).ToHashSet();
        if (before.Count > 0)
        {
            Assert.Skip("not run: the Windows Camera app is already running");
        }

        Process.Start(new ProcessStartInfo("microsoft.windows.camera:") { UseShellExecute = true })?.Dispose();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        int[] started = [];
        while (started.Length == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(250, TestContext.Current.CancellationToken);
            started = [.. Process.GetProcessesByName("WindowsCamera").Select(process => process.Id)];
        }

        Assert.NotEmpty(started);
        await Task.Delay(4000, TestContext.Current.CancellationToken);
        CameraHardware.Log($"Windows Camera app started (PID {string.Join(',', started)}).");
        return started;
    }

    private static void Close(int[] processIds)
    {
        foreach (var id in processIds)
        {
            try
            {
                using var process = Process.GetProcessById(id);
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
        }
    }
}
