using System.Collections.Concurrent;
using System.Reflection;
using SkiaSharp;

namespace FotoHavn.Catalog;

/// <summary>
/// The artwork and fonts, loaded once from the assembly's embedded resources and cached. Fonts never come from the system
/// font manager, so a strip looks the same on every machine. Every member is thread-safe. The returned images and typefaces
/// belong to the cache: draw with them freely, and never dispose them.
/// </summary>
public static class CatalogAssets
{
    public const string CormorantGaramondMediumResource = "Assets/Fonts/CormorantGaramond-Medium.ttf";
    public const string ManropeRegularResource = "Assets/Fonts/Manrope-Regular.ttf";
    public const string CormorantGaramondLicenseResource = "Assets/Fonts/OFL-CormorantGaramond.txt";
    public const string ManropeLicenseResource = "Assets/Fonts/OFL-Manrope.txt";

    private static readonly Assembly Source = typeof(CatalogAssets).Assembly;
    private static readonly Lazy<SKTypeface> CormorantGaramondMediumFace = new(() => LoadTypeface(CormorantGaramondMediumResource));
    private static readonly Lazy<SKTypeface> ManropeRegularFace = new(() => LoadTypeface(ManropeRegularResource));
    private static readonly ConcurrentDictionary<string, Lazy<SKImage>> ArtworkImages = new(StringComparer.Ordinal);

    /// <summary>Cormorant Garamond Medium (500): the <c>FOTOHAVN</c> signature face.</summary>
    public static SKTypeface CormorantGaramondMedium => CormorantGaramondMediumFace.Value;

    /// <summary>Manrope Regular (400).</summary>
    public static SKTypeface ManropeRegular => ManropeRegularFace.Value;

    /// <summary>The embedded resource name of a Template's artwork, <c>Assets/Templates/&lt;templateId&gt;.png</c>.</summary>
    public static string ArtworkResource(string templateId)
    {
        ArgumentException.ThrowIfNullOrEmpty(templateId);
        return $"Assets/Templates/{templateId}.png";
    }

    /// <summary>The Template's artwork at its native 600×1800 size.</summary>
    /// <exception cref="ArgumentException">The Template has no artwork.</exception>
    public static SKImage Artwork(string templateId)
    {
        var resource = ArtworkResource(templateId);
        if (Source.GetManifestResourceInfo(resource) is null)
        {
            throw new ArgumentException($"The Template {templateId} has no artwork.", nameof(templateId));
        }

        return ArtworkImages.GetOrAdd(templateId, _ => new Lazy<SKImage>(() => LoadImage(resource))).Value;
    }

    /// <summary>Opens an embedded resource. The caller disposes the stream.</summary>
    /// <exception cref="ArgumentException">There is no resource of that name.</exception>
    public static Stream OpenResource(string resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return Source.GetManifestResourceStream(resource)
            ?? throw new ArgumentException($"No embedded resource {resource}.", nameof(resource));
    }

    private static byte[] ReadResource(string resource)
    {
        using var stream = OpenResource(resource);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    internal static SKImage DecodeImage(byte[] bytes, string name)
    {
        using var encoded = SKImage.FromEncodedData(bytes)
            ?? throw new InvalidOperationException($"The embedded image {name} can't be decoded.");
        return encoded.ToRasterImage(ensurePixelData: true)
            ?? throw new InvalidOperationException($"The embedded image {name} can't be decoded.");
    }

    internal static SKTypeface DecodeTypeface(byte[] bytes, string name)
    {
        using var data = SKData.CreateCopy(bytes);
        return SKTypeface.FromData(data)
            ?? throw new InvalidOperationException($"The embedded font {name} can't be loaded.");
    }

    private static SKImage LoadImage(string resource) => DecodeImage(ReadResource(resource), resource);

    private static SKTypeface LoadTypeface(string resource) => DecodeTypeface(ReadResource(resource), resource);
}
