using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using SkiaSharp;

namespace FotoHavn.Catalog.Tests;

/// <summary>
/// Deterministic synthetic photographs, built from integer arithmetic only (no Skia drawing, no randomness) and encoded as
/// PNG so decoding is exact. Each quadrant looks different and the figures sit off-centre, so a crop, flip, or swap shows.
/// </summary>
internal static class FixturePhotos
{
    private static readonly Lazy<byte[]> LandscapeBytes = new(() => Scene(1920, 1080));
    private static readonly Lazy<byte[]> PortraitBytes = new(() => Scene(1080, 1920));
    private static readonly Lazy<byte[]> SquareBytes = new(() => Scene(1200, 1200));
    private static readonly Lazy<byte[]> FourThreeBytes = new(() => Scene(1600, 1200));

    /// <summary>1920×1080.</summary>
    public static byte[] Landscape => LandscapeBytes.Value;

    /// <summary>1080×1920.</summary>
    public static byte[] Portrait => PortraitBytes.Value;

    /// <summary>1200×1200.</summary>
    public static byte[] Square => SquareBytes.Value;

    /// <summary>1600×1200.</summary>
    public static byte[] FourThree => FourThreeBytes.Value;

    public static IReadOnlyList<byte[]> All => [Landscape, Portrait, Square, FourThree];

    /// <summary>The photograph for window <paramref name="index"/>: the four fixtures in turn.</summary>
    public static CompositionPhoto ForWindow(int index) => new CompositionPhoto.Encoded(All[index % All.Count]);

    public static byte[] Solid(byte r, byte g, byte b, int width = 64, int height = 64) =>
        Encode(width, height, (_, _) => (r, g, b));

    /// <summary>Left half red, right half blue.</summary>
    public static byte[] LeftRight(int width, int height) =>
        Encode(width, height, (x, _) => x * 2 < width ? ((byte)255, (byte)0, (byte)0) : ((byte)0, (byte)0, (byte)255));

    /// <summary>Top half green, bottom half yellow.</summary>
    public static byte[] TopBottom(int width, int height) =>
        Encode(width, height, (_, y) => y * 2 < height ? ((byte)0, (byte)200, (byte)0) : ((byte)255, (byte)220, (byte)0));

    /// <summary>Grey with a red band on the left (and blue on the right) edge, <paramref name="band"/> pixels wide.</summary>
    public static byte[] SideBands(int width, int height, int band) =>
        Encode(width, height, (x, _) => x < band ? ((byte)255, (byte)0, (byte)0) : x >= width - band ? ((byte)0, (byte)0, (byte)255) : ((byte)128, (byte)128, (byte)128));

    /// <summary>Grey with a red band on the top (and blue on the bottom) edge, <paramref name="band"/> pixels tall.</summary>
    public static byte[] EdgeBands(int width, int height, int band) =>
        Encode(width, height, (_, y) => y < band ? ((byte)255, (byte)0, (byte)0) : y >= height - band ? ((byte)0, (byte)0, (byte)255) : ((byte)128, (byte)128, (byte)128));

    public static byte[] Jpeg(byte[] png, int quality = 90)
    {
        using var image = SKImage.FromEncodedData(png);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.ToArray();
    }

    /// <summary>The photograph as a raw BGRA frame, each row padded to <paramref name="stride"/> bytes with 0xAB.</summary>
    public static CapturedFrame Frame(byte[] png, int stride)
    {
        var image = PixelImage.Decode(png);
        var bytes = new byte[stride * image.Height];
        Array.Fill(bytes, (byte)0xAB);
        for (var row = 0; row < image.Height; row++)
        {
            Array.Copy(image.Bgra, row * image.Width * 4, bytes, row * stride, image.Width * 4);
        }

        return new CapturedFrame(bytes, image.Width, image.Height, stride, TimeSpan.Zero);
    }

    private static byte[] Scene(int width, int height) => Encode(width, height, (x, y) => Pixel(x, y, width, height));

    private static (byte R, byte G, byte B) Pixel(int x, int y, int width, int height)
    {
        var halfWidth = width / 2;
        var halfHeight = height / 2;
        var localX = (x % halfWidth) * 255 / halfWidth;
        var localY = (y % halfHeight) * 255 / halfHeight;
        var right = x >= halfWidth;
        var bottom = y >= halfHeight;

        // A pale disc up-left of centre and a dark block low-right of centre break every symmetry.
        var dx = x - (width * 3 / 10);
        var dy = y - (height * 35 / 100);
        var radius = Math.Min(width, height) * 12 / 100;
        if ((dx * dx) + (dy * dy) <= radius * radius)
        {
            return (250, 250, 240);
        }

        if (x >= width * 62 / 100 && x < width * 80 / 100 && y >= height * 55 / 100 && y < height * 90 / 100)
        {
            return (20, 30, 70);
        }

        return (right, bottom) switch
        {
            (false, false) => (255, (byte)localX, 40),
            (true, false) => (40, (byte)(120 + (localY / 2)), (byte)(255 - (localY / 2))),
            (false, true) => (((x / 60) + (y / 60)) % 2 == 0) ? ((byte)40, (byte)200, (byte)90) : ((byte)200, (byte)40, (byte)160),
            _ => ((byte)localX, 128, (byte)(255 - localY)),
        };
    }

    private static byte[] Encode(int width, int height, Func<int, int, (byte R, byte G, byte B)> pixel)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var span = bitmap.GetPixelSpan();
        var offset = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = pixel(x, y);
                span[offset++] = r;
                span[offset++] = g;
                span[offset++] = b;
                span[offset++] = 255;
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
