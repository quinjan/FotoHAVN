using System.Buffers.Binary;

namespace FotoHavn.Protocol.Cameras;

// The preview frame channel (plan section 9, Live preview frames; ADR 0004, Spike S1 result). Frames never travel on the
// bridge. The WPF host posts a shared buffer with PreviewFrameMetadata as its additional data; the headless host sends a
// binary WebSocket message that starts with PreviewFrameHeader. The page acks each frame with FrameAckMessage.

/// <summary>The pixel format of a preview frame's bytes.</summary>
public enum PreviewPixelFormat
{
    /// <summary>Four bytes per pixel in R, G, B, A order, rows tightly packed (stride = width × 4), alpha 255.</summary>
    Rgba,
}

/// <summary>
/// The additional data of <c>PostSharedBufferToScript</c> for one preview frame, as JSON:
/// <c>{"type":"previewFrame","seq":42,"width":1280,"height":720,"format":"rgba","timestampMs":18304}</c>.
/// It is not a bridge message: the brain never sends or receives it.
/// </summary>
/// <param name="Seq">The frame's sequence number modulo 2³² (see <see cref="PreviewFrameHeader.ToWireSequence"/>); the page echoes it in its ack.</param>
/// <param name="Width">Frame width in pixels.</param>
/// <param name="Height">Frame height in pixels.</param>
/// <param name="Format">The pixel format of the buffer.</param>
/// <param name="TimestampMs">The frame's camera-clock timestamp in whole milliseconds.</param>
public sealed record PreviewFrameMetadata(long Seq, int Width, int Height, PreviewPixelFormat Format, long TimestampMs) : ProtocolMessage
{
    protected override string MessageType => MessageTypes.PreviewFrame;
}

/// <summary>
/// The page's ack of one preview frame, sent after it drew the frame and released its buffer. The WebView2 host reads it
/// from <c>chrome.webview.postMessage</c> and the headless host from the WebSocket as a text message; each intercepts it
/// before the brain. A brain that receives one anyway ignores it: no reply, no state change.
/// </summary>
/// <param name="Seq">The acked frame's wire sequence number.</param>
public sealed record FrameAckMessage(long Seq) : ClientMessage
{
    protected override string MessageType => MessageTypes.FrameAck;
}

/// <summary>
/// The 24-byte little-endian header of one binary WebSocket preview frame; width × height × 4 RGBA bytes follow.
/// <code>
/// offset size field
///      0    4 magic "FHPF" (0x46 0x48 0x50 0x46)
///      4    2 version u16 = 1
///      6    2 reserved u16 = 0
///      8    4 seq u32
///     12    2 width u16
///     14    2 height u16
///     16    8 timestampMs u64
/// </code>
/// </summary>
/// <param name="Sequence">The wire sequence number.</param>
/// <param name="Width">Frame width in pixels (1 to 65535).</param>
/// <param name="Height">Frame height in pixels (1 to 65535).</param>
/// <param name="TimestampMs">The camera-clock timestamp in whole milliseconds.</param>
public readonly record struct PreviewFrameHeader(uint Sequence, ushort Width, ushort Height, ulong TimestampMs)
{
    /// <summary>The header's size in bytes.</summary>
    public const int Size = 24;

    /// <summary>The only header version.</summary>
    public const ushort CurrentVersion = 1;

    /// <summary>Bytes per pixel of a <see cref="PreviewPixelFormat.Rgba"/> frame.</summary>
    public const int BytesPerPixel = 4;

    private static ReadOnlySpan<byte> Magic => "FHPF"u8;

    /// <summary>The number of pixel bytes that follow the header.</summary>
    public int PayloadLength => Width * Height * BytesPerPixel;

    /// <summary>The wire form of a sequence number: the low 32 bits.</summary>
    public static uint ToWireSequence(long sequence) => unchecked((uint)sequence);

    /// <summary>Writes the header into the first <see cref="Size"/> bytes of <paramref name="destination"/>.</summary>
    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException($"A preview frame header needs {Size} bytes.", nameof(destination));
        }

        Magic.CopyTo(destination);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], CurrentVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], 0);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], Sequence);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[12..], Width);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[14..], Height);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[16..], TimestampMs);
    }

    /// <summary>
    /// Reads the header of a whole message. Returns false when the message is shorter than the header, the magic or
    /// version is wrong, the width or height is zero, or the message isn't exactly the header plus
    /// <see cref="PayloadLength"/> bytes. The reserved field is ignored.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> message, out PreviewFrameHeader header)
    {
        header = default;
        if (message.Length < Size
            || !message[..Magic.Length].SequenceEqual(Magic)
            || BinaryPrimitives.ReadUInt16LittleEndian(message[4..]) != CurrentVersion)
        {
            return false;
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(message[12..]);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(message[14..]);
        var candidate = new PreviewFrameHeader(
            BinaryPrimitives.ReadUInt32LittleEndian(message[8..]),
            width,
            height,
            BinaryPrimitives.ReadUInt64LittleEndian(message[16..]));
        if (width == 0 || height == 0 || message.Length != Size + candidate.PayloadLength)
        {
            return false;
        }

        header = candidate;
        return true;
    }
}
