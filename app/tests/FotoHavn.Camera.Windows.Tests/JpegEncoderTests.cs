using System.Buffers;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using SkiaSharp;

namespace FotoHavn.Camera.Windows.Tests;

public sealed class JpegEncoderTests
{
    [Fact]
    public void Encodes_a_decodable_jpeg_of_the_same_size()
    {
        var frame = Gradient(320, 240);

        var jpeg = JpegEncoder.Encode(frame);

        Assert.Equal([(byte)0xFF, (byte)0xD8], jpeg[..2]);
        Assert.Equal([(byte)0xFF, (byte)0xD9], jpeg[^2..]);
        using var decoded = SKBitmap.Decode(jpeg);
        Assert.NotNull(decoded);
        Assert.Equal((320, 240), (decoded.Width, decoded.Height));
    }

    [Fact]
    public void Decoded_pixels_stay_close_to_the_source_at_the_specified_quality()
    {
        var frame = Gradient(320, 240);

        using var decoded = SKBitmap.Decode(JpegEncoder.Encode(frame));
        var bgra = decoded.Copy(SKColorType.Bgra8888);

        var totalError = 0L;
        var samples = 0;
        var source = frame.Bgra.Span;
        for (var y = 0; y < 240; y += 3)
        {
            for (var x = 0; x < 320; x += 3)
            {
                var offset = (y * frame.Stride) + (x * 4);
                var pixel = bgra.GetPixel(x, y);
                totalError += Math.Abs(pixel.Blue - source[offset]) + Math.Abs(pixel.Green - source[offset + 1]) + Math.Abs(pixel.Red - source[offset + 2]);
                samples += 3;
            }
        }

        Assert.True((double)totalError / samples < 4, $"Mean channel error was {(double)totalError / samples:0.0}.");
    }

    [Fact]
    public void Writes_no_exif_or_icc_metadata()
    {
        var jpeg = JpegEncoder.Encode(Gradient(64, 48));

        var markers = new List<byte>();
        var position = 2;
        while (position + 4 <= jpeg.Length && jpeg[position] == 0xFF && jpeg[position + 1] != 0xDA)
        {
            markers.Add(jpeg[position + 1]);
            position += 2 + (jpeg[position + 2] << 8 | jpeg[position + 3]);
        }

        Assert.DoesNotContain((byte)0xE1, markers);
        Assert.DoesNotContain((byte)0xE2, markers);
        Assert.DoesNotContain((byte)0xFE, markers);
        var text = System.Text.Encoding.Latin1.GetString(jpeg);
        Assert.DoesNotContain("Exif", text);
        Assert.DoesNotContain("ICC_PROFILE", text);
    }

    [Fact]
    public void Honours_the_row_stride_and_ignores_the_padding()
    {
        var packed = Gradient(100, 60);
        var padded = new byte[(100 * 4 + 40) * 60];
        for (var y = 0; y < 60; y++)
        {
            packed.Bgra.Span.Slice(y * 400, 400).CopyTo(padded.AsSpan(y * 440, 400));
            padded.AsSpan((y * 440) + 400, 40).Fill(0xAB);
        }

        var fromPacked = JpegEncoder.Encode(packed);
        var fromPadded = JpegEncoder.Encode(new CapturedFrame(padded, 100, 60, 440, TimeSpan.Zero));

        Assert.Equal(fromPacked, fromPadded);
    }

    [Fact]
    public void Encodes_a_frame_that_is_a_slice_of_a_larger_array()
    {
        var packed = Gradient(64, 48);
        var big = new byte[100 + packed.Bgra.Length];
        packed.Bgra.Span.CopyTo(big.AsSpan(100));

        var sliced = JpegEncoder.Encode(new CapturedFrame(big.AsMemory(100), 64, 48, 256, TimeSpan.Zero));

        Assert.Equal(JpegEncoder.Encode(packed), sliced);
    }

    [Fact]
    public void Encodes_a_frame_whose_memory_is_not_array_backed()
    {
        var packed = Gradient(64, 48);
        using var manager = new NativeLikeMemory(packed.Bgra.ToArray());

        var jpeg = JpegEncoder.Encode(new CapturedFrame(manager.Memory, 64, 48, 256, TimeSpan.Zero));

        Assert.Equal(JpegEncoder.Encode(packed), jpeg);
    }

    [Fact]
    public void A_mirrored_capture_encodes_as_the_mirror_image()
    {
        var frame = TwoHalves(80, 40);
        var mirrored = frame.Bgra.ToArray();
        MirrorTransform.MirrorBgraInPlace(mirrored, frame.Width, frame.Height, frame.Stride);

        using var plain = SKBitmap.Decode(JpegEncoder.Encode(frame));
        using var flipped = SKBitmap.Decode(JpegEncoder.Encode(frame with { Bgra = mirrored }));

        Assert.True(IsRedish(plain.GetPixel(10, 20)));
        Assert.True(IsBluish(plain.GetPixel(70, 20)));
        Assert.True(IsBluish(flipped.GetPixel(10, 20)));
        Assert.True(IsRedish(flipped.GetPixel(70, 20)));
    }

    [Fact]
    public void Higher_detail_makes_a_bigger_file_than_a_flat_frame()
    {
        var flat = new CapturedFrame(new byte[320 * 240 * 4], 320, 240, 1280, TimeSpan.Zero);

        Assert.True(JpegEncoder.Encode(Gradient(320, 240)).Length > JpegEncoder.Encode(flat).Length);
    }

    [Fact]
    public void Rejects_a_missing_empty_or_short_frame()
    {
        Assert.Throws<ArgumentNullException>(() => JpegEncoder.Encode(null!));
        Assert.Throws<ArgumentException>(() => JpegEncoder.Encode(new CapturedFrame(new byte[16], 0, 4, 0, TimeSpan.Zero)));
        Assert.Throws<ArgumentException>(() => JpegEncoder.Encode(new CapturedFrame(new byte[16], 4, 4, 16, TimeSpan.Zero)));
        Assert.Throws<ArgumentException>(() => JpegEncoder.Encode(new CapturedFrame(new byte[64], 4, 4, 8, TimeSpan.Zero)));
    }

    private static bool IsRedish(SKColor color) => color.Red > 150 && color.Blue < 100;

    private static bool IsBluish(SKColor color) => color.Blue > 150 && color.Red < 100;

    private static CapturedFrame Gradient(int width, int height)
    {
        var bytes = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 4;
                bytes[offset] = (byte)(x * 255 / width);
                bytes[offset + 1] = (byte)(y * 255 / height);
                bytes[offset + 2] = (byte)((x + y) * 255 / (width + height));
                bytes[offset + 3] = 255;
            }
        }

        return new CapturedFrame(bytes, width, height, width * 4, TimeSpan.FromSeconds(1));
    }

    private static CapturedFrame TwoHalves(int width, int height)
    {
        var bytes = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 4;
                var left = x < width / 2;
                bytes[offset] = (byte)(left ? 0 : 255);
                bytes[offset + 1] = 0;
                bytes[offset + 2] = (byte)(left ? 255 : 0);
                bytes[offset + 3] = 255;
            }
        }

        return new CapturedFrame(bytes, width, height, width * 4, TimeSpan.Zero);
    }

    private sealed class NativeLikeMemory(byte[] data) : MemoryManager<byte>
    {
        public override Span<byte> GetSpan() => data;

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
