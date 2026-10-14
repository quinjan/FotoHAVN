using FotoHavn.Catalog;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Fakes;
using SkiaSharp;

namespace FotoHavn.Core.Tests.Fakes;

public sealed class FakeCompositorTests
{
    private readonly CatalogData _catalog = new();

    private static CompositionPhoto?[] Empty(int count) => new CompositionPhoto?[count];

    [Theory]
    [InlineData("signature-strip", 600, 1800)]
    [InlineData("panorama-four", 1800, 600)]
    [InlineData("butter-gingham", 600, 1800)]
    [InlineData("polka-keepsake", 600, 1800)]
    public async Task It_answers_a_valid_opaque_png_of_the_templates_native_size(string templateId, int width, int height)
    {
        var compositor = new FakeCompositor(_catalog);
        var windows = _catalog.FindTemplate(templateId)!.WindowCount;

        var result = await compositor.ComposeAsync(new CompositionRequest(templateId, "naturale-v1", Empty(windows)), TestContext.Current.CancellationToken);

        Assert.Equal((width, height), (result.Width, result.Height));
        using var bitmap = SKBitmap.Decode(result.Png.ToArray());
        Assert.Equal((width, height), (bitmap.Width, bitmap.Height));
        Assert.Equal(new SKColor(0xF3, 0xEB, 0xDD), bitmap.GetPixel(width - 1, height - 1));
        Assert.True(bitmap.Info.IsOpaque);
    }

    [Fact]
    public async Task It_records_the_requests_it_receives()
    {
        var compositor = new FakeCompositor(_catalog);
        var first = new CompositionRequest("signature-strip", "sepia-v1", Empty(4));
        var second = new CompositionRequest("butter-gingham", "naturale-v1", Empty(3));

        await compositor.ComposeAsync(first, TestContext.Current.CancellationToken);
        await compositor.ComposeAsync(second, TestContext.Current.CancellationToken);

        Assert.Equal([first, second], compositor.Requests);
    }

    [Fact]
    public async Task It_rejects_an_unknown_template_an_unknown_look_and_a_wrong_photo_count()
    {
        var compositor = new FakeCompositor(_catalog);

        var template = await Assert.ThrowsAsync<CompositionException>(() => compositor.ComposeAsync(new CompositionRequest("nope", "naturale-v1", Empty(4)), TestContext.Current.CancellationToken));
        var look = await Assert.ThrowsAsync<CompositionException>(() => compositor.ComposeAsync(new CompositionRequest("signature-strip", "nope", Empty(4)), TestContext.Current.CancellationToken));
        var count = await Assert.ThrowsAsync<CompositionException>(() => compositor.ComposeAsync(new CompositionRequest("signature-strip", "naturale-v1", Empty(3)), TestContext.Current.CancellationToken));

        Assert.Equal(
            [CompositionErrorKind.UnknownTemplate, CompositionErrorKind.UnknownLook, CompositionErrorKind.WrongPhotoCount],
            [template.Kind, look.Kind, count.Kind]);
        Assert.Equal(3, compositor.Requests.Count);
    }

    [Fact]
    public async Task A_thumbnail_is_the_requested_width_at_the_aspect_ratio_and_is_recorded()
    {
        var compositor = new FakeCompositor(_catalog);
        var photo = new CompositionPhoto.Bgra(new CapturedFrame(new byte[16], 2, 2, 8, TimeSpan.Zero));

        var result = await compositor.ComposeLookThumbnailAsync(photo, 4.0 / 3, "sepia-v1", 240, TestContext.Current.CancellationToken);

        Assert.Equal((240, 180), (result.Width, result.Height));
        using var bitmap = SKBitmap.Decode(result.Png.ToArray());
        Assert.Equal((240, 180), (bitmap.Width, bitmap.Height));
        var recorded = Assert.Single(compositor.ThumbnailRequests);
        Assert.Equal(new FakeCompositor.ThumbnailRequest(photo, 4.0 / 3, "sepia-v1", 240), recorded);
    }

    [Fact]
    public async Task A_thumbnail_rejects_an_unknown_look_and_unusable_sizes()
    {
        var compositor = new FakeCompositor(_catalog);
        var photo = new CompositionPhoto.Encoded(new byte[] { 1 });
        var ct = TestContext.Current.CancellationToken;

        var look = await Assert.ThrowsAsync<CompositionException>(() => compositor.ComposeLookThumbnailAsync(photo, 1, "nope", 100, ct));
        var width = await Assert.ThrowsAsync<CompositionException>(() => compositor.ComposeLookThumbnailAsync(photo, 1, "naturale-v1", 0, ct));
        var ratio = await Assert.ThrowsAsync<CompositionException>(() => compositor.ComposeLookThumbnailAsync(photo, 0, "naturale-v1", 100, ct));
        var infinite = await Assert.ThrowsAsync<CompositionException>(() => compositor.ComposeLookThumbnailAsync(photo, double.PositiveInfinity, "naturale-v1", 100, ct));

        Assert.Equal(CompositionErrorKind.UnknownLook, look.Kind);
        Assert.All([width, ratio, infinite], exception => Assert.Equal(CompositionErrorKind.Unknown, exception.Kind));
    }

    [Fact]
    public async Task A_very_wide_thumbnail_is_at_least_one_pixel_tall()
    {
        var compositor = new FakeCompositor(_catalog);

        var result = await compositor.ComposeLookThumbnailAsync(new CompositionPhoto.Encoded(new byte[] { 1 }), 1000, "naturale-v1", 10, TestContext.Current.CancellationToken);

        Assert.Equal((10, 1), (result.Width, result.Height));
    }

    [Fact]
    public async Task A_configured_failure_fails_every_call_and_a_canceled_token_cancels()
    {
        var compositor = new FakeCompositor(_catalog) { Failure = new InvalidOperationException("boom") };
        var photo = new CompositionPhoto.Encoded(new byte[] { 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => compositor.ComposeAsync(new CompositionRequest("signature-strip", "naturale-v1", Empty(4)), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => compositor.ComposeLookThumbnailAsync(photo, 1, "naturale-v1", 10, TestContext.Current.CancellationToken));

        compositor.Failure = null;
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(() => compositor.ComposeAsync(new CompositionRequest("signature-strip", "naturale-v1", Empty(4)), canceled.Token));
        await Assert.ThrowsAsync<TaskCanceledException>(() => compositor.ComposeLookThumbnailAsync(photo, 1, "naturale-v1", 10, canceled.Token));
    }

    [Fact]
    public void Arguments_are_required()
    {
        var compositor = new FakeCompositor(_catalog);

        Assert.Throws<ArgumentNullException>(() => { _ = compositor.ComposeAsync(null!, CancellationToken.None); });
        Assert.Throws<ArgumentNullException>(() => { _ = compositor.ComposeLookThumbnailAsync(null!, 1, "naturale-v1", 10, CancellationToken.None); });
    }
}
