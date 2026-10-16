using FotoHavn.Core.Camera;
using FotoHavn.Core.GuestCycles.Orchestration;

namespace FotoHavn.Host.Diagnostics;

/// <summary>
/// Encodes a Guest Cycle Capture's frame (plan section 7, Capture sequence step 5): the same JPEG settings as the diagnostics Capture
/// (<c>DiagnosticsCaptureWriter</c>: quality 92, sRGB, no metadata), because there is one encoding for every Capture. The brain calls it off
/// its executor.
/// </summary>
/// <remarks>Both hosts compile this one file, as they do <c>DiagnosticsCaptureWriter</c> (the headless project owns it; the WPF host links it).</remarks>
internal sealed class HostCaptureEncoder : ICaptureEncoder
{
    public byte[] EncodeJpeg(CapturedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        DiagnosticsCaptureWriter.Validate(frame);
        return DiagnosticsCaptureWriter.Encode(frame);
    }
}
