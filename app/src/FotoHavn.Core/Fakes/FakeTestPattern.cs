namespace FotoHavn.Core.Fakes;

/// <summary>
/// The fake camera's test pattern: colour bars over a grey ramp, a block that moves with the frame sequence, and the
/// sequence number in digits. Deterministic and cheap, so a test can tell frames apart and a person watching the
/// headless preview can see that frames are live.
/// </summary>
public static class FakeTestPattern
{
    private const int BlockSize = 96;
    private const int DigitScale = 8;
    private const int DigitWidth = 3;
    private const int DigitHeight = 5;
    private const int DigitSpacing = 1;
    private const int Margin = 16;

    // Bars, left to right: white, yellow, cyan, green, magenta, red, blue, black (R, G, B).
    private static readonly (byte R, byte G, byte B)[] Bars =
    [
        (210, 210, 210), (210, 210, 16), (16, 210, 210), (16, 210, 16), (210, 16, 210), (210, 16, 16), (16, 16, 210), (16, 16, 16),
    ];

    // 3×5 digit glyphs, one row per entry, the low three bits left to right.
    private static readonly byte[][] Glyphs =
    [
        [0b111, 0b101, 0b101, 0b101, 0b111],
        [0b010, 0b110, 0b010, 0b010, 0b111],
        [0b111, 0b001, 0b111, 0b100, 0b111],
        [0b111, 0b001, 0b111, 0b001, 0b111],
        [0b101, 0b101, 0b111, 0b001, 0b001],
        [0b111, 0b100, 0b111, 0b001, 0b111],
        [0b111, 0b100, 0b111, 0b101, 0b111],
        [0b111, 0b001, 0b010, 0b010, 0b010],
        [0b111, 0b101, 0b111, 0b101, 0b111],
        [0b111, 0b101, 0b111, 0b001, 0b111],
    ];

    /// <summary>
    /// Draws frame <paramref name="sequence"/> into <paramref name="pixels"/> (tightly packed rows, four bytes per pixel,
    /// alpha 255). The same arguments always write the same bytes.
    /// </summary>
    /// <param name="pixels">At least <c>width × height × 4</c> bytes.</param>
    /// <param name="width">The frame width; at least 2 × <see cref="BlockSize"/>.</param>
    /// <param name="height">The frame height; at least 2 × <see cref="BlockSize"/>.</param>
    /// <param name="sequence">The frame number, drawn as digits and moving the block.</param>
    /// <param name="rgba">True for R, G, B, A byte order (preview); false for B, G, R, A (Capture).</param>
    public static void Render(Span<byte> pixels, int width, int height, long sequence, bool rgba)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 2 * BlockSize);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 2 * BlockSize);
        ArgumentOutOfRangeException.ThrowIfLessThan(pixels.Length, width * height * 4);

        sequence = Math.Max(sequence, 0);
        var barsHeight = height * 3 / 5;
        var redOffset = rgba ? 0 : 2;
        var blueOffset = rgba ? 2 : 0;
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * width * 4, width * 4);
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = y < barsHeight
                    ? Bars[x * Bars.Length / width]
                    : Ramp(x, width, y, height);
                var pixel = row.Slice(x * 4, 4);
                pixel[redOffset] = r;
                pixel[1] = g;
                pixel[blueOffset] = b;
                pixel[3] = 255;
            }
        }

        DrawBlock(pixels, width, height, sequence, redOffset, blueOffset);
        DrawNumber(pixels, width, sequence, redOffset, blueOffset);
    }

    private static (byte R, byte G, byte B) Ramp(int x, int width, int y, int height)
    {
        var level = (byte)(x * 255 / (width - 1));
        return y > height - height / 10 ? ((byte)(255 - level), (byte)(255 - level), (byte)(255 - level)) : (level, level, level);
    }

    private static void DrawBlock(Span<byte> pixels, int width, int height, long sequence, int redOffset, int blueOffset)
    {
        var travel = width - BlockSize;
        var left = (int)(sequence * 12 % travel);
        var top = height * 3 / 10;
        for (var y = top; y < top + BlockSize; y++)
        {
            for (var x = left; x < left + BlockSize; x++)
            {
                var edge = x - left < 6 || left + BlockSize - 1 - x < 6 || y - top < 6 || top + BlockSize - 1 - y < 6;
                Set(pixels, width, x, y, edge ? (byte)0 : (byte)255, redOffset, blueOffset);
            }
        }
    }

    private static void DrawNumber(Span<byte> pixels, int width, long sequence, int redOffset, int blueOffset)
    {
        var cell = (DigitWidth + DigitSpacing) * DigitScale;
        var fitting = (width - (2 * Margin)) / cell;
        var all = (sequence < 0 ? 0 : sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var digits = all.Length > fitting ? all[^fitting..] : all;
        var boxWidth = (digits.Length * cell) + (2 * Margin);
        var boxHeight = (DigitHeight * DigitScale) + (2 * Margin);
        for (var y = 0; y < boxHeight; y++)
        {
            for (var x = 0; x < boxWidth; x++)
            {
                Set(pixels, width, x, y, 0, redOffset, blueOffset);
            }
        }

        for (var index = 0; index < digits.Length; index++)
        {
            var glyph = Glyphs[digits[index] - '0'];
            for (var row = 0; row < DigitHeight; row++)
            {
                for (var column = 0; column < DigitWidth; column++)
                {
                    if ((glyph[row] & (1 << (DigitWidth - 1 - column))) == 0)
                    {
                        continue;
                    }

                    FillSquare(pixels, width, Margin + (index * cell) + (column * DigitScale), Margin + (row * DigitScale), redOffset, blueOffset);
                }
            }
        }
    }

    private static void FillSquare(Span<byte> pixels, int width, int left, int top, int redOffset, int blueOffset)
    {
        for (var y = top; y < top + DigitScale; y++)
        {
            for (var x = left; x < left + DigitScale; x++)
            {
                Set(pixels, width, x, y, 255, redOffset, blueOffset);
            }
        }
    }

    private static void Set(Span<byte> pixels, int width, int x, int y, byte gray, int redOffset, int blueOffset)
    {
        var offset = ((y * width) + x) * 4;
        pixels[offset + redOffset] = gray;
        pixels[offset + 1] = gray;
        pixels[offset + blueOffset] = gray;
        pixels[offset + 3] = 255;
    }
}
