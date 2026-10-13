using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Camera;

/// <summary>One downsized preview frame.</summary>
/// <param name="Sequence">The camera frame's sequence number. The wire carries its low 32 bits (<see cref="PreviewFrameHeader.ToWireSequence"/>).</param>
/// <param name="Width">The frame width in pixels (at most 1280 from the Windows adapter).</param>
/// <param name="Height">The frame height in pixels.</param>
/// <param name="Rgba">Width × Height × 4 bytes in R, G, B, A order, rows tightly packed, alpha 255. Valid only during <see cref="IPreviewChannel.TryPost"/>.</param>
/// <param name="Timestamp">The camera frame's timestamp on the session's clock.</param>
public readonly record struct PreviewFrame(long Sequence, int Width, int Height, ReadOnlyMemory<byte> Rgba, TimeSpan Timestamp);

/// <summary>A preview target's frame counters.</summary>
/// <param name="Delivered">Frames the page drew and acked since the app started.</param>
/// <param name="Dropped">Frames dropped because one was still in flight since the app started.</param>
/// <param name="LastDeliveredAt">When the latest ack arrived (wall clock), or null before one.</param>
/// <param name="DeliveredPerSecond">Frames acked per second over the trailing <c>PreviewBackPressure.RateWindow</c>.</param>
public readonly record struct PreviewFrameStats(long Delivered, long Dropped, DateTimeOffset? LastDeliveredAt, double DeliveredPerSecond);

/// <summary>The brain's read-only view of the preview channel's counters, for <c>test.getDiagnostics</c>.</summary>
public interface IPreviewStats
{
    /// <summary>The counters for <paramref name="target"/>; all zero for <see cref="PreviewTarget.None"/>. Thread-safe.</summary>
    PreviewFrameStats GetStats(PreviewTarget target);
}

/// <summary>
/// Where an adapter delivers preview frames: implemented by each host (WPF: a shared buffer per window; headless: binary
/// WebSocket messages). The host constructs the adapter with its channel. Frames never pass through the brain.
/// </summary>
/// <remarks>
/// Threading: <see cref="TryPost"/> is called from the adapter's frame thread and must be thread-safe; the acks arrive on
/// the host's own threads. It must return quickly: copy (or swizzle) the pixels into the host's buffer and post, never
/// wait for the page.
/// </remarks>
public interface IPreviewChannel : IPreviewStats
{
    /// <summary>
    /// Delivers <paramref name="frame"/> to the page for <paramref name="target"/> when none of its frames is in flight.
    /// Returns false, and the frame is dropped, when a frame is still in flight for that target (counted in
    /// <see cref="PreviewFrameStats.Dropped"/>), when no page for the target is connected, or when the target is
    /// <see cref="PreviewTarget.None"/>. The channel doesn't keep <paramref name="frame"/>'s memory after it returns.
    /// </summary>
    bool TryPost(PreviewTarget target, PreviewFrame frame);
}

/// <summary>A channel nobody listens to: it accepts no frame and counts nothing. For tests and hosts without a preview.</summary>
public sealed class NullPreviewChannel : IPreviewChannel
{
    public static NullPreviewChannel Instance { get; } = new();

    private NullPreviewChannel()
    {
    }

    public bool TryPost(PreviewTarget target, PreviewFrame frame) => false;

    public PreviewFrameStats GetStats(PreviewTarget target) => default;
}
