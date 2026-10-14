using FotoHavn.Core.Catalog;

namespace FotoHavn.Catalog.Tests;

// See GoldenStore for the update procedure (FOTOHAVN_UPDATE_GOLDEN=1).
public sealed class CompositionGoldenTests
{
    private static readonly string[] TemplateIds = [CatalogData.SignatureStripId, CatalogData.PanoramaFourId, CatalogData.ButterGinghamId, CatalogData.PolkaKeepsakeId];

    public static TheoryData<string, string> TemplatesWithLooks
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var template in TemplateIds)
            {
                foreach (var look in new CatalogData().Looks)
                {
                    data.Add(template, look.Id);
                }
            }

            return data;
        }
    }

    public static TheoryData<string> Templates => [.. TemplateIds];

    [Theory]
    [MemberData(nameof(TemplatesWithLooks))]
    public void Strip_matches_its_golden_pixels(string templateId, string lookId)
    {
        var actual = PixelImage.Decode(GoldenStore.ComposeFull(templateId, lookId)).Hash();

        Assert.Equal(GoldenStore.Expected(GoldenStore.FullKey(templateId, lookId)), actual);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void Blank_preview_matches_its_golden_pixels(string templateId)
    {
        var actual = PixelImage.Decode(GoldenStore.ComposeBlank(templateId)).Hash();

        Assert.Equal(GoldenStore.Expected(GoldenStore.BlankKey(templateId)), actual);
    }

    [Fact]
    public void The_golden_file_has_every_case_and_nothing_else()
    {
        var expected = TemplateIds.SelectMany(t => new CatalogData().Looks.Select(l => GoldenStore.FullKey(t, l.Id)).Append(GoldenStore.BlankKey(t))).Order(StringComparer.Ordinal);
        var stored = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(GoldenStore.HashesPath))!;

        Assert.Equal(expected, stored.Keys.Order(StringComparer.Ordinal));
        Assert.All(stored.Values, hash => Assert.Matches("^[0-9a-f]{64}$", hash));
        Assert.Equal(32, stored.Count);
    }

    [Fact]
    public void Composing_twice_gives_identical_pixels()
    {
        var first = PixelImage.Decode(GoldenStore.ComposeFull(CatalogData.PolkaKeepsakeId, CatalogData.ClassicId)).Hash();
        var second = PixelImage.Decode(GoldenStore.ComposeFull(CatalogData.PolkaKeepsakeId, CatalogData.ClassicId)).Hash();

        Assert.Equal(first, second);
    }
}
