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
}
