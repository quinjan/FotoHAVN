using FotoHavn.Core.Camera;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Brains;

/// <summary>The camera state machine, ownership of the session, and the freshness timer, against the fake camera on a manual clock.</summary>
public sealed class CameraRuntimeTests
{
    private static readonly AvailableCamera Other = new("other-camera", "Other camera");

    [Fact]
    public void The_brain_lists_the_cameras_at_startup_but_shows_nothing_until_diagnostics_open()
    {
        using var rig = new CameraRig([FakeCameraAdapter.DefaultCamera, Other]);

        Assert.Equal(OperatorSurface.EventList, rig.Presentation!.Surface);
        Assert.Null(rig.Presentation.CameraDiagnostics);

        Assert.True(rig.OpenDiagnostics().Ok);

        Assert.Equal(OperatorSurface.CameraDiagnostics, rig.Presentation!.Surface);
        Assert.Equal(
            ProtocolJson.Serialize(new CameraDiagnosticsView(
                [new CameraOption("fake-camera", "Fake camera"), new CameraOption("other-camera", "Other camera")],
                null,
                CameraState.Idle,
                null,
                null,
                false,
                null,
                null)),
            ProtocolJson.Serialize(rig.View));
        Assert.Equal(0, rig.Fake.OpenSessionCount);
    }

    [Fact]
    public void A_camera_is_ready_only_after_a_fresh_frame_proves_it()
    {
        using var rig = new CameraRig();
        rig.OpenDiagnostics();

        Assert.True(rig.Select("fake-camera").Ok);
        Assert.Equal(CameraState.Checking, rig.View.State);
        Assert.Equal(new CameraFormatView(1280, 720, 30), rig.View.Format);
        Assert.Equal("fake-camera", rig.View.SelectedDeviceId);

        rig.Run(TimeSpan.FromMilliseconds(250));

        Assert.Equal(CameraState.Ready, rig.View.State);
        Assert.Null(rig.View.Reason);
    }

    [Fact]
    public void A_camera_that_opens_but_never_delivers_is_checking_and_then_stale()
    {
        using var rig = new CameraRig();
        rig.OpenDiagnostics();
        rig.Fake.Stall();
        rig.Select("fake-camera");

        rig.Run(TimeSpan.FromSeconds(2));

        Assert.Equal(CameraState.Checking, rig.View.State);

        rig.Run(TimeSpan.FromMilliseconds(500));

        Assert.Equal(CameraState.NotAvailable, rig.View.State);
        Assert.Equal(CameraFailureKind.Stale, rig.View.Reason);
        Assert.Equal(0, rig.Fake.OpenSessionCount);
    }

    [Theory]
    [InlineData(CameraFailureKind.InUseByAnotherApp, CameraState.InUseByAnotherApp)]
    [InlineData(CameraFailureKind.PrivacyDenied, CameraState.NotAvailable)]
    [InlineData(CameraFailureKind.NoSupportedFormat, CameraState.NotAvailable)]
    [InlineData(CameraFailureKind.NotFound, CameraState.NotAvailable)]
    [InlineData(CameraFailureKind.Failed, CameraState.NotAvailable)]
    [InlineData(CameraFailureKind.Unknown, CameraState.NotAvailable)]
    public void Each_open_failure_maps_to_its_state_and_reason(CameraFailureKind kind, CameraState state)
    {
        using var rig = new CameraRig();
        rig.OpenDiagnostics();
        rig.Fake.Fail(kind);

        Assert.True(rig.Select("fake-camera").Ok);

        Assert.Equal(state, rig.View.State);
        Assert.Equal(kind, rig.View.Reason);
        Assert.Null(rig.View.Format);
    }

    [Theory]
    [InlineData(CameraFailureKind.Failed)]
    [InlineData(CameraFailureKind.ExclusiveLost)]
    [InlineData(CameraFailureKind.PrivacyDenied)]
    [InlineData(CameraFailureKind.Removed)]
    [InlineData(CameraFailureKind.Unknown)]
    public void Each_streaming_failure_ends_the_session_and_maps_to_not_available(CameraFailureKind kind)
    {
        using var rig = new CameraRig();
        rig.SelectAndWaitUntilReady();

        rig.Fake.Fail(kind);
        rig.Harness.Run();

        Assert.Equal(CameraState.NotAvailable, rig.View.State);
        Assert.Equal(kind, rig.View.Reason);
        Assert.Equal(0, rig.Fake.OpenSessionCount);
        Assert.Equal(PreviewTarget.None, rig.Diagnostics().PreviewTarget);
    }

    [Fact]
    public void A_ready_camera_goes_stale_when_its_frames_stop_for_more_than_two_seconds()
    {
        using var rig = new CameraRig();
        rig.SelectAndWaitUntilReady();
        rig.Fake.Stall();

        // The latest frame is at most one frame old when it stalls, so a bit under two seconds later it is still fresh.
        rig.Run(TimeSpan.FromMilliseconds(1750));
        Assert.Equal(CameraState.Ready, rig.View.State);

        rig.Run(TimeSpan.FromMilliseconds(500));

        Assert.Equal(CameraState.NotAvailable, rig.View.State);
        Assert.Equal(CameraFailureKind.Stale, rig.View.Reason);
        Assert.Equal(0, rig.Fake.OpenSessionCount);
    }

    [Fact]
    public void Ready_goes_stale_at_exactly_two_seconds_of_frame_age_and_not_before()
    {
        var scripted = new ScriptedCameraAdapter();
        using var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var session = scripted.NewSession("scripted");
        scripted.OpenBehavior = (_, _) => Task.FromResult<ICameraSession>(session);
        var client = harness.Handshake(Role.Operator);
        Open(harness, client, "scripted");
        session.Deliver(TimeSpan.FromSeconds(10));
        Tick(harness);
        Assert.Equal(CameraState.Ready, State(client));

        session.Now = TimeSpan.FromSeconds(12);
        Tick(harness);
        Assert.Equal(CameraState.Ready, State(client));

        session.Now = TimeSpan.FromSeconds(12) + TimeSpan.FromTicks(1);
        Tick(harness);
        Assert.Equal(CameraState.NotAvailable, State(client));
        Assert.Equal(1, session.Disposals);
    }

    [Fact]
    public void The_readiness_proof_needs_a_frame_no_older_than_two_seconds()
    {
        var scripted = new ScriptedCameraAdapter();
        using var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var session = scripted.NewSession("scripted");
        session.Now = TimeSpan.FromSeconds(10);
        scripted.OpenBehavior = (_, _) => Task.FromResult<ICameraSession>(session);
        var client = harness.Handshake(Role.Operator);
        Open(harness, client, "scripted");

        // A frame 3 s old is no proof; within its first two seconds the camera is still checking.
        session.LatestFrame = new CameraFrameInfo(1, TimeSpan.FromSeconds(7), 1920, 1080);
        Tick(harness);
        Assert.Equal(CameraState.Checking, State(client));

        session.Deliver(TimeSpan.FromSeconds(10.5));
        Tick(harness);
        Assert.Equal(CameraState.Ready, State(client));
    }

    [Fact]
    public void Removing_the_selected_camera_makes_it_unavailable_and_never_substitutes_another()
    {
        using var rig = new CameraRig([FakeCameraAdapter.DefaultCamera, Other]);
        rig.SelectAndWaitUntilReady();

        Assert.True(rig.Fake.Disappear("fake-camera"));
        rig.Harness.Run();

        Assert.Equal(CameraState.NotAvailable, rig.View.State);
        Assert.Equal(CameraFailureKind.Removed, rig.View.Reason);
        Assert.Equal("fake-camera", rig.View.SelectedDeviceId);
        Assert.Equal([new CameraOption("other-camera", "Other camera")], rig.View.Cameras);
        Assert.Equal(0, rig.Fake.OpenSessionCount);

        rig.Run(TimeSpan.FromSeconds(3));

        Assert.Equal(0, rig.Fake.OpenSessionCount);
        Assert.Equal("fake-camera", rig.View.SelectedDeviceId);
    }

    [Fact]
    public void A_removed_camera_that_comes_back_is_reopened_and_becomes_ready_again()
    {
        using var rig = new CameraRig([FakeCameraAdapter.DefaultCamera, Other]);
        rig.SelectAndWaitUntilReady();
        rig.Fake.Disappear("fake-camera");
        rig.Harness.Run();

        rig.Fake.Recover();
        rig.Harness.Run();

        Assert.Equal(CameraState.Checking, rig.View.State);
        Assert.Equal(1, rig.Fake.OpenSessionCount);

        rig.Run(TimeSpan.FromMilliseconds(500));

        Assert.Equal(CameraState.Ready, rig.View.State);
        Assert.Null(rig.View.Reason);
        Assert.Equal(2, rig.View.Cameras.Count);
    }

    [Fact]
    public void A_camera_that_disappears_while_the_brain_never_opened_it_does_not_change_the_state()
    {
        using var rig = new CameraRig([FakeCameraAdapter.DefaultCamera, Other]);
        rig.OpenDiagnostics();

        rig.Fake.Disappear("other-camera");
        rig.Harness.Run();

        Assert.Equal(CameraState.Idle, rig.View.State);
        Assert.Equal([new CameraOption("fake-camera", "Fake camera")], rig.View.Cameras);
    }

    [Fact]
    public void A_camera_added_later_shows_up_in_the_list()
    {
        using var rig = new CameraRig();
        rig.OpenDiagnostics();

        rig.Fake.AddCamera(Other);
        rig.Harness.Run();

        Assert.Equal(["fake-camera", "other-camera"], rig.View.Cameras.Select(camera => camera.DeviceId));
    }

    [Fact]
    public void Selecting_a_camera_that_is_not_listed_is_an_unknown_camera_and_opens_nothing()
    {
        using var rig = new CameraRig();
        rig.OpenDiagnostics();

        var result = rig.Select("nonexistent");

        Assert.Equal(TestHookError.UnknownCamera, result.Error);
        Assert.Equal(CameraState.Idle, rig.View.State);
        Assert.Equal(0, rig.Fake.OpenSessionCount);
    }

    [Fact]
    public void Selecting_another_camera_closes_the_first_session_and_moves_the_preview()
    {
        using var rig = new CameraRig([FakeCameraAdapter.DefaultCamera, Other]);
        rig.SelectAndWaitUntilReady();

        Assert.True(rig.Select("other-camera").Ok);

        Assert.Equal(1, rig.Fake.OpenSessionCount);
        Assert.Equal(CameraState.Checking, rig.View.State);
        rig.Run(TimeSpan.FromMilliseconds(500));
        Assert.Equal(CameraState.Ready, rig.View.State);
        Assert.Equal("other-camera", rig.View.SelectedDeviceId);
        Assert.Equal(PreviewTarget.Operator, rig.Diagnostics().PreviewTarget);
        Assert.Equal("other-camera", rig.Diagnostics().DeviceId);
    }

    [Fact]
    public void Selecting_the_same_camera_twice_in_a_row_leaves_one_session()
    {
        using var rig = new CameraRig();
        rig.OpenDiagnostics();
        var first = rig.Operator.Test(TestHookNames.SelectDiagnosticsCamera, """{"deviceId":"fake-camera"}""");
        var second = rig.Operator.Test(TestHookNames.SelectDiagnosticsCamera, """{"deviceId":"fake-camera"}""");
        rig.Harness.Run();

        Assert.All(new[] { first, second }, id => Assert.True(rig.Operator.Messages.OfType<TestResultMessage>().Single(message => message.Id == id).Ok));
        Assert.Equal(1, rig.Fake.OpenSessionCount);
        rig.Run(TimeSpan.FromMilliseconds(500));
        Assert.Equal(CameraState.Ready, rig.View.State);
    }

    [Fact]
    public void Disposing_the_brain_closes_the_session_and_stops_listening_to_the_camera()
    {
        var rig = new CameraRig([FakeCameraAdapter.DefaultCamera, Other]);
        rig.SelectAndWaitUntilReady();
        Assert.Equal(1, rig.Fake.OpenSessionCount);

        rig.Dispose();

        Assert.Equal(0, rig.Fake.OpenSessionCount);
        var pending = rig.Harness.Executor.PendingCount;
        rig.Fake.AddCamera(new AvailableCamera("third", "Third"));
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(pending, rig.Harness.Executor.PendingCount);
    }

    [Fact]
    public async Task A_session_that_arrives_after_the_brain_was_disposed_is_closed()
    {
        var scripted = new ScriptedCameraAdapter();
        var gate = new TaskCompletionSource<ICameraSession>();
        var observedToken = CancellationToken.None;
        scripted.OpenBehavior = (_, token) =>
        {
            observedToken = token;
            return gate.Task;
        };
        var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var client = harness.Handshake(Role.Operator);
        Open(harness, client, "scripted");

        harness.Dispose();
        Assert.True(observedToken.IsCancellationRequested);
        var session = scripted.NewSession("scripted");
        gate.SetResult(session);
        await Task.Yield();
        harness.Run();

        Assert.Equal(1, session.Disposals);
    }

    [Fact]
    public void A_session_that_was_opened_for_an_earlier_selection_is_closed_when_it_arrives()
    {
        var scripted = new ScriptedCameraAdapter();
        scripted.Cameras = [new AvailableCamera("a", "A"), new AvailableCamera("b", "B")];
        var gates = new Dictionary<string, TaskCompletionSource<ICameraSession>>
        {
            ["a"] = new(),
            ["b"] = new(),
        };
        scripted.OpenBehavior = (id, _) => gates[id].Task;
        using var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var client = harness.Handshake(Role.Operator);
        Open(harness, client, "a");
        Open(harness, client, "b");
        var sessionA = scripted.NewSession("a");
        var sessionB = scripted.NewSession("b");

        gates["a"].SetResult(sessionA);
        harness.Run();

        Assert.Equal(1, sessionA.Disposals);
        Assert.Equal(2, scripted.OpenCalls);

        gates["b"].SetResult(sessionB);
        harness.Run();

        Assert.Equal(0, sessionB.Disposals);
        Assert.Equal([PreviewTarget.Operator], sessionB.Targets);
        Assert.Equal("b", Assert.IsType<OperatorPresentationMessage>(client.Messages[^1]).Presentation.CameraDiagnostics!.SelectedDeviceId);
    }

    [Fact]
    public void An_adapter_that_throws_instead_of_faulting_its_task_still_gives_a_state()
    {
        var scripted = new ScriptedCameraAdapter { OpenBehavior = (_, _) => throw new CameraException(CameraFailureKind.PrivacyDenied) };
        using var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var client = harness.Handshake(Role.Operator);

        Open(harness, client, "scripted");

        Assert.Equal(CameraState.NotAvailable, State(client));
        Assert.Equal(CameraFailureKind.PrivacyDenied, Reason(client));
    }

    [Fact]
    public void A_failed_camera_listing_keeps_the_previous_list_and_is_logged()
    {
        var scripted = new ScriptedCameraAdapter();
        using var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var client = harness.Handshake(Role.Operator);
        Call(harness, client, TestHookNames.OpenCameraDiagnostics);

        scripted.ListBehavior = () => Task.FromException<IReadOnlyList<AvailableCamera>>(new InvalidOperationException("No list."));
        scripted.RaiseCamerasChanged();
        harness.Run();
        scripted.ListBehavior = () => throw new InvalidOperationException("No list either.");
        scripted.RaiseCamerasChanged();
        harness.Run();

        Assert.Equal(["scripted"], ((OperatorPresentationMessage)client.Messages.Last(message => message is OperatorPresentationMessage)).Presentation.CameraDiagnostics!.Cameras.Select(camera => camera.DeviceId));
        Assert.Equal(2, harness.Logger.Entries.Count(entry => entry.Message == "Listing the cameras failed."));
    }

    [Fact]
    public void An_older_camera_listing_that_finishes_late_is_ignored()
    {
        var scripted = new ScriptedCameraAdapter();
        using var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var client = harness.Handshake(Role.Operator);
        Call(harness, client, TestHookNames.OpenCameraDiagnostics);
        var slow = new TaskCompletionSource<IReadOnlyList<AvailableCamera>>();
        scripted.ListBehavior = () => slow.Task;
        scripted.RaiseCamerasChanged();
        harness.Run();
        scripted.ListBehavior = () => Task.FromResult<IReadOnlyList<AvailableCamera>>([new AvailableCamera("fresh", "Fresh")]);
        scripted.RaiseCamerasChanged();
        harness.Run();

        slow.SetResult([new AvailableCamera("old", "Old")]);
        harness.Run();

        Assert.Equal(["fresh"], ((OperatorPresentationMessage)client.Messages.Last(message => message is OperatorPresentationMessage)).Presentation.CameraDiagnostics!.Cameras.Select(camera => camera.DeviceId));
    }

    [Fact]
    public void A_session_failure_event_from_a_closed_session_is_ignored()
    {
        var scripted = new ScriptedCameraAdapter { Cameras = [new AvailableCamera("a", "A"), new AvailableCamera("b", "B")] };
        using var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var client = harness.Handshake(Role.Operator);
        Open(harness, client, "a");
        var first = scripted.Sessions[0];
        Open(harness, client, "b");

        first.Fail(CameraFailureKind.Failed);
        harness.Run();

        Assert.Equal(CameraState.Checking, State(client));
        Assert.False(first.HasFailedSubscribers);
    }

    [Fact]
    public void A_session_that_fails_to_close_is_logged_and_does_not_block_the_next_open()
    {
        var scripted = new ScriptedCameraAdapter { Cameras = [new AvailableCamera("a", "A"), new AvailableCamera("b", "B")] };
        using var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var client = harness.Handshake(Role.Operator);
        Open(harness, client, "a");
        scripted.Sessions[0].DisposeBehavior = () => throw new InvalidOperationException("Stuck.");
        Open(harness, client, "b");
        scripted.Sessions[1].DisposeBehavior = () => ValueTask.FromException(new InvalidOperationException("Stuck async."));

        Open(harness, client, "a");

        Assert.Equal(3, scripted.OpenCalls);
        Assert.Equal(2, harness.Logger.Entries.Count(entry => entry.Message == "Closing a camera session failed."));
    }

    [Fact]
    public async Task The_next_open_waits_for_the_previous_session_to_finish_closing()
    {
        var scripted = new ScriptedCameraAdapter { Cameras = [new AvailableCamera("a", "A"), new AvailableCamera("b", "B")] };
        var closing = new TaskCompletionSource();
        using var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var client = harness.Handshake(Role.Operator);
        Open(harness, client, "a");
        scripted.Sessions[0].DisposeBehavior = () => new ValueTask(closing.Task);

        Open(harness, client, "b");
        Assert.Equal(1, scripted.OpenCalls);

        closing.SetResult();
        await WaitUntilAsync(() => scripted.OpenCalls == 2);
        harness.Run();

        Assert.Equal(2, scripted.OpenCalls);
        Assert.Equal(CameraState.Checking, State(client));
    }

    [Fact]
    public void An_unclassified_open_cancellation_is_unknown()
    {
        var scripted = new ScriptedCameraAdapter { OpenBehavior = (_, _) => Task.FromCanceled<ICameraSession>(new CancellationToken(canceled: true)) };
        using var harness = new BrainHarness(hooks: true, cameraOverride: scripted);
        var client = harness.Handshake(Role.Operator);

        Open(harness, client, "scripted");

        Assert.Equal(CameraFailureKind.Unknown, Reason(client));
    }

    [Fact]
    public void The_presentation_does_not_change_at_frame_rate()
    {
        using var rig = new CameraRig();
        rig.SelectAndWaitUntilReady();
        var before = rig.Revisions.Count;
        var frames = rig.Preview.Posted.Count;

        rig.Run(TimeSpan.FromSeconds(5));

        Assert.Equal(before, rig.Revisions.Count);
        Assert.True(rig.Preview.Posted.Count > frames + 100, "Frames should keep flowing to the channel.");
    }

    [Fact]
    public void Operator_revisions_only_increase_through_a_whole_camera_session()
    {
        using var rig = new CameraRig([FakeCameraAdapter.DefaultCamera, Other]);
        rig.SelectAndWaitUntilReady();
        rig.Call(TestHookNames.SetDiagnosticsMirror, """{"mirrored":true}""");
        rig.Fake.Stall();
        rig.Run(TimeSpan.FromSeconds(3));
        rig.Fake.Recover();
        rig.Select("other-camera");
        rig.Run(TimeSpan.FromMilliseconds(500));
        rig.Fake.Disappear("other-camera");
        rig.Harness.Run();

        var revisions = rig.Revisions;

        Assert.True(revisions.Count >= 6);
        Assert.Equal(revisions.Order(), revisions);
        Assert.Equal(revisions.Distinct(), revisions);
    }

    private static void Open(BrainHarness harness, TestClient client, string deviceId)
    {
        Call(harness, client, TestHookNames.OpenCameraDiagnostics);
        Call(harness, client, TestHookNames.SelectDiagnosticsCamera, $$"""{"deviceId":"{{deviceId}}"}""");
    }

    private static TestResultMessage Call(BrainHarness harness, TestClient client, string name, string? args = null)
    {
        var id = client.Test(name, args);
        harness.Run();
        return client.Messages.OfType<TestResultMessage>().Single(message => message.Id == id);
    }

    private static void Tick(BrainHarness harness)
    {
        harness.Clock.Advance(Brain.FreshnessPollInterval);
        harness.Run();
    }

    private static CameraDiagnosticsView View(TestClient client) =>
        ((OperatorPresentationMessage)client.Messages.Last(message => message is OperatorPresentationMessage)).Presentation.CameraDiagnostics!;

    private static CameraState State(TestClient client) => View(client).State;

    private static CameraFailureKind? Reason(TestClient client) => View(client).Reason;

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition never became true.");
            await Task.Delay(2);
        }
    }
}
