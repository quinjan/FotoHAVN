using System.Globalization;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using FotoHavn.Catalog;

namespace FotoHavn.Host.Windows.Screens;

/// <summary>One brand typeface for WPF: the family and the face's own weight, style, and stretch.</summary>
internal sealed record OverlayTypeface(FontFamily Family, FontWeight Weight, FontStyle Style, FontStretch Stretch);

/// <summary>
/// The overlays' brand fonts. WPF only loads a font family from a file or pack location, so the two TTFs embedded in
/// <c>FotoHavn.Catalog</c> (<see cref="CatalogAssets"/>; the same files the compositor uses) are written once to a folder named by
/// their content hash, and the families are read from there. The system fonts are never used.
/// </summary>
internal sealed class OverlayFonts
{
    private OverlayFonts(OverlayTypeface display, OverlayTypeface meta)
    {
        Display = display;
        Meta = meta;
    }

    /// <summary>Cormorant Garamond Medium: the Display role.</summary>
    public OverlayTypeface Display { get; }

    /// <summary>Manrope Regular: the Meta role.</summary>
    public OverlayTypeface Meta { get; }

    /// <summary>The default place for the extracted fonts.</summary>
    public static string DefaultFolder => Path.Combine(Path.GetTempPath(), "FOTOHAVN", "fonts");

    /// <summary>Extracts the embedded fonts under <paramref name="folder"/> (nothing is rewritten when they are already there) and loads them.</summary>
    public static OverlayFonts Load(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var display = ReadResource(CatalogAssets.CormorantGaramondMediumResource);
        var meta = ReadResource(CatalogAssets.ManropeRegularResource);
        var directory = Path.Combine(Path.GetFullPath(folder), Convert.ToHexStringLower(SHA256.HashData(display.Bytes.Concat(meta.Bytes).ToArray()))[..16]);
        Directory.CreateDirectory(directory);
        // Both files go in before WPF reads either: it indexes a font folder when it first opens a file in it.
        var displayPath = Place(directory, display);
        var metaPath = Place(directory, meta);
        return new OverlayFonts(Typeface(displayPath), Typeface(metaPath));
    }

    private static (string Name, byte[] Bytes) ReadResource(string resource)
    {
        using var stream = CatalogAssets.OpenResource(resource);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return (Path.GetFileName(resource), copy.ToArray());
    }

    private static string Place(string directory, (string Name, byte[] Bytes) font)
    {
        var path = Path.Combine(directory, font.Name);
        if (File.Exists(path) && new FileInfo(path).Length == font.Bytes.Length)
        {
            return path;
        }

        // Write beside the target and move it in, so a half-written font is never read; another process may win the race.
        var partial = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.partial";
        File.WriteAllBytes(partial, font.Bytes);
        try
        {
            File.Move(partial, path, overwrite: true);
        }
        catch (IOException) when (File.Exists(path) && new FileInfo(path).Length == font.Bytes.Length)
        {
            File.Delete(partial);
        }

        return path;
    }

    private static OverlayTypeface Typeface(string path)
    {
        var glyphs = new GlyphTypeface(new Uri(path, UriKind.Absolute));
        var family = glyphs.FamilyNames.TryGetValue(CultureInfo.GetCultureInfo("en-US"), out var name) ? name : glyphs.FamilyNames.Values.First();
        var folder = new Uri(Path.GetDirectoryName(path)! + Path.DirectorySeparatorChar, UriKind.Absolute);
        return new OverlayTypeface(new FontFamily(folder, $"./#{family}"), glyphs.Weight, glyphs.Style, glyphs.Stretch);
    }
}
