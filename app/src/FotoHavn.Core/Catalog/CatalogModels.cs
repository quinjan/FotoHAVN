namespace FotoHavn.Core.Catalog;

// The catalog port (plan section 12, ADR 0006). The brain reads Templates and Looks only through ICatalog; FotoHavn.Catalog
// holds the data (a .NET copy of the website's presets.ts), and the brain never touches SkiaSharp.

/// <summary>A photograph window inside a Template, in the Template's native pixels.</summary>
public sealed record WindowRect(int X, int Y, int Width, int Height);

/// <summary>A Template: a paper size with photograph windows, and optionally artwork drawn over the paper.</summary>
/// <param name="Id">The website's Template ID (<c>signature-strip</c>, <c>panorama-four</c>, <c>butter-gingham</c>, <c>polka-keepsake</c>).</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">The one-line description.</param>
/// <param name="Width">The native paper width in pixels.</param>
/// <param name="Height">The native paper height in pixels.</param>
/// <param name="Windows">The photograph windows in reading order (the order Captures are arranged into them).</param>
/// <param name="HasArtwork">Whether the Template has artwork; otherwise it draws the <c>FOTOHAVN</c> signature.</param>
public sealed record TemplateDefinition(
    string Id,
    string Name,
    string Description,
    int Width,
    int Height,
    IReadOnlyList<WindowRect> Windows,
    bool HasArtwork)
{
    /// <summary>Whether the paper is wider than tall; the preview rotates a quarter turn (see <see cref="PreviewGeometry"/>).</summary>
    public bool IsLandscape => Width > Height;

    /// <summary>How many photographs the Template holds.</summary>
    public int WindowCount => Windows.Count;
}

/// <summary>A Look: a named colour treatment applied to photograph windows.</summary>
/// <param name="Id">The website's Look ID (<c>naturale-v1</c>, <c>monochrome-v1</c>, …).</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">The one-line description.</param>
/// <param name="Order">The position in the brand's Look order (<c>brand/photography.md</c>), starting at 0.</param>
public sealed record LookDefinition(string Id, string Name, string Description, int Order);

/// <summary>The Templates and Looks the app offers.</summary>
public interface ICatalog
{
    /// <summary>The Templates in the website's order: Signature Strip, Panorama Four, Butter Gingham, Polka Keepsake.</summary>
    IReadOnlyList<TemplateDefinition> Templates { get; }

    /// <summary>The Looks in the brand's order: Monochrome, Naturale, Soft Monochrome, Soft Color, Sepia, Warm Monochrome, Classic.</summary>
    IReadOnlyList<LookDefinition> Looks { get; }

    /// <summary>The Look an Event offers first and a new Event starts with: <c>naturale-v1</c>.</summary>
    string DefaultLookId { get; }

    /// <summary>The Template with this ID (ordinal match), or null.</summary>
    TemplateDefinition? FindTemplate(string id);

    /// <summary>The Look with this ID (ordinal match), or null.</summary>
    LookDefinition? FindLook(string id);
}
