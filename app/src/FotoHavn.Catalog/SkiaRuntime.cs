using SkiaSharp;

namespace FotoHavn.Catalog;

/// <summary>
/// Placeholder until M7 brings the catalog and compositor. Proves the native SkiaSharp library loads.
/// </summary>
public static class SkiaRuntime
{
    public static Version NativeVersion => SkiaSharpVersion.Native;

    public static SKColor SamplePixel(SKColor fill)
    {
        using var bitmap = new SKBitmap(2, 2);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(fill);
        }

        return bitmap.GetPixel(1, 1);
    }
}
