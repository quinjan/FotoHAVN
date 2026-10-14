#if E2E_HOOKS
using FotoHavn.Core.Camera;
using FotoHavn.Core.E2E;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Hooks;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
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
        using var brain = new Brain(TestDependencies.Create(executor, new FakeClock(), FakeDisplayAdapter.CreateDefault(), new FakeCameraAdapter(new FakeClock()),         new RecordingHostServices("fake-primary"), new InMemoryEventStore()) with
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
        Assert.Contains(Wire.Json(TestResultMessage.Success(cameras, new ListCamerasResult([new TestCamera("fake-camera", "Fake camera")]))), client.Raw);
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
        using var brain = new Brain(TestDependencies.Create(executor, new FakeClock(), FakeDisplayAdapter.CreateDefault(), new FakeCameraAdapter(new FakeClock()), new RecordingHostServices(), new InMemoryEventStore()));
        var client = new TestClient(Role.Operator);
        client.Connection = brain.Connect(Role.Operator, client.Receive);

        var close = client.Test(TestHookNames.CloseGuestScreen);
        var exit = client.Test(TestHookNames.Exit);
        executor.RunPending();

        client.AssertReceived(TestResultMessage.Success(close, EmptyHookResult.Instance), TestResultMessage.Failure(exit, TestHookError.NotSupported));
    }

    [Fact]
    public void The_public_constructor_forwards_storage_fault_hooks_through_IStorageTestHooks()
    {
        var hooks = new StorageHooks();
        var executor = new ManualBrainExecutor();
        using var brain = new Brain(TestDependencies.Create(executor, new FakeClock(), FakeDisplayAdapter.CreateDefault(), new FakeCameraAdapter(new FakeClock()), new RecordingHostServices(),         new InMemoryEventStore()) with
        {
            StorageTestHooks = hooks,
        });
        var client = new TestClient(Role.Operator);
        client.Connection = brain.Connect(Role.Operator, client.Receive);

        var inject = client.Test(TestHookNames.InjectStorageFault, """{"kind":"diskFull","scope":"allWrites"}""");
        var clear = client.Test(TestHookNames.ClearStorageFault);
        executor.RunPending();

        client.AssertReceived(TestResultMessage.Success(inject, EmptyHookResult.Instance), TestResultMessage.Success(clear, EmptyHookResult.Instance));
        Assert.Equal(["InjectFault(DiskFull, AllWrites)", "ClearFault"], hooks.Calls);
    }

    [Fact]
    public void Without_storage_hooks_the_fault_hooks_are_not_supported()
    {
        var executor = new ManualBrainExecutor();
        using var brain = new Brain(TestDependencies.Create(executor, new FakeClock(), FakeDisplayAdapter.CreateDefault(), new FakeCameraAdapter(new FakeClock()), new RecordingHostServices(), new InMemoryEventStore()));
        var client = new TestClient(Role.Operator);
        client.Connection = brain.Connect(Role.Operator, client.Receive);

        var inject = client.Test(TestHookNames.InjectStorageFault, """{"kind":"locked","scope":"nextWrite"}""");
        executor.RunPending();

        client.AssertReceived(TestResultMessage.Failure(inject, TestHookError.NotSupported));
    }

    [Fact]
    public async Task The_host_hook_port_forwards_the_diagnostics_capture_to_the_host()
    {
        var hooks = new Hooks();
        var port = new HostTestHooksPort(hooks);
        var frame = new CapturedFrame(new byte[16], 2, 2, 8, TimeSpan.Zero);

        var saved = await port.SaveDiagnosticsCaptureAsync(frame, mirrored: true);

        Assert.Equal(new CameraCaptureView("capture.jpg", 2, 2, 99, true), saved);
    }

    [Fact]
    public void The_public_constructor_hands_the_camera_fault_control_to_the_camera_fault_hooks()
    {
        var clock = new FakeClock();
        var faults = new FaultInjectingCameraAdapter(new FakeCameraAdapter(clock));
        var executor = new ManualBrainExecutor();
        using var brain = new Brain(TestDependencies.Create(executor, clock, FakeDisplayAdapter.CreateDefault(), faults, new RecordingHostServices(),         new InMemoryEventStore()) with
        {
            CameraFaults = faults,
        });
        var client = new TestClient(Role.Operator);
        client.Connection = brain.Connect(Role.Operator, client.Receive);

        var inject = client.Test(TestHookNames.InjectCameraFault, """{"kind":"stale"}""");
        executor.RunPending();

        client.AssertReceived(TestResultMessage.Success(inject, EmptyHookResult.Instance));
        Assert.Equal(CameraFaultKind.Stale, faults.ActiveFault);
    }

    private sealed class StorageHooks : IStorageTestHooks
    {
        public List<string> Calls { get; } = [];

        public void InjectFault(StorageFaultKind kind, StorageFaultScope scope) => Calls.Add($"InjectFault({kind}, {scope})");

        public void ClearFault() => Calls.Add("ClearFault");
    }

    private sealed class Hooks : IHostTestHooks
    {
        public bool Exited { get; private set; }

        public Task<IReadOnlyList<TestWindow>> GetWindowsAsync() => Task.FromResult<IReadOnlyList<TestWindow>>([]);

        public Task<TestWindow> SetWindowBoundsAsync(Role role, int width, int height) =>
            Task.FromException<TestWindow>(new TestHookException(TestHookError.NotSupported));

        public bool ScheduleCrashUi(Role role) => throw new InvalidOperationException("No browser process.");

        public Task<CameraCaptureView> SaveDiagnosticsCaptureAsync(CapturedFrame frame, bool mirrored) =>
            Task.FromResult(new CameraCaptureView("capture.jpg", frame.Width, frame.Height, 99, mirrored));

        public void ScheduleExit() => Exited = true;
    }
}
#endif
