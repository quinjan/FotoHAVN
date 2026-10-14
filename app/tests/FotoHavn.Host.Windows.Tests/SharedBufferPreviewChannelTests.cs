using System.Runtime.InteropServices;
using System.Text.Json;
using FotoHavn.Core.Camera;
using FotoHavn.Host.Windows.Preview;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Host.Windows.Tests;

public sealed class SharedBufferPreviewChannelTests
{
    private readonly Queue<Action> _ui = new();
    private readonly ManualTimeProvider _time = new();
    private readonly RecordingLogger _logger = new();
    private readonly FakeSurface _operatorSurface = new();
    private readonly FakeSurface _guestSurface = new();
    private readonly SharedBufferPreviewChannel _channel;

    public SharedBufferPreviewChannelTests()
    {
        _channel = new SharedBufferPreviewChannel(_ui.Enqueue, _time, _logger);
    }

    [Fact]
    public void Attaching_a_surface_creates_one_buffer_sized_for_1280_by_720()
    {
        _channel.Attach(Role.Operator, _operatorSurface);

        var buffer = Assert.Single(_operatorSurface.Buffers);
        Assert.Equal(1280L * 720 * 4, buffer.Size);
    }

    [Fact]
    public void A_frame_is_copied_into_the_cached_buffer_and_posted_from_the_UI_thread()
    {
        _channel.Attach(Role.Operator, _operatorSurface);

        var accepted = _channel.TryPost(PreviewTarget.Operator, Frame(42, fill: 0xAB, timestampMs: 18304));

        Assert.True(accepted);
        var buffer = _operatorSurface.Buffers[0];
        Assert.Equal(Enumerable.Repeat((byte)0xAB, 32), buffer.Read(32));
        Assert.Empty(_operatorSurface.Posts);
        RunUi();
        var post = Assert.Single(_operatorSurface.Posts);
        Assert.Same(buffer, post.Buffer);
        Assert.Equal("""{"type":"previewFrame","seq":42,"width":4,"height":2,"format":"rgba","timestampMs":18304}""", post.Json);
        Assert.Single(_operatorSurface.Buffers);
    }

    [Fact]
    public void The_metadata_names_the_wire_sequence_the_page_echoes()
    {
        _channel.Attach(Role.Operator, _operatorSurface);

        _channel.TryPost(PreviewTarget.Operator, Frame(4_294_967_297, fill: 1));
        RunUi();

        using var json = JsonDocument.Parse(_operatorSurface.Posts[0].Json);
        Assert.Equal(1, json.RootElement.GetProperty("seq").GetInt64());
    }

    [Fact]
    public void A_second_frame_is_dropped_while_the_first_is_in_flight_and_does_not_touch_the_buffer()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 0x11));

        var second = _channel.TryPost(PreviewTarget.Operator, Frame(2, fill: 0x22));

        Assert.False(second);
        Assert.Equal(Enumerable.Repeat((byte)0x11, 32), _operatorSurface.Buffers[0].Read(32));
        Assert.Equal(1, _channel.GetStats(PreviewTarget.Operator).Dropped);
        RunUi();
        Assert.Single(_operatorSurface.Posts);
    }

    [Fact]
    public void The_ack_frees_the_slot_and_counts_the_frame_as_delivered()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 0x11));
        RunUi();

        Assert.True(_channel.Acknowledge(Role.Operator, 1));

        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(2, fill: 0x22)));
        var stats = _channel.GetStats(PreviewTarget.Operator);
        Assert.Equal(1, stats.Delivered);
        Assert.Equal(0, stats.Dropped);
        Assert.Equal(_time.GetUtcNow(), stats.LastDeliveredAt);
        Assert.Equal(Enumerable.Repeat((byte)0x22, 32), _operatorSurface.Buffers[0].Read(32));
    }

    [Fact]
    public void An_ack_for_another_sequence_changes_nothing()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.TryPost(PreviewTarget.Operator, Frame(7, fill: 1));

        Assert.False(_channel.Acknowledge(Role.Operator, 6));

        Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(8, fill: 2)));
        Assert.Equal(0, _channel.GetStats(PreviewTarget.Operator).Delivered);
    }

    [Fact]
    public void A_lost_ack_frees_the_slot_after_the_timeout_and_counts_a_drop()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1));
        _time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(2, fill: 2)));

        _time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(3, fill: 3)));
        Assert.Equal(2, _channel.GetStats(PreviewTarget.Operator).Dropped);
        // The late ack of the lost frame no longer matches.
        Assert.False(_channel.Acknowledge(Role.Operator, 1));
        Assert.True(_channel.Acknowledge(Role.Operator, 3));
    }

    [Fact]
    public void Delivered_per_second_counts_the_acks_of_the_last_two_seconds()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        for (var sequence = 1; sequence <= 10; sequence++)
        {
            _time.Advance(TimeSpan.FromMilliseconds(100));
            Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(sequence, fill: 1)));
            Assert.True(_channel.Acknowledge(Role.Operator, (uint)sequence));
        }

        Assert.Equal(5.0, _channel.GetStats(PreviewTarget.Operator).DeliveredPerSecond, 3);

        _time.Advance(TimeSpan.FromSeconds(2));

        var later = _channel.GetStats(PreviewTarget.Operator);
        Assert.Equal(0.0, later.DeliveredPerSecond);
        Assert.Equal(10, later.Delivered);
    }

    [Fact]
    public void Each_role_has_its_own_buffer_and_slot()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.Attach(Role.Guest, _guestSurface);

        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 0x11)));
        Assert.True(_channel.TryPost(PreviewTarget.Guest, Frame(1, fill: 0x22)));
        RunUi();

        Assert.Equal(Enumerable.Repeat((byte)0x11, 32), _operatorSurface.Buffers[0].Read(32));
        Assert.Equal(Enumerable.Repeat((byte)0x22, 32), _guestSurface.Buffers[0].Read(32));
        Assert.Single(_operatorSurface.Posts);
        Assert.Single(_guestSurface.Posts);
        Assert.False(_channel.Acknowledge(Role.Guest, 99));
        Assert.True(_channel.Acknowledge(Role.Guest, 1));
        Assert.Equal(0, _channel.GetStats(PreviewTarget.Operator).Delivered);
        Assert.Equal(1, _channel.GetStats(PreviewTarget.Guest).Delivered);
    }

    [Fact]
    public void A_role_without_a_window_has_no_target()
    {
        _channel.Attach(Role.Operator, _operatorSurface);

        Assert.False(_channel.TryPost(PreviewTarget.Guest, Frame(1, fill: 1)));

        Assert.Equal(0, _channel.GetStats(PreviewTarget.Guest).Dropped);
        Assert.Empty(_ui);
    }

    [Fact]
    public void The_none_target_accepts_nothing_and_reports_zero()
    {
        _channel.Attach(Role.Operator, _operatorSurface);

        Assert.False(_channel.TryPost(PreviewTarget.None, Frame(1, fill: 1)));

        Assert.Equal(default, _channel.GetStats(PreviewTarget.None));
    }

    [Theory]
    [InlineData(0, 2, 32)]
    [InlineData(4, 0, 32)]
    [InlineData(-4, 2, 32)]
    [InlineData(4, 2, 31)]
    public void A_malformed_frame_is_refused_without_counting(int width, int height, int bytes)
    {
        _channel.Attach(Role.Operator, _operatorSurface);

        var accepted = _channel.TryPost(PreviewTarget.Operator, new PreviewFrame(1, width, height, new byte[bytes], TimeSpan.Zero));

        Assert.False(accepted);
        Assert.Equal(default, _channel.GetStats(PreviewTarget.Operator) with { LastDeliveredAt = null });
        Assert.Empty(_ui);
    }

    [Fact]
    public void Detaching_releases_the_buffer_and_stops_the_role()
    {
        _channel.Attach(Role.Operator, _operatorSurface);

        _channel.Detach(Role.Operator, _operatorSurface);

        Assert.True(_operatorSurface.Buffers[0].Disposed);
        Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1)));
    }

    [Fact]
    public void A_frame_queued_before_the_window_went_away_is_not_posted()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1));

        _channel.Detach(Role.Operator, _operatorSurface);
        RunUi();

        Assert.Empty(_operatorSurface.Posts);
    }

    [Fact]
    public void Detaching_a_surface_that_is_no_longer_the_target_changes_nothing()
    {
        var replacement = new FakeSurface();
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.Attach(Role.Operator, replacement);

        _channel.Detach(Role.Operator, _operatorSurface);

        Assert.False(replacement.Buffers[0].Disposed);
        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1)));
    }

    [Fact]
    public void A_recreated_WebView2_gets_a_new_buffer_and_a_free_slot()
    {
        var recreated = new FakeSurface();
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1));

        _channel.Attach(Role.Operator, recreated);

        Assert.True(_operatorSurface.Buffers[0].Disposed);
        Assert.Single(recreated.Buffers);
        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(2, fill: 2)));
        RunUi();
        Assert.Empty(_operatorSurface.Posts);
        Assert.Single(recreated.Posts);
    }

    [Fact]
    public void A_larger_frame_grows_the_buffer_once_after_the_slot_is_free()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        var large = Frame(1, width: 1280, height: 721, fill: 5);

        Assert.False(_channel.TryPost(PreviewTarget.Operator, large));
        Assert.False(_channel.TryPost(PreviewTarget.Operator, large));
        RunUi();

        Assert.Equal(2, _operatorSurface.Buffers.Count);
        Assert.True(_operatorSurface.Buffers[0].Disposed);
        Assert.Equal(1280L * 721 * 4, _operatorSurface.Buffers[1].Size);
        Assert.True(_channel.TryPost(PreviewTarget.Operator, large));
        RunUi();
        var post = Assert.Single(_operatorSurface.Posts);
        Assert.Same(_operatorSurface.Buffers[1], post.Buffer);
        Assert.Equal(0, _channel.GetStats(PreviewTarget.Operator).Dropped);
        Assert.False(_channel.TryPost(PreviewTarget.Operator, large));
    }

    [Fact]
    public void A_failed_grow_keeps_the_old_buffer_and_tries_again_on_the_next_frame()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        var large = Frame(1, width: 1280, height: 721, fill: 5);
        _channel.TryPost(PreviewTarget.Operator, large);
        _operatorSurface.CreateFailure = Com("out of memory");
        RunUi();

        Assert.False(_channel.TryPost(PreviewTarget.Operator, large));

        Assert.False(_operatorSurface.Buffers[0].Disposed);
        Assert.Contains(_logger.Entries, entry => entry.EventId.Name == "previewBufferFailed");
        _operatorSurface.CreateFailure = null;
        Assert.False(_channel.TryPost(PreviewTarget.Operator, large), "the retry waits out the rate limit");
        _time.Advance(SharedBufferPreviewChannel.RetryInterval);
        Assert.False(_channel.TryPost(PreviewTarget.Operator, large));
        RunUi();
        Assert.True(_channel.TryPost(PreviewTarget.Operator, large));
    }

    [Fact]
    public void A_surface_that_cannot_make_a_buffer_leaves_the_role_without_a_target()
    {
        _operatorSurface.CreateFailure = Com("no shared buffers");

        _channel.Attach(Role.Operator, _operatorSurface);

        Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1)));
        Assert.Contains(_logger.Entries, entry => entry.EventId.Name == "previewBufferFailed");
        Assert.Equal(1, _channel.TransportFailures);
    }

    [Fact]
    public void A_buffer_that_failed_at_attach_is_created_on_a_later_frame_and_frames_then_flow()
    {
        _operatorSurface.CreateFailure = Com("no shared buffers");
        _channel.Attach(Role.Operator, _operatorSurface);

        // Inside the rate limit: no attempt, nothing queued, however many frames arrive.
        for (var sequence = 1; sequence <= 5; sequence++)
        {
            Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(sequence, fill: 1)));
        }

        Assert.Empty(_ui);
        Assert.Equal(1, _operatorSurface.CreateAttempts);

        // Two more attempts, one per second, fail as well.
        for (var retry = 2; retry <= 3; retry++)
        {
            _time.Advance(TimeSpan.FromMilliseconds(999));
            Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(10, fill: 1)));
            Assert.Empty(_ui);
            _time.Advance(TimeSpan.FromMilliseconds(1));
            Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(11, fill: 1)));
            Assert.Single(_ui);
            Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(12, fill: 1)), "one creation at a time");
            Assert.Single(_ui);
            RunUi();
            Assert.Equal(retry, _operatorSurface.CreateAttempts);
        }

        Assert.Equal(3, _channel.TransportFailures);
        Assert.Single(_logger.Entries, entry => entry.EventId.Name == "previewBufferFailed");

        _operatorSurface.CreateFailure = null;
        _time.Advance(SharedBufferPreviewChannel.RetryInterval);
        Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(20, fill: 1)));
        RunUi();
        Assert.Equal(4, _operatorSurface.CreateAttempts);

        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(21, fill: 0x33)));
        RunUi();
        var post = Assert.Single(_operatorSurface.Posts);
        Assert.Same(Assert.Single(_operatorSurface.Buffers), post.Buffer);
        Assert.Equal(Enumerable.Repeat((byte)0x33, 32), post.Pixels.Take(32));
        Assert.Contains(_logger.Entries, entry => entry.EventId.Name == "previewSurfaceRecovered");
        Assert.Equal(3, _channel.TransportFailures);
    }

    [Fact]
    public void Detaching_a_surface_without_a_buffer_stops_its_retries()
    {
        _operatorSurface.CreateFailure = Com("no shared buffers");
        _channel.Attach(Role.Operator, _operatorSurface);

        _channel.Detach(Role.Operator, _operatorSurface);
        _time.Advance(SharedBufferPreviewChannel.RetryInterval);

        Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1)));
        Assert.Empty(_ui);
    }

    [Fact]
    public void A_retry_that_finishes_after_the_surface_was_replaced_is_discarded()
    {
        var replacement = new FakeSurface();
        _operatorSurface.CreateFailure = Com("no shared buffers");
        _channel.Attach(Role.Operator, _operatorSurface);
        _time.Advance(SharedBufferPreviewChannel.RetryInterval);
        _operatorSurface.CreateFailure = null;
        _channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1));

        _channel.Attach(Role.Operator, replacement);
        RunUi();

        Assert.Single(replacement.Buffers);
        Assert.True(_operatorSurface.Buffers[0].Disposed);
        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(2, fill: 2)));
        RunUi();
        Assert.Single(replacement.Posts);
    }

    [Fact]
    public void Repeated_post_failures_are_counted_and_logged_once_until_a_post_works()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _operatorSurface.PostFailure = Com("page gone");

        for (var sequence = 1; sequence <= 3; sequence++)
        {
            Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(sequence, fill: 1)));
            RunUi();
        }

        Assert.Equal(3, _channel.TransportFailures);
        Assert.Single(_logger.Entries, entry => entry.EventId.Name == "previewBufferFailed");

        _operatorSurface.PostFailure = null;
        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(4, fill: 1)));
        RunUi();

        Assert.Single(_operatorSurface.Posts);
        Assert.Single(_logger.Entries, entry => entry.EventId.Name == "previewSurfaceRecovered");
        Assert.Equal(3, _channel.TransportFailures);
    }

    [Theory]
    [InlineData(typeof(COMException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ObjectDisposedException))]
    public void A_failed_post_frees_the_slot_and_is_logged(Type failure)
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _operatorSurface.PostFailure = failure == typeof(ObjectDisposedException)
            ? new ObjectDisposedException("core")
            : (Exception)Activator.CreateInstance(failure)!;
        _channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1));

        RunUi();

        Assert.Contains(_logger.Entries, entry => entry.EventId.Name == "previewBufferFailed");
        _operatorSurface.PostFailure = null;
        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(2, fill: 2)));
    }

    [Fact]
    public void Reset_in_flight_frees_the_slot_without_counting_a_frame()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1));

        _channel.ResetInFlight(Role.Operator);

        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(2, fill: 2)));
        Assert.Equal(0, _channel.GetStats(PreviewTarget.Operator).Delivered);
    }

    [Theory]
    [InlineData("""{"type":"frameAck","seq":1}""")]
    [InlineData("""{ "seq": 1, "type": "frameAck" }""")]
    public void A_well_formed_ack_is_taken_and_frees_the_slot(string text)
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1));

        Assert.True(_channel.TryHandleAck(Role.Operator, text));

        Assert.Equal(1, _channel.GetStats(PreviewTarget.Operator).Delivered);
    }

    [Theory]
    [InlineData("""{"type":"hello","protocolVersion":6}""")]
    [InlineData("""{"type":"frameAck","seq":-1}""")]
    [InlineData("""{"type":"frameAck","seq":1,"role":"guest"}""")]
    [InlineData("""{"type":"frameAck"}""")]
    [InlineData("not json frameAck")]
    public void Anything_else_is_left_for_the_brain(string text)
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1));

        Assert.False(_channel.TryHandleAck(Role.Operator, text));

        Assert.Equal(0, _channel.GetStats(PreviewTarget.Operator).Delivered);
    }

    [Fact]
    public void A_long_message_is_never_parsed_as_an_ack()
    {
        var padded = """{"type":"frameAck","seq":1,"note":"{""" + new string('x', 200) + """}"}""";

        Assert.False(_channel.TryHandleAck(Role.Operator, padded));
    }

    [Fact]
    public void An_ack_acks_the_role_it_came_from()
    {
        _channel.Attach(Role.Operator, _operatorSurface);
        _channel.Attach(Role.Guest, _guestSurface);
        _channel.TryPost(PreviewTarget.Operator, Frame(1, fill: 1));
        _channel.TryPost(PreviewTarget.Guest, Frame(1, fill: 1));

        _channel.TryHandleAck(Role.Guest, """{"type":"frameAck","seq":1}""");

        Assert.Equal(0, _channel.GetStats(PreviewTarget.Operator).Delivered);
        Assert.Equal(1, _channel.GetStats(PreviewTarget.Guest).Delivered);
    }

    [Fact]
    public void The_constructor_and_attach_reject_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new SharedBufferPreviewChannel(null!, _time, _logger));
        Assert.Throws<ArgumentNullException>(() => new SharedBufferPreviewChannel(_ui.Enqueue, null!, _logger));
        Assert.Throws<ArgumentNullException>(() => new SharedBufferPreviewChannel(_ui.Enqueue, _time, null!));
        Assert.Throws<ArgumentNullException>(() => _channel.Attach(Role.Operator, null!));
        Assert.Throws<ArgumentNullException>(() => _channel.Detach(Role.Operator, null!));
        Assert.Throws<ArgumentNullException>(() => _channel.TryHandleAck(Role.Operator, null!));
    }

    // A WebView2 call fails with a COMException; the channel must treat it as a dead page, not a crash.
#pragma warning disable CA2201
    private static COMException Com(string message) => new(message);
#pragma warning restore CA2201

    private static PreviewFrame Frame(long sequence, byte fill, int width = 4, int height = 2, long timestampMs = 0)
    {
        var pixels = new byte[width * height * 4];
        Array.Fill(pixels, fill);
        return new PreviewFrame(sequence, width, height, pixels, TimeSpan.FromMilliseconds(timestampMs));
    }

    private void RunUi()
    {
        while (_ui.TryDequeue(out var work))
        {
            work();
        }
    }
}
