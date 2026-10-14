using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;

namespace FotoHavn.Core.Tests.Catalog;

public sealed class CompositionContractTests
{
    [Fact]
    public void A_composition_exception_carries_its_kind_and_message()
    {
        var inner = new InvalidOperationException("inner");

        Assert.Equal(CompositionErrorKind.Unknown, new CompositionException().Kind);
        Assert.Equal(CompositionErrorKind.Unknown, new CompositionException("boom").Kind);
        Assert.Equal("boom", new CompositionException("boom").Message);
        Assert.Equal(CompositionErrorKind.Unknown, new CompositionException("boom", inner).Kind);
        Assert.Same(inner, new CompositionException("boom", inner).InnerException);
        Assert.Equal(CompositionErrorKind.UnknownLook, new CompositionException(CompositionErrorKind.UnknownLook).Kind);
        Assert.Contains("UnknownLook", new CompositionException(CompositionErrorKind.UnknownLook).Message, StringComparison.Ordinal);
        Assert.Equal(CompositionErrorKind.WrongPhotoCount, new CompositionException(CompositionErrorKind.WrongPhotoCount, "three").Kind);
        Assert.Equal("three", new CompositionException(CompositionErrorKind.WrongPhotoCount, "three").Message);
        var full = new CompositionException(CompositionErrorKind.UndecodablePhoto, "bad", inner);
        Assert.Equal((CompositionErrorKind.UndecodablePhoto, "bad"), (full.Kind, full.Message));
        Assert.Same(inner, full.InnerException);
    }

    [Fact]
    public void A_photo_is_either_encoded_or_raw_and_compares_by_value()
    {
        byte[] bytes = [1, 2, 3];
        var frame = new CapturedFrame(new byte[16], 2, 2, 8, TimeSpan.Zero);

        CompositionPhoto encoded = new CompositionPhoto.Encoded(bytes);
        CompositionPhoto raw = new CompositionPhoto.Bgra(frame);

        Assert.Equal(new CompositionPhoto.Encoded(bytes), encoded);
        Assert.Equal(new CompositionPhoto.Bgra(frame), raw);
        Assert.NotEqual(encoded, raw);
    }

    [Fact]
    public async Task The_placeholder_compositor_fails_every_call_as_unknown()
    {
        var compositor = new NotYetComposingCompositor();
        var request = new CompositionRequest("signature-strip", "naturale-v1", [null, null, null, null]);
        var photo = new CompositionPhoto.Encoded(new byte[] { 1 });

        var compose = await Assert.ThrowsAsync<CompositionException>(() => compositor.ComposeAsync(request, TestContext.Current.CancellationToken));
        var thumbnail = await Assert.ThrowsAsync<CompositionException>(() => compositor.ComposeLookThumbnailAsync(photo, 1, "naturale-v1", 100, TestContext.Current.CancellationToken));

        Assert.Equal(CompositionErrorKind.Unknown, compose.Kind);
        Assert.Equal(CompositionErrorKind.Unknown, thumbnail.Kind);
        Assert.Throws<ArgumentNullException>(() => { _ = compositor.ComposeAsync(null!, CancellationToken.None); });
        Assert.Throws<ArgumentNullException>(() => { _ = compositor.ComposeLookThumbnailAsync(null!, 1, "naturale-v1", 100, CancellationToken.None); });
    }
}
