using FotoHavn.Core.Camera;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Hooks;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Tests.Cameras;

public sealed class FaultInjectingCameraAdapterTests
{
    private static readonly TimeSpan Tenth = TimeSpan.FromMilliseconds(100);

    [Fact]
    public async Task Without_a_fault_the_decorator_is_transparent()
    {
        var rig = new Rig();

        Assert.Equal([FakeCameraAdapter.DefaultCamera], await rig.Faults.ListAsync(CancellationToken.None));
        await using var session = await rig.Faults.OpenAsync("fake-camera", CancellationToken.None);
        session.SetPreviewTarget(PreviewTarget.Operator);
        rig.Clock.Advance(Tenth);

        Assert.Null(rig.Faults.ActiveFault);
        Assert.Equal("fake-camera", session.DeviceId);
        Assert.Equal(new CameraFormat(1280, 720, 30, "NV12"), session.Format);
        Assert.Equal(Tenth, session.Now);
        Assert.NotNull(session.LatestFrame);
        Assert.NotEmpty(rig.Preview.Posted);
        var pending = session.CaptureAsync(session.Now, CancellationToken.None);
        rig.Clock.Advance(Tenth);
        var capture = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(capture.Timestamp >= Tenth);
    }

    [Fact]
    public async Task The_decorator_needs_an_adapter_and_a_device_id()
    {
        Assert.Throws<ArgumentNullException>(() => new FaultInjectingCameraAdapter(null!));
        var rig = new Rig();
        await Assert.ThrowsAsync<ArgumentNullException>(() => rig.Faults.OpenAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task A_stale_fault_stops_frames_and_the_preview_but_not_the_clock_until_cleared()
    {
        var rig = new Rig();
        await using var session = await rig.Open();
        rig.Clock.Advance(Tenth);
        var frozen = session.LatestFrame;
        var posted = rig.Preview.Posted.Count;

        rig.Faults.InjectFault(CameraFaultKind.Stale);
        rig.Faults.InjectFault(CameraFaultKind.Stale);
        rig.Clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(CameraFaultKind.Stale, rig.Faults.ActiveFault);
        Assert.Equal(frozen, session.LatestFrame);
        Assert.Equal(posted, rig.Preview.Posted.Count);
        Assert.Equal(Tenth + TimeSpan.FromSeconds(1), session.Now);

        rig.Faults.ClearFault();
        rig.Faults.ClearFault();
        rig.Clock.Advance(Tenth);

        Assert.Null(rig.Faults.ActiveFault);
        Assert.True(session.LatestFrame!.Sequence > frozen!.Sequence);
        Assert.True(rig.Preview.Posted.Count > posted);
    }

    [Fact]
    public async Task A_session_opened_under_a_stale_fault_delivers_nothing_until_it_is_cleared()
    {
        var rig = new Rig();
        rig.Faults.InjectFault(CameraFaultKind.Stale);
        await using var session = await rig.Open();

        session.SetPreviewTarget(PreviewTarget.Operator);
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(session.LatestFrame);
        Assert.Empty(rig.Preview.Posted);

        rig.Faults.ClearFault();
        rig.Clock.Advance(Tenth);

        Assert.NotNull(session.LatestFrame);
        Assert.NotEmpty(rig.Preview.Posted);
    }

    [Theory]
    [InlineData(CameraFaultKind.Failed, CameraFailureKind.Failed)]
    [InlineData(CameraFaultKind.ExclusiveLost, CameraFailureKind.ExclusiveLost)]
    [InlineData(CameraFaultKind.Removed, CameraFailureKind.Removed)]
    public async Task A_failing_fault_raises_failed_once_ends_the_session_and_releases_the_device(CameraFaultKind fault, CameraFailureKind kind)
    {
        var rig = new Rig();
        await using var session = await rig.Open();
        var failures = new List<CameraFailure>();
        session.Failed += (_, failure) => failures.Add(failure);

        rig.Faults.InjectFault(fault);
        rig.Faults.InjectFault(fault);

        Assert.Equal(kind, Assert.Single(failures).Kind);
        Assert.Equal(0, rig.Fake.OpenSessionCount);
        var captureFailure = await Assert.ThrowsAsync<CameraException>(() => session.CaptureAsync(TimeSpan.Zero, CancellationToken.None));
        Assert.Equal(kind, captureFailure.Kind);
        session.SetPreviewTarget(PreviewTarget.Guest);
        Assert.DoesNotContain(rig.Preview.Posted, frame => frame.Target == PreviewTarget.Guest);
    }

    [Theory]
    [InlineData(CameraFaultKind.Failed, CameraFailureKind.Failed)]
    [InlineData(CameraFaultKind.ExclusiveLost, CameraFailureKind.ExclusiveLost)]
    [InlineData(CameraFaultKind.Removed, CameraFailureKind.NotFound)]
    public async Task Opening_while_a_failing_fault_is_in_force_fails_until_it_is_cleared(CameraFaultKind fault, CameraFailureKind kind)
    {
        var rig = new Rig();
        await using (await rig.Open())
        {
            rig.Faults.InjectFault(fault);
        }

        var failure = await Assert.ThrowsAsync<CameraException>(() => rig.Open());
        Assert.Equal(kind, failure.Kind);
        Assert.Equal(0, rig.Fake.OpenSessionCount);

        rig.Faults.ClearFault();
        await using var reopened = await rig.Open();

        Assert.Equal(1, rig.Fake.OpenSessionCount);
    }

    [Fact]
    public async Task A_removed_camera_leaves_the_list_and_comes_back_with_a_cameras_changed_each_way()
    {
        var rig = new Rig([FakeCameraAdapter.DefaultCamera, new AvailableCamera("other", "Other")]);
        var changes = 0;
        rig.Faults.CamerasChanged += (_, _) => changes++;
        await using var session = await rig.Open();

        rig.Faults.InjectFault(CameraFaultKind.Removed);
        rig.Faults.InjectFault(CameraFaultKind.Removed);

        Assert.Equal(["other"], (await rig.Faults.ListAsync(CancellationToken.None)).Select(camera => camera.DeviceId));
        Assert.Equal(1, changes);
        await using var other = await rig.Faults.OpenAsync("other", CancellationToken.None);

        rig.Faults.ClearFault();

        Assert.Equal(2, changes);
        Assert.Equal(2, (await rig.Faults.ListAsync(CancellationToken.None)).Count);
        rig.Faults.ClearFault();
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task Removing_before_anything_was_opened_hides_nothing()
    {
        var rig = new Rig();
        var changes = 0;
        rig.Faults.CamerasChanged += (_, _) => changes++;

        rig.Faults.InjectFault(CameraFaultKind.Removed);

        Assert.Equal(0, changes);
        Assert.Single(await rig.Faults.ListAsync(CancellationToken.None));
    }

    [Fact]
    public void The_cameras_changed_of_the_wrapped_adapter_comes_through()
    {
        var rig = new Rig();
        var changes = 0;
        rig.Faults.CamerasChanged += (_, _) => changes++;

        rig.Fake.AddCamera(new AvailableCamera("new", "New"));

        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task A_failure_of_the_wrapped_session_is_forwarded_once()
    {
        var rig = new Rig();
        await using var session = await rig.Open();
        var failures = new List<CameraFailure>();
        session.Failed += (_, failure) => failures.Add(failure);

        rig.Fake.Fail(CameraFailureKind.PrivacyDenied);
        rig.Faults.InjectFault(CameraFaultKind.Failed);

        Assert.Equal(CameraFailureKind.PrivacyDenied, Assert.Single(failures).Kind);
    }

    [Fact]
    public async Task A_capture_waits_through_a_stale_fault_and_finishes_when_it_is_cleared()
    {
        var rig = new Rig();
        await using var session = await rig.Open();
        rig.Faults.InjectFault(CameraFaultKind.Stale);

        var capture = session.CaptureAsync(session.Now, CancellationToken.None);
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(capture.IsCompleted);

        rig.Faults.ClearFault();
        rig.Clock.Advance(Tenth);

        Assert.True((await capture.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Timestamp >= TimeSpan.Zero);
    }

    [Fact]
    public async Task A_capture_waiting_through_a_stale_fault_can_be_cancelled_failed_or_disposed()
    {
        var rig = new Rig();
        await using var session = await rig.Open();
        rig.Faults.InjectFault(CameraFaultKind.Stale);
        using var cancellation = new CancellationTokenSource();

        var cancelled = session.CaptureAsync(session.Now, cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

        var failed = session.CaptureAsync(session.Now, CancellationToken.None);
        rig.Faults.InjectFault(CameraFaultKind.Failed);
        Assert.Equal(CameraFailureKind.Failed, (await Assert.ThrowsAsync<CameraException>(() => failed)).Kind);
        rig.Faults.ClearFault();

        var second = await rig.Open();
        rig.Faults.InjectFault(CameraFaultKind.Stale);
        var pending = second.CaptureAsync(second.Now, CancellationToken.None);
        await second.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => second.CaptureAsync(TimeSpan.Zero, CancellationToken.None));
        second.SetPreviewTarget(PreviewTarget.Operator);
        await second.DisposeAsync();
        rig.Faults.InjectFault(CameraFaultKind.Failed);
        rig.Faults.ClearFault();
    }

    [Fact]
    public async Task Clearing_after_a_stale_session_was_killed_leaves_it_dead()
    {
        var rig = new Rig();
        await using var session = await rig.Open();
        rig.Faults.InjectFault(CameraFaultKind.Stale);
        rig.Faults.InjectFault(CameraFaultKind.Failed);

        rig.Faults.ClearFault();

        await Assert.ThrowsAsync<CameraException>(() => session.CaptureAsync(TimeSpan.Zero, CancellationToken.None));
        Assert.Equal(0, rig.Fake.OpenSessionCount);
    }

    [Fact]
    public async Task A_fault_injected_while_the_open_runs_applies_to_the_session_it_returns()
    {
        var scripted = new ScriptedCameraAdapter();
        var gate = new TaskCompletionSource<ICameraSession>();
        scripted.OpenBehavior = (_, _) => gate.Task;
        var faults = new FaultInjectingCameraAdapter(scripted);

        var staleOpen = faults.OpenAsync("scripted", CancellationToken.None);
        faults.InjectFault(CameraFaultKind.Stale);
        var staleInner = scripted.NewSession("scripted");
        staleInner.Deliver(TimeSpan.FromSeconds(1));
        gate.SetResult(staleInner);
        var staleSession = await staleOpen;

        Assert.Null(staleSession.LatestFrame);
        Assert.Equal([PreviewTarget.None], staleInner.Targets);

        faults.ClearFault();
        var gate2 = new TaskCompletionSource<ICameraSession>();
        scripted.OpenBehavior = (_, _) => gate2.Task;
        var failedOpen = faults.OpenAsync("scripted", CancellationToken.None);
        faults.InjectFault(CameraFaultKind.Failed);
        var failedInner = scripted.NewSession("scripted");
        gate2.SetResult(failedInner);

        Assert.Equal(CameraFailureKind.Failed, (await Assert.ThrowsAsync<CameraException>(() => failedOpen)).Kind);
        Assert.Equal(1, failedInner.Disposals);
    }

    [Fact]
    public async Task A_session_that_fails_to_close_after_a_failed_open_does_not_hide_the_open_failure()
    {
        var scripted = new ScriptedCameraAdapter();
        var gate = new TaskCompletionSource<ICameraSession>();
        scripted.OpenBehavior = (_, _) => gate.Task;
        var faults = new FaultInjectingCameraAdapter(scripted);
        var open = faults.OpenAsync("scripted", CancellationToken.None);
        faults.InjectFault(CameraFaultKind.ExclusiveLost);
        var inner = scripted.NewSession("scripted");
        inner.DisposeBehavior = () => throw new InvalidOperationException("Stuck.");
        gate.SetResult(inner);

        Assert.Equal(CameraFailureKind.ExclusiveLost, (await Assert.ThrowsAsync<CameraException>(() => open)).Kind);
    }

    private sealed class Rig
    {
        public Rig(IEnumerable<AvailableCamera>? cameras = null)
        {
            Clock = new FakeClock();
            Preview = new FakePreviewChannel(Clock);
            Fake = new FakeCameraAdapter(Clock, Preview, cameras);
            Faults = new FaultInjectingCameraAdapter(Fake);
        }

        public FakeClock Clock { get; }

        public FakePreviewChannel Preview { get; }

        public FakeCameraAdapter Fake { get; }

        public FaultInjectingCameraAdapter Faults { get; }

        public async Task<ICameraSession> Open()
        {
            var session = await Faults.OpenAsync("fake-camera", CancellationToken.None);
            session.SetPreviewTarget(PreviewTarget.Operator);
            return session;
        }
    }
}
