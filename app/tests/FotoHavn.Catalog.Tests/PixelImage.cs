using System.Security.Cryptography;
using SkiaSharp;

namespace FotoHavn.Catalog.Tests;

/// <summary>A decoded PNG, held as plain (unpremultiplied) BGRA, the byte order the Look kernels use.</summary>
internal sealed class PixelImage
{
    private PixelImage(int width, int height, byte[] bgra)
    {
        Width = width;
        Height = height;
        Bgra = bgra;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Bgra { get; }

    public static PixelImage Decode(ReadOnlyMemory<byte> png)
    {
        using var data = SKData.CreateCopy(png.Span);
        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Not an image.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = SKBitmap.Decode(codec, info);
        return new PixelImage(info.Width, info.Height, bitmap.Bytes);
    }

    public (byte R, byte G, byte B, byte A) At(int x, int y)
    {
        var i = ((y * Width) + x) * 4;
        return (Bgra[i + 2], Bgra[i + 1], Bgra[i], Bgra[i + 3]);
    }

    /// <summary>SHA-256 of the decoded RGBA pixels, so encoder metadata never changes a golden hash.</summary>
    public string Hash()
    {
        var rgba = new byte[Bgra.Length];
        for (var i = 0; i < Bgra.Length; i += 4)
        {
            rgba[i] = Bgra[i + 2];
            rgba[i + 1] = Bgra[i + 1];
            rgba[i + 2] = Bgra[i];
            rgba[i + 3] = Bgra[i + 3];
        }

        return Convert.ToHexString(SHA256.HashData(rgba)).ToLowerInvariant();
    }

    /// <summary>The window's pixels, row by row, tightly packed.</summary>
    public byte[] Region(int x, int y, int width, int height)
    {
        var region = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            Array.Copy(Bgra, (((y + row) * Width) + x) * 4, region, row * width * 4, width * 4);
        }

        return region;
    }

    public void SetRegion(int x, int y, int width, int height, byte[] region)
    {
        for (var row = 0; row < height; row++)
        {
            Array.Copy(region, row * width * 4, Bgra, (((y + row) * Width) + x) * 4, width * 4);
        }
    }
}
