using FotoHavn.Core.Catalog;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace FotoHavn.Catalog.Tests;

public sealed class SkiaCompositorTests
{
    private static readonly SkiaCompositor Compositor = new();

    private static CompositionPhoto.Encoded Photo(byte[] bytes) => new CompositionPhoto.Encoded(bytes);

    private static PixelImage Compose(string templateId, string lookId, params CompositionPhoto?[] photos) =>
        PixelImage.Decode(Run(templateId, lookId, photos).Png);

    private static CompositionResult Run(string templateId, string lookId, CompositionPhoto?[] photos) =>
        Compositor.ComposeAsync(new CompositionRequest(templateId, lookId, photos), TestContext.Current.CancellationToken).GetAwaiter().GetResult();

    private static WindowRect? SignatureBox(string templateId) => templateId switch
    {
        CatalogData.SignatureStripId => new WindowRect(60, 840, 480, 120),
        CatalogData.PanoramaFourId => new WindowRect(1450, 500, 350, 100),
        _ => null,
    };

    private static PixelImage ComposeWith(SkiaCompositor compositor, string lookId, CompositionPhoto?[] photos) =>
        PixelImage.Decode(compositor.ComposeAsync(new CompositionRequest(TestCatalog.PlainId, lookId, photos), TestContext.Current.CancellationToken).GetAwaiter().GetResult().Png);

    private static CompositionPhoto?[] Same(int count, byte[] png) => [.. Enumerable.Repeat<CompositionPhoto?>(Photo(png), count)];

    private static void AssertColor((byte R, byte G, byte B, byte A) actual, byte r, byte g, byte b, int tolerance = 2)
    {
        Assert.InRange(actual.R, Math.Max(0, r - tolerance), Math.Min(255, r + tolerance));
        Assert.InRange(actual.G, Math.Max(0, g - tolerance), Math.Min(255, g + tolerance));
        Assert.InRange(actual.B, Math.Max(0, b - tolerance), Math.Min(255, b + tolerance));
        Assert.Equal(255, actual.A);
    }

    private static (int MinX, int MinY, int MaxX, int MaxY)? WhiteBounds(PixelImage image, int left, int top, int right, int bottom)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var (r, g, b, _) = image.At(x, y);
                if (r >= 0xF8 && g >= 0xF8 && b >= 0xF8)
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        return maxX < 0 ? null : (minX, minY, maxX, maxY);
    }

    public static TheoryData<string> AllTemplates => [.. new CatalogData().Templates.Select(t => t.Id)];

    public static TheoryData<string> ChangingLooks => [.. new CatalogData().Looks.Where(l => l.Id != CatalogData.NaturaleId).Select(l => l.Id)];

    // ---- Output shape ----

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Output_is_an_opaque_png_at_the_native_size_and_orientation(string templateId)
    {
        var template = new CatalogData().FindTemplate(templateId)!;
        var result = Run(templateId, CatalogData.NaturaleId, [.. Enumerable.Range(0, template.WindowCount).Select(FixturePhotos.ForWindow)]);

        Assert.Equal((template.Width, template.Height), (result.Width, result.Height));
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], result.Png.Span[..4].ToArray());
        var image = PixelImage.Decode(result.Png);
        Assert.Equal((template.Width, template.Height), (image.Width, image.Height));
        for (var i = 3; i < image.Bgra.Length; i += 4)
        {
            Assert.Equal(255, image.Bgra[i]);
        }
    }

    [Fact]
    public void Panorama_stays_1800_by_600_with_window_zero_at_the_left()
    {
        var photos = new CompositionPhoto?[]
        {
            Photo(FixturePhotos.Solid(255, 0, 0)),
            Photo(FixturePhotos.Solid(0, 255, 0)),
            Photo(FixturePhotos.Solid(0, 0, 255)),
            Photo(FixturePhotos.Solid(255, 255, 0)),
        };

        var image = Compose(CatalogData.PanoramaFourId, CatalogData.NaturaleId, photos);

        Assert.Equal((1800, 600), (image.Width, image.Height));
        AssertColor(image.At(0, 10), 255, 0, 0);
        AssertColor(image.At(449, 10), 255, 0, 0);
        AssertColor(image.At(450, 10), 0, 255, 0);
        AssertColor(image.At(899, 10), 0, 255, 0);
        AssertColor(image.At(900, 10), 0, 0, 255);
        AssertColor(image.At(1350, 10), 255, 255, 0);
        AssertColor(image.At(1799, 10), 255, 255, 0);
    }

    [Fact]
    public void Windows_are_placed_in_order_down_a_portrait_strip()
    {
        var colors = new (byte R, byte G, byte B)[] { (255, 0, 0), (0, 255, 0), (0, 0, 255), (255, 255, 0) };
        var photos = colors.Select(c => (CompositionPhoto?)Photo(FixturePhotos.Solid(c.R, c.G, c.B))).ToArray();

        var image = Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, photos);

        for (var i = 0; i < 4; i++)
        {
            AssertColor(image.At(10, (i * 450) + 5), colors[i].R, colors[i].G, colors[i].B);
            AssertColor(image.At(590, (i * 450) + 445), colors[i].R, colors[i].G, colors[i].B);
        }
    }

    // ---- Artwork, paper, empty windows ----

    [Fact]
    public void Artwork_templates_draw_the_artwork_and_leave_empty_windows_blank()
    {
        var image = Compose(CatalogData.ButterGinghamId, CatalogData.NaturaleId, null, null, null);

        AssertColor(image.At(100, 100), 0xE5, 0xE0, 0xD7, 0);
        AssertColor(image.At(300, 700), 0xE5, 0xE0, 0xD7, 0);
        var (r, g, b, _) = image.At(3, 3);
        Assert.False(r == 0xE5 && g == 0xE0 && b == 0xD7);
    }

    [Fact]
    public void Artwork_is_drawn_unscaled_under_the_windows()
    {
        using var artwork = SKBitmap.Decode(SKData.Create(CatalogAssets.OpenResource(CatalogAssets.ArtworkResource(CatalogData.PolkaKeepsakeId))));
        var image = Compose(CatalogData.PolkaKeepsakeId, CatalogData.MonochromeId, null, null, null, null);

        foreach (var (x, y) in new[] { (3, 3), (300, 20), (20, 600), (595, 1790), (300, 1780) })
        {
            var expected = artwork.GetPixel(x, y);
            AssertColor(image.At(x, y), expected.Red, expected.Green, expected.Blue, 0);
        }
    }

    // ---- Look confinement and identity ----

    [Theory]
    [MemberData(nameof(ChangingLooks))]
    public void The_look_touches_only_the_photograph_windows(string lookId)
    {
        foreach (var templateId in new[] { CatalogData.ButterGinghamId, CatalogData.PolkaKeepsakeId, CatalogData.SignatureStripId, CatalogData.PanoramaFourId })
        {
            var template = new CatalogData().FindTemplate(templateId)!;
            var photos = Enumerable.Range(0, template.WindowCount).Select(i => (CompositionPhoto?)FixturePhotos.ForWindow(i)).ToArray();
            var natural = Compose(templateId, CatalogData.NaturaleId, photos);
            var looked = Compose(templateId, lookId, photos);

            // Expected: the naturale pixels of every window, run through the kernel; everything else byte-identical.
            var expected = Compose(templateId, CatalogData.NaturaleId, photos);
            foreach (var window in template.Windows)
            {
                var region = natural.Region(window.X, window.Y, window.Width, window.Height);
                LookKernels.Apply(region, lookId);
                expected.SetRegion(window.X, window.Y, window.Width, window.Height, region);
            }

            // The white signature is drawn after the Look, so it is the one thing in the windows the kernel never reaches.
            if (SignatureBox(templateId) is { } box)
            {
                expected.SetRegion(box.X, box.Y, box.Width, box.Height, natural.Region(box.X, box.Y, box.Width, box.Height));
                looked.SetRegion(box.X, box.Y, box.Width, box.Height, natural.Region(box.X, box.Y, box.Width, box.Height));
            }

            Assert.Equal(expected.Bgra, looked.Bgra);
            Assert.NotEqual(natural.Bgra, looked.Bgra);
        }
    }

    [Fact]
    public void Look_on_butter_gingham_leaves_artwork_outside_the_windows_byte_identical()
    {
        var photos = Enumerable.Range(0, 3).Select(i => (CompositionPhoto?)FixturePhotos.ForWindow(i)).ToArray();
        var natural = Compose(CatalogData.ButterGinghamId, CatalogData.NaturaleId, photos);
        var mono = Compose(CatalogData.ButterGinghamId, CatalogData.MonochromeId, photos);
        var windows = new CatalogData().FindTemplate(CatalogData.ButterGinghamId)!.Windows;

        var outside = 0;
        for (var y = 0; y < natural.Height; y++)
        {
            for (var x = 0; x < natural.Width; x++)
            {
                if (windows.Any(w => x >= w.X && x < w.X + w.Width && y >= w.Y && y < w.Y + w.Height))
                {
                    continue;
                }

                outside++;
                Assert.Equal(natural.At(x, y), mono.At(x, y));
            }
        }

        Assert.True(outside > 50_000);
    }

    [Fact]
    public void Look_leaves_paper_and_the_naturale_pixels_identical_with_no_artwork()
    {
        var catalog = new TestCatalog();
        var compositor = new SkiaCompositor(catalog);
        var photos = new CompositionPhoto?[] { Photo(FixturePhotos.Landscape) };

        var natural = ComposeWith(compositor, CatalogData.NaturaleId, photos);
        var mono = ComposeWith(compositor, CatalogData.MonochromeId, photos);

        // Paper is Warm Ivory #F3EBDD everywhere outside the window, in both.
        foreach (var (x, y) in new[] { (0, 0), (399, 299), (99, 100), (301, 100), (200, 49), (200, 151) })
        {
            AssertColor(natural.At(x, y), 0xF3, 0xEB, 0xDD, 0);
            AssertColor(mono.At(x, y), 0xF3, 0xEB, 0xDD, 0);
        }

        Assert.NotEqual(natural.Region(100, 50, 200, 100), mono.Region(100, 50, 200, 100));
    }

    [Fact]
    public void Naturale_equals_a_plain_cover_crop_draw_with_no_kernel()
    {
        var compositor = new SkiaCompositor(new TestCatalog());
        var composed = ComposeWith(compositor, CatalogData.NaturaleId, [Photo(FixturePhotos.FourThree)]);

        // The same draw by hand: crop 1600x1200 to 2:1, 1600x800 from y=200, into 200x100 (an 8x downscale uses trilinear mips).
        using var source = SKImage.FromEncodedData(FixturePhotos.FourThree);
        using var reference = new SKBitmap(new SKImageInfo(200, 100, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(reference))
        {
            canvas.Clear(new SKColor(0xE5, 0xE0, 0xD7));
            canvas.DrawImage(source, new SKRect(0, 200, 1600, 1000), new SKRect(0, 0, 200, 100), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        using var image = SKImage.FromBitmap(reference);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var expected = PixelImage.Decode(data.ToArray());
        Assert.Equal(expected.Bgra, composed.Region(100, 50, 200, 100));
    }

    // ---- Hairlines ----

    [Fact]
    public void Blank_signature_strip_has_hairlines_between_the_empty_windows()
    {
        var image = Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, null, null, null, null);

        foreach (var y in new[] { 450, 900, 1350 })
        {
            foreach (var row in new[] { y - 1, y })
            {
                var (r, g, b, _) = image.At(20, row);
                Assert.InRange((int)r, 0xE6, 0xEE);
                Assert.InRange((int)g, 0xE1, 0xEA);
                Assert.InRange((int)b, 0xD8, 0xE3);
            }

            AssertColor(image.At(580, y + 3), 0xE5, 0xE0, 0xD7, 0);
        }

        AssertColor(image.At(20, 200), 0xE5, 0xE0, 0xD7, 0);
    }

    [Fact]
    public void Blank_panorama_has_vertical_hairlines()
    {
        var image = Compose(CatalogData.PanoramaFourId, CatalogData.NaturaleId, null, null, null, null);

        foreach (var x in new[] { 450, 900, 1350 })
        {
            var (r, _, _, _) = image.At(x, 20);
            Assert.InRange((int)r, 0xE6, 0xEE);
            AssertColor(image.At(x + 3, 20), 0xE5, 0xE0, 0xD7, 0);
        }
    }

    [Fact]
    public void Hairlines_are_never_drawn_beside_a_photograph()
    {
        var image = Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, Same(4, FixturePhotos.Solid(0x33, 0x66, 0x99)));

        foreach (var y in new[] { 449, 450, 899, 900, 1349, 1350 })
        {
            AssertColor(image.At(20, y), 0x33, 0x66, 0x99, 0);
        }
    }

    [Fact]
    public void In_a_mix_a_hairline_appears_only_between_two_empty_windows()
    {
        var photo = Photo(FixturePhotos.Solid(0x33, 0x66, 0x99));

        var image = Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, null, null, photo, null);

        var (r, _, _, _) = image.At(20, 450);
        Assert.InRange((int)r, 0xE6, 0xEE);
        AssertColor(image.At(20, 899), 0xE5, 0xE0, 0xD7, 0);
        AssertColor(image.At(20, 900), 0x33, 0x66, 0x99, 0);
        AssertColor(image.At(20, 1349), 0x33, 0x66, 0x99, 0);
        AssertColor(image.At(20, 1350), 0xE5, 0xE0, 0xD7, 0);
    }

    [Fact]
    public void Artwork_templates_never_get_hairlines_or_a_signature()
    {
        var blank = Compose(CatalogData.PolkaKeepsakeId, CatalogData.NaturaleId, null, null, null, null);

        // The artwork's blank windows stay uniform, with no line at the shared edge and no white text.
        foreach (var window in new CatalogData().FindTemplate(CatalogData.PolkaKeepsakeId)!.Windows)
        {
            Assert.Null(WhiteBounds(blank, window.X, window.Y, window.X + window.Width, window.Y + window.Height));
            AssertColor(blank.At(window.X + 5, window.Y), 0xE5, 0xE0, 0xD7, 0);
            AssertColor(blank.At(window.X + 5, window.Y + (window.Height / 2)), 0xE5, 0xE0, 0xD7, 0);
        }
    }

    // ---- Signature ----

    [Fact]
    public void Signature_comes_from_the_embedded_typeface()
    {
        Assert.StartsWith("Cormorant Garamond", CatalogAssets.CormorantGaramondMedium.FamilyName, StringComparison.Ordinal);
    }

    [Fact]
    public void Signature_strip_signature_is_centred_on_the_middle_of_the_strip_within_390()
    {
        var image = Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, null, null, null, null);

        var bounds = WhiteBounds(image, 0, 800, 600, 1000);

        Assert.NotNull(bounds);
        var (minX, minY, maxX, maxY) = bounds.Value;
        Assert.InRange((minX + maxX) / 2.0, 292, 308);
        Assert.InRange((minY + maxY) / 2.0, 870, 930);
        Assert.InRange(maxX - minX, 250, 392);
        Assert.InRange(maxY - minY, 20, 58);
    }

    [Fact]
    public void Panorama_signature_is_right_aligned_at_24_from_the_edge_within_300()
    {
        var image = Compose(CatalogData.PanoramaFourId, CatalogData.NaturaleId, null, null, null, null);

        var bounds = WhiteBounds(image, 1300, 450, 1800, 600);

        Assert.NotNull(bounds);
        var (minX, minY, maxX, maxY) = bounds.Value;
        Assert.InRange(maxX, 1768, 1776);
        Assert.InRange(maxX - minX, 200, 302);
        Assert.InRange((minY + maxY) / 2.0, 535, 590);
        Assert.InRange(maxY - minY, 18, 48);
    }

    [Fact]
    public void A_signature_wider_than_its_limit_is_squeezed_to_fit()
    {
        using var panorama = new SKFont(CatalogAssets.CormorantGaramondMedium, 48);
        using var strip = new SKFont(CatalogAssets.CormorantGaramondMedium, 58);
        var panoramaBounds = WhiteBounds(Compose(CatalogData.PanoramaFourId, CatalogData.NaturaleId, null, null, null, null), 1300, 450, 1800, 600)!.Value;
        var stripBounds = WhiteBounds(Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, null, null, null, null), 0, 800, 600, 1000)!.Value;

        // Whichever way a face measures, the drawn text never exceeds maxWidth, and is within a side bearing of the scaled advance.
        Assert.InRange(panoramaBounds.MaxX - panoramaBounds.MinX, Math.Min(panorama.MeasureText("FOTOHAVN"), 300) - 14, 302);
        Assert.InRange(stripBounds.MaxX - stripBounds.MinX, Math.Min(strip.MeasureText("FOTOHAVN"), 390) - 14, 392);
    }

    [Fact]
    public void The_shaped_signature_is_narrower_than_the_unshaped_one_by_the_font_kerning()
    {
        using var font = new SKFont(CatalogAssets.CormorantGaramondMedium, 58) { Subpixel = true, Hinting = SKFontHinting.None };
        using var shaper = new SKShaper(CatalogAssets.CormorantGaramondMedium);

        var shaped = shaper.Shape("FOTOHAVN", font).Width;
        var unshaped = font.MeasureText("FOTOHAVN");

        // A-V -81, T-O -15, O-T -23 per 1000 em is 6.9 px at 58 px.
        Assert.InRange(unshaped - shaped, 5, 9);
    }

    [Fact]
    public void The_A_and_V_of_the_shaped_signature_are_closer_than_unkerned()
    {
        using var font = new SKFont(CatalogAssets.CormorantGaramondMedium, 58) { Subpixel = true, Hinting = SKFontHinting.None };
        using var shaper = new SKShaper(CatalogAssets.CormorantGaramondMedium);

        var shaped = shaper.Shape("FOTOHAVN", font);
        var advanceOfA = font.GetGlyphWidths(font.GetGlyphs("A"))[0];
        var distanceAToV = shaped.Points[6].X - shaped.Points[5].X;

        Assert.InRange(advanceOfA - distanceAToV, 3.5, 6);
    }

    [Fact]
    public void The_drawn_signature_is_narrower_than_unkerned_text_by_the_kerning()
    {
        var drawn = WhiteBounds(Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, null, null, null, null), 0, 800, 600, 1000)!.Value;
        using var font = new SKFont(CatalogAssets.CormorantGaramondMedium, 58) { Edging = SKFontEdging.Antialias, Subpixel = true, Hinting = SKFontHinting.None };
        using var bitmap = new SKBitmap(600, 200);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.Clear(new SKColor(0xE5, 0xE0, 0xD7));
            canvas.DrawText("FOTOHAVN", 300 - (font.MeasureText("FOTOHAVN") / 2), 100, font, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var unkerned = WhiteBounds(PixelImage.Decode(png.ToArray()), 0, 0, 600, 200)!.Value;

        Assert.InRange((unkerned.MaxX - unkerned.MinX) - (drawn.MaxX - drawn.MinX), 5, 9);
    }

    [Fact]
    public void The_signature_is_drawn_over_photographs_too()
    {
        var image = Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, Same(4, FixturePhotos.Solid(0, 0, 0)));

        Assert.NotNull(WhiteBounds(image, 0, 800, 600, 1000));
    }

    // ---- Cover crop and orientation ----

    public static TheoryData<int, int> SourceSizes => new() { { 1920, 1080 }, { 1080, 1920 }, { 1200, 1200 }, { 1600, 1200 } };

    [Theory]
    [MemberData(nameof(SourceSizes))]
    public void Left_and_right_halves_land_on_the_left_and_right_in_every_window(int width, int height)
    {
        var png = FixturePhotos.LeftRight(width, height);
        foreach (var template in new CatalogData().Templates)
        {
            var image = Compose(template.Id, CatalogData.NaturaleId, Same(template.WindowCount, png));
            foreach (var window in template.Windows)
            {
                var middleY = window.Y + (window.Height / 2);
                AssertColor(image.At(window.X + (window.Width / 4), middleY), 255, 0, 0);
                AssertColor(image.At(window.X + (window.Width * 3 / 4), middleY), 0, 0, 255);
            }
        }
    }

    [Theory]
    [MemberData(nameof(SourceSizes))]
    public void Top_and_bottom_halves_land_on_top_and_bottom_in_every_window(int width, int height)
    {
        var png = FixturePhotos.TopBottom(width, height);
        foreach (var template in new CatalogData().Templates)
        {
            var image = Compose(template.Id, CatalogData.NaturaleId, Same(template.WindowCount, png));
            foreach (var window in template.Windows)
            {
                var middleX = window.X + (window.Width / 2);
                AssertColor(image.At(middleX, window.Y + (window.Height / 4)), 0, 200, 0);
                AssertColor(image.At(middleX, window.Y + (window.Height * 3 / 4)), 255, 220, 0);
            }
        }
    }

    [Fact]
    public void A_wide_photograph_loses_its_side_edges_in_a_narrower_window()
    {
        var image = Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, Same(4, FixturePhotos.SideBands(1920, 1080, 100)));

        for (var window = 0; window < 4; window++)
        {
            AssertColor(image.At(0, (window * 450) + 225), 128, 128, 128);
            AssertColor(image.At(599, (window * 450) + 225), 128, 128, 128);
        }
    }

    [Fact]
    public void A_tall_photograph_loses_its_top_and_bottom_edges_in_a_wider_window()
    {
        var image = Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, Same(4, FixturePhotos.EdgeBands(1080, 1920, 100)));

        for (var window = 0; window < 4; window++)
        {
            AssertColor(image.At(10, (window * 450) + 2), 128, 128, 128);
            AssertColor(image.At(10, (window * 450) + 447), 128, 128, 128);
        }
    }

    [Fact]
    public void A_photograph_exactly_matching_the_window_ratio_is_not_cropped()
    {
        var png = FixturePhotos.SideBands(1200, 900, 100);

        var image = Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, Same(4, png));

        AssertColor(image.At(10, 225), 255, 0, 0);
        AssertColor(image.At(590, 225), 0, 0, 255);
    }

    [Fact]
    public void Jpeg_photographs_decode_and_compose()
    {
        var jpeg = FixturePhotos.Jpeg(FixturePhotos.LeftRight(1920, 1080));

        var image = Compose(CatalogData.SignatureStripId, CatalogData.NaturaleId, Same(4, jpeg));

        AssertColor(image.At(150, 225), 255, 0, 0, 12);
        AssertColor(image.At(450, 225), 0, 0, 255, 12);
    }

    // ---- BGRA input ----

    [Fact]
    public void A_padded_bgra_frame_composes_the_same_as_the_equivalent_png()
    {
        var png = FixturePhotos.FourThree;
        var frame = FixturePhotos.Frame(png, stride: (1600 * 4) + 64);
        Assert.True(frame.Stride > frame.Width * 4);

        var fromFrame = Compose(CatalogData.SignatureStripId, CatalogData.ClassicId, [.. Enumerable.Repeat<CompositionPhoto?>(new CompositionPhoto.Bgra(frame), 4)]);
        var fromPng = Compose(CatalogData.SignatureStripId, CatalogData.ClassicId, Same(4, png));

        Assert.Equal(fromPng.Bgra, fromFrame.Bgra);
    }

    [Fact]
    public void A_tightly_packed_bgra_frame_composes_the_same_as_the_equivalent_png()
    {
        var png = FixturePhotos.Square;
        var frame = FixturePhotos.Frame(png, stride: 1200 * 4);

        var fromFrame = Compose(CatalogData.PolkaKeepsakeId, CatalogData.SepiaId, [.. Enumerable.Repeat<CompositionPhoto?>(new CompositionPhoto.Bgra(frame), 4)]);
        var fromPng = Compose(CatalogData.PolkaKeepsakeId, CatalogData.SepiaId, Same(4, png));

        Assert.Equal(fromPng.Bgra, fromFrame.Bgra);
    }

    // ---- Errors ----

    private static async Task<CompositionException> Fail(Task<CompositionResult> task) => await Assert.ThrowsAsync<CompositionException>(() => task);

    [Fact]
    public async Task Unknown_template_fails_with_UnknownTemplate()
    {
        var error = await Fail(Compositor.ComposeAsync(new CompositionRequest("nope", CatalogData.NaturaleId, []), TestContext.Current.CancellationToken));

        Assert.Equal(CompositionErrorKind.UnknownTemplate, error.Kind);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("")]
    public async Task Unknown_look_fails_with_UnknownLook(string lookId)
    {
        var error = await Fail(Compositor.ComposeAsync(new CompositionRequest(CatalogData.SignatureStripId, lookId, new CompositionPhoto?[4]), TestContext.Current.CancellationToken));

        Assert.Equal(CompositionErrorKind.UnknownLook, error.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task A_photo_count_that_is_not_the_window_count_fails_with_WrongPhotoCount(int count)
    {
        var error = await Fail(Compositor.ComposeAsync(new CompositionRequest(CatalogData.SignatureStripId, CatalogData.NaturaleId, new CompositionPhoto?[count]), TestContext.Current.CancellationToken));

        Assert.Equal(CompositionErrorKind.WrongPhotoCount, error.Kind);
    }

    [Fact]
    public async Task Undecodable_bytes_fail_with_UndecodablePhoto()
    {
        var photos = new CompositionPhoto?[] { null, Photo([1, 2, 3, 4, 5, 6, 7, 8]), null, null };

        var error = await Fail(Compositor.ComposeAsync(new CompositionRequest(CatalogData.SignatureStripId, CatalogData.NaturaleId, photos), TestContext.Current.CancellationToken));

        Assert.Equal(CompositionErrorKind.UndecodablePhoto, error.Kind);
    }

    [Fact]
    public async Task Empty_bytes_fail_with_UndecodablePhoto()
    {
        var photos = new CompositionPhoto?[] { Photo([]), null, null, null };

        var error = await Fail(Compositor.ComposeAsync(new CompositionRequest(CatalogData.SignatureStripId, CatalogData.NaturaleId, photos), TestContext.Current.CancellationToken));

        Assert.Equal(CompositionErrorKind.UndecodablePhoto, error.Kind);
    }

    public static TheoryData<int, int, int, int> BadFrames => new()
    {
        { 0, 10, 4, 100 },
        { 10, 0, 40, 100 },
        { 10, 10, 39, 400 },
        { 10, 10, 40, 399 },
    };

    [Theory]
    [MemberData(nameof(BadFrames))]
    public async Task A_raw_frame_whose_geometry_does_not_fit_its_bytes_fails_with_UndecodablePhoto(int width, int height, int stride, int length)
    {
        var frame = new FotoHavn.Core.Camera.CapturedFrame(new byte[length], width, height, stride, TimeSpan.Zero);
        var photos = new CompositionPhoto?[] { new CompositionPhoto.Bgra(frame), null, null, null };

        var error = await Fail(Compositor.ComposeAsync(new CompositionRequest(CatalogData.SignatureStripId, CatalogData.NaturaleId, photos), TestContext.Current.CancellationToken));

        Assert.Equal(CompositionErrorKind.UndecodablePhoto, error.Kind);
    }

    [Fact]
    public async Task Errors_fail_the_returned_task_instead_of_throwing_synchronously()
    {
        var task = Compositor.ComposeAsync(new CompositionRequest("nope", CatalogData.NaturaleId, []), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<CompositionException>(() => task);
    }

    [Fact]
    public async Task An_unexpected_failure_is_reported_as_Unknown_with_the_cause()
    {
        var compositor = new SkiaCompositor(new TestCatalog { FindTemplateFailure = new InvalidOperationException("catalog exploded") });

        var error = await Fail(compositor.ComposeAsync(new CompositionRequest(TestCatalog.PlainId, CatalogData.NaturaleId, [null]), TestContext.Current.CancellationToken));

        Assert.Equal(CompositionErrorKind.Unknown, error.Kind);
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains("catalog exploded", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_request_fails_the_task_with_an_argument_error()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Compositor.ComposeAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_compositor_needs_a_catalog()
    {
        Assert.Throws<ArgumentNullException>(() => new SkiaCompositor(null!));
    }

    // ---- Cancellation ----

    [Fact]
    public async Task A_cancelled_token_cancels_the_composition()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var task = Compositor.ComposeAsync(new CompositionRequest(CatalogData.SignatureStripId, CatalogData.NaturaleId, new CompositionPhoto?[4]), cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(task.IsCanceled);
    }

    [Fact]
    public async Task Cancellation_is_honoured_between_windows()
    {
        using var cts = new CancellationTokenSource();
        var compositor = new SkiaCompositor(new TestCatalog { OnFindLook = () => cts.Cancel() });

        var task = compositor.ComposeAsync(new CompositionRequest(TestCatalog.PlainId, CatalogData.NaturaleId, [Photo(FixturePhotos.Landscape)]), cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(task.IsCanceled);
    }

    // ---- Thumbnails ----

    private static Task<CompositionResult> Thumb(CompositionPhoto photo, double aspect, string lookId, int width, CancellationToken? ct = null) =>
        Compositor.ComposeLookThumbnailAsync(photo, aspect, lookId, width, ct ?? TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(200, 1.5, 133)]
    [InlineData(300, 1.0, 300)]
    [InlineData(150, 0.75, 200)]
    [InlineData(1000, 16.0 / 9.0, 563)]
    public async Task A_thumbnail_has_the_requested_width_and_aspect(int width, double aspect, int expectedHeight)
    {
        var result = await Thumb(Photo(FixturePhotos.Landscape), aspect, CatalogData.NaturaleId, width);

        Assert.Equal((width, expectedHeight), (result.Width, result.Height));
        var image = PixelImage.Decode(result.Png);
        Assert.Equal((width, expectedHeight), (image.Width, image.Height));
    }

    [Fact]
    public async Task A_very_wide_thumbnail_is_at_least_one_pixel_tall()
    {
        var result = await Thumb(Photo(FixturePhotos.Landscape), 1000, CatalogData.NaturaleId, 10);

        Assert.Equal((10, 1), (result.Width, result.Height));
    }

    [Theory]
    [MemberData(nameof(ChangingLooks))]
    public async Task A_thumbnail_applies_the_look(string lookId)
    {
        var natural = PixelImage.Decode((await Thumb(Photo(FixturePhotos.Portrait), 1.5, CatalogData.NaturaleId, 240)).Png);
        var looked = PixelImage.Decode((await Thumb(Photo(FixturePhotos.Portrait), 1.5, lookId, 240)).Png);

        var expected = (byte[])natural.Bgra.Clone();
        LookKernels.Apply(expected, lookId);
        Assert.Equal(expected, looked.Bgra);
        Assert.NotEqual(natural.Bgra, looked.Bgra);
    }

    [Fact]
    public async Task A_thumbnail_cover_crops_the_photograph()
    {
        var result = await Thumb(Photo(FixturePhotos.LeftRight(1920, 1080)), 1.0, CatalogData.NaturaleId, 100);

        var image = PixelImage.Decode(result.Png);
        AssertColor(image.At(25, 50), 255, 0, 0);
        AssertColor(image.At(75, 50), 0, 0, 255);
    }

    [Fact]
    public async Task A_thumbnail_accepts_a_raw_frame()
    {
        var frame = FixturePhotos.Frame(FixturePhotos.Square, (1200 * 4) + 16);

        var fromFrame = await Thumb(new CompositionPhoto.Bgra(frame), 1.0, CatalogData.SoftColorId, 120);
        var fromPng = await Thumb(Photo(FixturePhotos.Square), 1.0, CatalogData.SoftColorId, 120);

        Assert.Equal(PixelImage.Decode(fromPng.Png).Bgra, PixelImage.Decode(fromFrame.Png).Bgra);
    }

    [Fact]
    public async Task A_thumbnail_with_an_unknown_look_fails_with_UnknownLook()
    {
        var error = await Fail(Thumb(Photo(FixturePhotos.Square), 1, "nope", 100));

        Assert.Equal(CompositionErrorKind.UnknownLook, error.Kind);
    }

    [Fact]
    public async Task A_thumbnail_of_an_undecodable_photo_fails_with_UndecodablePhoto()
    {
        var error = await Fail(Thumb(Photo([9, 9, 9]), 1, CatalogData.NaturaleId, 100));

        Assert.Equal(CompositionErrorKind.UndecodablePhoto, error.Kind);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(-5, 1.0)]
    [InlineData(100, 0.0)]
    [InlineData(100, -1.0)]
    [InlineData(100, double.NaN)]
    [InlineData(100, double.PositiveInfinity)]
    public async Task A_thumbnail_with_an_unusable_size_fails_the_task(int width, double aspect)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Thumb(Photo(FixturePhotos.Square), aspect, CatalogData.NaturaleId, width));
    }

    [Fact]
    public async Task A_thumbnail_without_a_photo_fails_the_task()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Thumb(null!, 1, CatalogData.NaturaleId, 100));
    }

    [Fact]
    public async Task A_cancelled_thumbnail_is_cancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var task = Thumb(Photo(FixturePhotos.Square), 1, CatalogData.NaturaleId, 100, cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task A_thumbnail_cancelled_while_it_starts_is_cancelled()
    {
        using var cts = new CancellationTokenSource();
        var compositor = new SkiaCompositor(new TestCatalog { OnFindLook = () => cts.Cancel() });

        var task = compositor.ComposeLookThumbnailAsync(Photo(FixturePhotos.Square), 1, CatalogData.NaturaleId, 100, cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    // ---- Thread safety ----

    [Fact]
    public async Task Concurrent_compositions_give_the_same_pixels_as_one_alone()
    {
        var photos = Enumerable.Range(0, 4).Select(i => (CompositionPhoto?)FixturePhotos.ForWindow(i)).ToArray();
        var request = new CompositionRequest(CatalogData.SignatureStripId, CatalogData.ClassicId, photos);
        var alone = PixelImage.Decode((await Compositor.ComposeAsync(request, TestContext.Current.CancellationToken)).Png).Hash();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Compositor.ComposeAsync(request, TestContext.Current.CancellationToken)));

        Assert.All(results, result => Assert.Equal(alone, PixelImage.Decode(result.Png).Hash()));
    }

    [Fact]
    public async Task The_default_compositor_uses_the_real_catalog()
    {
        var result = await new SkiaCompositor().ComposeAsync(new CompositionRequest(CatalogData.PanoramaFourId, CatalogData.NaturaleId, new CompositionPhoto?[4]), TestContext.Current.CancellationToken);

        Assert.Equal((1800, 600), (result.Width, result.Height));
    }
}
