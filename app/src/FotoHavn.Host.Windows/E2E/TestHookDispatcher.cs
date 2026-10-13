#if E2E_HOOKS
using System.Text.Json.Nodes;
using FotoHavn.Camera.Windows;
using FotoHavn.Host.Windows.TestHooks;

namespace FotoHavn.Host.Windows.E2E;

/// <summary>
/// E2E-only test hooks (compiled only with <c>E2E_HOOKS</c>). A field build never contains this type; the
/// publish guard and the reflection test in FotoHavn.Host.Windows.Tests enforce it.
/// </summary>
internal sealed class TestHookDispatcher(Func<Task<IReadOnlyList<VideoCaptureDevice>>> listCameras) : ITestHookDispatcher
{
    public async Task<TestHookReply> DispatchAsync(TestHookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Type switch
        {
            "test.listCameras" => await ListCamerasAsync(request.Id),
            "test.exit" => TestHookReply.Success(request.Id, [], exitAfterReply: true),
            _ => TestHookReply.Failure(request.Id, TestHookErrors.UnknownHook),
        };
    }

    private async Task<TestHookReply> ListCamerasAsync(string id)
    {
        IReadOnlyList<VideoCaptureDevice> cameras;
        try
        {
            cameras = await listCameras();
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return TestHookReply.Failure(id, TestHookErrors.HookFailed);
        }

        var list = new JsonArray();
        foreach (var camera in cameras)
        {
            list.Add(new JsonObject { ["id"] = camera.Id, ["name"] = camera.Name });
        }

        return TestHookReply.Success(id, new JsonObject { ["cameras"] = list });
    }
}
#endif
