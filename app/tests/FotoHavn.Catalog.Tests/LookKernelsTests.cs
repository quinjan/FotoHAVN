using FotoHavn.Catalog;

namespace FotoHavn.Catalog.Tests;

public sealed class LookKernelsTests
{
    private static readonly string[] Changing =
        ["monochrome-v1", "soft-monochrome-v1", "soft-color-v1", "sepia-v1", "warm-monochrome-v1", "classic-v1"];

    private static readonly byte[] Levels = [0, 1, 17, 64, 127, 128, 200, 254, 255];

    public static TheoryData<string> ChangingLooks => [.. Changing];

    public static TheoryData<string> AllLooks => ["naturale-v1", .. Changing];

    private static byte[] Bgra(byte r, byte g, byte b, byte a = 255) => [b, g, r, a];

    // The website's applyLook, written out per Look with JavaScript's Math.round (floor(x + 0.5)) as the oracle.
    private static (byte R, byte G, byte B) JsLook(string look, byte red, byte green, byte blue)
    {
        double r = red / 255.0, g = green / 255.0, b = blue / 255.0;
        if (look == "classic-v1")
        {
            r *= 1.055;
            b *= 0.91;
        }

        var luma = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        switch (look)
        {
            case "monochrome-v1":
                r = g = b = ((luma - 0.5) * 1.1) + 0.51;
                break;
            case "soft-monochrome-v1":
                r = g = b = ((luma - 0.5) * 0.94) + 0.52;
                break;
            case "warm-monochrome-v1":
                var warm = ((luma - 0.5) * 1.02) + 0.505;
                r = warm + 0.02;
                g = warm + 0.015;
                b = warm - 0.012;
                break;
            case "sepia-v1":
                var sepia = ((luma - 0.5) * 0.94) + 0.52;
                r = sepia + 0.045;
                g = sepia + 0.025;
                b = sepia - 0.025;
                break;
            default:
                var saturation = look == "soft-color-v1" ? 0.88 : 0.94;
                var contrast = look == "soft-color-v1" ? 0.94 : 0.98;
                var lift = look == "soft-color-v1" ? 0.02 : 0.015;
                r = ((((luma + ((r - luma) * saturation)) - 0.5) * contrast) + 0.5) + lift;
                g = ((((luma + ((g - luma) * saturation)) - 0.5) * contrast) + 0.5) + lift;
                b = ((((luma + ((b - luma) * saturation)) - 0.5) * contrast) + 0.5) + lift;
                break;
        }

        static byte Round(double value) => (byte)Math.Floor((Math.Max(0, Math.Min(1, value)) * 255) + 0.5);
        return (Round(r), Round(g), Round(b));
    }

    [Theory]
    [InlineData("monochrome-v1", 200, 100, 50, 119, 119, 119)]
    [InlineData("soft-monochrome-v1", 200, 100, 50, 123, 123, 123)]
    [InlineData("warm-monochrome-v1", 200, 100, 50, 124, 123, 116)]
    [InlineData("sepia-v1", 200, 100, 50, 135, 130, 117)]
    [InlineData("soft-color-v1", 200, 100, 50, 191, 109, 67)]
    [InlineData("classic-v1", 200, 100, 50, 208, 106, 55)]
    [InlineData("monochrome-v1", 128, 128, 128, 131, 131, 131)]
    public void Hand_computed_pixels_come_out_as_the_formula_says(string look, byte r, byte g, byte b, byte expectedR, byte expectedG, byte expectedB)
    {
        var pixel = Bgra(r, g, b);

        LookKernels.Apply(pixel, look);

        Assert.Equal(Bgra(expectedR, expectedG, expectedB), pixel);
    }

    [Theory]
    [MemberData(nameof(ChangingLooks))]
    public void Every_look_matches_the_javascript_oracle_on_a_grid_of_colours(string look)
    {
        foreach (var r in Levels)
        {
            foreach (var g in Levels)
            {
                foreach (var b in Levels)
                {
                    var pixel = Bgra(r, g, b, 77);

                    LookKernels.Apply(pixel, look);

                    var (expectedR, expectedG, expectedB) = JsLook(look, r, g, b);
                    Assert.True(
                        pixel.AsSpan().SequenceEqual(Bgra(expectedR, expectedG, expectedB, 77)),
                        $"{look} ({r},{g},{b}) gave ({pixel[2]},{pixel[1]},{pixel[0]}), expected ({expectedR},{expectedG},{expectedB}).");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(ChangingLooks))]
    public void Every_look_matches_the_oracle_on_every_gray_and_every_single_channel(string look)
    {
        for (var level = 0; level < 256; level++)
        {
            foreach (var (r, g, b) in new[] { (level, level, level), (level, 0, 0), (0, level, 0), (0, 0, level), (level, 255 - level, 128) })
            {
                var pixel = Bgra((byte)r, (byte)g, (byte)b);

                LookKernels.Apply(pixel, look);

                var (expectedR, expectedG, expectedB) = JsLook(look, (byte)r, (byte)g, (byte)b);
                Assert.Equal(Bgra(expectedR, expectedG, expectedB), pixel);
            }
        }
    }

    [Fact]
    public void Naturale_leaves_every_byte_alone()
    {
        var buffer = new byte[4 * 1024];
        new Random(7).NextBytes(buffer);
        var copy = buffer.ToArray();

        LookKernels.Apply(buffer, "naturale-v1");

        Assert.Equal(copy, buffer);
    }

    [Theory]
    [MemberData(nameof(AllLooks))]
    public void Alpha_is_never_touched(string look)
    {
        byte[] alphas = [0, 1, 127, 128, 254, 255];
        var buffer = alphas.SelectMany(alpha => Bgra(200, 100, 50, alpha)).ToArray();

        LookKernels.Apply(buffer, look);

        Assert.Equal(alphas, Enumerable.Range(0, alphas.Length).Select(pixel => buffer[(pixel * 4) + 3]));
    }

    [Theory]
    [MemberData(nameof(ChangingLooks))]
    public void Black_and_white_clamp_into_range_and_the_output_is_deterministic(string look)
    {
        var black = Bgra(0, 0, 0);
        var white = Bgra(255, 255, 255);

        LookKernels.Apply(black, look);
        LookKernels.Apply(white, look);

        var (blackR, blackG, blackB) = JsLook(look, 0, 0, 0);
        var (whiteR, whiteG, whiteB) = JsLook(look, 255, 255, 255);
        Assert.Equal(Bgra(blackR, blackG, blackB), black);
        Assert.Equal(Bgra(whiteR, whiteG, whiteB), white);
    }

    [Fact]
    public void Monochrome_clamps_below_zero_and_above_one()
    {
        var black = Bgra(0, 0, 0);
        var white = Bgra(255, 255, 255);

        LookKernels.Apply(black, "monochrome-v1");
        LookKernels.Apply(white, "monochrome-v1");

        // black: (0 - 0.5) * 1.1 + 0.51 = -0.04, clamped to 0; white: 0.5 * 1.1 + 0.51 = 1.06, clamped to 1.
        Assert.Equal(Bgra(0, 0, 0), black);
        Assert.Equal(Bgra(255, 255, 255), white);
    }

    [Fact]
    public void Classic_clamps_the_boosted_red_channel()
    {
        var pixel = Bgra(255, 255, 255);

        LookKernels.Apply(pixel, "classic-v1");

        var (r, g, b) = JsLook("classic-v1", 255, 255, 255);
        Assert.Equal(Bgra(r, g, b), pixel);
        Assert.Equal(255, pixel[2]);
        Assert.True(pixel[0] < 255, "The blue channel is pulled down, so white turns golden.");
    }

    [Fact]
    public void Warm_monochrome_and_sepia_tint_a_neutral_gray_warm()
    {
        foreach (var look in new[] { "warm-monochrome-v1", "sepia-v1" })
        {
            var pixel = Bgra(128, 128, 128);

            LookKernels.Apply(pixel, look);

            Assert.True(pixel[2] > pixel[1] && pixel[1] > pixel[0], $"{look} is warmer toward red.");
        }
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(2.5, 3)]
    [InlineData(126.5, 127)]
    [InlineData(127.5, 128)]
    [InlineData(253.5, 254)]
    [InlineData(254.5, 255)]
    public void Halves_round_up_never_to_even(double scaled, byte expected)
    {
        var unit = scaled / 255.0;
        Assert.Equal(scaled, unit * 255.0);

        Assert.Equal(expected, LookKernels.ToByte(unit));
        Assert.Equal(expected, (byte)Math.Floor(scaled + 0.5));
        Assert.Equal(expected, (byte)Math.Round(scaled, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void Banker_rounding_would_have_got_these_halves_wrong()
    {
        Assert.Equal(0, (byte)Math.Round(0.5));
        Assert.Equal(2, (byte)Math.Round(2.5));
        Assert.Equal(126, (byte)Math.Round(126.5));
    }

    [Theory]
    [InlineData(-3.0, 0)]
    [InlineData(-0.0001, 0)]
    [InlineData(0.0, 0)]
    [InlineData(1.0, 255)]
    [InlineData(1.0001, 255)]
    [InlineData(40.0, 255)]
    public void Values_clamp_into_a_byte(double unit, byte expected)
    {
        Assert.Equal(expected, LookKernels.ToByte(unit));
    }

    [Theory]
    [MemberData(nameof(AllLooks))]
    public void Only_the_given_span_is_changed(string look)
    {
        var buffer = new byte[24];
        Array.Fill(buffer, (byte)200);
        var copy = buffer.ToArray();

        LookKernels.Apply(buffer.AsSpan(8, 8), look);

        Assert.Equal(copy[..8], buffer[..8]);
        Assert.Equal(copy[16..], buffer[16..]);
    }

    [Fact]
    public void An_empty_buffer_is_fine_for_a_known_look()
    {
        LookKernels.Apply([], "sepia-v1");
        LookKernels.Apply([], "naturale-v1");
    }

    [Fact]
    public void An_unknown_look_is_an_error_even_for_an_empty_buffer()
    {
        Assert.Throws<ArgumentException>(() => LookKernels.Apply([], "vintage-v1"));
        Assert.Throws<ArgumentException>(() => LookKernels.Apply(new byte[4], "naturale"));
        Assert.Throws<ArgumentException>(() => LookKernels.Apply(new byte[4], string.Empty));
        Assert.Throws<ArgumentNullException>(() => LookKernels.Apply(new byte[4], null!));
        Assert.False(LookKernels.IsKnown("Naturale-v1"));
        Assert.Throws<ArgumentNullException>(() => LookKernels.IsKnown(null!));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void A_buffer_that_is_not_whole_pixels_is_an_error(int length)
    {
        Assert.Throws<ArgumentException>(() => LookKernels.Apply(new byte[length], "sepia-v1"));
        Assert.Throws<ArgumentException>(() => LookKernels.Apply(new byte[length], "naturale-v1"));
    }
}
