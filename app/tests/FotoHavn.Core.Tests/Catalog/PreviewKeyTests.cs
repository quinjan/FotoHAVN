using FotoHavn.Catalog;
using FotoHavn.Core.Catalog;

namespace FotoHavn.Core.Tests.Catalog;

public sealed class PreviewKeyTests
{
    [Fact]
    public void The_key_is_template_look_and_revision()
    {
        Assert.Equal("signature-strip.naturale-v1.r42", PreviewKey.For("signature-strip", "naturale-v1", 42));
        Assert.Equal("panorama-four.sepia-v1.r0", PreviewKey.For("panorama-four", "sepia-v1", 0));
        Assert.Equal($"butter-gingham.classic-v1.r{long.MaxValue}", PreviewKey.For("butter-gingham", "classic-v1", long.MaxValue));
    }

    [Fact]
    public void The_key_is_deterministic_and_distinguishes_each_part()
    {
        var key = PreviewKey.For("signature-strip", "naturale-v1", 3);

        Assert.Equal(key, PreviewKey.For("signature-strip", "naturale-v1", 3));
        Assert.NotEqual(key, PreviewKey.For("panorama-four", "naturale-v1", 3));
        Assert.NotEqual(key, PreviewKey.For("signature-strip", "sepia-v1", 3));
        Assert.NotEqual(key, PreviewKey.For("signature-strip", "naturale-v1", 4));
    }

    [Fact]
    public void Every_template_and_look_makes_a_valid_single_lowercase_segment_the_preview_route_accepts()
    {
        var catalog = new CatalogData();
        foreach (var template in catalog.Templates)
        {
            foreach (var look in catalog.Looks)
            {
                var key = PreviewKey.For(template.Id, look.Id, 123_456);

                Assert.Matches("^[a-z0-9.-]+$", key);
                Assert.DoesNotContain('/', key);
                Assert.True(PreviewKey.IsValid(key));
                Assert.True(Uri.TryCreate($"https://preview.fotohavn/{key}.png", UriKind.Absolute, out var uri));
                Assert.Equal($"/{key}.png", uri.AbsolutePath);
            }
        }
    }

    [Theory]
    [InlineData("", "naturale-v1")]
    [InlineData("Signature-Strip", "naturale-v1")]
    [InlineData("signature strip", "naturale-v1")]
    [InlineData("signature/strip", "naturale-v1")]
    [InlineData("signature-strip", "")]
    [InlineData("signature-strip", "naturale_v1")]
    [InlineData("signature-strip", "naturale.v1")]
    public void An_id_outside_lowercase_letters_digits_and_dash_is_an_error(string templateId, string lookId)
    {
        Assert.Throws<ArgumentException>(() => PreviewKey.For(templateId, lookId, 1));
    }

    [Fact]
    public void Null_ids_and_a_negative_revision_are_errors()
    {
        Assert.Throws<ArgumentNullException>(() => PreviewKey.For(null!, "naturale-v1", 1));
        Assert.Throws<ArgumentNullException>(() => PreviewKey.For("signature-strip", null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PreviewKey.For("signature-strip", "naturale-v1", -1));
    }

    [Theory]
    [InlineData("signature-strip.naturale-v1.r0")]
    [InlineData("a.b.r12")]
    [InlineData("polka-keepsake.soft-color-v1.r9223372036854775807")]
    public void A_key_the_builder_makes_is_valid(string key)
    {
        Assert.True(PreviewKey.IsValid(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("strip-preview")]
    [InlineData("a.b")]
    [InlineData("a.b.c.r1")]
    [InlineData(".b.r1")]
    [InlineData("a..r1")]
    [InlineData("a.b.r")]
    [InlineData("a.b.1")]
    [InlineData("a.b.rx")]
    [InlineData("a.b.r-1")]
    [InlineData("a.b.r+1")]
    [InlineData("A.b.r1")]
    [InlineData("a.b.R1")]
    [InlineData("a/b.c.r1")]
    [InlineData("a.b.r9223372036854775808")]
    [InlineData("a.b.r1 ")]
    public void Any_other_key_is_not_valid(string? key)
    {
        Assert.False(PreviewKey.IsValid(key));
    }
}
