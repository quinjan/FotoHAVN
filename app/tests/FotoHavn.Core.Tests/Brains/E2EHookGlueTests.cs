#if E2E_HOOKS
using FotoHavn.Core.E2E;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Brains;

/// <summary>
/// The E2E-only glue, run only by an E2E build of the tests (<c>dotnet test ... -p:FotoHavnE2E=true</c>): the public
/// constructor switches the hooks on and forwards host hooks through <see cref="IHostTestHooks"/>.
/// </summary>
public sealed class E2EHookGlueTests
{
    [Fact]
    public void The_public_constructor_forwards_host_hooks_and_maps_their_exceptions()
    {
        var hooks = new Hooks();
        var executor = new ManualBrainExecutor();
        using var brain = new Brain(new BrainDependencies(executor, new FakeClock(), FakeDisplayAdapter.CreateDefault(), new RecordingHostServices("fake-primary"))
        {
            HostTestHooks = hooks,
        });
        var client = new TestClient(Role.Operator);
        client.Connection = brain.Connect(Role.Operator, client.Receive);

        var cameras = client.Test(TestHookNames.ListCameras);
        var bounds = client.Test(TestHookNames.SetWindowBounds, """{"role":"guest","width":1280,"height":720}""");
        var crash = client.Test(TestHookNames.CrashUi, """{"role":"guest"}""");
        var diagnostics = client.Test(TestHookNames.GetDiagnostics);
        var exit = client.Test(TestHookNames.Exit);
        executor.RunPending();

        Assert.Equal(5, client.Raw.Count);
        Assert.Contains(Wire.Json(TestResultMessage.Success(cameras, new ListCamerasResult([new TestCamera("camera", "Camera")]))), client.Raw);
        Assert.Contains(Wire.Json(TestResultMessage.Failure(bounds, TestHookError.NotSupported)), client.Raw);
        Assert.Contains(Wire.Json(TestResultMessage.Failure(crash, TestHookError.HookFailed)), client.Raw);
        Assert.Contains(Wire.Json(TestResultMessage.Success(exit, EmptyHookResult.Instance)), client.Raw);
        Assert.Contains(client.Raw, json => json.Contains(diagnostics, StringComparison.Ordinal) && json.Contains("\"ok\":true", StringComparison.Ordinal));
        Assert.True(hooks.Exited);
    }

    [Fact]
    public void Without_host_hooks_the_brain_hooks_still_run()
    {
        var executor = new ManualBrainExecutor();
        using var brain = new Brain(new BrainDependencies(executor, new FakeClock(), FakeDisplayAdapter.CreateDefault(), new RecordingHostServices()));
        var client = new TestClient(Role.Operator);
        client.Connection = brain.Connect(Role.Operator, client.Receive);

        var close = client.Test(TestHookNames.CloseGuestScreen);
        var exit = client.Test(TestHookNames.Exit);
        executor.RunPending();

        client.AssertReceived(TestResultMessage.Success(close, EmptyHookResult.Instance), TestResultMessage.Failure(exit, TestHookError.NotSupported));
    }

    private sealed class Hooks : IHostTestHooks
    {
        public bool Exited { get; private set; }

        public Task<IReadOnlyList<TestWindow>> GetWindowsAsync() => Task.FromResult<IReadOnlyList<TestWindow>>([]);

        public Task<TestWindow> SetWindowBoundsAsync(Role role, int width, int height) =>
            Task.FromException<TestWindow>(new TestHookException(TestHookError.NotSupported));

        public bool ScheduleCrashUi(Role role) => throw new InvalidOperationException("No browser process.");

        public Task<IReadOnlyList<TestCamera>> ListCamerasAsync() => Task.FromResult<IReadOnlyList<TestCamera>>([new("camera", "Camera")]);

        public void ScheduleExit() => Exited = true;
    }
}
#endif
