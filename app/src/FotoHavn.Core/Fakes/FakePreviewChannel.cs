using FotoHavn.Core.Camera;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Fakes;

/// <summary>One frame a <see cref="FakePreviewChannel"/> accepted.</summary>
/// <param name="Target">The page it went to.</param>
/// <param name="Sequence">The camera frame's sequence number.</param>
/// <param name="Width">The frame width.</param>
/// <param name="Height">The frame height.</param>
/// <param name="Timestamp">The frame's timestamp on the camera's clock.</param>
/// <param name="Rgba">A copy of the pixels when the channel keeps them (<see cref="FakePreviewChannel.KeepPixels"/>), else empty.</param>
public sealed record PostedPreviewFrame(PreviewTarget Target, long Sequence, int Width, int Height, TimeSpan Timestamp, byte[] Rgba);

/// <summary>
/// A preview channel for tests and adapters under test: it records the frames it accepts and applies the one-frame-in-flight
/// rule per target. By default it acks every frame at once; turn <see cref="AutoAcknowledge"/> off to hold a frame in
/// flight until <see cref="Acknowledge"/>. Thread-safe. It measures no frame rate (<see cref="PreviewFrameStats.DeliveredPerSecond"/> is 0).
/// </summary>
public sealed class FakePreviewChannel(IClock? clock = null) : IPreviewChannel
{
    private readonly object _gate = new();
    private readonly List<PostedPreviewFrame> _posted = [];
    private readonly Dictionary<PreviewTarget, TargetState> _targets = [];

    /// <summary>Whether a posted frame counts as delivered at once. Default true.</summary>
    public bool AutoAcknowledge { get; set; } = true;

    /// <summary>Whether to keep a copy of each frame's pixels in <see cref="Posted"/>. Default false, because frames are large.</summary>
    public bool KeepPixels { get; set; }

    /// <summary>The frames accepted so far, oldest first.</summary>
    public IReadOnlyList<PostedPreviewFrame> Posted
    {
        get
        {
            lock (_gate)
            {
                return [.. _posted];
            }
        }
    }

    /// <summary>Marks a target as having (or not having) a connected page. Every target is connected at first.</summary>
    public void SetConnected(PreviewTarget target, bool connected)
    {
        lock (_gate)
        {
            State(target).Connected = connected;
            if (!connected)
            {
                State(target).InFlight = false;
            }
        }
    }

    /// <summary>Acks the frame in flight for <paramref name="target"/>. Returns false when none is.</summary>
    public bool Acknowledge(PreviewTarget target)
    {
        lock (_gate)
        {
            var state = State(target);
            if (!state.InFlight)
            {
                return false;
            }

            state.InFlight = false;
            state.Delivered++;
            state.LastDeliveredAt = Now();
            return true;
        }
    }

    public bool TryPost(PreviewTarget target, PreviewFrame frame)
    {
        if (target == PreviewTarget.None)
        {
            return false;
        }

        lock (_gate)
        {
            var state = State(target);
            if (!state.Connected)
            {
                return false;
            }

            if (state.InFlight)
            {
                state.Dropped++;
                return false;
            }

            _posted.Add(new PostedPreviewFrame(target, frame.Sequence, frame.Width, frame.Height, frame.Timestamp, KeepPixels ? frame.Rgba.ToArray() : []));
            if (AutoAcknowledge)
            {
                state.Delivered++;
                state.LastDeliveredAt = Now();
            }
            else
            {
                state.InFlight = true;
            }

            return true;
        }
    }

    public PreviewFrameStats GetStats(PreviewTarget target)
    {
        if (target == PreviewTarget.None)
        {
            return default;
        }

        lock (_gate)
        {
            var state = State(target);
            return new PreviewFrameStats(state.Delivered, state.Dropped, state.LastDeliveredAt, 0);
        }
    }

    private DateTimeOffset Now() => clock?.UtcNow ?? DateTimeOffset.UtcNow;

    private TargetState State(PreviewTarget target)
    {
        if (!_targets.TryGetValue(target, out var state))
        {
            state = new TargetState();
            _targets[target] = state;
        }

        return state;
    }

    private sealed class TargetState
    {
        public bool Connected { get; set; } = true;

        public bool InFlight { get; set; }

        public long Delivered { get; set; }

        public long Dropped { get; set; }

        public DateTimeOffset? LastDeliveredAt { get; set; }
    }
}
