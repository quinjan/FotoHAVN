using Microsoft.UI.Xaml;

namespace FotoHavn.App.Controls;

internal enum ResponsiveLayoutMode
{
    Standard,
    Compact,
    Stress,
}

internal static class ResponsiveLayout
{
    public static ResponsiveLayoutMode Resolve(double width, double height)
    {
        var resources = Application.Current.Resources;
        if (width >= (double)resources["ResponsiveStandardMinimumWidth"] &&
            height >= (double)resources["ResponsiveStandardMinimumHeight"])
        {
            return ResponsiveLayoutMode.Standard;
        }

        if (width >= (double)resources["ResponsiveCompactMinimumWidth"] &&
            height >= (double)resources["ResponsiveCompactMinimumHeight"])
        {
            return ResponsiveLayoutMode.Compact;
        }

        _ = resources["ResponsiveStressMinimumWidth"];
        _ = resources["ResponsiveStressMinimumHeight"];
        return ResponsiveLayoutMode.Stress;
    }
}
