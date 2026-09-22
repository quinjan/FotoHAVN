using System.Runtime.InteropServices.WindowsRuntime;
using FotoHavn.App;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Xunit;

namespace FotoHavn.WindowsIntegrationTests;

public sealed class CaptureFrameEncoderIntegrationTests
{
    [Fact]
    public async Task Saved_Capture_is_the_shared_three_by_two_crop_seen_in_the_preview()
    {
        using var source = CreateWideFrame();
        var preview = CameraPreviewRenderPolicy.CalculateLayout(
            availableWidth: 80,
            availableHeight: 40);

        var result = await CaptureFrameEncoder.EncodeJpegAsync(
            source,
            sequence: 7,
            receivedAt: DateTimeOffset.UnixEpoch,
            preview.SourceCrop);

        Assert.Equal(7, result.Sequence);
        Assert.Equal(68, result.Width);
        Assert.Equal(45, result.Height);
        var decoded = await DecodeAsync(result.JpegBytes);
        Assert.Equal(BitmapDecoder.JpegDecoderId, decoded.CodecId);
        Assert.Equal(68u, decoded.Width);
        Assert.Equal(45u, decoded.Height);
        AssertCenterPixel(decoded.Pixels, decoded.Width, x: 5, y: 20);
        AssertCenterPixel(decoded.Pixels, decoded.Width, x: 62, y: 20);
        AssertVerticalBandPixel(decoded.Pixels, decoded.Width, x: 34, y: 2);
        AssertVerticalBandPixel(decoded.Pixels, decoded.Width, x: 34, y: 42);
    }

    private static SoftwareBitmap CreateWideFrame()
    {
        const int width = 80;
        const int height = 45;
        var bitmap = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8,
            width,
            height,
            BitmapAlphaMode.Premultiplied);
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 4;
                var isSideBand = x < 6 || x >= 74;
                var isVerticalBand = y < 5 || y >= 40;
                pixels[offset] = isVerticalBand && !isSideBand ? (byte)220 : (byte)32;
                pixels[offset + 1] = isSideBand || isVerticalBand ? (byte)32 : (byte)188;
                pixels[offset + 2] = isSideBand ? (byte)220 : (byte)32;
                pixels[offset + 3] = 255;
            }
        }

        bitmap.CopyFromBuffer(pixels.AsBuffer());
        return bitmap;
    }

    private static async Task<(Guid CodecId, uint Width, uint Height, byte[] Pixels)> DecodeAsync(
        ReadOnlyMemory<byte> jpeg)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(jpeg.ToArray());
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
        return (decoder.DecoderInformation.CodecId, decoder.PixelWidth, decoder.PixelHeight, data.DetachPixelData());
    }

    private static void AssertCenterPixel(byte[] pixels, uint width, int x, int y)
    {
        var offset = checked(((y * (int)width) + x) * 4);
        Assert.InRange(pixels[offset + 1], 130, 230);
        Assert.InRange(pixels[offset + 2], 0, 90);
    }

    private static void AssertVerticalBandPixel(byte[] pixels, uint width, int x, int y)
    {
        var offset = checked(((y * (int)width) + x) * 4);
        Assert.InRange(pixels[offset], 130, 230);
        Assert.InRange(pixels[offset + 1], 0, 90);
        Assert.InRange(pixels[offset + 2], 0, 90);
    }
}
