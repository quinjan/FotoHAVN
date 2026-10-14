namespace FotoHavn.Catalog;

/// <summary>
/// The Look kernels, a port of the website's <c>applyLook</c>, operation for operation: gains, luma and saturation, contrast,
/// offsets, then clamp and round, in double precision on encoded sRGB. Naturale never changes a byte; alpha is never touched.
/// </summary>
public static class LookKernels
{
    private const double LumaRed = 0.2126;
    private const double LumaGreen = 0.7152;
    private const double LumaBlue = 0.0722;

    private enum Kind
    {
        Naturale,
        Monochrome,
        SoftMonochrome,
        WarmMonochrome,
        Sepia,
        SoftColor,
        Classic,
    }

    /// <summary>Whether <paramref name="lookId"/> is one of the seven Looks.</summary>
    public static bool IsKnown(string lookId)
    {
        ArgumentNullException.ThrowIfNull(lookId);
        return TryKind(lookId, out _);
    }

    /// <summary>
    /// Applies the Look to <paramref name="bgra"/> in place. The buffer is BGRA, four bytes per pixel in B, G, R, A order
    /// (SkiaSharp's native <c>Bgra8888</c> on Windows, and the order camera frames arrive in). The compositor applies it to
    /// one photograph window at a time, never to the whole paper.
    /// </summary>
    /// <exception cref="ArgumentException">The Look is unknown, or the length isn't a multiple of four.</exception>
    public static void Apply(Span<byte> bgra, string lookId)
    {
        ArgumentNullException.ThrowIfNull(lookId);
        if (!TryKind(lookId, out var kind))
        {
            throw new ArgumentException($"Unknown Look {lookId}.", nameof(lookId));
        }

        if (bgra.Length % 4 != 0)
        {
            throw new ArgumentException("A BGRA buffer holds whole four-byte pixels.", nameof(bgra));
        }

        if (kind == Kind.Naturale)
        {
            return;
        }

        for (var i = 0; i < bgra.Length; i += 4)
        {
            var b = bgra[i] / 255.0;
            var g = bgra[i + 1] / 255.0;
            var r = bgra[i + 2] / 255.0;
            if (kind == Kind.Classic)
            {
                r *= 1.055;
                b *= 0.91;
            }

            var luma = (LumaRed * r) + (LumaGreen * g) + (LumaBlue * b);
            switch (kind)
            {
                case Kind.Monochrome:
                    r = g = b = ((luma - 0.5) * 1.1) + 0.51;
                    break;
                case Kind.SoftMonochrome:
                    r = g = b = ((luma - 0.5) * 0.94) + 0.52;
                    break;
                case Kind.WarmMonochrome:
                    var warmTone = ((luma - 0.5) * 1.02) + 0.505;
                    r = warmTone + 0.02;
                    g = warmTone + 0.015;
                    b = warmTone - 0.012;
                    break;
                case Kind.Sepia:
                    var sepiaTone = ((luma - 0.5) * 0.94) + 0.52;
                    r = sepiaTone + 0.045;
                    g = sepiaTone + 0.025;
                    b = sepiaTone - 0.025;
                    break;
                default:
                    var soft = kind == Kind.SoftColor;
                    var saturation = soft ? 0.88 : 0.94;
                    var contrast = soft ? 0.94 : 0.98;
                    var lift = soft ? 0.02 : 0.015;
                    r = ((((luma + ((r - luma) * saturation)) - 0.5) * contrast) + 0.5) + lift;
                    g = ((((luma + ((g - luma) * saturation)) - 0.5) * contrast) + 0.5) + lift;
                    b = ((((luma + ((b - luma) * saturation)) - 0.5) * contrast) + 0.5) + lift;
                    break;
            }

            bgra[i] = ToByte(b);
            bgra[i + 1] = ToByte(g);
            bgra[i + 2] = ToByte(r);
        }
    }

    private static bool TryKind(string lookId, out Kind kind)
    {
        switch (lookId)
        {
            case CatalogData.NaturaleId: kind = Kind.Naturale; return true;
            case CatalogData.MonochromeId: kind = Kind.Monochrome; return true;
            case CatalogData.SoftMonochromeId: kind = Kind.SoftMonochrome; return true;
            case CatalogData.WarmMonochromeId: kind = Kind.WarmMonochrome; return true;
            case CatalogData.SepiaId: kind = Kind.Sepia; return true;
            case CatalogData.SoftColorId: kind = Kind.SoftColor; return true;
            case CatalogData.ClassicId: kind = Kind.Classic; return true;
            default: kind = Kind.Naturale; return false;
        }
    }

    // JavaScript's Math.round rounds halves up; the value is non-negative after the clamp, so AwayFromZero is the same rule
    // (the default banker's rounding is not).
    internal static byte ToByte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255, MidpointRounding.AwayFromZero);
}
