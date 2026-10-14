using FotoHavn.Core.Displays;
using FotoHavn.Host.Windows.Screens;

namespace FotoHavn.Host.Windows.Tests;

public sealed class IdentifyOverlayLayoutTests
{
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3840, 2160)]
    [InlineData(1280, 720)]
    [InlineData(1080, 1920)]
    public void The_panel_fits_its_display_and_is_sized_from_the_short_side(int width, int height)
    {
        var shortSide = Math.Min(width, height);

        var metrics = IdentifyOverlayLayout.For(new DisplayRect(0, 0, width, height));

        Assert.Equal((int)Math.Round(shortSide * IdentifyOverlayLayout.PanelWidthShare), metrics.PanelWidth);
        Assert.Equal((int)Math.Round(shortSide * IdentifyOverlayLayout.PanelHeightShare), metrics.PanelHeight);
        Assert.True(metrics.PanelWidth < width && metrics.PanelHeight < height);
        Assert.True(metrics.NumberFontSize > metrics.NameFontSize);
        Assert.True(metrics.NumberFontSize + metrics.NameFontSize + (2 * metrics.Padding) < metrics.PanelHeight, "The number, the name, and the padding fit the panel's height.");
        Assert.True(metrics.NameFontSize >= IdentifyOverlayLayout.MinimumNameFontSize);
    }

    [Fact]
    public void The_layout_scales_with_the_display_and_ignores_its_position()
    {
        var small = IdentifyOverlayLayout.For(new DisplayRect(0, 0, 1280, 720));
        var moved = IdentifyOverlayLayout.For(new DisplayRect(-1920, 400, 1280, 720));
        var large = IdentifyOverlayLayout.For(new DisplayRect(0, 0, 2560, 1440));

        Assert.Equal(small, moved);
        Assert.Equal(large.PanelWidth, small.PanelWidth * 2);
        Assert.True(large.NumberFontSize > small.NumberFontSize);
    }

    [Fact]
    public void A_tiny_or_empty_display_still_gets_a_drawable_panel()
    {
        foreach (var bounds in new[] { default, new DisplayRect(0, 0, 10, 10), new DisplayRect(0, 0, 0, 1080) })
        {
            var metrics = IdentifyOverlayLayout.For(bounds);

            Assert.True(metrics.PanelWidth >= 1);
            Assert.True(metrics.PanelHeight >= 1);
            Assert.True(metrics.Padding >= 4);
            Assert.True(metrics.NameFontSize >= IdentifyOverlayLayout.MinimumNameFontSize);
        }
    }
}
