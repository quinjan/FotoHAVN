using FotoHavn.Core.Media;

namespace FotoHavn.Core.Tests.Media;

public sealed class MediaPathPolicyTests
{
    private const string DataRoot = @"C:\FotoHAVN\Data";

    [Theory]
    [InlineData("/events/event-1/guest-cycles/cycle-7/photo-strip.png", @"C:\FotoHAVN\Data\Events\event-1\GuestCycles\cycle-7\photo-strip.png")]
    [InlineData("events/E1/guest-cycles/C2/capture-1.jpg", @"C:\FotoHAVN\Data\Events\E1\GuestCycles\C2\capture-1.jpg")]
    [InlineData("/thumbnails/source-1-0123abcd-480.jpg", @"C:\FotoHAVN\Data\Cache\thumbnails\source-1-0123abcd-480.jpg")]
    [InlineData("thumbnails/a_b.c-d.jpg", @"C:\FotoHAVN\Data\Cache\thumbnails\a_b.c-d.jpg")]
    [InlineData("/thumbnails/%61bc.jpg", @"C:\FotoHAVN\Data\Cache\thumbnails\abc.jpg")]
    [InlineData("/events/%45vent/guest-cycles/c/%5f.png", @"C:\FotoHAVN\Data\Events\Event\GuestCycles\c\_.png")]
    [InlineData("/thumbnails/console.jpg", @"C:\FotoHAVN\Data\Cache\thumbnails\console.jpg")]
    public void Accepts_the_two_shapes(string requestPath, string expected)
    {
        Assert.True(MediaPathPolicy.TryResolve(DataRoot, requestPath, out var fullPath));
        Assert.Equal(expected, fullPath);
    }

    [Theory]
    [InlineData(@"C:\FotoHAVN\Data\")]
    [InlineData(@"C:\FotoHAVN\Other\..\Data")]
    [InlineData("C:/FotoHAVN/Data")]
    public void Normalizes_the_data_root(string dataRoot)
    {
        Assert.True(MediaPathPolicy.TryResolve(dataRoot, "/thumbnails/a.jpg", out var fullPath));
        Assert.Equal(@"C:\FotoHAVN\Data\Cache\thumbnails\a.jpg", fullPath);
    }

    [Theory]
    // Shape
    [InlineData("")]
    [InlineData("/")]
    [InlineData("//thumbnails/a.jpg")]
    [InlineData("/thumbnails")]
    [InlineData("/thumbnails/")]
    [InlineData("/thumbnails/a.jpg/")]
    [InlineData("/thumbnails/sub/a.jpg")]
    [InlineData("/Thumbnails/a.jpg")]
    [InlineData("/cache/a.jpg")]
    [InlineData("/events/e/guest-cycles/c")]
    [InlineData("/events/e/guest-cycles/c/")]
    [InlineData("/events/e/guest-cycles/c/f.png/x")]
    [InlineData("/Events/e/guest-cycles/c/f.png")]
    [InlineData("/events/e/GuestCycles/c/f.png")]
    [InlineData("/events/e/guest-cycles/c/.staging")]
    [InlineData("/events//guest-cycles/c/f.png")]
    [InlineData("/events/e/guest-cycles//f.png")]
    [InlineData("/thumbnails/a.jpg?v=1")]
    // Traversal and absolute paths
    [InlineData("/thumbnails/..")]
    [InlineData("/thumbnails/.")]
    [InlineData("/thumbnails/../fotohavn.db")]
    [InlineData("/events/../guest-cycles/c/f.png")]
    [InlineData("/events/e/guest-cycles/../f.png")]
    [InlineData("/events/e/guest-cycles/c/..")]
    [InlineData(@"/thumbnails/..\fotohavn.db")]
    [InlineData(@"\thumbnails\a.jpg")]
    [InlineData("/thumbnails/C:a.jpg")]
    [InlineData("C:/thumbnails/a.jpg")]
    [InlineData(@"\\server\share\a.jpg")]
    [InlineData("/thumbnails/a.jpg:secret")]
    [InlineData("/thumbnails/a.jpg::$DATA")]
    // File and id rules
    [InlineData("/thumbnails/.hidden")]
    [InlineData("/thumbnails/trailing.")]
    [InlineData("/thumbnails/a b.jpg")]
    [InlineData("/thumbnails/a~b.jpg")]
    [InlineData("/thumbnails/ä.jpg")]
    [InlineData("/thumbnails/CON")]
    [InlineData("/thumbnails/nul.txt")]
    [InlineData("/thumbnails/com1.jpg")]
    [InlineData("/thumbnails/Lpt9")]
    [InlineData("/events/AUX/guest-cycles/c/f.png")]
    [InlineData("/events/e/guest-cycles/prn/f.png")]
    [InlineData("/events/e.1/guest-cycles/c/f.png")]
    [InlineData("/events/e_1/guest-cycles/c/f.png")]
    [InlineData("/events/e/guest-cycles/c.1/f.png")]
    // Percent-encoding
    [InlineData("/thumbnails%2Fa.jpg")]
    [InlineData("/thumbnails%2fa.jpg")]
    [InlineData("/thumbnails/%2E%2E")]
    [InlineData("/thumbnails/%2e%2e/fotohavn.db")]
    [InlineData("/thumbnails/a%2Ejpg")]
    [InlineData("/thumbnails/..%5Cfotohavn.db")]
    [InlineData("/thumbnails/..%5cfotohavn.db")]
    [InlineData("/thumbnails/%252E%252E")]
    [InlineData("/thumbnails/a%3Ab.jpg")]
    [InlineData("/thumbnails/a%00.jpg")]
    [InlineData("/thumbnails/a%20.jpg")]
    [InlineData("/thumbnails/%C3%A4.jpg")]
    [InlineData("/thumbnails/a.jpg%")]
    [InlineData("/thumbnails/a.jpg%4")]
    [InlineData("/thumbnails/a%G1.jpg")]
    [InlineData("/thumbnails/a%1G.jpg")]
    public void Refuses_everything_else(string requestPath)
    {
        Assert.False(MediaPathPolicy.TryResolve(DataRoot, requestPath, out var fullPath));
        Assert.Equal(string.Empty, fullPath);
    }

    [Fact]
    public void Enforces_the_length_limits()
    {
        var id = new string('a', MediaPathPolicy.MaxIdLength);
        var file = new string('b', MediaPathPolicy.MaxFileNameLength);

        Assert.True(MediaPathPolicy.TryResolve(DataRoot, $"/events/{id}/guest-cycles/{id}/{file}", out _));
        Assert.True(MediaPathPolicy.TryResolve(DataRoot, $"/thumbnails/{file}", out _));
        Assert.False(MediaPathPolicy.TryResolve(DataRoot, $"/events/{id}a/guest-cycles/{id}/{file}", out _));
        Assert.False(MediaPathPolicy.TryResolve(DataRoot, $"/events/{id}/guest-cycles/{id}a/{file}", out _));
        Assert.False(MediaPathPolicy.TryResolve(DataRoot, $"/events/{id}/guest-cycles/{id}/{file}b", out _));
        Assert.False(MediaPathPolicy.TryResolve(DataRoot, $"/thumbnails/{file}b", out _));
    }

    [Fact]
    public void Requires_its_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => MediaPathPolicy.TryResolve(null!, "/thumbnails/a.jpg", out _));
        Assert.Throws<ArgumentNullException>(() => MediaPathPolicy.TryResolve(DataRoot, null!, out _));
    }

    [Theory]
    [InlineData("Data")]
    [InlineData(@"\FotoHAVN\Data")]
    [InlineData("C:Data")]
    [InlineData("")]
    public void Requires_a_fully_qualified_data_root(string dataRoot)
    {
        var exception = Assert.Throws<ArgumentException>(() => MediaPathPolicy.TryResolve(dataRoot, "/thumbnails/a.jpg", out _));
        Assert.Equal("dataRoot", exception.ParamName);
    }

    [Theory]
    [InlineData(@"C:\Data\Cache\thumbnails", @"C:\Data\Cache\thumbnails\a.jpg", @"C:\Data\Cache\thumbnails\a.jpg")]
    [InlineData(@"C:\Data\Cache\thumbnails", @"c:\data\cache\THUMBNAILS\a.jpg", @"c:\data\cache\THUMBNAILS\a.jpg")]
    [InlineData(@"C:\Data\Cache\thumbnails", @"C:\Data\Cache\thumbnails\sub\..\a.jpg", @"C:\Data\Cache\thumbnails\a.jpg")]
    [InlineData(@"C:\", @"C:\a.jpg", @"C:\a.jpg")]
    [InlineData(@"C:\Data\Cache\thumbnails", @"C:\Data\Cache\thumbnails", "")]
    [InlineData(@"C:\Data\Cache\thumbnails", @"C:\Data\Cache\thumbnails\", "")]
    [InlineData(@"C:\Data\Cache\thumbnails", @"C:\Data\Cache\thumbnails\a\..", "")]
    [InlineData(@"C:\Data\Cache\thumbnails", @"C:\Data\Cache\thumbnails\..\..\fotohavn.db", "")]
    [InlineData(@"C:\Data\Cache\thumbnails", @"C:\Data\Cache\thumbnails-other\a.jpg", "")]
    [InlineData(@"C:\", @"D:\a.jpg", "")]
    public void Containment_check_requires_a_normalized_path_strictly_below_the_root(string root, string candidate, string expected)
    {
        Assert.Equal(expected.Length > 0, MediaPathPolicy.TryNormalizeInside(root, candidate, out var fullPath));
        Assert.Equal(expected, fullPath);
    }
}
