using FotoHavn.Catalog;
using FotoHavn.Core.Catalog;

namespace FotoHavn.Catalog.Tests;

public sealed class CatalogDataTests
{
    private readonly CatalogData _catalog = new();

    [Fact]
    public void Templates_use_the_websites_ids_in_the_websites_order()
    {
        Assert.Equal(["signature-strip", "panorama-four", "butter-gingham", "polka-keepsake"], _catalog.Templates.Select(template => template.Id));
        Assert.Equal(["Signature Strip", "Panorama Four", "Butter Gingham", "Polka Keepsake"], _catalog.Templates.Select(template => template.Name));
    }

    [Fact]
    public void Looks_use_the_websites_ids_in_the_brand_order()
    {
        Assert.Equal(
            ["monochrome-v1", "naturale-v1", "soft-monochrome-v1", "soft-color-v1", "sepia-v1", "warm-monochrome-v1", "classic-v1"],
            _catalog.Looks.Select(look => look.Id));
        Assert.Equal(
            ["Monochrome", "Naturale", "Soft Monochrome", "Soft Color", "Sepia", "Warm Monochrome", "Classic"],
            _catalog.Looks.Select(look => look.Name));
        Assert.Equal(Enumerable.Range(0, 7), _catalog.Looks.Select(look => look.Order));
        Assert.Equal("naturale-v1", _catalog.DefaultLookId);
        Assert.NotNull(_catalog.FindLook(_catalog.DefaultLookId));
    }

    [Fact]
    public void Descriptions_are_the_websites_verbatim()
    {
        Assert.Equal(
            [
                "Four edge-to-edge photographs with a signature across the middle.",
                "Four portraits side by side, with a quiet corner signature.",
                "Three photographs framed in soft yellow checks.",
                "Four photographs bordered by playful black dots.",
            ],
            _catalog.Templates.Select(template => template.Description));
        Assert.Equal(
            [
                "Black and white with deep blacks and strong contrast.",
                "Original colors. No filter.",
                "Black and white with softer contrast.",
                "Slightly muted colors and softer highlights.",
                "Soft brown tones with faded contrast.",
                "Black and white with a light warm tint.",
                "Warm colors with a golden tint.",
            ],
            _catalog.Looks.Select(look => look.Description));
    }

    [Fact]
    public void Signature_strip_is_four_edge_to_edge_windows()
    {
        var template = _catalog.FindTemplate("signature-strip")!;

        Assert.Equal((600, 1800, false, false, 4), (template.Width, template.Height, template.HasArtwork, template.IsLandscape, template.WindowCount));
        Assert.Equal(
            [new WindowRect(0, 0, 600, 450), new WindowRect(0, 450, 600, 450), new WindowRect(0, 900, 600, 450), new WindowRect(0, 1350, 600, 450)],
            template.Windows);
    }

    [Fact]
    public void Panorama_four_is_landscape_with_four_side_by_side_windows()
    {
        var template = _catalog.FindTemplate("panorama-four")!;

        Assert.Equal((1800, 600, false, true, 4), (template.Width, template.Height, template.HasArtwork, template.IsLandscape, template.WindowCount));
        Assert.Equal(
            [new WindowRect(0, 0, 450, 600), new WindowRect(450, 0, 450, 600), new WindowRect(900, 0, 450, 600), new WindowRect(1350, 0, 450, 600)],
            template.Windows);
    }

    [Fact]
    public void Butter_gingham_has_artwork_and_three_windows()
    {
        var template = _catalog.FindTemplate("butter-gingham")!;

        Assert.Equal((600, 1800, true, 3), (template.Width, template.Height, template.HasArtwork, template.WindowCount));
        Assert.Equal([new WindowRect(27, 32, 547, 522), new WindowRect(27, 573, 547, 522), new WindowRect(27, 1113, 547, 522)], template.Windows);
    }

    [Fact]
    public void Polka_keepsake_has_artwork_and_four_windows()
    {
        var template = _catalog.FindTemplate("polka-keepsake")!;

        Assert.Equal((600, 1800, true, 4), (template.Width, template.Height, template.HasArtwork, template.WindowCount));
        Assert.Equal(
            [new WindowRect(42, 39, 513, 390), new WindowRect(42, 441, 513, 390), new WindowRect(42, 846, 513, 390), new WindowRect(42, 1251, 513, 390)],
            template.Windows);
    }

    [Fact]
    public void Every_window_lies_inside_its_paper_and_windows_never_overlap()
    {
        foreach (var template in _catalog.Templates)
        {
            foreach (var window in template.Windows)
            {
                Assert.InRange(window.X, 0, template.Width - window.Width);
                Assert.InRange(window.Y, 0, template.Height - window.Height);
            }

            foreach (var (first, second) in template.Windows.Zip(template.Windows.Skip(1)))
            {
                var apart = first.X + first.Width <= second.X || second.X + second.Width <= first.X
                    || first.Y + first.Height <= second.Y || second.Y + second.Height <= first.Y;
                Assert.True(apart, $"{template.Id} has overlapping windows.");
            }
        }
    }

    [Fact]
    public void Templates_with_artwork_are_exactly_the_ones_with_an_embedded_image()
    {
        foreach (var template in _catalog.Templates)
        {
            Assert.Equal(template.HasArtwork, typeof(CatalogData).Assembly.GetManifestResourceInfo(CatalogAssets.ArtworkResource(template.Id)) is not null);
        }
    }

    [Fact]
    public void Lookups_match_ids_exactly()
    {
        Assert.Same(_catalog.Templates[2], _catalog.FindTemplate("butter-gingham"));
        Assert.Same(_catalog.Looks[4], _catalog.FindLook("sepia-v1"));
        Assert.Null(_catalog.FindTemplate("Butter-Gingham"));
        Assert.Null(_catalog.FindTemplate("classic-strip"));
        Assert.Null(_catalog.FindLook("naturale"));
        Assert.Null(_catalog.FindLook(string.Empty));
        Assert.Throws<ArgumentNullException>(() => _catalog.FindTemplate(null!));
        Assert.Throws<ArgumentNullException>(() => _catalog.FindLook(null!));
    }

    [Fact]
    public void Every_look_has_a_kernel()
    {
        Assert.All(_catalog.Looks, look => Assert.True(LookKernels.IsKnown(look.Id)));
    }
}
