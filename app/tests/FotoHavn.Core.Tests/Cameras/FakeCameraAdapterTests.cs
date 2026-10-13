using System.Runtime.InteropServices;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Fakes;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Tests.Cameras;

public sealed class FakeCameraAdapterTests
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30);

    [Fact]
    public async Task It_lists_the_default_camera_or_the_configured_ones()
    {
        var ct = TestContext.Current.CancellationToken;
        var byDefault = new FakeCameraAdapter(new FakeClock());
        var configured = new FakeCameraAdapter(new FakeClock(), cameras: [new AvailableCamera("a", "Camera A"), new AvailableCamera("b", "Camera B")]);

        Assert.Equal([new AvailableCamera("fake-camera", "Fake camera")], await byDefault.ListAsync(ct));
        Assert.Equal(["a", "b"], (await configured.ListAsync(ct)).Select(camera => camera.DeviceId));
    }

    [Fact]
    public async Task Listing_honours_cancellation()
    {
        var adapter = new FakeCameraAdapter(new FakeClock());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.ListAsync(cancelled.Token));
    }

    [Fact]
    public async Task An_open_session_reports_its_device_format_and_a_clock_that_starts_at_zero()
    {
        var clock = new FakeClock();
        var adapter = new FakeCameraAdapter(clock, framesPerSecond: 25);

        await using var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);

        Assert.Equal("fake-camera", session.DeviceId);
        Assert.Equal(new CameraFormat(1280, 720, 25, "NV12"), session.Format);
        Assert.Equal(TimeSpan.Zero, session.Now);
        Assert.Null(session.LatestFrame);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(3), session.Now);
    }

    [Fact]
    public async Task Frames_arrive_at_the_configured_rate_on_the_clock()
    {
        var clock = new FakeClock();
        var adapter = new FakeCameraAdapter(clock);
        await using var session = (FakeCameraSession)await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(3, session.FramesProduced);
        Assert.Equal(new CameraFrameInfo(3, 3 * FrameInterval, 1280, 720), session.LatestFrame);
    }

    [Fact]
    public async Task Preview_frames_go_only_to_the_chosen_target()
    {
        var clock = new FakeClock();
        var channel = new FakePreviewChannel(clock) { KeepPixels = true };
        var adapter = new FakeCameraAdapter(clock, channel);
        await using var session = (FakeCameraSession)await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);

        clock.Advance(FrameInterval);
        Assert.Empty(channel.Posted);
        Assert.Equal(PreviewTarget.None, session.PreviewTarget);

        session.SetPreviewTarget(PreviewTarget.Operator);
        clock.Advance(FrameInterval);
        session.SetPreviewTarget(PreviewTarget.Guest);
        clock.Advance(FrameInterval);
        session.SetPreviewTarget(PreviewTarget.None);
        clock.Advance(FrameInterval);

        Assert.Equal([(PreviewTarget.Operator, 2L), (PreviewTarget.Guest, 3L)], channel.Posted.Select(post => (post.Target, post.Sequence)));
        var first = channel.Posted[0];
        Assert.Equal((1280, 720, 2 * FrameInterval), (first.Width, first.Height, first.Timestamp));
        var expected = new byte[1280 * 720 * 4];
        FakeTestPattern.Render(expected, 1280, 720, 2, rgba: true);
        Assert.True(expected.AsSpan().SequenceEqual(first.Rgba));
    }

    [Fact]
    public async Task A_frame_the_page_has_not_acked_makes_the_next_ones_drop()
    {
        var clock = new FakeClock();
        var channel = new FakePreviewChannel(clock) { AutoAcknowledge = false };
        var adapter = new FakeCameraAdapter(clock, channel);
        await using var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        session.SetPreviewTarget(PreviewTarget.Guest);

        clock.Advance(3 * FrameInterval);

        Assert.Equal([1L], channel.Posted.Select(post => post.Sequence));
        Assert.Equal(2, channel.GetStats(PreviewTarget.Guest).Dropped);
        Assert.True(channel.Acknowledge(PreviewTarget.Guest));
        clock.Advance(FrameInterval);
        Assert.Equal([1L, 4L], channel.Posted.Select(post => post.Sequence));
    }

    [Fact]
    public async Task A_capture_after_a_timestamp_waits_for_the_first_frame_at_or_after_it()
    {
        var clock = new FakeClock();
        var adapter = new FakeCameraAdapter(clock);
        await using var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(200));
        var notBefore = session.Now + TimeSpan.FromMilliseconds(50);

        var capture = session.CaptureAsync(notBefore, TestContext.Current.CancellationToken);
        clock.Advance(FrameInterval);
        Assert.False(capture.IsCompleted);
        clock.Advance(FrameInterval);

        var frame = await capture;
        Assert.True(frame.Timestamp >= notBefore);
        Assert.Equal(8 * FrameInterval, frame.Timestamp);
        Assert.Equal(1280, frame.Width);
        Assert.Equal(720, frame.Height);
        Assert.Equal(1280 * 4, frame.Stride);
        var expected = new byte[1280 * 720 * 4];
        FakeTestPattern.Render(expected, 1280, 720, 8, rgba: false);
        Assert.True(expected.AsSpan().SequenceEqual(frame.Bgra.Span));
    }

    [Fact]
    public async Task A_capture_never_returns_a_frame_from_before_the_timestamp()
    {
        var clock = new FakeClock();
        var adapter = new FakeCameraAdapter(clock);
        await using var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        var latestBefore = session.LatestFrame!.Timestamp;

        var capture = session.CaptureAsync(latestBefore + TimeSpan.FromTicks(1), TestContext.Current.CancellationToken);
        Assert.False(capture.IsCompleted);
        clock.Advance(FrameInterval);

        Assert.True((await capture).Timestamp > latestBefore);
    }

    [Fact]
    public async Task A_capture_whose_timestamp_the_newest_frame_already_meets_completes_at_once()
    {
        var clock = new FakeClock();
        var adapter = new FakeCameraAdapter(clock);
        await using var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(100));

        var capture = session.CaptureAsync(session.LatestFrame!.Timestamp, TestContext.Current.CancellationToken);

        Assert.True(capture.IsCompletedSuccessfully);
        Assert.Equal(session.LatestFrame.Timestamp, (await capture).Timestamp);
    }

    [Fact]
    public async Task Captures_return_the_fixture_photographs_in_turn_as_copies()
    {
        var clock = new FakeClock();
        byte[] red = [0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255];
        byte[] blue = [255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255];
        var adapter = new FakeCameraAdapter(clock, photographs: [new FixturePhotograph(red, 2, 2), new FixturePhotograph(blue, 4, 1)]);
        await using var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        clock.Advance(FrameInterval);
        var now = session.Now;

        var first = await session.CaptureAsync(now, TestContext.Current.CancellationToken);
        Assert.True(MemoryMarshal.TryGetArray(first.Bgra, out var segment));
        segment.Array![0] = 9;
        var second = await session.CaptureAsync(now, TestContext.Current.CancellationToken);
        var third = await session.CaptureAsync(now, TestContext.Current.CancellationToken);

        Assert.Equal((2, 2, 8), (first.Width, first.Height, first.Stride));
        Assert.Equal((4, 1, 16), (second.Width, second.Height, second.Stride));
        Assert.Equal(blue, second.Bgra.ToArray());
        Assert.Equal(red, third.Bgra.ToArray());
        Assert.Equal(0, red[0]);
    }

    [Fact]
    public async Task A_cancelled_capture_is_cancelled_and_later_frames_are_unaffected()
    {
        var clock = new FakeClock();
        var adapter = new FakeCameraAdapter(clock);
        await using var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();

        var capture = session.CaptureAsync(TimeSpan.FromSeconds(10), cancellation.Token);
        await cancellation.CancelAsync();
        clock.Advance(TimeSpan.FromSeconds(11));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
        Assert.Equal(330, session.LatestFrame!.Sequence);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.CaptureAsync(TimeSpan.Zero, cancellation.Token));
    }

    [Fact]
    public async Task Disposing_a_session_releases_the_camera_and_fails_waiting_captures()
    {
        var clock = new FakeClock();
        var adapter = new FakeCameraAdapter(clock);
        var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        var waiting = session.CaptureAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await session.DisposeAsync();
        await session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => waiting);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.CaptureAsync(TimeSpan.Zero, TestContext.Current.CancellationToken));
        Assert.Equal(0, adapter.OpenSessionCount);
        Assert.Equal(0, clock.PendingCount);
        await using var reopened = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        Assert.Equal(1, adapter.OpenSessionCount);
    }

    [Fact]
    public async Task A_device_has_at_most_one_session()
    {
        var adapter = new FakeCameraAdapter(new FakeClock());
        await using var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);

        var second = await Assert.ThrowsAsync<CameraException>(() => adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken));

        Assert.Equal(CameraFailureKind.InUseByAnotherApp, second.Kind);
    }

    [Fact]
    public async Task Opening_an_unknown_camera_is_not_found_and_a_cancelled_open_throws()
    {
        var adapter = new FakeCameraAdapter(new FakeClock());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var notFound = await Assert.ThrowsAsync<CameraException>(() => adapter.OpenAsync("nope", TestContext.Current.CancellationToken));

        Assert.Equal(CameraFailureKind.NotFound, notFound.Kind);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.OpenAsync("fake-camera", cancelled.Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.OpenAsync(null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(CameraFailureKind.Failed)]
    [InlineData(CameraFailureKind.ExclusiveLost)]
    [InlineData(CameraFailureKind.Unknown)]
    public async Task Fail_kills_open_sessions_once_and_blocks_opening_until_recover(CameraFailureKind kind)
    {
        var clock = new FakeClock();
        var adapter = new FakeCameraAdapter(clock, new FakePreviewChannel(clock));
        var session = (FakeCameraSession)await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        session.SetPreviewTarget(PreviewTarget.Operator);
        var failures = new List<CameraFailure>();
        session.Failed += (_, failure) => failures.Add(failure);
        var waiting = session.CaptureAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        var framesBefore = session.FramesProduced;

        adapter.Fail(kind);
        adapter.Fail(kind);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(kind, Assert.Single(failures).Kind);
        Assert.Equal(kind, (await Assert.ThrowsAsync<CameraException>(() => waiting)).Kind);
        Assert.Equal(framesBefore, session.FramesProduced);
        Assert.Equal(PreviewTarget.None, session.PreviewTarget);
        Assert.Equal(kind, (await Assert.ThrowsAsync<CameraException>(() => session.CaptureAsync(TimeSpan.Zero, TestContext.Current.CancellationToken))).Kind);
        Assert.Equal(kind, (await Assert.ThrowsAsync<CameraException>(() => adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken))).Kind);
        Assert.Equal(0, adapter.OpenSessionCount);

        adapter.Recover();
        await using var reopened = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        clock.Advance(FrameInterval);
        Assert.Equal(1, reopened.LatestFrame!.Sequence);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Stall_stops_frames_without_a_failure_and_recover_resumes_them()
    {
        var clock = new FakeClock();
        var channel = new FakePreviewChannel(clock);
        var adapter = new FakeCameraAdapter(clock, channel);
        await using var session = (FakeCameraSession)await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        session.SetPreviewTarget(PreviewTarget.Guest);
        var failed = false;
        session.Failed += (_, _) => failed = true;
        clock.Advance(3 * FrameInterval);
        var latest = session.LatestFrame;

        adapter.Stall();
        clock.Advance(TimeSpan.FromSeconds(3));

        Assert.Same(latest, session.LatestFrame);
        Assert.Equal(3, channel.Posted.Count);
        Assert.False(failed);
        Assert.True(session.Now - session.LatestFrame!.Timestamp > TimeSpan.FromSeconds(2));

        adapter.Recover();
        clock.Advance(FrameInterval);
        Assert.Equal(4, session.LatestFrame!.Sequence);
        Assert.Equal(4, channel.Posted.Count);
    }

    [Fact]
    public async Task Disappear_unplugs_the_camera_and_recover_plugs_it_back_in()
    {
        var clock = new FakeClock();
        var adapter = new FakeCameraAdapter(clock, cameras: [new AvailableCamera("a", "A"), new AvailableCamera("b", "B")]);
        var session = await adapter.OpenAsync("a", TestContext.Current.CancellationToken);
        var changes = 0;
        adapter.CamerasChanged += (_, _) => changes++;
        var failures = new List<CameraFailure>();
        session.Failed += (_, failure) => failures.Add(failure);

        Assert.True(adapter.Disappear("a"));
        Assert.False(adapter.Disappear("a"));
        Assert.False(adapter.Disappear("unknown"));

        Assert.Equal(1, changes);
        Assert.Equal(CameraFailureKind.Removed, Assert.Single(failures).Kind);
        Assert.Equal(["b"], (await adapter.ListAsync(TestContext.Current.CancellationToken)).Select(camera => camera.DeviceId));
        Assert.Equal(CameraFailureKind.NotFound, (await Assert.ThrowsAsync<CameraException>(() => adapter.OpenAsync("a", TestContext.Current.CancellationToken))).Kind);

        adapter.Recover();
        adapter.Recover();

        Assert.Equal(2, changes);
        Assert.Equal(["a", "b"], (await adapter.ListAsync(TestContext.Current.CancellationToken)).Select(camera => camera.DeviceId));
        await using var reopened = await adapter.OpenAsync("a", TestContext.Current.CancellationToken);
        await session.DisposeAsync();
        Assert.Equal(1, adapter.OpenSessionCount);
    }

    [Fact]
    public async Task Disappear_of_a_camera_with_no_session_only_changes_the_list()
    {
        var adapter = new FakeCameraAdapter(new FakeClock());
        var changes = 0;
        adapter.CamerasChanged += (_, _) => changes++;

        Assert.True(adapter.Disappear("fake-camera"));

        Assert.Equal(1, changes);
        Assert.Empty(await adapter.ListAsync(TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => adapter.Disappear(null!));
    }

    [Fact]
    public async Task In_use_by_another_app_blocks_opening_that_camera_until_recover_and_spares_open_sessions()
    {
        var adapter = new FakeCameraAdapter(new FakeClock(), cameras: [new AvailableCamera("a", "A"), new AvailableCamera("b", "B")]);
        await using var open = await adapter.OpenAsync("b", TestContext.Current.CancellationToken);

        adapter.InUseByAnotherApp("a");
        adapter.InUseByAnotherApp("b");

        Assert.Equal(CameraFailureKind.InUseByAnotherApp, (await Assert.ThrowsAsync<CameraException>(() => adapter.OpenAsync("a", TestContext.Current.CancellationToken))).Kind);
        Assert.Equal(1, adapter.OpenSessionCount);

        adapter.Recover();

        await using var opened = await adapter.OpenAsync("a", TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentNullException>(() => adapter.InUseByAnotherApp(null!));
    }

    [Fact]
    public async Task Adding_a_camera_lists_it_renames_an_existing_one_and_raises_the_change_event()
    {
        var adapter = new FakeCameraAdapter(new FakeClock());
        var changes = 0;
        adapter.CamerasChanged += (_, _) => changes++;
        adapter.Disappear("fake-camera");

        adapter.AddCamera(new AvailableCamera("second", "Second"));
        adapter.AddCamera(new AvailableCamera("fake-camera", "Renamed"));

        Assert.Equal(3, changes);
        Assert.Equal([("fake-camera", "Renamed"), ("second", "Second")], (await adapter.ListAsync(TestContext.Current.CancellationToken)).Select(camera => (camera.DeviceId, camera.Name)));
        Assert.Throws<ArgumentNullException>(() => adapter.AddCamera(null!));
    }

    [Fact]
    public async Task A_preview_target_must_be_a_defined_value()
    {
        var adapter = new FakeCameraAdapter(new FakeClock());
        await using var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);

        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetPreviewTarget((PreviewTarget)99));
    }

    [Fact]
    public void The_constructor_validates_its_arguments()
    {
        var clock = new FakeClock();

        Assert.Throws<ArgumentNullException>(() => new FakeCameraAdapter(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FakeCameraAdapter(clock, framesPerSecond: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FakeCameraAdapter(clock, framesPerSecond: 121));
        Assert.Throws<ArgumentException>(() => new FakeCameraAdapter(clock, photographs: [new FixturePhotograph(new byte[3], 1, 1)]));
        Assert.Throws<ArgumentException>(() => new FakeCameraAdapter(clock, photographs: [new FixturePhotograph([], 0, 0)]));
    }

    [Fact]
    public async Task A_session_disposed_while_a_frame_is_delivered_stops_the_timer()
    {
        var clock = new FakeClock();
        var channel = new ActionChannel();
        var adapter = new FakeCameraAdapter(clock, channel);
        var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        session.SetPreviewTarget(PreviewTarget.Operator);
        channel.OnPost = () => session.DisposeAsync().AsTask().GetAwaiter().GetResult();

        clock.Advance(3 * FrameInterval);

        Assert.Equal(1, channel.Posts);
        Assert.Equal(0, clock.PendingCount);
    }

    [Fact]
    public async Task A_failure_while_a_frame_is_delivered_stops_the_timer()
    {
        var clock = new FakeClock();
        var channel = new ActionChannel();
        var adapter = new FakeCameraAdapter(clock, channel);
        await using var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        session.SetPreviewTarget(PreviewTarget.Operator);
        channel.OnPost = () => adapter.Fail(CameraFailureKind.Failed);

        clock.Advance(3 * FrameInterval);

        Assert.Equal(1, channel.Posts);
        Assert.Equal(0, clock.PendingCount);
    }

    [Fact]
    public async Task On_the_real_clock_frames_arrive_in_real_time_and_stop_on_dispose()
    {
        var adapter = new FakeCameraAdapter(SystemClock.Instance, framesPerSecond: 120);
        var session = await adapter.OpenAsync("fake-camera", TestContext.Current.CancellationToken);

        var capture = await session.CaptureAsync(session.Now + TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await session.DisposeAsync();

        Assert.True(capture.Timestamp >= TimeSpan.FromMilliseconds(30));
        Assert.Equal(1280, capture.Width);
    }

    private sealed class ActionChannel : IPreviewChannel
    {
        public int Posts { get; private set; }

        public Action? OnPost { get; set; }

        public bool TryPost(PreviewTarget target, PreviewFrame frame)
        {
            Posts++;
            OnPost?.Invoke();
            return true;
        }

        public PreviewFrameStats GetStats(PreviewTarget target) => default;
    }
}
