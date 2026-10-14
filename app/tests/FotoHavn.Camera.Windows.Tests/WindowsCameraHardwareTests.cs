using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Protocol.Cameras;
using SkiaSharp;
using Windows.Media.Capture;

namespace FotoHavn.Camera.Windows.Tests;

/// <summary>
/// Hardware tests: they open the real webcam and report "not run: no camera" (skipped) without one. The camera on the build
/// machine may show its "camera off" placeholder, so they assert properties of the stream, never picture content. Only one
/// process holds the camera, so these share one collection and wait out collisions with other processes.
/// </summary>
[Collection("camera")]
public sealed class WindowsCameraHardwareTests
{
    [Fact]
    public async Task Lists_the_cameras_Windows_reports_with_unique_ids_and_names()
    {
        using var adapter = CameraHardware.CreateAdapter();

        var cameras = await adapter.ListAsync(TestContext.Current.CancellationToken);

        if (cameras.Count == 0)
        {
            Assert.Skip("not run: no camera");
        }

        foreach (var camera in cameras)
        {
            CameraHardware.Log($"{camera.Name} | {camera.DeviceId}");
        }

        Assert.All(cameras, camera =>
        {
            Assert.False(string.IsNullOrWhiteSpace(camera.DeviceId));
            Assert.False(string.IsNullOrWhiteSpace(camera.Name));
        });
        Assert.Equal(cameras.Count, cameras.Select(camera => camera.DeviceId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(cameras.Select(camera => camera.DeviceId), (await adapter.ListAsync(TestContext.Current.CancellationToken)).Select(camera => camera.DeviceId));
    }

    [Fact]
    public async Task Opens_exclusively_and_delivers_fresh_preview_frames_for_three_seconds()
    {
        var channel = new RecordingPreviewChannel();
        using var adapter = CameraHardware.CreateAdapter(channel);
        var camera = await CameraHardware.RequireCameraAsync(adapter);

        await using var session = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);

        Assert.Equal(camera.DeviceId, session.DeviceId);
        Assert.True(session.Format.Width >= 1280 && session.Format.Height >= 720, $"Format {session.Format}.");
        Assert.True(session.Format.FramesPerSecond >= 15, $"Format {session.Format}.");
        CameraHardware.Log($"Format {session.Format}");
        Assert.Null(session.LatestFrame);

        session.SetPreviewTarget(PreviewTarget.Operator);
        await CameraHardware.WaitForFirstFrameAsync(session, TimeSpan.FromSeconds(10));
        var startPosted = channel.Posted(PreviewTarget.Operator);
        var startSequence = session.LatestFrame!.Sequence;
        var started = session.Now;

        var worstAge = TimeSpan.Zero;
        var lastSequence = startSequence;
        var lastTimestamp = session.LatestFrame.Timestamp;
        while (session.Now - started < TimeSpan.FromSeconds(3))
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
            var latest = session.LatestFrame!;
            var age = session.Now - latest.Timestamp;
            worstAge = age > worstAge ? age : worstAge;
            Assert.True(latest.Sequence >= lastSequence);
            Assert.True(latest.Timestamp >= lastTimestamp, "Frame timestamps must never go backwards.");
            lastSequence = latest.Sequence;
            lastTimestamp = latest.Timestamp;
        }

        var elapsed = (session.Now - started).TotalSeconds;
        var arrived = lastSequence - startSequence;
        var posted = channel.Posted(PreviewTarget.Operator) - startPosted;
        var clock = ((WindowsCameraSession)session).Clock;
        CameraHardware.Log($"Worst frame age {worstAge.TotalMilliseconds:0} ms; {arrived} frames arrived and {posted} were posted in {elapsed:0.0} s ({posted / elapsed:0.0} fps); camera-time stamps {clock.CameraTimeStamps}, arrival stamps {clock.ArrivalStamps}.");

        Assert.True(worstAge < TimeSpan.FromMilliseconds(500), $"Worst frame age {worstAge.TotalMilliseconds:0} ms.");
        Assert.True(posted / elapsed >= 15, $"Preview rate {posted / elapsed:0.0} fps.");
        Assert.True(posted >= arrived * 0.9, $"{posted} of {arrived} arrived frames were posted.");

        var preview = channel.LastFrame!.Value;
        Assert.InRange(preview.Width, 1, 1280);
        Assert.Equal(preview.Width * preview.Height * 4, channel.LastPixels.Length);
        Assert.Equal(255, channel.LastPixels[3]);
        Assert.Equal(255, channel.LastPixels[^1]);
        Assert.Equal((double)session.Format.Width / session.Format.Height, (double)preview.Width / preview.Height, 0.01);
    }

    [Fact]
    public async Task Capture_returns_a_full_size_frame_taken_after_not_before_that_encodes_to_a_decodable_jpeg()
    {
        using var adapter = CameraHardware.CreateAdapter();
        var camera = await CameraHardware.RequireCameraAsync(adapter);
        await using var session = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);
        await CameraHardware.WaitForFirstFrameAsync(session, TimeSpan.FromSeconds(10));

        var notBefore = session.Now;
        var frame = await session.CaptureAsync(notBefore, TestContext.Current.CancellationToken);

        Assert.True(frame.Width >= 1280 && frame.Height >= 720, $"{frame.Width}x{frame.Height}");
        Assert.Equal((session.Format.Width, session.Format.Height), (frame.Width, frame.Height));
        Assert.True(frame.Timestamp >= notBefore, $"Frame {frame.Timestamp} is earlier than {notBefore}.");
        Assert.True(frame.Stride >= frame.Width * 4);
        Assert.Equal(frame.Stride * frame.Height, frame.Bgra.Length);

        var jpeg = JpegEncoder.Encode(frame);
        using var decoded = SKBitmap.Decode(jpeg);
        Assert.NotNull(decoded);
        Assert.Equal((frame.Width, frame.Height), (decoded.Width, decoded.Height));
        CameraHardware.Log($"Captured {frame.Width}x{frame.Height}, JPEG {jpeg.Length} bytes.");

        var later = session.Now + TimeSpan.FromMilliseconds(400);
        var second = await session.CaptureAsync(later, TestContext.Current.CancellationToken);
        Assert.True(second.Timestamp >= later);
        Assert.True(second.Timestamp > frame.Timestamp);
    }

    [Fact]
    public async Task Capture_waits_for_a_future_moment_and_is_cancelled_by_its_token()
    {
        using var adapter = CameraHardware.CreateAdapter();
        var camera = await CameraHardware.RequireCameraAsync(adapter);
        await using var session = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);
        await CameraHardware.WaitForFirstFrameAsync(session, TimeSpan.FromSeconds(10));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var pending = session.CaptureAsync(session.Now + TimeSpan.FromMinutes(5), cancellation.Token);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.False(pending.IsCompleted);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task Frames_go_only_to_the_chosen_target_and_stop_for_none()
    {
        var channel = new RecordingPreviewChannel();
        using var adapter = CameraHardware.CreateAdapter(channel);
        var camera = await CameraHardware.RequireCameraAsync(adapter);
        await using var session = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);

        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Equal(0, channel.Posted(PreviewTarget.Operator) + channel.Posted(PreviewTarget.Guest));

        session.SetPreviewTarget(PreviewTarget.Guest);
        await Task.Delay(700, TestContext.Current.CancellationToken);
        var guest = channel.Posted(PreviewTarget.Guest);
        Assert.True(guest > 0);
        Assert.Equal(0, channel.Posted(PreviewTarget.Operator));

        session.SetPreviewTarget(PreviewTarget.None);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        var settled = channel.Posted(PreviewTarget.Guest);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Equal(settled, channel.Posted(PreviewTarget.Guest));
        Assert.NotNull(session.LatestFrame);
    }

    [Fact]
    public async Task Releases_the_camera_on_dispose_so_it_can_be_opened_again()
    {
        using var adapter = CameraHardware.CreateAdapter();
        var camera = await CameraHardware.RequireCameraAsync(adapter);

        var first = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);
        var failed = 0;
        first.Failed += (_, _) => Interlocked.Increment(ref failed);
        await CameraHardware.WaitForFirstFrameAsync(first, TimeSpan.FromSeconds(10));
        await first.DisposeAsync();
        await first.DisposeAsync();

        Assert.Equal(0, adapter.HeldDeviceCount);
        await using var second = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);
        await CameraHardware.WaitForFirstFrameAsync(second, TimeSpan.FromSeconds(10));
        Assert.Equal(0, failed);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task Dispose_faults_pending_and_later_captures_with_object_disposed()
    {
        using var adapter = CameraHardware.CreateAdapter();
        var camera = await CameraHardware.RequireCameraAsync(adapter);
        var session = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);
        await CameraHardware.WaitForFirstFrameAsync(session, TimeSpan.FromSeconds(10));
        var pending = session.CaptureAsync(session.Now + TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        await session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.CaptureAsync(session.Now, TestContext.Current.CancellationToken));
        Assert.NotNull(session.LatestFrame);
    }

    [Fact]
    public async Task A_second_open_of_a_held_camera_fails_as_in_use_and_the_first_session_keeps_streaming()
    {
        using var adapter = CameraHardware.CreateAdapter();
        var camera = await CameraHardware.RequireCameraAsync(adapter);
        await using var first = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);

        var failure = await Assert.ThrowsAsync<CameraException>(() => adapter.OpenAsync(camera.DeviceId, TestContext.Current.CancellationToken));

        Assert.Equal(CameraFailureKind.InUseByAnotherApp, failure.Kind);
        await CameraHardware.WaitForFirstFrameAsync(first, TimeSpan.FromSeconds(10));
        Assert.Equal(1, adapter.HeldDeviceCount);
    }

    [Fact]
    public async Task A_second_exclusive_media_capture_in_this_process_does_not_disturb_the_first_session()
    {
        using var adapter = CameraHardware.CreateAdapter();
        var camera = await CameraHardware.RequireCameraAsync(adapter);
        await using var first = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);
        var failed = 0;
        first.Failed += (_, _) => Interlocked.Increment(ref failed);
        await CameraHardware.WaitForFirstFrameAsync(first, TimeSpan.FromSeconds(10));

        // Windows lets one process hold two exclusive MediaCaptures on a camera, so the "in use" fact inside this process is
        // the adapter's own one-session-per-device guard (tested above); across processes it is the OS (see the Camera app test).
        using var rival = new MediaCapture();
        var failure = await Record.ExceptionAsync(() => rival.InitializeAsync(new MediaCaptureInitializationSettings
        {
            VideoDeviceId = camera.DeviceId,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            SharingMode = MediaCaptureSharingMode.ExclusiveControl,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu,
        }).AsTask(TestContext.Current.CancellationToken));

        if (failure is null)
        {
            CameraHardware.Log("A second exclusive MediaCapture in the same process was ACCEPTED by Windows.");
        }
        else
        {
            var kind = ResilientPolicies.Instance.Classify(failure, CameraOperation.Open);
            CameraHardware.Log($"Second exclusive MediaCapture in-process refused: {failure.GetType().Name} 0x{failure.HResult:X8}: {failure.Message} => {kind}");
            Assert.Equal(CameraFailureKind.InUseByAnotherApp, kind);
        }

        var sequence = first.LatestFrame!.Sequence;
        await Task.Delay(750, TestContext.Current.CancellationToken);
        Assert.True(first.LatestFrame.Sequence > sequence, "The first session stopped streaming.");
        Assert.Equal(0, failed);
    }

    [Fact]
    public async Task An_unknown_device_id_fails_with_a_camera_exception_and_leaves_nothing_held()
    {
        using var adapter = CameraHardware.CreateAdapter();
        await CameraHardware.RequireCameraAsync(adapter);

        var failure = await Assert.ThrowsAsync<CameraException>(() => adapter.OpenAsync(@"\\?\no-such-camera#{e5323777-f976-4f5b-9b55-b94699c46e44}\global", TestContext.Current.CancellationToken));

        CameraHardware.Log($"Unknown device id: {failure.Kind}; inner {failure.InnerException?.GetType().Name} 0x{failure.InnerException?.HResult:X8}");
        Assert.Contains(failure.Kind, new[] { CameraFailureKind.NotFound, CameraFailureKind.Unknown });
        Assert.Equal(0, adapter.HeldDeviceCount);
    }

    [Fact]
    public async Task A_cancelled_open_leaves_nothing_open()
    {
        using var adapter = CameraHardware.CreateAdapter();
        var camera = await CameraHardware.RequireCameraAsync(adapter);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.OpenAsync(camera.DeviceId, cancelled.Token));
        Assert.Equal(0, adapter.HeldDeviceCount);

        using var midway = new CancellationTokenSource();
        var opening = adapter.OpenAsync(camera.DeviceId, midway.Token);
        midway.CancelAfter(TimeSpan.FromMilliseconds(15));
        try
        {
            await (await opening.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)).DisposeAsync();
        }
        catch (OperationCanceledException)
        {
            // The cancellation won the race: it must have released everything.
        }
        catch (CameraException exception) when (exception.Kind == CameraFailureKind.InUseByAnotherApp)
        {
            CameraHardware.Log("Another process held the camera during the mid-open cancel test.");
        }

        Assert.Equal(0, adapter.HeldDeviceCount);
        await using var again = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);
        await CameraHardware.WaitForFirstFrameAsync(again, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task The_device_watcher_starts_and_stops_without_reporting_a_change_for_the_cameras_already_there()
    {
        var changes = 0;
        var adapter = CameraHardware.CreateAdapter(watchDevices: true);
        adapter.CamerasChanged += (_, _) => Interlocked.Increment(ref changes);
        await CameraHardware.RequireCameraAsync(adapter);

        await Task.Delay(1500, TestContext.Current.CancellationToken);
        adapter.Dispose();
        adapter.Dispose();

        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task Works_with_the_real_core_policies_once_they_are_implemented()
    {
        var before = ResilientPolicies.Instance.Fallbacks;
        using var adapter = CameraHardware.CreateAdapter();
        var camera = await CameraHardware.RequireCameraAsync(adapter);
        await using var session = await CameraHardware.OpenWithRetryAsync(adapter, camera.DeviceId);
        await CameraHardware.WaitForFirstFrameAsync(session, TimeSpan.FromSeconds(10));
        await session.CaptureAsync(session.Now, TestContext.Current.CancellationToken);

        var fallbacks = ResilientPolicies.Instance.Fallbacks - before;
        if (fallbacks > 0)
        {
            Assert.Skip($"not run against the real policies: {fallbacks} call(s) fell back to the stand-in because M5 wave B1 has not landed; wave C re-runs this.");
        }

        Assert.True(session.Format.Width >= 1280);
    }
}
