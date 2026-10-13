using SkiaSharp;

namespace FotoHavn.Catalog.Tests;

public sealed class SkiaRuntimeTests
{
    [Fact]
    public void Native_skia_library_loads()
    {
        Assert.True(SkiaRuntime.NativeVersion.Major > 0);
    }

    [Fact]
    public void Draws_into_a_bitmap()
    {
        Assert.Equal(SKColors.Red, SkiaRuntime.SamplePixel(SKColors.Red));
    }
}
