using FotoHavn.Core.Camera;

namespace FotoHavn.Core.GuestCycles.Orchestration;

/// <summary>
/// Encodes a Capture's frame as the JPEG the store keeps (plan section 7, Capture sequence step 5: JPEG at quality 92, sRGB, no
/// metadata). The brain has no imaging library, so each host supplies one. It runs off the brain's executor and may take a while.
/// </summary>
public interface ICaptureEncoder
{
    /// <summary>Encodes <paramref name="frame"/> (BGRA8, already mirrored when the Event mirrors) as a JPEG. Never returns empty bytes.</summary>
    /// <exception cref="ArgumentException">The frame's size, stride, and pixel count don't match.</exception>
    byte[] EncodeJpeg(CapturedFrame frame);
}
