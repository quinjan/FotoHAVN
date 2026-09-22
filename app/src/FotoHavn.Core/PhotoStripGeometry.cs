namespace FotoHavn.Core;

public static class PhotoStripGeometry
{
    public const int Width = 600;
    public const int Height = 1800;
    public const double AspectRatio = (double)Width / Height;

    public static double WidthForHeight(double height) => height * AspectRatio;
}
