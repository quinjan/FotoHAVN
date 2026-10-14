using System.Security.Cryptography;
using FotoHavn.Catalog;
using SkiaSharp;

namespace FotoHavn.Catalog.Tests;

public sealed class CatalogAssetsTests
{
    private static readonly string[] AllResources =
    [
        "Assets/Templates/butter-gingham.png",
        "Assets/Templates/polka-keepsake.png",
        CatalogAssets.CormorantGaramondMediumResource,
        CatalogAssets.ManropeRegularResource,
        CatalogAssets.CormorantGaramondLicenseResource,
        CatalogAssets.ManropeLicenseResource,
    ];

    [Fact]
    public void Every_resource_is_embedded_and_opens()
    {
        var embedded = typeof(CatalogAssets).Assembly.GetManifestResourceNames();

        foreach (var resource in AllResources)
        {
            Assert.Contains(resource, embedded);
            using var stream = CatalogAssets.OpenResource(resource);
            Assert.True(stream.Length > 0, resource);
        }
    }

    [Theory]
    [InlineData("butter-gingham")]
    [InlineData("polka-keepsake")]
    public void Artwork_loads_at_the_templates_native_size(string templateId)
    {
        var image = CatalogAssets.Artwork(templateId);

        Assert.Equal((600, 1800), (image.Width, image.Height));
        Assert.False(image.IsTextureBacked);
    }

    [Fact]
    public void Artwork_is_cached_and_safe_to_ask_for_from_many_threads()
    {
        var images = new SKImage[32];

        Parallel.For(0, images.Length, index => images[index] = CatalogAssets.Artwork(index % 2 == 0 ? "butter-gingham" : "polka-keepsake"));

        Assert.Single(images.Where((_, index) => index % 2 == 0).Distinct());
        Assert.Single(images.Where((_, index) => index % 2 == 1).Distinct());
        Assert.NotSame(CatalogAssets.Artwork("butter-gingham"), CatalogAssets.Artwork("polka-keepsake"));
    }

    [Fact]
    public void Artwork_can_be_drawn_after_the_image_was_handed_out()
    {
        using var bitmap = new SKBitmap(60, 180);
        using var canvas = new SKCanvas(bitmap);

        canvas.DrawImage(CatalogAssets.Artwork("polka-keepsake"), new SKRect(0, 0, 60, 180));

        Assert.NotEqual(SKColors.Transparent, bitmap.GetPixel(30, 90));
    }

    [Theory]
    [InlineData("signature-strip")]
    [InlineData("panorama-four")]
    [InlineData("nope")]
    public void A_template_without_artwork_is_an_error(string templateId)
    {
        Assert.Throws<ArgumentException>(() => CatalogAssets.Artwork(templateId));
    }

    [Fact]
    public void Resource_names_are_validated()
    {
        Assert.Equal("Assets/Templates/butter-gingham.png", CatalogAssets.ArtworkResource("butter-gingham"));
        Assert.Throws<ArgumentException>(() => CatalogAssets.ArtworkResource(string.Empty));
        Assert.Throws<ArgumentNullException>(() => CatalogAssets.ArtworkResource(null!));
        Assert.Throws<ArgumentNullException>(() => CatalogAssets.OpenResource(null!));
        Assert.Throws<ArgumentException>(() => CatalogAssets.OpenResource("Assets/nope.png"));
    }

    [Fact]
    public void Cormorant_garamond_medium_loads_from_the_resource_and_draws_the_signature_glyphs()
    {
        var face = CatalogAssets.CormorantGaramondMedium;

        Assert.StartsWith("Cormorant Garamond", face.FamilyName, StringComparison.Ordinal);
        Assert.Equal(500, face.FontWeight);
        AssertSignatureGlyphs(face);
        Assert.Same(face, CatalogAssets.CormorantGaramondMedium);
    }

    [Fact]
    public void Manrope_regular_loads_from_the_resource_and_draws_the_signature_glyphs()
    {
        var face = CatalogAssets.ManropeRegular;

        Assert.StartsWith("Manrope", face.FamilyName, StringComparison.Ordinal);
        Assert.Equal(400, face.FontWeight);
        AssertSignatureGlyphs(face);
        Assert.Same(face, CatalogAssets.ManropeRegular);
    }

    [Fact]
    public void The_embedded_fonts_are_the_files_recorded_in_the_sources_note()
    {
        Assert.Equal("f372d662a48554b94189673e28eb5a0cbd75bb0b4662df3a26d60efb46dd7073", Sha256(CatalogAssets.CormorantGaramondMediumResource));
        Assert.Equal("4eefc9018b1a6775d54c7c015f1067eaa1272628bc348b220d541c7e4a8ab38a", Sha256(CatalogAssets.ManropeRegularResource));
    }

    [Fact]
    public void The_font_licences_are_embedded_with_the_fonts()
    {
        foreach (var resource in new[] { CatalogAssets.CormorantGaramondLicenseResource, CatalogAssets.ManropeLicenseResource })
        {
            using var reader = new StreamReader(CatalogAssets.OpenResource(resource));
            Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", reader.ReadToEnd(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Undecodable_bytes_are_reported_not_hidden()
    {
        Assert.Throws<InvalidOperationException>(() => CatalogAssets.DecodeImage([1, 2, 3], "bad.png"));
        Assert.Throws<InvalidOperationException>(() => CatalogAssets.DecodeTypeface([1, 2, 3], "bad.ttf"));
    }

    private static void AssertSignatureGlyphs(SKTypeface face)
    {
        var glyphs = face.GetGlyphs("FOTOHAVN");

        Assert.Equal(8, glyphs.Length);
        Assert.All(glyphs, glyph => Assert.NotEqual(0, glyph));
    }

    private static string Sha256(string resource)
    {
        using var stream = CatalogAssets.OpenResource(resource);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
