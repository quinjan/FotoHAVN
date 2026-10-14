using System.Globalization;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Protocol.Presentations;
using SkiaSharp;

namespace FotoHavn.Host.Diagnostics;

/// <summary>
/// Saves the camera diagnostics Capture (<c>test.captureDiagnostics</c>) as <c>&lt;data&gt;/diagnostics/capture-&lt;yyyyMMdd-HHmmss-fff&gt;.jpg</c>:
/// JPEG at <see cref="JpegEncodingSpec.Quality"/>, sRGB, no metadata. The folder is host-owned E2E diagnostics, not Event data.
/// </summary>
/// <remarks>
/// Both hosts compile this one file (the headless project owns it; the WPF host links it). The Windows camera adapter has its
/// own SkiaSharp encoder for real Captures (the headless project is plain net10.0 and can't reference it); both encode with the same
/// <see cref="JpegEncodingSpec"/> settings, and a Host.Windows test pins byte-equal output. A failure is an ordinary exception: the
/// brain turns any exception from the host hook into <c>hookFailed</c>.
/// </remarks>
/// <param name="dataFolder">The host's Data folder.</param>
/// <param name="time">The clock for file names (local time).</param>
internal sealed class DiagnosticsCaptureWriter(string dataFolder, TimeProvider time)
{
    public const string FolderName = "diagnostics";

    private const int BytesPerPixel = 4;

    public string Folder { get; } = Path.Combine(dataFolder, FolderName);

    /// <summary>Encodes and writes <paramref name="frame"/> off the calling thread and returns the Capture's facts.</summary>
    /// <param name="frame">A BGRA8 frame; the caller already mirrored it when <paramref name="mirrored"/> is true.</param>
    /// <param name="mirrored">Whether the frame was mirrored, for the facts.</param>
    public Task<CameraCaptureView> SaveAsync(CapturedFrame frame, bool mirrored)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return Task.Run(() => Save(frame, mirrored));
    }

    private static byte[] Encode(CapturedFrame frame)
    {
        var rowBytes = frame.Stride;
        var pixels = frame.Bgra;
        var needed = (long)rowBytes * frame.Height;
        if (pixels.Length < needed)
        {
            // The last row may omit its padding: give Skia whole rows.
            var padded = new byte[needed];
            pixels.Span.CopyTo(padded);
            pixels = padded;
        }

        // Same Skia settings as FotoHavn.Camera.Windows.JpegEncoder (a Host.Windows test pins byte-equal output). No colour
        // space on the image: Skia treats it as sRGB and writes no ICC profile (a Capture carries no metadata).
        var info = new SKImageInfo(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var image = SKImage.FromPixelCopy(info, pixels.Span, rowBytes)
            ?? throw new InvalidOperationException("The frame's pixels could not be read.");
        using var pixmap = image.PeekPixels()
            ?? throw new InvalidOperationException("The frame's pixels could not be read.");
        var options = new SKJpegEncoderOptions(JpegEncodingSpec.Quality, SKJpegEncoderDownsample.Downsample420, SKJpegEncoderAlphaOption.Ignore);
        using var data = pixmap.Encode(options)
            ?? throw new InvalidOperationException("The frame could not be encoded as JPEG.");
        return data.ToArray();
    }

    private static void Validate(CapturedFrame frame)
    {
        var rowBytes = (long)frame.Width * BytesPerPixel;
        if (frame.Width <= 0
            || frame.Height <= 0
            || frame.Stride < rowBytes
            || frame.Bgra.Length < (long)frame.Stride * (frame.Height - 1) + rowBytes)
        {
            throw new ArgumentException("The frame's size, stride, and pixel count don't match.", nameof(frame));
        }
    }

    private CameraCaptureView Save(CapturedFrame frame, bool mirrored)
    {
        Validate(frame);
        var jpeg = Encode(frame);
        Directory.CreateDirectory(Folder);

        var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        for (var attempt = 0; ; attempt++)
        {
            var fileName = attempt == 0 ? $"capture-{stamp}.jpg" : $"capture-{stamp}-{attempt}.jpg";
            var path = Path.Combine(Folder, fileName);
            if (File.Exists(path))
            {
                // Two captures in the same millisecond: take the next name.
                continue;
            }

            File.WriteAllBytes(path, jpeg);
            return new CameraCaptureView(fileName, frame.Width, frame.Height, jpeg.Length, mirrored);
        }
    }
}
