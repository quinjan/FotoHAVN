using FotoHavn.Catalog;

namespace FotoHavn.Catalog.Tests;

public sealed class CoverCropTests
{
    public static TheoryData<double> WindowRatios => [600.0 / 450, 450.0 / 600, 547.0 / 522, 513.0 / 390];

    public static TheoryData<double, double> Sources => new()
    {
        { 1920, 1080 },
        { 1080, 1920 },
        { 1000, 1000 },
        { 1600, 1200 },
        { 1200, 1600 },
        { 10_000, 100 },
        { 100, 10_000 },
        { 1, 1 },
        { 3, 2 },
    };

    private static void AssertCrop((double X, double Y, double Width, double Height) expected, (double X, double Y, double Width, double Height) actual)
    {
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
        Assert.Equal(expected.Width, actual.Width, 9);
        Assert.Equal(expected.Height, actual.Height, 9);
    }

    [Fact]
    public void A_landscape_source_into_a_four_by_three_window_trims_the_sides()
    {
        AssertCrop((240, 0, 1440, 1080), CoverCrop.Compute(1920, 1080, 600.0 / 450));
    }

    [Fact]
    public void A_portrait_source_into_a_four_by_three_window_trims_top_and_bottom()
    {
        AssertCrop((0, 555, 1080, 810), CoverCrop.Compute(1080, 1920, 4.0 / 3));
    }

    [Fact]
    public void A_source_with_the_target_ratio_is_not_cropped()
    {
        AssertCrop((0, 0, 1600, 1200), CoverCrop.Compute(1600, 1200, 4.0 / 3));
    }

    [Fact]
    public void A_square_source_into_a_portrait_window_trims_the_sides()
    {
        AssertCrop((125, 0, 750, 1000), CoverCrop.Compute(1000, 1000, 450.0 / 600));
    }

    [Theory]
    [MemberData(nameof(WindowRatios))]
    public void Every_window_ratio_covers_every_source_centred_and_inside(double ratio)
    {
        foreach (var (sourceWidth, sourceHeight) in new (double, double)[] { (1920, 1080), (1080, 1920), (1000, 1000), (1600, 1200), (1200, 1600), (10_000, 100), (100, 10_000), (1, 1), (3, 2) })
        {
            var (x, y, width, height) = CoverCrop.Compute(sourceWidth, sourceHeight, ratio);

            Assert.Equal(ratio, width / height, 9);
            Assert.True(x >= -1e-9 && y >= -1e-9, "The crop starts inside the source.");
            Assert.True(x + width <= sourceWidth + 1e-9 && y + height <= sourceHeight + 1e-9, "The crop ends inside the source.");
            Assert.Equal(sourceWidth - (x + width), x, 9);
            Assert.Equal(sourceHeight - (y + height), y, 9);
            Assert.True(Math.Abs(width - sourceWidth) < 1e-9 || Math.Abs(height - sourceHeight) < 1e-9, "The crop is as large as it can be.");
        }
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void The_default_four_by_three_ratio_of_the_website_matches_the_formula(double sourceWidth, double sourceHeight)
    {
        var expectedWidth = Math.Min(sourceWidth, sourceHeight * (4.0 / 3));
        var expectedHeight = expectedWidth / (4.0 / 3);

        Assert.Equal(
            ((sourceWidth - expectedWidth) / 2, (sourceHeight - expectedHeight) / 2, expectedWidth, expectedHeight),
            CoverCrop.Compute(sourceWidth, sourceHeight, 4.0 / 3));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    [InlineData(double.NaN, 100)]
    [InlineData(100, double.NaN)]
    [InlineData(double.PositiveInfinity, 100)]
    [InlineData(100, double.PositiveInfinity)]
    public void A_source_without_usable_dimensions_is_an_error(double width, double height)
    {
        Assert.Throws<ArgumentException>(() => CoverCrop.Compute(width, height, 1.5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_target_ratio_that_is_not_positive_and_finite_is_an_error(double ratio)
    {
        Assert.Throws<ArgumentException>(() => CoverCrop.Compute(100, 100, ratio));
    }
}
