using FotoHavn.Core.Camera;
using FotoHavn.Core.GuestCycles.Orchestration;

namespace FotoHavn.Core.Fakes;

/// <summary>
/// An <see cref="ICaptureEncoder"/> for tests and the headless host's fakes: it answers a small, distinct byte string for each frame
/// (a JPEG start marker, the frame's size, and its first pixel), and records every frame it was given. <see cref="Failure"/> makes every
/// call throw it.
/// </summary>
public sealed class FakeCaptureEncoder : ICaptureEncoder
{
    private readonly Lock _gate = new();
    private readonly List<EncodedFrame> _frames = [];

    /// <summary>The exception every call throws while set.</summary>
    public Exception? Failure { get; set; }

    /// <summary>The frames received, oldest first.</summary>
    public IReadOnlyList<EncodedFrame> Frames
    {
        get
        {
            lock (_gate)
            {
                return [.. _frames];
            }
        }
    }

    public byte[] EncodeJpeg(CapturedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (Failure is { } failure)
        {
            throw failure;
        }

        var span = frame.Bgra.Span;
        var first = span.Length >= 4 ? span[..4].ToArray() : span.ToArray();
        lock (_gate)
        {
            _frames.Add(new EncodedFrame(frame.Width, frame.Height, first));
        }

        return [0xFF, 0xD8, (byte)(frame.Width >> 8), (byte)frame.Width, (byte)(frame.Height >> 8), (byte)frame.Height, .. first, 0xFF, 0xD9];
    }

    /// <summary>A frame the encoder was given.</summary>
    /// <param name="Width">The frame width.</param>
    /// <param name="Height">The frame height.</param>
    /// <param name="FirstPixel">The first pixel's four BGRA bytes after any mirroring, so a test can tell a mirrored frame from a plain one.</param>
    public sealed record EncodedFrame(int Width, int Height, byte[] FirstPixel);
}
