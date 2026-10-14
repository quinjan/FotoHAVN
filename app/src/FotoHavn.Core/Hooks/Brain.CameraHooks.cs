using FotoHavn.Core.Hooks;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core;

// The camera test hooks (M5 contract section 6). They compile into every build and run only when the hook dispatcher is on;
// the fault hooks also need the host to have passed an ICameraFaultControl.
public sealed partial class Brain
{
    private readonly ICameraFaultControl? _cameraFaults;

    private void RunCameraHook(Connection connection, string id, string name, object? args)
    {
        switch (name)
        {
            case TestHookNames.OpenCameraDiagnostics:
                ReleaseDiagnosticsCaptures();
                _cameraDiagnosticsOpen = true;
                StartCameraList();
                Reply(connection, id, HookOutcome.Ok(EmptyHookResult.Instance));
                break;
            case TestHookNames.SelectDiagnosticsCamera:
                SelectDiagnosticsCamera(connection, id, ((SelectDiagnosticsCameraArgs)args!).DeviceId);
                break;
            case TestHookNames.SetDiagnosticsMirror:
                _mirrored = ((SetDiagnosticsMirrorArgs)args!).Mirrored;
                Reply(connection, id, HookOutcome.Ok(EmptyHookResult.Instance));
                break;
            case TestHookNames.CaptureDiagnostics:
                CaptureDiagnosticsFrame(connection, id);
                break;
            case TestHookNames.InjectCameraFault:
                Reply(connection, id, RunCameraFaultHook(faults => faults.InjectFault(((InjectCameraFaultArgs)args!).Kind)));
                break;
            case TestHookNames.ClearCameraFault:
                Reply(connection, id, ClearCameraFault());
                break;
            default:
                throw new InvalidOperationException($"The test hook {name} is not a camera hook.");
        }
    }

    /// <summary>
    /// Lists the cameras afresh (the cached list may lag a change the adapter hasn't announced yet) and, when the ID is on it,
    /// closes the open camera, opens this one, and previews it to the Operator Screen. It answers when the request is
    /// accepted, not when the camera is ready.
    /// </summary>
    private void SelectDiagnosticsCamera(Connection connection, string id, string deviceId)
    {
        var request = ++_cameraListRequest;
        ListCameras().ContinueWith(
            completed => Post(() =>
            {
                CompleteCameraList(request, completed);
                var known = completed.IsCompletedSuccessfully ? completed.Result : _cameras;
                if (!known.Any(camera => string.Equals(camera.DeviceId, deviceId, StringComparison.Ordinal)))
                {
                    Reply(connection, id, HookOutcome.Fail(TestHookError.UnknownCamera));
                }
                else
                {
                    _previewTarget = PreviewTarget.Operator;
                    StartCameraOpen(deviceId);
                    Reply(connection, id, HookOutcome.Ok(EmptyHookResult.Instance));
                }

                PublishChanges();
            }),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Removes the fault and reopens the selected camera, unless it is already open or opening (a thawed session recovers by
    /// itself, and a second open would only fight the first for the device).
    /// </summary>
    private HookOutcome ClearCameraFault()
    {
        var outcome = RunCameraFaultHook(faults => faults.ClearFault());
        if (outcome.Error is null && _selectedCameraId is { } selected && _cameraState is CameraState.NotAvailable or CameraState.InUseByAnotherApp)
        {
            StartCameraOpen(selected);
        }

        return outcome;
    }

    private HookOutcome RunCameraFaultHook(Action<ICameraFaultControl> run)
    {
        if (_cameraFaults is not { } faults)
        {
            return HookOutcome.Fail(TestHookError.NotSupported);
        }

        try
        {
            run(faults);
            return HookOutcome.Ok(EmptyHookResult.Instance);
        }
#pragma warning disable CA1031 // Any failure becomes the hook's error (contract section 8).
        catch (Exception)
#pragma warning restore CA1031
        {
            return HookOutcome.Fail(TestHookError.HookFailed);
        }
    }
}
