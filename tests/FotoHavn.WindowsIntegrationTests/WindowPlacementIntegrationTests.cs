using FotoHavn.App;
using Windows.Graphics;
using Xunit;

namespace FotoHavn.WindowsIntegrationTests;

public sealed class WindowPlacementIntegrationTests
{
    [Fact]
    public void Standard_canvas_is_scaled_to_the_window_DPI_and_centered_in_the_display_work_area()
    {
        var placement = WindowPlacement.ForClientArea(
            effectiveClientWidth: 1280,
            effectiveClientHeight: 720,
            dpi: 120,
            nonClientWidth: 6,
            nonClientHeight: 6,
            workArea: new RectInt32(100, 50, 1920, 1080));

        Assert.Equal(new RectInt32(257, 137, 1606, 906), placement);
    }

    [Fact]
    public void Standard_canvas_remains_centered_at_one_hundred_percent_scaling()
    {
        var placement = WindowPlacement.ForClientArea(
            effectiveClientWidth: 1280,
            effectiveClientHeight: 720,
            dpi: 96,
            nonClientWidth: 6,
            nonClientHeight: 6,
            workArea: new RectInt32(0, 0, 1920, 1080));

        Assert.Equal(new RectInt32(317, 177, 1286, 726), placement);
    }
}
