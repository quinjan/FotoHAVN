using System.Buffers;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Host.Headless.Preview;

/// <summary>One role's bridge socket, as the frame channel sees it.</summary>
internal interface IPreviewSink
{
    /// <summary>
    /// Queues one binary message for the client (the 24-byte header and the RGBA pixels) and takes ownership of
    /// <paramref name="message"/>, a rented array whose first <paramref name="length"/> bytes are the message; the sink
    /// returns it to <see cref="ArrayPool{T}.Shared"/> after sending. Returns false, without taking ownership, once the
    /// session is closing.
    /// </summary>
    bool TrySendFrame(byte[] message, int length);
}

/// <summary>
/// The headless host's preview channel: the same rules as the WPF host's (one frame in flight per role, freed by the
/// page's <c>frameAck</c>), over the role's bridge socket. A frame is one binary WebSocket message: the 24-byte
/// <see cref="PreviewFrameHeader"/>, then width × height × 4 RGBA bytes. Acks arrive as text messages on the same socket
/// and are taken here before the brain sees them.
/// </summary>
/// <remarks>
/// Threading: <see cref="TryPost"/> runs on the camera thread and only queues the message on the socket's writer; acks arrive on
/// the socket's receive loop. When a role has several sockets, the newest one gets the frames.
/// </remarks>
internal sealed class WebSocketPreviewChannel : IPreviewChannel
{
    /// <summary>A <c>frameAck</c> message is about 40 characters; anything much longer is not one.</summary>
    private const int MaxAckTextLength = 128;

    private readonly TimeProvider _time;
    private readonly RoleSlot _operator = new();
    private readonly RoleSlot _guest = new();

    public WebSocketPreviewChannel(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <summary>Makes <paramref name="sink"/> the role's target (a new connection), with nothing in flight.</summary>
    public void Attach(Role role, IPreviewSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        var slot = SlotFor(role);
        lock (slot.Gate)
        {
            slot.Sink = sink;
            slot.BackPressure.Reset();
        }
    }

    /// <summary>Stops the role's frames when <paramref name="sink"/> is still its target (its socket ended).</summary>
    public void Detach(Role role, IPreviewSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        var slot = SlotFor(role);
        lock (slot.Gate)
        {
            if (ReferenceEquals(slot.Sink, sink))
            {
                slot.Sink = null;
                slot.BackPressure.Reset();
            }
        }
    }

    /// <summary>Handles the page's ack of the frame with <paramref name="wireSequence"/>; false when it isn't the in-flight frame.</summary>
    public bool Acknowledge(Role role, uint wireSequence) =>
        SlotFor(role).BackPressure.Acknowledge(wireSequence, _time.GetUtcNow());

    /// <summary>
    /// Takes the page's text message when it is a well-formed <c>frameAck</c>, and acks it for <paramref name="role"/>. Anything
    /// else, including a malformed ack, is left for the brain (which answers it).
    /// </summary>
    public bool TryHandleAck(Role role, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaxAckTextLength
            || !text.Contains("\"frameAck\"", StringComparison.Ordinal)
            || ClientMessageParser.Parse(text, role) is not ParsedFrameAck ack)
        {
            return false;
        }

        Acknowledge(role, (uint)ack.Message.Seq);
        return true;
    }

    public bool TryPost(PreviewTarget target, PreviewFrame frame)
    {
        var slot = target switch
        {
            PreviewTarget.Operator => _operator,
            PreviewTarget.Guest => _guest,
            _ => null,
        };
        if (slot is null
            || frame.Width is <= 0 or > ushort.MaxValue
            || frame.Height is <= 0 or > ushort.MaxValue)
        {
            return false;
        }

        var header = new PreviewFrameHeader(
            PreviewFrameHeader.ToWireSequence(frame.Sequence),
            (ushort)frame.Width,
            (ushort)frame.Height,
            (ulong)Math.Max(0, (long)frame.Timestamp.TotalMilliseconds));
        var payload = header.PayloadLength;
        if (frame.Rgba.Length < payload)
        {
            return false;
        }

        lock (slot.Gate)
        {
            if (slot.Sink is not { } sink || !slot.BackPressure.TryBegin(frame.Sequence, _time.GetUtcNow()))
            {
                return false;
            }

            var length = PreviewFrameHeader.Size + payload;
            var message = ArrayPool<byte>.Shared.Rent(length);
            header.WriteTo(message);
            frame.Rgba.Span[..payload].CopyTo(message.AsSpan(PreviewFrameHeader.Size));
            if (sink.TrySendFrame(message, length))
            {
                return true;
            }

            ArrayPool<byte>.Shared.Return(message);
            slot.BackPressure.Reset();
            return false;
        }
    }

    public PreviewFrameStats GetStats(PreviewTarget target) => target switch
    {
        PreviewTarget.Operator => _operator.BackPressure.Snapshot(_time.GetUtcNow()),
        PreviewTarget.Guest => _guest.BackPressure.Snapshot(_time.GetUtcNow()),
        _ => default,
    };

    private RoleSlot SlotFor(Role role) => role == Role.Operator ? _operator : _guest;

    private sealed class RoleSlot
    {
        public object Gate { get; } = new();

        public PreviewBackPressure BackPressure { get; } = new();

        public IPreviewSink? Sink { get; set; }
    }
}
