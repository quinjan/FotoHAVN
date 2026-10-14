using FotoHavn.Core.Catalog;

namespace FotoHavn.Catalog;

/// <summary>
/// The Templates and Looks, a .NET copy of the website's <c>presets.ts</c> (D8): the same IDs, names, descriptions, and
/// geometry, so the same Template means the same thing everywhere. The website stays unchanged and no test compares them.
/// </summary>
public sealed class CatalogData : ICatalog
{
    public const string SignatureStripId = "signature-strip";
    public const string PanoramaFourId = "panorama-four";
    public const string ButterGinghamId = "butter-gingham";
    public const string PolkaKeepsakeId = "polka-keepsake";

    public const string MonochromeId = "monochrome-v1";
    public const string NaturaleId = "naturale-v1";
    public const string SoftMonochromeId = "soft-monochrome-v1";
    public const string SoftColorId = "soft-color-v1";
    public const string SepiaId = "sepia-v1";
    public const string WarmMonochromeId = "warm-monochrome-v1";
    public const string ClassicId = "classic-v1";

    private static readonly TemplateDefinition[] AllTemplates =
    [
        new(
            SignatureStripId,
            "Signature Strip",
            "Four edge-to-edge photographs with a signature across the middle.",
            600,
            1800,
            [.. Enumerable.Range(0, 4).Select(index => new WindowRect(0, index * 450, 600, 450))],
            HasArtwork: false),
        new(
            PanoramaFourId,
            "Panorama Four",
            "Four portraits side by side, with a quiet corner signature.",
            1800,
            600,
            [.. Enumerable.Range(0, 4).Select(index => new WindowRect(index * 450, 0, 450, 600))],
            HasArtwork: false),
        new(
            ButterGinghamId,
            "Butter Gingham",
            "Three photographs framed in soft yellow checks.",
            600,
            1800,
            [.. new[] { 32, 573, 1113 }.Select(y => new WindowRect(27, y, 547, 522))],
            HasArtwork: true),
        new(
            PolkaKeepsakeId,
            "Polka Keepsake",
            "Four photographs bordered by playful black dots.",
            600,
            1800,
            [.. new[] { 39, 441, 846, 1251 }.Select(y => new WindowRect(42, y, 513, 390))],
            HasArtwork: true),
    ];

    private static readonly LookDefinition[] AllLooks =
    [
        new(MonochromeId, "Monochrome", "Black and white with deep blacks and strong contrast.", 0),
        new(NaturaleId, "Naturale", "Original colors. No filter.", 1),
        new(SoftMonochromeId, "Soft Monochrome", "Black and white with softer contrast.", 2),
        new(SoftColorId, "Soft Color", "Slightly muted colors and softer highlights.", 3),
        new(SepiaId, "Sepia", "Soft brown tones with faded contrast.", 4),
        new(WarmMonochromeId, "Warm Monochrome", "Black and white with a light warm tint.", 5),
        new(ClassicId, "Classic", "Warm colors with a golden tint.", 6),
    ];

    public IReadOnlyList<TemplateDefinition> Templates => AllTemplates;

    public IReadOnlyList<LookDefinition> Looks => AllLooks;

    public string DefaultLookId => NaturaleId;

    public TemplateDefinition? FindTemplate(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Array.Find(AllTemplates, template => string.Equals(template.Id, id, StringComparison.Ordinal));
    }

    public LookDefinition? FindLook(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Array.Find(AllLooks, look => string.Equals(look.Id, id, StringComparison.Ordinal));
    }
}
