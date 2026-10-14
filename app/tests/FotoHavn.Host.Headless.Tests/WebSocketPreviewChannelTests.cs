using System.Buffers.Binary;
using FotoHavn.Core.Camera;
using FotoHavn.Host.Headless.Preview;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Host.Headless.Tests;

public sealed class WebSocketPreviewChannelTests
{
    private readonly ManualTime _time = new();
    private readonly WebSocketPreviewChannel _channel;
    private readonly RecordingSink _operator = new();
    private readonly RecordingSink _guest = new();

    public WebSocketPreviewChannelTests()
    {
        _channel = new WebSocketPreviewChannel(_time);
    }

    [Fact]
    public void A_frame_is_one_binary_message_with_the_24_byte_header_and_the_rgba_pixels()
    {
        _channel.Attach(Role.Operator, _operator);
        var pixels = Enumerable.Range(0, 4 * 2 * 4).Select(index => (byte)(index * 3)).ToArray();

        var accepted = _channel.TryPost(PreviewTarget.Operator, new PreviewFrame(42, 4, 2, pixels, TimeSpan.FromMilliseconds(18304)));

        Assert.True(accepted);
        var message = Assert.Single(_operator.Messages);
        Assert.Equal(24 + pixels.Length, message.Length);
        Assert.True(PreviewFrameHeader.TryRead(message, out var header));
        Assert.Equal(new PreviewFrameHeader(42, 4, 2, 18304), header);
        Assert.Equal(pixels, message[24..]);
    }

    [Fact]
    public void The_header_is_little_endian_with_the_magic_and_version_one()
    {
        _channel.Attach(Role.Operator, _operator);
        _channel.TryPost(PreviewTarget.Operator, new PreviewFrame(0x0102_0304_0506, 3, 1, new byte[12], TimeSpan.FromMilliseconds(0x0A0B)));

        var message = _operator.Messages[0];

        Assert.Equal("FHPF"u8.ToArray(), message[..4]);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(message.AsSpan(4)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(message.AsSpan(6)));
        Assert.Equal(0x0304_0506u, BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(8)));
        Assert.Equal(3, BinaryPrimitives.ReadUInt16LittleEndian(message.AsSpan(12)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(message.AsSpan(14)));
        Assert.Equal(0x0A0Bul, BinaryPrimitives.ReadUInt64LittleEndian(message.AsSpan(16)));
    }

    [Fact]
    public void Only_one_frame_is_in_flight_per_role_until_the_ack_arrives()
    {
        _channel.Attach(Role.Operator, _operator);
        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(1)));

        Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(2)));
        Assert.False(_channel.Acknowledge(Role.Operator, 2));
        Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(2)));
        Assert.True(_channel.Acknowledge(Role.Operator, 1));
        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(3)));

        Assert.Equal(2, _operator.Messages.Count);
        var stats = _channel.GetStats(PreviewTarget.Operator);
        Assert.Equal(1, stats.Delivered);
        Assert.Equal(2, stats.Dropped);
    }

    [Fact]
    public void A_lost_ack_frees_the_slot_after_one_second()
    {
        _channel.Attach(Role.Operator, _operator);
        _channel.TryPost(PreviewTarget.Operator, Frame(1));
        _time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(2)));

        _time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(3)));
        Assert.Equal(2, _channel.GetStats(PreviewTarget.Operator).Dropped);
    }

    [Fact]
    public void Delivered_per_second_counts_the_acks_of_the_last_two_seconds()
    {
        _channel.Attach(Role.Guest, _guest);
        for (var sequence = 1; sequence <= 6; sequence++)
        {
            _time.Advance(TimeSpan.FromMilliseconds(250));
            Assert.True(_channel.TryPost(PreviewTarget.Guest, Frame(sequence)));
            Assert.True(_channel.Acknowledge(Role.Guest, (uint)sequence));
        }

        var stats = _channel.GetStats(PreviewTarget.Guest);

        Assert.Equal(3.0, stats.DeliveredPerSecond, 3);
        Assert.Equal(6, stats.Delivered);
        Assert.Equal(_time.GetUtcNow(), stats.LastDeliveredAt);
    }

    [Fact]
    public void Roles_do_not_share_frames_or_slots()
    {
        _channel.Attach(Role.Operator, _operator);
        _channel.Attach(Role.Guest, _guest);

        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(1)));
        Assert.True(_channel.TryPost(PreviewTarget.Guest, Frame(1)));

        Assert.Single(_operator.Messages);
        Assert.Single(_guest.Messages);
        Assert.True(_channel.Acknowledge(Role.Guest, 1));
        Assert.Equal(0, _channel.GetStats(PreviewTarget.Operator).Delivered);
    }

    [Fact]
    public void A_role_without_a_socket_and_the_none_target_have_no_target()
    {
        _channel.Attach(Role.Operator, _operator);

        Assert.False(_channel.TryPost(PreviewTarget.Guest, Frame(1)));
        Assert.False(_channel.TryPost(PreviewTarget.None, Frame(1)));

        Assert.Equal(default, _channel.GetStats(PreviewTarget.None));
        Assert.Equal(0, _channel.GetStats(PreviewTarget.Guest).Dropped);
    }

    [Fact]
    public void A_closing_socket_refuses_the_frame_and_frees_the_slot()
    {
        _channel.Attach(Role.Operator, _operator);
        _operator.Accepting = false;
        Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(1)));

        _operator.Accepting = true;

        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(2)));
        Assert.Equal(0, _channel.GetStats(PreviewTarget.Operator).Dropped);
    }

    [Fact]
    public void A_new_connection_replaces_the_target_and_starts_with_a_free_slot()
    {
        var newer = new RecordingSink();
        _channel.Attach(Role.Operator, _operator);
        _channel.TryPost(PreviewTarget.Operator, Frame(1));

        _channel.Attach(Role.Operator, newer);

        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(2)));
        Assert.Single(_operator.Messages);
        Assert.Single(newer.Messages);
    }

    [Fact]
    public void An_old_socket_ending_does_not_remove_the_newer_one()
    {
        var newer = new RecordingSink();
        _channel.Attach(Role.Operator, _operator);
        _channel.Attach(Role.Operator, newer);

        _channel.Detach(Role.Operator, _operator);

        Assert.True(_channel.TryPost(PreviewTarget.Operator, Frame(1)));
        _channel.Detach(Role.Operator, newer);
        Assert.False(_channel.TryPost(PreviewTarget.Operator, Frame(2)));
    }

    [Theory]
    [InlineData(0, 2, 32)]
    [InlineData(4, 0, 32)]
    [InlineData(65536, 1, 1_000_000)]
    [InlineData(1, 65536, 1_000_000)]
    [InlineData(4, 2, 31)]
    public void A_frame_the_header_cannot_describe_or_that_lacks_pixels_is_refused(int width, int height, int bytes)
    {
        _channel.Attach(Role.Operator, _operator);

        var accepted = _channel.TryPost(PreviewTarget.Operator, new PreviewFrame(1, width, height, new byte[bytes], TimeSpan.Zero));

        Assert.False(accepted);
        Assert.Empty(_operator.Messages);
        Assert.Equal(0, _channel.GetStats(PreviewTarget.Operator).Dropped);
    }

    [Fact]
    public void A_negative_timestamp_is_sent_as_zero()
    {
        _channel.Attach(Role.Operator, _operator);

        _channel.TryPost(PreviewTarget.Operator, new PreviewFrame(1, 1, 1, new byte[4], TimeSpan.FromMilliseconds(-5)));

        Assert.True(PreviewFrameHeader.TryRead(_operator.Messages[0], out var header));
        Assert.Equal(0ul, header.TimestampMs);
    }

    [Theory]
    [InlineData("""{"type":"frameAck","seq":1}""", true)]
    [InlineData("""{ "seq": 1, "type": "frameAck" }""", true)]
    [InlineData("""{"type":"hello","protocolVersion":4}""", false)]
    [InlineData("""{"type":"frameAck","seq":-1}""", false)]
    [InlineData("""{"type":"frameAck","seq":1,"role":"guest"}""", false)]
    [InlineData("frameAck without quotes", false)]
    public void Only_a_well_formed_text_ack_is_taken(string text, bool taken)
    {
        _channel.Attach(Role.Operator, _operator);
        _channel.TryPost(PreviewTarget.Operator, Frame(1));

        Assert.Equal(taken, _channel.TryHandleAck(Role.Operator, text));

        Assert.Equal(taken ? 1 : 0, _channel.GetStats(PreviewTarget.Operator).Delivered);
    }

    [Fact]
    public void A_long_message_is_never_parsed_as_an_ack()
    {
        var padded = """{"type":"frameAck","seq":1,"note":"{""" + new string('x', 200) + """}"}""";

        Assert.False(_channel.TryHandleAck(Role.Operator, padded));
    }

    [Fact]
    public void The_constructor_and_the_session_calls_reject_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new WebSocketPreviewChannel(null!));
        Assert.Throws<ArgumentNullException>(() => _channel.Attach(Role.Operator, null!));
        Assert.Throws<ArgumentNullException>(() => _channel.Detach(Role.Operator, null!));
        Assert.Throws<ArgumentNullException>(() => _channel.TryHandleAck(Role.Operator, null!));
    }

    private static PreviewFrame Frame(long sequence) => new(sequence, 2, 2, new byte[16], TimeSpan.Zero);

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class RecordingSink : IPreviewSink
    {
        public List<byte[]> Messages { get; } = [];

        public bool Accepting { get; set; } = true;

        public bool TrySendFrame(byte[] message, int length)
        {
            if (!Accepting)
            {
                return false;
            }

            Messages.Add(message[..length]);
            return true;
        }
    }
}
