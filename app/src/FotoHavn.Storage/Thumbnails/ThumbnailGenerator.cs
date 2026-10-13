using FotoHavn.Core.Storage;
using SkiaSharp;

namespace FotoHavn.Storage.Thumbnails;

/// <summary>Makes the derived JPEG thumbnails of Captures and Photo Strips.</summary>
public static class ThumbnailGenerator
{
    public const int JpegQuality = 85;

    /// <summary>
    /// Decodes <paramref name="source"/> (JPEG or PNG), resizes it to <paramref name="width"/> pixels wide keeping its aspect
    /// ratio with cubic resampling over white, and encodes it as JPEG quality 85.
    /// </summary>
    /// <exception cref="StorageException">The bytes aren't an image Skia can decode.</exception>
    public static byte[] CreateJpeg(ReadOnlySpan<byte> source, int width)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        using var data = SKData.CreateCopy(source);
        using var original = SKBitmap.Decode(data)
            ?? throw new StorageException(StorageErrorKind.Unknown, "The source image can't be decoded.");
        var height = Math.Max(1, (int)Math.Round((double)original.Height * width / original.Width, MidpointRounding.AwayFromZero));
        using var sourceImage = SKImage.FromBitmap(original);
        using var resized = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(resized))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawImage(sourceImage, new SKRect(0, 0, width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        }

        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return encoded.ToArray();
    }
}
