using System.Buffers.Binary;
using System.Text.Json;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Protocol.Tests;

public sealed class PreviewFrameTests
{
    [Fact]
    public void The_header_is_24_little_endian_bytes_in_the_documented_layout()
    {
        var header = new PreviewFrameHeader(0x0A0B0C0D, 1280, 720, 0x0102030405060708);
        var bytes = new byte[PreviewFrameHeader.Size];

        header.WriteTo(bytes);

        Assert.Equal(
            new byte[]
            {
                0x46, 0x48, 0x50, 0x46, // "FHPF"
                0x01, 0x00, // version 1
                0x00, 0x00, // reserved
                0x0D, 0x0C, 0x0B, 0x0A, // seq
                0x00, 0x05, // width 1280
                0xD0, 0x02, // height 720
                0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, // timestampMs
            },
            bytes);
        Assert.Equal(24, PreviewFrameHeader.Size);
    }

    [Fact]
    public void A_header_round_trips_with_its_payload()
    {
        var header = new PreviewFrameHeader(4_294_967_295, 1280, 720, 18_304);
        var message = new byte[PreviewFrameHeader.Size + header.PayloadLength];
        header.WriteTo(message);

        Assert.True(PreviewFrameHeader.TryRead(message, out var read));

        Assert.Equal(header, read);
        Assert.Equal(1280 * 720 * 4, read.PayloadLength);
    }

    [Fact]
    public void Writing_needs_room_for_the_whole_header()
    {
        var header = new PreviewFrameHeader(1, 2, 2, 0);

        Assert.Throws<ArgumentException>(() => header.WriteTo(new byte[PreviewFrameHeader.Size - 1]));
    }

    [Fact]
    public void The_reserved_field_is_ignored_on_read()
    {
        var message = Message(new PreviewFrameHeader(1, 2, 2, 5));
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(6), 0xFFFF);

        Assert.True(PreviewFrameHeader.TryRead(message, out _));
    }

    [Fact]
    public void A_message_shorter_than_the_header_is_not_a_frame()
    {
        Assert.False(PreviewFrameHeader.TryRead(new byte[PreviewFrameHeader.Size - 1], out var header));
        Assert.Equal(default, header);
    }

    [Theory]
    [InlineData(0, 0x00)]
    [InlineData(3, 0x00)]
    public void A_wrong_magic_is_not_a_frame(int index, byte value)
    {
        var message = Message(new PreviewFrameHeader(1, 2, 2, 5));
        message[index] = value;

        Assert.False(PreviewFrameHeader.TryRead(message, out _));
    }

    [Fact]
    public void A_wrong_version_is_not_a_frame()
    {
        var message = Message(new PreviewFrameHeader(1, 2, 2, 5));
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(4), 2);

        Assert.False(PreviewFrameHeader.TryRead(message, out _));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    public void A_zero_size_is_not_a_frame(ushort width, ushort height)
    {
        var message = new byte[PreviewFrameHeader.Size];
        new PreviewFrameHeader(1, width, height, 5).WriteTo(message);

        Assert.False(PreviewFrameHeader.TryRead(message, out _));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void A_payload_of_the_wrong_length_is_not_a_frame(int difference)
    {
        var message = Message(new PreviewFrameHeader(1, 2, 2, 5));
        Array.Resize(ref message, message.Length + difference);

        Assert.False(PreviewFrameHeader.TryRead(message, out _));
    }

    [Theory]
    [InlineData(0L, 0U)]
    [InlineData(42L, 42U)]
    [InlineData(4_294_967_295L, 4_294_967_295U)]
    [InlineData(4_294_967_296L, 0U)]
    [InlineData(4_294_967_298L, 2U)]
    public void Sequence_numbers_go_on_the_wire_modulo_two_to_the_32(long sequence, uint wire)
    {
        Assert.Equal(wire, PreviewFrameHeader.ToWireSequence(sequence));
    }

    [Fact]
    public void The_shared_buffer_metadata_is_the_documented_json()
    {
        var json = ProtocolJson.Serialize(new PreviewFrameMetadata(42, 1280, 720, PreviewPixelFormat.Rgba, 18_304));

        Assert.Equal("""{"type":"previewFrame","seq":42,"width":1280,"height":720,"format":"rgba","timestampMs":18304}""", json);
        Assert.Equal(new PreviewFrameMetadata(42, 1280, 720, PreviewPixelFormat.Rgba, 18_304), ProtocolJson.Deserialize<PreviewFrameMetadata>(JsonDocument.Parse(json).RootElement));
    }

    [Fact]
    public void A_frame_ack_is_the_documented_json()
    {
        Assert.Equal("""{"type":"frameAck","seq":42}""", ProtocolJson.Serialize(new FrameAckMessage(42)));
    }

    [Fact]
    public void Camera_enums_use_their_camel_case_wire_names()
    {
        Assert.Equal("\"exclusiveLost\"", JsonSerializer.Serialize(CameraFailureKind.ExclusiveLost, ProtocolJson.Options));
        Assert.Equal("\"inUseByAnotherApp\"", JsonSerializer.Serialize(CameraFailureKind.InUseByAnotherApp, ProtocolJson.Options));
        Assert.Equal("\"notAvailable\"", JsonSerializer.Serialize(CameraState.NotAvailable, ProtocolJson.Options));
        Assert.Equal("\"guest\"", JsonSerializer.Serialize(PreviewTarget.Guest, ProtocolJson.Options));
        Assert.Equal("\"exclusiveLost\"", JsonSerializer.Serialize(CameraFaultKind.ExclusiveLost, ProtocolJson.Options));
    }

    private static byte[] Message(PreviewFrameHeader header)
    {
        var message = new byte[PreviewFrameHeader.Size + header.PayloadLength];
        header.WriteTo(message);
        return message;
    }
}
