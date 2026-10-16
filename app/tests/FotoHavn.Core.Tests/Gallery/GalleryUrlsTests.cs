using FotoHavn.Core.Gallery;
using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Gallery;

public sealed class GalleryUrlsTests
{
    [Theory]
    [InlineData(600, 1800, 400)]
    [InlineData(1800, 600, 1200)]
    [InlineData(600, 600, 600)]
    [InlineData(100, 50, 100)]
    [InlineData(30, 30, 64)]
    [InlineData(5000, 1000, 1200)]
    public void A_strip_thumbnail_scales_the_long_edge_to_1200_and_never_enlarges(int width, int height, int expected) =>
        Assert.Equal(expected, GalleryUrls.StripThumbnailWidth(width, height));

    [Theory]
    [InlineData(1640, 1230, 480)]
    [InlineData(1230, 1640, 360)]
    [InlineData(300, 200, 300)]
    public void A_Capture_thumbnail_scales_the_long_edge_to_480_and_never_enlarges(int width, int height, int expected) =>
        Assert.Equal(expected, GalleryUrls.CaptureThumbnailWidth(width, height));

    [Fact]
    public void Every_thumbnail_width_is_one_the_store_accepts()
    {
        foreach (var (width, height) in new[] { (600, 1800), (1800, 600), (1640, 1230), (1, 1), (4000, 3000), (64, 4000) })
        {
            Assert.InRange(GalleryUrls.StripThumbnailWidth(width, height), ThumbnailName.MinimumWidth, ThumbnailName.MaximumWidth);
            Assert.InRange(GalleryUrls.CaptureThumbnailWidth(width, height), ThumbnailName.MinimumWidth, ThumbnailName.MaximumWidth);
        }
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    public void A_size_below_one_pixel_is_refused(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GalleryUrls.StripThumbnailWidth(width, height));
        Assert.Throws<ArgumentOutOfRangeException>(() => GalleryUrls.CaptureThumbnailWidth(width, height));
    }

    [Fact]
    public void A_thumbnail_url_is_named_by_source_hash_and_width()
    {
        var url = GalleryUrls.ThumbnailUrl(GalleryKit.Id(1), GalleryKit.Sha(7), 400);

        Assert.Equal($"https://media.fotohavn/thumbnails/{GalleryKit.Id(1)}-00000007-400.jpg", url);
    }

    [Fact]
    public void The_url_of_the_path_the_store_returns_is_the_url_built_from_the_record()
    {
        var path = ThumbnailName.RelativePath(GalleryKit.Id(2), GalleryKit.Sha(9), 480);

        Assert.Equal(GalleryUrls.ThumbnailUrl(GalleryKit.Id(2), GalleryKit.Sha(9), 480), GalleryUrls.ThumbnailUrlFromStorePath(path));
    }

    [Theory]
    [InlineData("Events/x/photo-strip.png")]
    [InlineData("Cache/thumbnails/")]
    [InlineData("Cache/thumbnails/a/b.jpg")]
    [InlineData("cache/thumbnails/a.jpg")]
    [InlineData("")]
    public void A_path_outside_the_thumbnail_folder_is_refused(string path) =>
        Assert.Throws<ArgumentException>(() => GalleryUrls.ThumbnailUrlFromStorePath(path));

    [Fact]
    public void A_null_path_is_refused() => Assert.Throws<ArgumentNullException>(() => GalleryUrls.ThumbnailUrlFromStorePath(null!));

    [Fact]
    public void The_saved_files_have_media_urls_with_the_hash_as_a_cache_buster()
    {
        var capture = GalleryKit.Capture(1, 3);
        var strip = GalleryKit.Strip(1, [capture]);

        Assert.Equal($"https://media.fotohavn/events/{GalleryKit.EventId}/guest-cycles/{GalleryKit.Id(1)}/photo-strip.png?v=00000001", GalleryUrls.StripUrl(GalleryKit.EventId, strip));
        Assert.Equal($"https://media.fotohavn/events/{GalleryKit.EventId}/guest-cycles/{GalleryKit.Id(1)}/capture-3.jpg?v=00000003", GalleryUrls.CaptureUrl(GalleryKit.EventId, capture));
    }

    [Fact]
    public void Every_url_the_gallery_builds_is_one_the_media_policy_serves()
    {
        const string root = @"C:\FOTOHAVN\Data";
        var capture = GalleryKit.Capture(1, 3);
        var strip = GalleryKit.Strip(1, [capture]);
        var urls = new[]
        {
            GalleryUrls.StripUrl(GalleryKit.EventId, strip),
            GalleryUrls.CaptureUrl(GalleryKit.EventId, capture),
            GalleryUrls.ThumbnailUrl(GalleryKit.Id(1), strip.Sha256, 400),
        };

        foreach (var url in urls)
        {
            var uri = new Uri(url);
            Assert.Equal("media.fotohavn", uri.Host);
            Assert.True(FotoHavn.Core.Media.MediaPathPolicy.TryResolve(root, uri.AbsolutePath, out _), url);
        }
    }
}
