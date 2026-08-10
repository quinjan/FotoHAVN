using FotoHavn.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace FotoHavn.App;

internal static class CaptureFrameEncoder
{
    public static async Task<CapturedFrame> EncodeJpegAsync(
        SoftwareBitmap bitmap,
        long sequence,
        DateTimeOffset receivedAt,
        CameraSourceCrop crop)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(crop);
        var bounds = CalculateCenterCrop(
            checked((uint)bitmap.PixelWidth),
            checked((uint)bitmap.PixelHeight),
            crop);

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        encoder.BitmapTransform.Bounds = bounds;
        await encoder.FlushAsync();

        var bytes = new byte[checked((int)stream.Size)];
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)bytes.Length);
        reader.ReadBytes(bytes);
        return new CapturedFrame(
            sequence,
            receivedAt,
            checked((int)bounds.Width),
            checked((int)bounds.Height),
            bytes);
    }

    internal static BitmapBounds CalculateCenterCrop(uint width, uint height, CameraSourceCrop crop)
    {
        ArgumentOutOfRangeException.ThrowIfZero(width);
        ArgumentOutOfRangeException.ThrowIfZero(height);
        ArgumentNullException.ThrowIfNull(crop);
        if (crop.X < 0 || crop.Y < 0 || crop.Width <= 0 || crop.Height <= 0 ||
            crop.X + crop.Width > 1 || crop.Y + crop.Height > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(crop));
        }

        var cropWidth = Math.Clamp(
            checked((uint)Math.Round(crop.Width * width, MidpointRounding.AwayFromZero)),
            1,
            width);
        var cropHeight = Math.Clamp(
            checked((uint)Math.Round(crop.Height * height, MidpointRounding.AwayFromZero)),
            1,
            height);
        var centerX = (crop.X + (crop.Width / 2)) * width;
        var centerY = (crop.Y + (crop.Height / 2)) * height;
        var left = Math.Min(
            width - cropWidth,
            checked((uint)Math.Max(0, Math.Round(centerX - (cropWidth / 2d), MidpointRounding.AwayFromZero))));
        var top = Math.Min(
            height - cropHeight,
            checked((uint)Math.Max(0, Math.Round(centerY - (cropHeight / 2d), MidpointRounding.AwayFromZero))));
        return new BitmapBounds
        {
            X = left,
            Y = top,
            Width = cropWidth,
            Height = cropHeight,
        };
    }
}
