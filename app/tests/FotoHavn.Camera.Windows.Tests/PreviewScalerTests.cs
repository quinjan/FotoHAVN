namespace FotoHavn.Camera.Windows.Tests;

public sealed class PreviewScalerTests
{
    [Theory]
    [InlineData(1920, 1080, 1280, 720)]
    [InlineData(1280, 720, 1280, 720)]
    [InlineData(640, 480, 640, 480)]
    [InlineData(1600, 1200, 1280, 960)]
    [InlineData(3840, 2160, 1280, 720)]
    [InlineData(1281, 721, 1280, 720)]
    [InlineData(4000, 1, 1280, 1)]
    [InlineData(1, 1, 1, 1)]
    public void Target_size_keeps_the_aspect_ratio_and_never_upscales(int width, int height, int expectedWidth, int expectedHeight)
    {
        Assert.Equal((expectedWidth, expectedHeight), PreviewScaler.TargetSize(width, height));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    public void Target_size_rejects_an_empty_frame(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PreviewScaler.TargetSize(width, height));

    [Fact]
    public void Scale_swizzles_bgra_to_rgba_and_forces_alpha_to_opaque_without_resizing()
    {
        var source = new PinnedBuffer();
        var frame = new SolidFrame(64, 48, blue: 10, green: 20, red: 30, alpha: 0);
        var stride = frame.CopyTo(source);

        var (rgba, width, height) = new PreviewScaler().Scale(source, 64, 48, stride);

        Assert.Equal((64, 48), (width, height));
        Assert.Equal(64 * 48 * 4, rgba.Length);
        AssertEveryPixel(rgba.Span, red: 30, green: 20, blue: 10);
    }

    [Fact]
    public void Scale_downsizes_to_1280_wide_and_keeps_the_colour()
    {
        var source = new PinnedBuffer();
        var frame = new SolidFrame(1920, 1080, blue: 200, green: 100, red: 50);
        var stride = frame.CopyTo(source);

        var (rgba, width, height) = new PreviewScaler().Scale(source, 1920, 1080, stride);

        Assert.Equal((1280, 720), (width, height));
        Assert.Equal(1280 * 720 * 4, rgba.Length);
        AssertEveryPixel(rgba.Span, red: 50, green: 100, blue: 200);
    }

    [Fact]
    public void Scale_forces_alpha_to_opaque_when_resizing_too()
    {
        var source = new PinnedBuffer();
        var stride = new SolidFrame(1920, 1080, blue: 1, green: 2, red: 3, alpha: 0).CopyTo(source);

        var (rgba, _, _) = new PreviewScaler().Scale(source, 1920, 1080, stride);

        AssertEveryPixel(rgba.Span, red: 3, green: 2, blue: 1);
    }

    [Fact]
    public void Scale_honours_a_padded_source_stride()
    {
        var source = new PinnedBuffer();
        var frame = new SolidFrame(100, 50, blue: 1, green: 2, red: 3, strideBytes: 100 * 4 + 64);
        var stride = frame.CopyTo(source);

        var (rgba, width, height) = new PreviewScaler().Scale(source, 100, 50, stride);

        Assert.Equal((100, 50), (width, height));
        AssertEveryPixel(rgba.Span, red: 3, green: 2, blue: 1);
    }

    [Fact]
    public void Scale_keeps_each_pixels_position_when_the_frame_is_not_uniform()
    {
        const int width = 8;
        const int height = 4;
        var source = new PinnedBuffer();
        source.EnsureCapacity(width * height * 4);
        var pixels = source.AsSpan(width * height * 4);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 4;
                pixels[offset] = (byte)x;
                pixels[offset + 1] = (byte)y;
                pixels[offset + 2] = (byte)(x + y);
                pixels[offset + 3] = 255;
            }
        }

        var (rgba, _, _) = new PreviewScaler().Scale(source, width, height, width * 4);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 4;
                Assert.Equal((byte)(x + y), rgba.Span[offset]);
                Assert.Equal((byte)y, rgba.Span[offset + 1]);
                Assert.Equal((byte)x, rgba.Span[offset + 2]);
                Assert.Equal(255, rgba.Span[offset + 3]);
            }
        }
    }

    [Fact]
    public void Scale_reuses_one_output_buffer_across_frames_of_the_same_size()
    {
        var source = new PinnedBuffer();
        var frame = new SolidFrame(1920, 1080, 1, 2, 3);
        var stride = frame.CopyTo(source);
        var scaler = new PreviewScaler();

        var first = scaler.Scale(source, 1920, 1080, stride);
        var second = scaler.Scale(source, 1920, 1080, stride);

        Assert.Equal(1, scaler.Allocations);
        Assert.True(first.Rgba.Span.Overlaps(second.Rgba.Span));
    }

    [Fact]
    public void Scale_allocates_again_only_for_a_larger_output()
    {
        var scaler = new PreviewScaler();
        var small = new PinnedBuffer();
        var large = new PinnedBuffer();
        var smallStride = new SolidFrame(320, 240, 1, 2, 3).CopyTo(small);
        var largeStride = new SolidFrame(1920, 1080, 1, 2, 3).CopyTo(large);

        scaler.Scale(small, 320, 240, smallStride);
        scaler.Scale(large, 1920, 1080, largeStride);
        scaler.Scale(small, 320, 240, smallStride);

        Assert.Equal(2, scaler.Allocations);
    }

    [Fact]
    public void Scale_rejects_a_null_source() =>
        Assert.Throws<ArgumentNullException>(() => new PreviewScaler().Scale(null!, 10, 10, 40));

    private static void AssertEveryPixel(ReadOnlySpan<byte> rgba, byte red, byte green, byte blue)
    {
        for (var offset = 0; offset < rgba.Length; offset += 4)
        {
            if (rgba[offset] != red || rgba[offset + 1] != green || rgba[offset + 2] != blue || rgba[offset + 3] != 255)
            {
                Assert.Fail($"Pixel at byte {offset} is {rgba[offset]},{rgba[offset + 1]},{rgba[offset + 2]},{rgba[offset + 3]}; expected {red},{green},{blue},255.");
            }
        }
    }
}
