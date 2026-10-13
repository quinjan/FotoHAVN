using FotoHavn.Core.Media;

namespace FotoHavn.Core.Tests.Media;

public sealed class EmptyPreviewSourceTests
{
    [Fact]
    public void Has_no_previews()
    {
        var source = EmptyPreviewSource.Instance;

        Assert.IsAssignableFrom<IPreviewSource>(source);
        Assert.False(source.TryGet("preview-1", out var png));
        Assert.True(png.IsEmpty);
        Assert.Same(source, EmptyPreviewSource.Instance);
    }

    [Fact]
    public void Requires_a_key()
    {
        Assert.Throws<ArgumentNullException>(() => EmptyPreviewSource.Instance.TryGet(null!, out _));
    }
}
