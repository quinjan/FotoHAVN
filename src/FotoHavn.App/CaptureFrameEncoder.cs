using FotoHavn.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace FotoHavn.App;

internal static class CaptureFrameEncoder
{
    public static async Task<CapturedFrame> EncodeJpegAsync(
        SoftwareBitmap bitmap,
        long sequence,
        DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var crop = CenterCrop(
            checked((uint)bitmap.PixelWidth),
            checked((uint)bitmap.PixelHeight),
            CaptureCropPolicy.AspectRatio);

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        encoder.BitmapTransform.Bounds = crop;
        await encoder.FlushAsync();

        var bytes = new byte[checked((int)stream.Size)];
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)bytes.Length);
        reader.ReadBytes(bytes);
        return new CapturedFrame(
            sequence,
            receivedAt,
            checked((int)crop.Width),
            checked((int)crop.Height),
            bytes);
    }

    private static BitmapBounds CenterCrop(uint width, uint height, double targetAspectRatio)
    {
        ArgumentOutOfRangeException.ThrowIfZero(width);
        ArgumentOutOfRangeException.ThrowIfZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetAspectRatio);

        var sourceAspectRatio = (double)width / height;
        if (sourceAspectRatio > targetAspectRatio)
        {
            var cropWidth = Math.Min(width, checked((uint)Math.Round(height * targetAspectRatio)));
            return new BitmapBounds
            {
                X = (width - cropWidth) / 2,
                Y = 0,
                Width = cropWidth,
                Height = height,
            };
        }

        var cropHeight = Math.Min(height, checked((uint)Math.Round(width / targetAspectRatio)));
        return new BitmapBounds
        {
            X = 0,
            Y = (height - cropHeight) / 2,
            Width = width,
            Height = cropHeight,
        };
    }
}
