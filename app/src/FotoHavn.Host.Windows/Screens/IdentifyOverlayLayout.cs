using FotoHavn.Core.Displays;

namespace FotoHavn.Host.Windows.Screens;

/// <summary>Sizes of an identify overlay's panel and type, in physical pixels.</summary>
/// <param name="PanelWidth">The Off-white panel's width.</param>
/// <param name="PanelHeight">The panel's height.</param>
/// <param name="Padding">The space between the panel's edge and its text.</param>
/// <param name="NumberFontSize">The display number (Display role), as a font size.</param>
/// <param name="NameFontSize">The display name (Meta role), as a font size.</param>
internal readonly record struct IdentifyOverlayMetrics(int PanelWidth, int PanelHeight, int Padding, double NumberFontSize, double NameFontSize);

/// <summary>
/// The overlay's proportions, from the display's own bounds so one design reads on a 1280 × 720 virtual display and a 4K screen
/// alike. Pure: the window converts physical pixels to device-independent pixels with its own DPI.
/// </summary>
internal static class IdentifyOverlayLayout
{
    public const double PanelWidthShare = 0.5;
    public const double PanelHeightShare = 0.42;
    public const double NumberShare = 0.24;
    public const double NameShare = 0.022;
    public const double MinimumNameFontSize = 14;

    public static IdentifyOverlayMetrics For(DisplayRect bounds)
    {
        var shortSide = Math.Max(1, Math.Min(bounds.Width, bounds.Height));
        var padding = Math.Max(4, (int)Math.Round(shortSide * 0.04));
        return new IdentifyOverlayMetrics(
            PanelWidth: Math.Max(1, (int)Math.Round(shortSide * PanelWidthShare)),
            PanelHeight: Math.Max(1, (int)Math.Round(shortSide * PanelHeightShare)),
            Padding: padding,
            NumberFontSize: Math.Round(shortSide * NumberShare, 1),
            NameFontSize: Math.Max(MinimumNameFontSize, Math.Round(shortSide * NameShare, 1)));
    }
}
