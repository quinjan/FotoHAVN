using FotoHavn.Core.Camera;
using FotoHavn.Protocol.Cameras;
using Microsoft.Extensions.Logging.Abstractions;

namespace FotoHavn.Camera.Windows.Tests;

public sealed class FramePipelineTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(33);

    private readonly RecordingPreviewChannel _channel = new();

    private FramePipeline Create() => new(_channel, TestPolicies.Instance, NullLogger.Instance);

    [Fact]
    public void Latest_frame_is_null_until_the_first_frame_and_then_counts_from_one()
    {
        var pipeline = Create();
        Assert.Null(pipeline.LatestFrame);

        pipeline.Process(Frame, new SolidFrame(1280, 720, 1, 2, 3));
        Assert.Equal(new CameraFrameInfo(1, Frame, 1280, 720), pipeline.LatestFrame);

        pipeline.Process(Frame * 2, new SolidFrame(1920, 1080, 1, 2, 3));
        Assert.Equal(new CameraFrameInfo(2, Frame * 2, 1920, 1080), pipeline.LatestFrame);
    }

    [Fact]
    public void Nothing_is_copied_while_no_target_is_set_and_no_capture_waits()
    {
        var pipeline = Create();
        var frame = new SolidFrame(1280, 720, 1, 2, 3);

        pipeline.Process(Frame, frame);

        Assert.Equal(0, frame.CopyCount);
        Assert.Equal(PreviewTarget.None, pipeline.Target);
        Assert.Equal(0, _channel.Posted(PreviewTarget.Operator));
    }

    [Theory]
    [InlineData(PreviewTarget.Operator)]
    [InlineData(PreviewTarget.Guest)]
    public void A_preview_target_receives_1280_wide_rgba_frames_with_the_frame_sequence_and_timestamp(PreviewTarget target)
    {
        var pipeline = Create();
        pipeline.SetPreviewTarget(target);

        pipeline.Process(Frame, new SolidFrame(1920, 1080, blue: 10, green: 20, red: 30));
        pipeline.Process(Frame * 2, new SolidFrame(1920, 1080, blue: 10, green: 20, red: 30));

        Assert.Equal(2, _channel.Posted(target));
        var posted = _channel.LastFrame!.Value;
        Assert.Equal(2, posted.Sequence);
        Assert.Equal(Frame * 2, posted.Timestamp);
        Assert.Equal((1280, 720), (posted.Width, posted.Height));
        Assert.Equal(1280 * 720 * 4, _channel.LastPixels.Length);
        Assert.Equal([(byte)30, (byte)20, (byte)10, (byte)255], _channel.LastPixels[..4]);
        Assert.Equal(2, pipeline.PreviewPosted);
        Assert.Equal(0, pipeline.PreviewDropped);
    }

    [Fact]
    public void Only_the_chosen_target_is_posted_to_and_none_stops_the_pushes()
    {
        var pipeline = Create();
        pipeline.SetPreviewTarget(PreviewTarget.Operator);
        pipeline.Process(Frame, new SolidFrame(640, 480, 1, 2, 3));
        pipeline.SetPreviewTarget(PreviewTarget.Guest);
        pipeline.Process(Frame * 2, new SolidFrame(640, 480, 1, 2, 3));
        pipeline.SetPreviewTarget(PreviewTarget.None);
        pipeline.Process(Frame * 3, new SolidFrame(640, 480, 1, 2, 3));

        Assert.Equal(1, _channel.Posted(PreviewTarget.Operator));
        Assert.Equal(1, _channel.Posted(PreviewTarget.Guest));
        Assert.Equal(2, pipeline.PreviewPosted);
        Assert.Equal(3, pipeline.LatestFrame!.Sequence);
    }

    [Fact]
    public void A_refusing_channel_counts_drops_and_never_blocks_the_frame_loop()
    {
        var pipeline = Create();
        pipeline.SetPreviewTarget(PreviewTarget.Operator);
        _channel.Accept = false;

        pipeline.Process(Frame, new SolidFrame(640, 480, 1, 2, 3));
        pipeline.Process(Frame * 2, new SolidFrame(640, 480, 1, 2, 3));
        _channel.Accept = true;
        pipeline.Process(Frame * 3, new SolidFrame(640, 480, 1, 2, 3));

        Assert.Equal(2, pipeline.PreviewDropped);
        Assert.Equal(1, pipeline.PreviewPosted);
        Assert.Equal(2, _channel.Refused(PreviewTarget.Operator));
        Assert.Equal(3, pipeline.LatestFrame!.Sequence);
    }

    [Fact]
    public void A_channel_that_throws_costs_a_drop_not_the_frame_loop()
    {
        var pipeline = Create();
        pipeline.SetPreviewTarget(PreviewTarget.Operator);
        _channel.ThrowOnPost = true;

        pipeline.Process(Frame, new SolidFrame(640, 480, 1, 2, 3));

        Assert.Equal(1, pipeline.PreviewDropped);
        Assert.Equal(1, pipeline.LatestFrame!.Sequence);
    }

    [Fact]
    public void The_preview_buffer_is_allocated_once_for_a_steady_stream()
    {
        var pipeline = Create();
        pipeline.SetPreviewTarget(PreviewTarget.Operator);

        for (var index = 1; index <= 20; index++)
        {
            pipeline.Process(Frame * index, new SolidFrame(1920, 1080, 1, 2, 3));
        }

        Assert.Equal(1, pipeline.PreviewBufferAllocations);
        Assert.Equal(20, pipeline.PreviewPosted);
    }

    [Fact]
    public void A_target_outside_the_enum_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create().SetPreviewTarget((PreviewTarget)42));

    [Fact]
    public async Task Capture_completes_with_the_first_frame_at_or_after_not_before_and_never_an_earlier_one()
    {
        var pipeline = Create();
        var notBefore = TimeSpan.FromMilliseconds(100);
        var capture = pipeline.CaptureAsync(notBefore, CancellationToken.None);

        pipeline.Process(TimeSpan.FromMilliseconds(40), new SolidFrame(64, 48, 1, 1, 1));
        pipeline.Process(TimeSpan.FromMilliseconds(99), new SolidFrame(64, 48, 2, 2, 2));
        Assert.False(capture.IsCompleted);

        pipeline.Process(notBefore, new SolidFrame(64, 48, 3, 3, 3));
        pipeline.Process(TimeSpan.FromMilliseconds(133), new SolidFrame(64, 48, 4, 4, 4));
        var frame = await capture;

        Assert.Equal(notBefore, frame.Timestamp);
        Assert.Equal((64, 48, 256), (frame.Width, frame.Height, frame.Stride));
        Assert.Equal(64 * 48 * 4, frame.Bgra.Length);
        Assert.Equal([(byte)3, (byte)3, (byte)3, (byte)255], frame.Bgra.Span[..4].ToArray());
    }

    [Fact]
    public async Task Capture_returns_full_size_bgra_with_the_source_stride()
    {
        var pipeline = Create();
        var capture = pipeline.CaptureAsync(TimeSpan.Zero, CancellationToken.None);

        pipeline.Process(Frame, new SolidFrame(100, 50, 7, 8, 9, strideBytes: 464));
        var frame = await capture;

        Assert.Equal((100, 50, 464), (frame.Width, frame.Height, frame.Stride));
        Assert.Equal(464 * 50, frame.Bgra.Length);
        Assert.Equal([(byte)7, (byte)8, (byte)9], frame.Bgra.Span[..3].ToArray());
    }

    [Fact]
    public async Task The_caller_owns_the_captured_bytes_and_later_frames_do_not_change_them()
    {
        var pipeline = Create();
        var capture = pipeline.CaptureAsync(TimeSpan.Zero, CancellationToken.None);
        pipeline.Process(Frame, new SolidFrame(32, 32, 1, 1, 1));
        var frame = await capture;
        var before = frame.Bgra.ToArray();

        pipeline.SetPreviewTarget(PreviewTarget.Operator);
        pipeline.Process(Frame * 2, new SolidFrame(32, 32, 99, 99, 99));

        Assert.Equal(before, frame.Bgra.ToArray());
        Assert.Equal(1, frame.Bgra.Span[0]);
    }

    [Fact]
    public async Task Capture_needs_no_preview_target()
    {
        var pipeline = Create();
        var capture = pipeline.CaptureAsync(TimeSpan.Zero, CancellationToken.None);

        pipeline.Process(Frame, new SolidFrame(64, 48, 1, 2, 3));
        await capture;

        Assert.Equal(0, pipeline.PreviewPosted);
        Assert.Equal(0, pipeline.PreviewDropped);
    }

    [Fact]
    public async Task One_frame_serves_every_waiting_capture_it_qualifies_for()
    {
        var pipeline = Create();
        var early = pipeline.CaptureAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None);
        var late = pipeline.CaptureAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);
        var same = pipeline.CaptureAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None);
        var frame = new SolidFrame(64, 48, 1, 2, 3);

        pipeline.Process(TimeSpan.FromMilliseconds(50), frame);

        var first = await early;
        var third = await same;
        Assert.False(late.IsCompleted);
        Assert.Equal(1, frame.CopyCount);
        Assert.Equal(first.Bgra.ToArray(), third.Bgra.ToArray());

        pipeline.Process(TimeSpan.FromMilliseconds(500), new SolidFrame(64, 48, 4, 5, 6));
        Assert.Equal(TimeSpan.FromMilliseconds(500), (await late).Timestamp);
    }

    [Fact]
    public async Task Capture_is_cancelled_by_its_token_and_a_later_frame_does_not_resurrect_it()
    {
        var pipeline = Create();
        using var cancellation = new CancellationTokenSource();
        var capture = pipeline.CaptureAsync(TimeSpan.FromSeconds(5), cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => capture);
        pipeline.Process(TimeSpan.FromSeconds(6), new SolidFrame(64, 48, 1, 2, 3));
        Assert.True(capture.IsCanceled);
    }

    [Fact]
    public async Task Capture_with_an_already_cancelled_token_is_cancelled_at_once()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var capture = Create().CaptureAsync(TimeSpan.Zero, cancellation.Token);

        Assert.True(capture.IsCanceled);
    }

    [Fact]
    public async Task Terminate_faults_pending_and_later_captures_with_the_same_exception()
    {
        var pipeline = Create();
        var pending = pipeline.CaptureAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        var reason = new CameraException(CameraFailureKind.ExclusiveLost, "Another app took over.");

        pipeline.Terminate(reason);
        pipeline.Terminate(new CameraException(CameraFailureKind.Removed, "Second reason is ignored."));

        Assert.Same(reason, await Assert.ThrowsAsync<CameraException>(() => pending));
        var later = pipeline.CaptureAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.Same(reason, await Assert.ThrowsAsync<CameraException>(() => later));
    }

    [Fact]
    public async Task Terminate_with_a_disposal_faults_captures_with_object_disposed()
    {
        var pipeline = Create();
        var pending = pipeline.CaptureAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        pipeline.Terminate(new ObjectDisposedException("session"));

        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pipeline.CaptureAsync(TimeSpan.Zero, CancellationToken.None));
    }

    [Fact]
    public void Latest_frame_keeps_its_last_value_after_terminate()
    {
        var pipeline = Create();
        pipeline.Process(Frame, new SolidFrame(64, 48, 1, 2, 3));

        pipeline.Terminate(new ObjectDisposedException("session"));

        Assert.Equal(1, pipeline.LatestFrame!.Sequence);
    }

    [Fact]
    public void Constructor_and_methods_reject_nulls()
    {
        Assert.Throws<ArgumentNullException>(() => new FramePipeline(null!, TestPolicies.Instance, NullLogger.Instance));
        Assert.Throws<ArgumentNullException>(() => new FramePipeline(_channel, null!, NullLogger.Instance));
        Assert.Throws<ArgumentNullException>(() => new FramePipeline(_channel, TestPolicies.Instance, null!));
        var pipeline = Create();
        Assert.Throws<ArgumentNullException>(() => pipeline.Terminate(null!));
        Assert.Throws<ArgumentNullException>(() => pipeline.Process(Frame, null!));
    }

    [Fact]
    public async Task Concurrent_captures_and_frames_complete_every_request_exactly_once()
    {
        var pipeline = Create();
        pipeline.SetPreviewTarget(PreviewTarget.Operator);
        var requests = Enumerable.Range(0, 200)
            .Select(index => pipeline.CaptureAsync(TimeSpan.FromMilliseconds(index), CancellationToken.None))
            .ToArray();

        var producer = Task.Run(() =>
        {
            for (var index = 0; index < 220; index++)
            {
                pipeline.Process(TimeSpan.FromMilliseconds(index), new SolidFrame(32, 24, 1, 2, 3));
            }
        }, TestContext.Current.CancellationToken);

        var frames = await Task.WhenAll(requests);
        await producer;

        for (var index = 0; index < frames.Length; index++)
        {
            Assert.True(frames[index].Timestamp >= TimeSpan.FromMilliseconds(index));
        }
    }
}
