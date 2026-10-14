using System.Runtime.InteropServices;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Host.Windows.Logging;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Preview;

/// <summary>A reused native buffer shared with one page. Its pointer is read once, on the UI thread, when it is created.</summary>
internal interface ISharedFrameBuffer : IDisposable
{
    /// <summary>The buffer's address, cached at creation. Valid until <see cref="IDisposable.Dispose"/>.</summary>
    IntPtr Pointer { get; }

    /// <summary>The buffer's size in bytes.</summary>
    long Size { get; }
}

/// <summary>One WebView2's frame surface (a <c>CoreWebView2</c> in production). Both members run on the UI thread.</summary>
internal interface IPreviewSurface
{
    ISharedFrameBuffer CreateBuffer(long size);

    void Post(ISharedFrameBuffer buffer, string metadataJson);
}

/// <summary>
/// The WPF host's preview channel (plan section 9, ADR 0004 Spike S1 result): one reused shared buffer per window role,
/// one frame in flight per role, freed by the page's <c>frameAck</c>.
/// </summary>
/// <remarks>
/// Threading: <see cref="TryPost"/> runs on the camera thread. It copies the pixels into the buffer's cached pointer under
/// the role's lock and queues <c>PostSharedBufferToScript</c> on the UI thread; it never waits for the UI thread. The UI
/// thread creates, swaps, and disposes buffers (taking the same lock only to swap fields), so the camera thread never
/// writes into a disposed buffer, and no <c>CoreWebView2*</c> object is touched off the UI thread.
/// </remarks>
internal sealed class SharedBufferPreviewChannel : IPreviewChannel
{
    /// <summary>1280 × 720 × 4: the buffer's first size; a larger frame grows it once.</summary>
    public const long InitialBufferBytes = 1280L * 720 * PreviewFrameHeader.BytesPerPixel;

    /// <summary>A <c>frameAck</c> message is about 40 characters; anything much longer is not one.</summary>
    private const int MaxAckTextLength = 128;

    private readonly Action<Action> _postToUi;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly RoleSlot _operator = new(Role.Operator);
    private readonly RoleSlot _guest = new(Role.Guest);

    /// <param name="postToUi">Queues work on the UI thread (<c>Dispatcher.InvokeAsync</c>).</param>
    /// <param name="time">The clock for the in-flight timeout and the rates.</param>
    /// <param name="logger">The host logger.</param>
    public SharedBufferPreviewChannel(Action<Action> postToUi, TimeProvider time, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(postToUi);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _postToUi = postToUi;
        _time = time;
        _logger = logger;
    }

    /// <summary>
    /// Makes <paramref name="surface"/> the target of <paramref name="role"/>'s frames (UI thread), with a new buffer: for a
    /// new WebView2 (first load, and after a crash). Whatever was attached before is released.
    /// </summary>
    public void Attach(Role role, IPreviewSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var slot = SlotFor(role);
        ISharedFrameBuffer? created = null;
        try
        {
            created = surface.CreateBuffer(InitialBufferBytes);
        }
        catch (Exception exception) when (IsSurfaceFailure(exception))
        {
            HostLog.PreviewBufferFailed(_logger, role, exception);
        }

        ISharedFrameBuffer? replaced;
        lock (slot.Gate)
        {
            replaced = slot.Buffer;
            slot.Surface = created is null ? null : surface;
            slot.Buffer = created;
            slot.Generation++;
            slot.Resizing = false;
            slot.BackPressure.Reset();
        }

        replaced?.Dispose();
    }

    /// <summary>Stops <paramref name="role"/>'s frames and releases its buffer, if <paramref name="surface"/> is still the target (UI thread).</summary>
    public void Detach(Role role, IPreviewSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var slot = SlotFor(role);
        ISharedFrameBuffer? released;
        lock (slot.Gate)
        {
            if (!ReferenceEquals(slot.Surface, surface))
            {
                return;
            }

            released = slot.Buffer;
            slot.Surface = null;
            slot.Buffer = null;
            slot.Generation++;
            slot.Resizing = false;
            slot.BackPressure.Reset();
        }

        released?.Dispose();
    }

    /// <summary>Frees the role's in-flight frame without counting it: the page reloaded, so it will never ack it.</summary>
    public void ResetInFlight(Role role) => SlotFor(role).BackPressure.Reset();

    /// <summary>Handles the page's ack of the frame with <paramref name="wireSequence"/>; false when it isn't the in-flight frame.</summary>
    public bool Acknowledge(Role role, uint wireSequence) =>
        SlotFor(role).BackPressure.Acknowledge(wireSequence, _time.GetUtcNow());

    /// <summary>
    /// Takes the page's message when it is a well-formed <c>frameAck</c>, and acks it for <paramref name="role"/>. A message
    /// that is anything else, including a malformed ack, is left for the brain (which answers it).
    /// </summary>
    /// <param name="role">The window's role.</param>
    /// <param name="protocolText">The page's message as protocol text.</param>
    public bool TryHandleAck(Role role, string protocolText)
    {
        ArgumentNullException.ThrowIfNull(protocolText);
        if (protocolText.Length > MaxAckTextLength
            || !protocolText.Contains("\"frameAck\"", StringComparison.Ordinal)
            || ClientMessageParser.Parse(protocolText, role) is not ParsedFrameAck ack)
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
        if (slot is null || !TryMeasure(frame, out var byteCount))
        {
            return false;
        }

        var metadata = ProtocolJson.Serialize(new PreviewFrameMetadata(
            PreviewFrameHeader.ToWireSequence(frame.Sequence),
            frame.Width,
            frame.Height,
            PreviewPixelFormat.Rgba,
            (long)frame.Timestamp.TotalMilliseconds));
        ISharedFrameBuffer buffer;
        int generation;
        lock (slot.Gate)
        {
            if (slot.Surface is null || slot.Buffer is null || slot.Resizing || !slot.BackPressure.TryBegin(frame.Sequence, _time.GetUtcNow()))
            {
                return false;
            }

            if (byteCount > slot.Buffer.Size)
            {
                // The slot is free (the page is done with the buffer), so the UI thread can swap in a bigger one.
                slot.BackPressure.Reset();
                slot.Resizing = true;
                var surface = slot.Surface;
                var pending = slot.Generation;
                _postToUi(() => Grow(slot, surface, pending, byteCount));
                return false;
            }

            NativeCopy.Copy(frame.Rgba.Span[..byteCount], slot.Buffer.Pointer);
            buffer = slot.Buffer;
            generation = slot.Generation;
        }

        _postToUi(() => PostToPage(slot, generation, buffer, metadata));
        return true;
    }

    public PreviewFrameStats GetStats(PreviewTarget target) => target switch
    {
        PreviewTarget.Operator => _operator.BackPressure.Snapshot(_time.GetUtcNow()),
        PreviewTarget.Guest => _guest.BackPressure.Snapshot(_time.GetUtcNow()),
        _ => default,
    };

    private static bool IsSurfaceFailure(Exception exception) =>
        exception is COMException or InvalidOperationException or ObjectDisposedException or OutOfMemoryException;

    private static bool TryMeasure(PreviewFrame frame, out int byteCount)
    {
        byteCount = 0;
        if (frame.Width <= 0 || frame.Height <= 0 || (long)frame.Width * frame.Height * PreviewFrameHeader.BytesPerPixel > int.MaxValue)
        {
            return false;
        }

        byteCount = frame.Width * frame.Height * PreviewFrameHeader.BytesPerPixel;
        return frame.Rgba.Length >= byteCount;
    }

    private RoleSlot SlotFor(Role role) => role == Role.Operator ? _operator : _guest;

    /// <summary>UI thread: posts the buffer unless the surface or the buffer was replaced since the frame was copied.</summary>
    private void PostToPage(RoleSlot slot, int generation, ISharedFrameBuffer buffer, string metadata)
    {
        IPreviewSurface? surface;
        lock (slot.Gate)
        {
            surface = slot.Generation == generation && ReferenceEquals(slot.Buffer, buffer) ? slot.Surface : null;
        }

        if (surface is null)
        {
            return;
        }

        try
        {
            surface.Post(buffer, metadata);
        }
        catch (Exception exception) when (IsSurfaceFailure(exception))
        {
            // The page is gone; no ack will come.
            slot.BackPressure.Reset();
            HostLog.PreviewBufferFailed(_logger, slot.Role, exception);
        }
    }

    /// <summary>UI thread: replaces the role's buffer with one of <paramref name="byteCount"/> bytes.</summary>
    private void Grow(RoleSlot slot, IPreviewSurface surface, int generation, int byteCount)
    {
        ISharedFrameBuffer? created = null;
        try
        {
            created = surface.CreateBuffer(byteCount);
        }
        catch (Exception exception) when (IsSurfaceFailure(exception))
        {
            HostLog.PreviewBufferFailed(_logger, slot.Role, exception);
        }

        ISharedFrameBuffer? replaced = null;
        lock (slot.Gate)
        {
            if (slot.Generation == generation)
            {
                slot.Resizing = false;
                if (created is not null)
                {
                    replaced = slot.Buffer;
                    slot.Buffer = created;
                    created = null;
                }
            }
        }

        replaced?.Dispose();
        created?.Dispose();
    }

    private sealed class RoleSlot(Role role)
    {
        public Role Role { get; } = role;

        public object Gate { get; } = new();

        public PreviewBackPressure BackPressure { get; } = new();

        public IPreviewSurface? Surface { get; set; }

        public ISharedFrameBuffer? Buffer { get; set; }

        public int Generation { get; set; }

        public bool Resizing { get; set; }
    }
}
