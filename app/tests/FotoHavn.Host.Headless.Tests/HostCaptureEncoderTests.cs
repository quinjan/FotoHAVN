using FotoHavn.Core.Camera;
using FotoHavn.Host.Diagnostics;
using SkiaSharp;

namespace FotoHavn.Host.Headless.Tests;

public sealed class HostCaptureEncoderTests
{
    private static CapturedFrame SolidFrame(int width, int height, byte blue, byte green, byte red)
    {
        var bgra = new byte[width * height * 4];
        for (var at = 0; at < bgra.Length; at += 4)
        {
            bgra[at] = blue;
            bgra[at + 1] = green;
            bgra[at + 2] = red;
            bgra[at + 3] = 255;
        }

        return new CapturedFrame(bgra, width, height, width * 4, TimeSpan.Zero);
    }

    [Fact]
    public void A_Capture_is_encoded_as_a_jpeg_of_the_frames_size_and_colours()
    {
        var jpeg = new HostCaptureEncoder().EncodeJpeg(SolidFrame(64, 48, blue: 10, green: 20, red: 200));

        Assert.Equal([0xFF, 0xD8], jpeg[..2]);
        using var bitmap = SKBitmap.Decode(jpeg);
        Assert.Equal((64, 48), (bitmap.Width, bitmap.Height));
        var pixel = bitmap.GetPixel(32, 24);
        Assert.InRange(pixel.Red, 190, 210);
        Assert.InRange(pixel.Green, 10, 30);
    }

    [Fact]
    public void A_Capture_is_encoded_exactly_as_the_diagnostics_Capture_is()
    {
        var frame = SolidFrame(32, 32, blue: 5, green: 90, red: 140);

        Assert.Equal(DiagnosticsCaptureWriter.Encode(frame), new HostCaptureEncoder().EncodeJpeg(frame));
    }

    [Fact]
    public void A_frame_whose_pixels_do_not_match_its_size_is_refused_and_a_missing_one_too()
    {
        var encoder = new HostCaptureEncoder();

        Assert.Throws<ArgumentException>(() => encoder.EncodeJpeg(new CapturedFrame(new byte[10], 4, 4, 16, TimeSpan.Zero)));
        Assert.Throws<ArgumentNullException>(() => encoder.EncodeJpeg(null!));
    }
}
