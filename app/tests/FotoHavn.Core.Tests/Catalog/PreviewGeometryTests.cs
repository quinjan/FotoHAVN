using FotoHavn.Core.Catalog;

namespace FotoHavn.Core.Tests.Catalog;

public sealed class PreviewGeometryTests
{
    private static readonly TemplateDefinition Panorama = new(
        "panorama-four",
        "Panorama Four",
        "d",
        1800,
        600,
        [new(0, 0, 450, 600), new(450, 0, 450, 600), new(900, 0, 450, 600), new(1350, 0, 450, 600)],
        HasArtwork: false);

    private static readonly TemplateDefinition Strip = new(
        "signature-strip",
        "Signature Strip",
        "d",
        600,
        1800,
        [new(0, 0, 600, 450), new(0, 450, 600, 450)],
        HasArtwork: false);

    [Fact]
    public void A_landscape_template_turns_a_quarter_turn_and_its_leftmost_window_becomes_the_bottom_one()
    {
        var rotated = PreviewGeometry.Rotate(Panorama);

        Assert.Equal((600, 1800), (rotated.Width, rotated.Height));
        Assert.False(rotated.IsLandscape);
        Assert.Equal(
            [new WindowRect(0, 1350, 600, 450), new WindowRect(0, 900, 600, 450), new WindowRect(0, 450, 600, 450), new WindowRect(0, 0, 600, 450)],
            rotated.Windows);
        Assert.Equal((Panorama.Id, Panorama.Name, Panorama.HasArtwork, Panorama.WindowCount), (rotated.Id, rotated.Name, rotated.HasArtwork, rotated.WindowCount));
    }

    [Fact]
    public void The_original_template_is_not_changed()
    {
        _ = PreviewGeometry.Rotate(Panorama);

        Assert.Equal((1800, 600), (Panorama.Width, Panorama.Height));
        Assert.Equal(new WindowRect(0, 0, 450, 600), Panorama.Windows[0]);
    }

    [Fact]
    public void Portrait_templates_come_back_unchanged()
    {
        Assert.Same(Strip, PreviewGeometry.Rotate(Strip));
    }

    [Fact]
    public void A_square_template_is_not_landscape()
    {
        var square = Strip with { Width = 600, Height = 600 };

        Assert.False(square.IsLandscape);
        Assert.Same(square, PreviewGeometry.Rotate(square));
    }

    [Fact]
    public void A_template_is_required()
    {
        Assert.Throws<ArgumentNullException>(() => PreviewGeometry.Rotate(null!));
    }
}
