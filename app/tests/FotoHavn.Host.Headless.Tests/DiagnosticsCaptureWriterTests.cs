using FotoHavn.Core.Camera;
using FotoHavn.Host.Diagnostics;
using SkiaSharp;

namespace FotoHavn.Host.Headless.Tests;

public sealed class DiagnosticsCaptureWriterTests : IDisposable
{
    private readonly string _data = Path.Combine(TestHost.NewScratchFolder(), "Data");
    private readonly FixedTime _time = new(new DateTimeOffset(2026, 10, 14, 7, 19, 10, 123, TimeSpan.Zero));

    [Fact]
    public async Task Writes_a_jpeg_to_the_diagnostics_folder_of_the_data_folder_and_returns_its_facts()
    {
        var writer = new DiagnosticsCaptureWriter(_data, _time);

        var view = await writer.SaveAsync(SolidFrame(64, 48, blue: 10, green: 20, red: 200), mirrored: true);

        var path = Path.Combine(_data, "diagnostics", view.FileName);
        Assert.Equal(Path.Combine(_data, "diagnostics"), writer.Folder);
        Assert.Matches(@"^capture-\d{8}-\d{6}-\d{3}\.jpg$", view.FileName);
        Assert.Equal("capture-" + _time.GetLocalNow().ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture) + ".jpg", view.FileName);
        Assert.Equal(64, view.Width);
        Assert.Equal(48, view.Height);
        Assert.True(view.Mirrored);
        Assert.Equal(new FileInfo(path).Length, view.ByteLength);
        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal([0xFF, 0xD8], bytes[..2]);
    }

    [Fact]
    public async Task The_jpeg_decodes_to_the_frames_size_and_colours()
    {
        var writer = new DiagnosticsCaptureWriter(_data, _time);

        var view = await writer.SaveAsync(SolidFrame(64, 48, blue: 10, green: 20, red: 200), mirrored: false);

        using var bitmap = SKBitmap.Decode(Path.Combine(writer.Folder, view.FileName));
        Assert.Equal(64, bitmap.Width);
        Assert.Equal(48, bitmap.Height);
        var pixel = bitmap.GetPixel(32, 24);
        Assert.InRange(pixel.Red, 190, 210);
        Assert.InRange(pixel.Green, 10, 30);
        Assert.InRange(pixel.Blue, 0, 20);
        Assert.False(view.Mirrored);
    }

    [Fact]
    public async Task The_jpeg_carries_no_metadata()
    {
        var writer = new DiagnosticsCaptureWriter(_data, _time);

        var view = await writer.SaveAsync(SolidFrame(32, 32, blue: 1, green: 2, red: 3), mirrored: false);

        var bytes = await File.ReadAllBytesAsync(Path.Combine(writer.Folder, view.FileName), TestContext.Current.CancellationToken);
        var text = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.DoesNotContain("Exif", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ICC_PROFILE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("http://ns.adobe.com/xap", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_captures_in_the_same_millisecond_get_different_names()
    {
        var writer = new DiagnosticsCaptureWriter(_data, _time);

        var first = await writer.SaveAsync(SolidFrame(8, 8, 0, 0, 0), mirrored: false);
        var second = await writer.SaveAsync(SolidFrame(8, 8, 0, 0, 0), mirrored: false);
        var third = await writer.SaveAsync(SolidFrame(8, 8, 0, 0, 0), mirrored: false);

        Assert.Equal(3, new[] { first.FileName, second.FileName, third.FileName }.Distinct().Count());
        Assert.EndsWith("-1.jpg", second.FileName, StringComparison.Ordinal);
        Assert.EndsWith("-2.jpg", third.FileName, StringComparison.Ordinal);
        Assert.Equal(3, Directory.GetFiles(writer.Folder).Length);
    }

    [Fact]
    public async Task Rows_with_padding_encode_from_the_stride()
    {
        var writer = new DiagnosticsCaptureWriter(_data, _time);
        const int width = 16;
        const int height = 8;
        const int stride = (width * 4) + 12;
        var bgra = new byte[stride * height];
        for (var row = 0; row < height; row++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = (row * stride) + (x * 4);
                bgra[at] = 255;
                bgra[at + 3] = 255;
            }
        }

        var view = await writer.SaveAsync(new CapturedFrame(bgra, width, height, stride, TimeSpan.Zero), mirrored: false);

        using var bitmap = SKBitmap.Decode(Path.Combine(writer.Folder, view.FileName));
        var pixel = bitmap.GetPixel(8, 4);
        Assert.InRange(pixel.Blue, 240, 255);
        Assert.InRange(pixel.Red, 0, 15);
    }

    [Fact]
    public async Task A_last_row_without_its_padding_still_encodes()
    {
        var writer = new DiagnosticsCaptureWriter(_data, _time);
        const int width = 16;
        const int height = 8;
        const int stride = (width * 4) + 12;
        var bgra = new byte[(stride * (height - 1)) + (width * 4)];
        Array.Fill(bgra, (byte)0x80);

        var view = await writer.SaveAsync(new CapturedFrame(bgra, width, height, stride, TimeSpan.Zero), mirrored: false);

        Assert.True(view.ByteLength > 0);
    }

    [Theory]
    [InlineData(0, 4, 0, 16)]
    [InlineData(4, 0, 16, 16)]
    [InlineData(4, 4, 15, 64)]
    [InlineData(4, 4, 16, 63)]
    public async Task A_frame_whose_size_stride_and_pixels_disagree_is_refused_and_writes_nothing(int width, int height, int stride, int bytes)
    {
        var writer = new DiagnosticsCaptureWriter(_data, _time);

        await Assert.ThrowsAsync<ArgumentException>(() => writer.SaveAsync(new CapturedFrame(new byte[bytes], width, height, stride, TimeSpan.Zero), mirrored: false));

        Assert.False(Directory.Exists(writer.Folder) && Directory.GetFiles(writer.Folder).Length > 0);
    }

    [Fact]
    public void A_null_frame_is_refused()
    {
        var writer = new DiagnosticsCaptureWriter(_data, _time);

        Assert.Throws<ArgumentNullException>(() => { _ = writer.SaveAsync(null!, mirrored: false); });
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path.GetDirectoryName(_data)!, recursive: true);
        }
        catch (IOException)
        {
            // Harmless: the scratch folder lives under the test output.
        }
    }

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

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
