using FotoHavn.Core.Camera;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Hooks;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Brains;

/// <summary>The camera test hooks: availability, diagnostics, mirror, capture, and the fault hooks.</summary>
public sealed class CameraHookTests
{
    [Theory]
    [InlineData(TestHookNames.OpenCameraDiagnostics, null)]
    [InlineData(TestHookNames.SelectDiagnosticsCamera, """{"deviceId":"fake-camera"}""")]
    [InlineData(TestHookNames.SetDiagnosticsMirror, """{"mirrored":true}""")]
    [InlineData(TestHookNames.CaptureDiagnostics, null)]
    [InlineData(TestHookNames.InjectCameraFault, """{"kind":"stale"}""")]
    [InlineData(TestHookNames.ClearCameraFault, null)]
    [InlineData(TestHookNames.ListCameras, null)]
    [InlineData(TestHookNames.GetDiagnostics, null)]
    public void Every_camera_hook_is_unavailable_without_the_e2e_hooks(string name, string? args)
    {
        using var harness = new BrainHarness(hooks: false, hostHooks: new FakeHostHookPort(), cameraFaults: new RecordingFaultControl());
        var client = harness.Connect(Role.Operator);

        var id = client.Test(name, args);
        harness.Run();
        harness.Run();

        Assert.Equal(TestResultMessage.Failure(id, TestHookError.HooksUnavailable), client.Single());
        Assert.Equal(0, harness.Camera.OpenSessionCount);
    }

    [Fact]
    public void List_cameras_reads_the_adapter_through_the_fault_decorator()
    {
        using var rig = new CameraRig([FakeCameraAdapter.DefaultCamera, new AvailableCamera("other", "Other")]);
        rig.SelectAndWaitUntilReady();

        Assert.Equal(["fake-camera", "other"], Cameras(rig));
        rig.Call(TestHookNames.InjectCameraFault, """{"kind":"removed"}""");
        Assert.Equal(["other"], Cameras(rig));
        rig.Call(TestHookNames.ClearCameraFault);
        Assert.Equal(["fake-camera", "other"], Cameras(rig));
    }

    [Fact]
    public void The_mirror_hook_sets_the_flag_in_the_view_and_a_new_revision()
    {
        using var rig = new CameraRig();
        rig.OpenDiagnostics();
        var revision = rig.Revisions[^1];

        Assert.True(rig.Call(TestHookNames.SetDiagnosticsMirror, """{"mirrored":true}""").Ok);

        Assert.True(rig.View.Mirrored);
        Assert.True(rig.Revisions[^1] > revision);
        Assert.True(rig.Call(TestHookNames.SetDiagnosticsMirror, """{"mirrored":false}""").Ok);
        Assert.False(rig.View.Mirrored);
    }

    [Fact]
    public void Capture_takes_a_frame_from_after_the_request_and_shows_it_as_the_last_capture()
    {
        using var rig = new CameraRig();
        rig.SelectAndWaitUntilReady();
        CapturedFrame? saved = null;
        rig.Host.SaveCapture = (frame, mirrored) =>
        {
            saved = frame;
            return Task.FromResult(new CameraCaptureView("capture-1.jpg", frame.Width, frame.Height, 4321, mirrored));
        };
        var requestedAt = rig.Clock.UtcNow - rig.Epoch;

        var id = rig.Operator.Test(TestHookNames.CaptureDiagnostics);
        rig.Harness.Run();
        Assert.Null(saved);
        rig.Advance(CameraRig.Frame * 2);
        var result = rig.Await(id);

        Assert.True(result.Ok);
        Assert.Equal(new CameraCaptureView("capture-1.jpg", 1280, 720, 4321, false), ProtocolJson.Deserialize<CameraCaptureView>(result.Result!.Value));
        Assert.NotNull(saved);
        Assert.True(saved.Timestamp >= requestedAt, $"The capture was stamped {saved.Timestamp}, before the request at {requestedAt}.");
        Assert.Equal(new CameraCaptureView("capture-1.jpg", 1280, 720, 4321, false), rig.View.LastCapture);
        Assert.Equal(["SaveDiagnosticsCapture(False)"], rig.Host.Calls.Where(call => call.StartsWith("SaveDiagnosticsCapture", StringComparison.Ordinal)));
    }

    [Fact]
    public void Capture_mirrors_the_pixels_only_when_the_diagnostics_mirror_is_on()
    {
        byte[] picture = [1, 2, 3, 255, 4, 5, 6, 255, 7, 8, 9, 255];
        using var rig = new CameraRig(photographs: [new FixturePhotograph(picture, 3, 1)]);
        rig.SelectAndWaitUntilReady();
        var frames = new List<byte[]>();
        rig.Host.SaveCapture = (frame, mirrored) =>
        {
            frames.Add(frame.Bgra.ToArray());
            return Task.FromResult(new CameraCaptureView("c.jpg", frame.Width, frame.Height, 1, mirrored));
        };

        CaptureWithFrames(rig);
        rig.Call(TestHookNames.SetDiagnosticsMirror, """{"mirrored":true}""");
        var result = CaptureWithFrames(rig);

        Assert.Equal(picture, frames[0]);
        Assert.Equal<byte[]>([7, 8, 9, 255, 4, 5, 6, 255, 1, 2, 3, 255], frames[1]);
        Assert.True(ProtocolJson.Deserialize<CameraCaptureView>(result.Result!.Value).Mirrored);
        Assert.True(rig.View.LastCapture!.Mirrored);
    }

    [Fact]
    public void Capture_fails_when_no_camera_is_ready()
    {
        using var rig = new CameraRig();
        rig.OpenDiagnostics();

        Assert.Equal(TestHookError.HookFailed, rig.Call(TestHookNames.CaptureDiagnostics).Error);

        rig.Select("fake-camera");
        Assert.Equal(TestHookError.HookFailed, rig.Call(TestHookNames.CaptureDiagnostics).Error);
        Assert.Null(rig.View.LastCapture);
    }

    [Fact]
    public void Capture_is_not_supported_without_a_host_to_save_it()
    {
        using var rig = new CameraRig(withHostHooks: false);
        rig.SelectAndWaitUntilReady();

        Assert.Equal(TestHookError.NotSupported, rig.Call(TestHookNames.CaptureDiagnostics).Error);
    }

    [Fact]
    public void Capture_gives_up_after_its_timeout_and_a_failing_host_save_becomes_the_hook_error()
    {
        using var rig = new CameraRig();
        rig.SelectAndWaitUntilReady();
        rig.Fake.Stall();

        var timedOut = rig.Operator.Test(TestHookNames.CaptureDiagnostics);
        rig.Harness.Run();
        Assert.DoesNotContain(rig.Operator.Messages, message => message is TestResultMessage { Id: var id } && id == timedOut);
        rig.Clock.Advance(Brain.CaptureTimeout);

        Assert.Equal(TestHookError.HookFailed, rig.Await(timedOut).Error);

        using var other = new CameraRig();
        other.SelectAndWaitUntilReady();
        other.Host.SaveCapture = (_, _) => Task.FromException<CameraCaptureView>(new HookFailure(TestHookError.NotSupported));
        var failing = other.Operator.Test(TestHookNames.CaptureDiagnostics);
        other.Harness.Run();
        other.Advance(CameraRig.Frame * 2);
        Assert.Equal(TestHookError.NotSupported, other.Await(failing).Error);
        Assert.Null(other.View.LastCapture);
    }

    [Fact]
    public void Capture_fails_when_the_session_dies_while_waiting_for_its_frame()
    {
        using var rig = new CameraRig();
        rig.SelectAndWaitUntilReady();
        rig.Fake.Stall();
        var id = rig.Operator.Test(TestHookNames.CaptureDiagnostics);
        rig.Harness.Run();

        rig.Fake.Fail(CameraFailureKind.Failed);

        Assert.Equal(TestHookError.HookFailed, rig.Await(id).Error);
        Assert.Equal(CameraState.NotAvailable, rig.View.State);
    }

    [Fact]
    public void A_frame_that_cannot_be_mirrored_or_a_capture_that_throws_fails_the_hook()
    {
        var scripted = new ScriptedCameraAdapter();
        using var harness = new BrainHarness(hooks: true, hostHooks: new FakeHostHookPort(), cameraOverride: scripted);
        var client = harness.Connect(Role.Operator);
        client.Hello();
        var session = ReadySession(harness, client, scripted);
        Call(harness, client, TestHookNames.SetDiagnosticsMirror, """{"mirrored":true}""");

        session.Capture = (_, _) => Task.FromResult(new CapturedFrame(new byte[16], 4, 1, 4, TimeSpan.Zero));
        var badStride = Call(harness, client, TestHookNames.CaptureDiagnostics);
        session.Capture = (_, _) => throw new InvalidOperationException("No frame.");
        var thrown = Call(harness, client, TestHookNames.CaptureDiagnostics);

        Assert.Equal(TestHookError.HookFailed, badStride.Error);
        Assert.Equal(TestHookError.HookFailed, thrown.Error);
        Assert.Contains(harness.Logger.Entries, entry => entry.Message == "Mirroring a diagnostics capture failed.");
    }

    [Fact]
    public void Diagnostics_report_the_camera_with_its_frame_age_and_the_preview_counters()
    {
        using var rig = new CameraRig();
        rig.Preview.AutoAcknowledge = false;
        Assert.Equal(new CameraDiagnostics(CameraState.Idle, null, null, null, null, null, PreviewTarget.None), rig.Diagnostics());
        rig.SelectAndWaitUntilReady();

        var camera = rig.Diagnostics();

        Assert.Equal(CameraState.Ready, camera.State);
        Assert.Equal("fake-camera", camera.DeviceId);
        Assert.Equal(new CameraFormatView(1280, 720, 30), camera.Format);
        Assert.InRange(camera.FrameAgeMs!.Value, 0, 34);
        Assert.Equal(PreviewTarget.Operator, camera.PreviewTarget);
        var counters = rig.Preview.GetStats(PreviewTarget.Operator);
        Assert.True(counters.Dropped > 0, "With no ack, frames after the first are dropped.");
        Assert.Equal((long?)counters.Dropped, camera.DroppedFrames);
        Assert.Equal((double?)counters.DeliveredPerSecond, camera.DeliveredFps);
    }

    [Fact]
    public void Diagnostics_leave_the_frame_figures_null_when_they_are_unknown()
    {
        var scripted = new ScriptedCameraAdapter();
        using var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var client = harness.Connect(Role.Operator);
        client.Hello();
        Call(harness, client, TestHookNames.OpenCameraDiagnostics);
        var session = scripted.NewSession("scripted");
        scripted.OpenBehavior = (_, _) => Task.FromResult<ICameraSession>(session);
        Call(harness, client, TestHookNames.SelectDiagnosticsCamera, """{"deviceId":"scripted"}""");

        var noFrameYet = Diagnostics(harness, client);
        session.Now = TimeSpan.FromSeconds(10);
        session.LatestFrame = new CameraFrameInfo(1, TimeSpan.FromSeconds(9.5), 1920, 1080);
        var withFrame = Diagnostics(harness, client);

        Assert.Equal(new CameraDiagnostics(CameraState.Checking, "scripted", new CameraFormatView(1920, 1080, 30), null, null, null, PreviewTarget.Operator), noFrameYet);
        Assert.Equal(new CameraDiagnostics(CameraState.Checking, "scripted", new CameraFormatView(1920, 1080, 30), 500, null, null, PreviewTarget.Operator), withFrame);
    }

    [Theory]
    [InlineData("failed", CameraFailureKind.Failed)]
    [InlineData("exclusiveLost", CameraFailureKind.ExclusiveLost)]
    [InlineData("removed", CameraFailureKind.Removed)]
    public void An_injected_session_fault_moves_the_state_at_once_and_clearing_it_brings_the_camera_back(string fault, CameraFailureKind reason)
    {
        using var rig = new CameraRig();
        rig.SelectAndWaitUntilReady();

        Assert.True(rig.Call(TestHookNames.InjectCameraFault, $$"""{"kind":"{{fault}}"}""").Ok);

        Assert.Equal(CameraState.NotAvailable, rig.View.State);
        Assert.Equal(reason, rig.View.Reason);
        Assert.Equal(0, rig.Fake.OpenSessionCount);

        // While the fault stays, selecting the camera again fails the same way.
        rig.Select("fake-camera");
        Assert.Equal(CameraState.NotAvailable, rig.View.State);

        Assert.True(rig.Call(TestHookNames.ClearCameraFault).Ok);
        Assert.Equal(CameraState.Checking, rig.View.State);
        rig.Run(TimeSpan.FromMilliseconds(500));

        Assert.Equal(CameraState.Ready, rig.View.State);
        Assert.Null(rig.View.Reason);
        Assert.Equal(1, rig.Fake.OpenSessionCount);
    }

    [Fact]
    public void An_injected_stale_fault_is_noticed_within_two_seconds_and_cleared_by_clearing_it()
    {
        using var rig = new CameraRig();
        rig.SelectAndWaitUntilReady();

        rig.Call(TestHookNames.InjectCameraFault, """{"kind":"stale"}""");
        rig.Run(TimeSpan.FromMilliseconds(1500));
        Assert.Equal(CameraState.Ready, rig.View.State);
        rig.Run(TimeSpan.FromMilliseconds(750));

        Assert.Equal(CameraState.NotAvailable, rig.View.State);
        Assert.Equal(CameraFailureKind.Stale, rig.View.Reason);

        rig.Call(TestHookNames.ClearCameraFault);
        rig.Run(TimeSpan.FromMilliseconds(500));

        Assert.Equal(CameraState.Ready, rig.View.State);
        Assert.Equal(1, rig.Fake.OpenSessionCount);
    }

    [Fact]
    public void Clearing_a_stale_fault_before_it_is_noticed_keeps_the_open_session()
    {
        using var rig = new CameraRig();
        rig.SelectAndWaitUntilReady();
        var before = rig.Preview.Posted.Count;
        rig.Call(TestHookNames.InjectCameraFault, """{"kind":"stale"}""");
        rig.Run(TimeSpan.FromMilliseconds(500));
        var during = rig.Preview.Posted.Count;

        rig.Call(TestHookNames.ClearCameraFault);
        rig.Run(TimeSpan.FromMilliseconds(500));

        Assert.True(during - before < 20, "The stale preview should have stopped.");
        Assert.True(rig.Preview.Posted.Count > during + 5, "The preview resumes after the fault is cleared.");
        Assert.Equal(CameraState.Ready, rig.View.State);
        Assert.Equal(1, rig.Fake.OpenSessionCount);
    }

    [Fact]
    public void The_fault_hooks_are_not_supported_without_a_fault_control_and_fail_when_it_throws()
    {
        using var plain = new CameraRig(withFaults: false);
        Assert.Equal(TestHookError.NotSupported, plain.Call(TestHookNames.InjectCameraFault, """{"kind":"stale"}""").Error);
        Assert.Equal(TestHookError.NotSupported, plain.Call(TestHookNames.ClearCameraFault).Error);

        var control = new RecordingFaultControl { Failure = new InvalidOperationException("No fault today.") };
        using var harness = new BrainHarness(hooks: true, cameraFaults: control);
        var client = harness.Connect(Role.Operator);
        var inject = Call(harness, client, TestHookNames.InjectCameraFault, """{"kind":"failed"}""");
        var clear = Call(harness, client, TestHookNames.ClearCameraFault);

        Assert.Equal(TestHookError.HookFailed, inject.Error);
        Assert.Equal(TestHookError.HookFailed, clear.Error);
        Assert.Equal(["Inject(Failed)", "Clear"], control.Calls);
    }

    [Fact]
    public void A_fault_hook_with_no_selected_camera_is_accepted_and_changes_nothing()
    {
        var control = new RecordingFaultControl();
        using var harness = new BrainHarness(hooks: true, cameraFaults: control);
        var client = harness.Connect(Role.Operator);

        var inject = Call(harness, client, TestHookNames.InjectCameraFault, """{"kind":"removed"}""");
        var clear = Call(harness, client, TestHookNames.ClearCameraFault);

        Assert.True(inject.Ok);
        Assert.True(clear.Ok);
        Assert.Equal(0, harness.Camera.OpenSessionCount);
        Assert.Equal(["Inject(Removed)", "Clear"], control.Calls);
    }

    private static string[] Cameras(CameraRig rig) =>
    [
        .. ProtocolJson.Deserialize<ListCamerasResult>(rig.Call(TestHookNames.ListCameras).Result!.Value).Cameras.Select(camera => camera.Id),
    ];

    private static TestResultMessage CaptureWithFrames(CameraRig rig)
    {
        var id = rig.Operator.Test(TestHookNames.CaptureDiagnostics);
        rig.Harness.Run();
        rig.Advance(CameraRig.Frame * 2);
        var result = rig.Await(id);
        Assert.True(result.Ok);
        return result;
    }

    private static ScriptedCameraSession ReadySession(BrainHarness harness, TestClient client, ScriptedCameraAdapter scripted)
    {
        var session = scripted.NewSession("scripted");
        scripted.OpenBehavior = (_, _) => Task.FromResult<ICameraSession>(session);
        Call(harness, client, TestHookNames.OpenCameraDiagnostics);
        Call(harness, client, TestHookNames.SelectDiagnosticsCamera, """{"deviceId":"scripted"}""");
        session.Deliver(TimeSpan.FromSeconds(1));
        harness.Clock.Advance(Brain.FreshnessPollInterval);
        harness.Run();
        return session;
    }

    private static TestResultMessage Call(BrainHarness harness, TestClient client, string name, string? args = null)
    {
        var id = client.Test(name, args);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            harness.Run();
            if (client.Messages.OfType<TestResultMessage>().FirstOrDefault(message => message.Id == id) is { } result)
            {
                return result;
            }

            Assert.True(DateTime.UtcNow < deadline, $"{name} was never answered.");
            Thread.Sleep(2);
        }
    }

    private static CameraDiagnostics Diagnostics(BrainHarness harness, TestClient client) =>
        ProtocolJson.Deserialize<DiagnosticsResult>(Call(harness, client, TestHookNames.GetDiagnostics).Result!.Value).Camera;

    private sealed class RecordingFaultControl : ICameraFaultControl
    {
        public List<string> Calls { get; } = [];

        public Exception? Failure { get; init; }

        public void InjectFault(CameraFaultKind kind)
        {
            Calls.Add($"Inject({kind})");
            if (Failure is not null)
            {
                throw Failure;
            }
        }

        public void ClearFault()
        {
            Calls.Add("Clear");
            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }
}
