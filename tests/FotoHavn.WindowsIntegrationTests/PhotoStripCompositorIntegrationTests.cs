using System.Runtime.InteropServices.WindowsRuntime;
using FotoHavn.App;
using FotoHavn.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Xunit;

namespace FotoHavn.WindowsIntegrationTests;

public sealed class PhotoStripCompositorIntegrationTests
{
    [Fact]
    public async Task Photo_Strip_is_a_lossless_white_two_by_six_with_four_center_cropped_Captures()
    {
        var root = Path.Combine(Path.GetTempPath(), "FotoHAVN-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var references = new List<CaptureReference>();
            for (var captureNumber = 1; captureNumber <= 4; captureNumber++)
            {
                var path = Path.Combine(root, $"capture-{captureNumber}.jpg");
                await File.WriteAllBytesAsync(
                    path,
                    await EncodeWideFrameAsync(),
                    TestContext.Current.CancellationToken);
                references.Add(new CaptureReference(path));
            }

            var result = await new PhotoStripCompositor().ComposeAsync(
                new PhotoStripCompositionRequest("Mika & Paolo's Wedding", references),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsAvailable);
            Assert.Equal(600, result.Width);
            Assert.Equal(1800, result.Height);
            var decoded = await DecodeAsync(result.PngBytes);
            Assert.Equal(BitmapDecoder.PngDecoderId, decoded.CodecId);
            AssertPixel(decoded.Pixels, result.Width, 0, 0, blue: 255, green: 255, red: 255);
            AssertPixel(decoded.Pixels, result.Width, 300, 200, blue: 32, green: 188, red: 32, tolerance: 30);
            AssertPixel(decoded.Pixels, result.Width, 300, 399, blue: 255, green: 255, red: 255);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    public async Task Missing_or_invalid_Capture_makes_composition_unavailable(string damage)
    {
        var root = Path.Combine(Path.GetTempPath(), "FotoHAVN-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var references = new List<CaptureReference>();
            for (var captureNumber = 1; captureNumber <= 4; captureNumber++)
            {
                var path = Path.Combine(root, $"capture-{captureNumber}.jpg");
                if (captureNumber != 3 || damage == "invalid")
                {
                    await File.WriteAllBytesAsync(
                        path,
                        captureNumber == 3 ? [1, 2, 3] : await EncodeWideFrameAsync(),
                        TestContext.Current.CancellationToken);
                }
                references.Add(new CaptureReference(path));
            }

            var result = await new PhotoStripCompositor().ComposeAsync(
                new PhotoStripCompositionRequest("Mika & Paolo's Wedding", references),
                TestContext.Current.CancellationToken);

            Assert.False(result.IsAvailable);
            Assert.True(result.PngBytes.IsEmpty);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<byte[]> EncodeWideFrameAsync()
    {
        const int width = 80;
        const int height = 40;
        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 4;
                var isOuterEdge = x < 10 || x >= 70;
                pixels[offset] = isOuterEdge ? (byte)32 : (byte)32;
                pixels[offset + 1] = isOuterEdge ? (byte)32 : (byte)188;
                pixels[offset + 2] = isOuterEdge ? (byte)220 : (byte)32;
                pixels[offset + 3] = 255;
            }
        }

        bitmap.CopyFromBuffer(pixels.AsBuffer());
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        return await ReadStreamAsync(stream);
    }

    private static async Task<(Guid CodecId, byte[] Pixels)> DecodeAsync(ReadOnlyMemory<byte> png)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(png.ToArray());
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var data = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);
        return (decoder.DecoderInformation.CodecId, data.DetachPixelData());
    }

    private static async Task<byte[]> ReadStreamAsync(IRandomAccessStream stream)
    {
        var result = new byte[checked((int)stream.Size)];
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)result.Length);
        reader.ReadBytes(result);
        return result;
    }

    private static void AssertPixel(
        byte[] pixels,
        int width,
        int x,
        int y,
        byte blue,
        byte green,
        byte red,
        int tolerance = 0)
    {
        var offset = ((y * width) + x) * 4;
        Assert.InRange(pixels[offset], Math.Max(0, blue - tolerance), Math.Min(255, blue + tolerance));
        Assert.InRange(pixels[offset + 1], Math.Max(0, green - tolerance), Math.Min(255, green + tolerance));
        Assert.InRange(pixels[offset + 2], Math.Max(0, red - tolerance), Math.Min(255, red + tolerance));
        Assert.Equal(255, pixels[offset + 3]);
    }
}
