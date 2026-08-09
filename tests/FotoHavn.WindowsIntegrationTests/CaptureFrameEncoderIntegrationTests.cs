using System.Runtime.InteropServices.WindowsRuntime;
using FotoHavn.App;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Xunit;

namespace FotoHavn.WindowsIntegrationTests;

public sealed class CaptureFrameEncoderIntegrationTests
{
    [Fact]
    public async Task Saved_Capture_is_the_shared_three_by_two_center_crop_seen_in_the_preview()
    {
        using var source = CreateWideFrame();

        var result = await CaptureFrameEncoder.EncodeJpegAsync(
            source,
            sequence: 7,
            receivedAt: DateTimeOffset.UnixEpoch);

        Assert.Equal(7, result.Sequence);
        Assert.Equal(60, result.Width);
        Assert.Equal(40, result.Height);
        var decoded = await DecodeAsync(result.JpegBytes);
        Assert.Equal(BitmapDecoder.JpegDecoderId, decoded.CodecId);
        Assert.Equal(60u, decoded.Width);
        Assert.Equal(40u, decoded.Height);
        AssertCenterPixel(decoded.Pixels, decoded.Width, x: 5, y: 20);
        AssertCenterPixel(decoded.Pixels, decoded.Width, x: 54, y: 20);
    }

    private static SoftwareBitmap CreateWideFrame()
    {
        const int width = 80;
        const int height = 40;
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
                var isOuterBand = x < 10 || x >= 70;
                pixels[offset] = 32;
                pixels[offset + 1] = isOuterBand ? (byte)32 : (byte)188;
                pixels[offset + 2] = isOuterBand ? (byte)220 : (byte)32;
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
}
