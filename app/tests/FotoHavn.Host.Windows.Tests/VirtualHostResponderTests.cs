using FotoHavn.Core.Media;
using FotoHavn.Host.Windows.WebView;

namespace FotoHavn.Host.Windows.Tests;

public sealed class VirtualHostResponderTests
{
    private const string DataRoot = @"C:\booth\Data";

    [Fact]
    public void Media_serves_guest_cycle_files_through_the_media_policy()
    {
        var response = VirtualHostResponder.Media("GET", "https://media.fotohavn/events/event-1/guest-cycles/cycle-2/strip.jpg?v=abc#x", DataRoot);

        var file = Assert.IsType<FileResponse>(response);
        Assert.Equal(@"C:\booth\Data\Events\event-1\GuestCycles\cycle-2\strip.jpg", file.Path);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.True(file.IncludeBody);
        Assert.Equal(200, file.StatusCode);
    }

    [Fact]
    public void Media_serves_thumbnails_and_answers_HEAD_without_a_body()
    {
        var response = VirtualHostResponder.Media("HEAD", "https://media.fotohavn/thumbnails/cycle-2.webp", DataRoot);

        var file = Assert.IsType<FileResponse>(response);
        Assert.Equal(@"C:\booth\Data\Cache\thumbnails\cycle-2.webp", file.Path);
        Assert.Equal("image/webp", file.ContentType);
        Assert.False(file.IncludeBody);
    }

    [Theory]
    [InlineData("https://media.fotohavn/thumbnails/..%2F..%2Ffotohavn.db")]
    [InlineData("https://media.fotohavn/thumbnails/%2E%2E/fotohavn.db")]
    [InlineData("https://media.fotohavn/thumbnails/../fotohavn.db")]
    [InlineData("https://media.fotohavn/events/event-1/guest-cycles/cycle-2/..%5C..%5Cx.jpg")]
    [InlineData("https://media.fotohavn/fotohavn.db")]
    [InlineData("https://media.fotohavn/logs/fotohavn.jsonl")]
    [InlineData("https://media.fotohavn/webview2-user-data/Default/Cookies")]
    [InlineData("https://media.fotohavn/thumbnails/CON.jpg")]
    [InlineData("https://media.fotohavn/thumbnails/C:%5Cwindows%5Cwin.ini")]
    [InlineData("https://media.fotohavn/Events/event-1/guest-cycles/cycle-2/strip.jpg")]
    [InlineData("https://media.fotohavn/")]
    [InlineData("https://media.fotohavn:8443/thumbnails/a.jpg")]
    [InlineData("http://media.fotohavn/thumbnails/a.jpg")]
    [InlineData("https://app.fotohavn/thumbnails/a.jpg")]
    [InlineData("not a uri")]
    public void Media_refuses_everything_the_policy_or_the_host_does_not_accept(string uri)
    {
        Assert.Same(VirtualHostResponse.NotFound, VirtualHostResponder.Media("GET", uri, DataRoot));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public void Media_allows_only_GET_and_HEAD(string method)
    {
        Assert.Same(VirtualHostResponse.MethodNotAllowed, VirtualHostResponder.Media(method, "https://media.fotohavn/thumbnails/a.jpg", DataRoot));
        Assert.Equal(405, VirtualHostResponse.MethodNotAllowed.StatusCode);
    }

    [Fact]
    public void Media_needs_a_fully_qualified_data_root()
    {
        Assert.Throws<ArgumentException>(() => VirtualHostResponder.Media("GET", "https://media.fotohavn/thumbnails/a.jpg", "Data"));
    }

    [Fact]
    public void Preview_serves_png_from_the_preview_source()
    {
        var source = new DictionaryPreviewSource(new() { ["signature-strip.naturale-v1.r7"] = [1, 2, 3] });

        var response = VirtualHostResponder.Preview("GET", "https://preview.fotohavn/signature-strip.naturale-v1.r7.png?rev=3", source);

        var content = Assert.IsType<ContentResponse>(response);
        Assert.Equal("image/png", content.ContentType);
        Assert.Equal([1, 2, 3], content.Content.ToArray());
        Assert.True(content.IncludeBody);
        Assert.Equal("no-store", content.CacheControl);
        Assert.Equal(["signature-strip.naturale-v1.r7"], source.Requested);
    }

    [Fact]
    public void Preview_answers_head_without_a_body()
    {
        var source = new DictionaryPreviewSource(new() { ["signature-strip.naturale-v1.r7"] = [1, 2, 3] });

        var content = Assert.IsType<ContentResponse>(VirtualHostResponder.Preview("HEAD", "https://preview.fotohavn/signature-strip.naturale-v1.r7.png", source));

        Assert.False(content.IncludeBody);
    }

    [Theory]
    [InlineData("https://preview.fotohavn/strip-7.png")]
    [InlineData("https://preview.fotohavn/Signature-Strip.naturale-v1.r7.png")]
    [InlineData("https://preview.fotohavn/signature-strip.naturale-v1.png")]
    [InlineData("https://preview.fotohavn/signature-strip.naturale-v1.r-7.png")]
    public void Preview_answers_404_for_a_key_that_is_not_a_preview_key_without_looking_it_up(string uri)
    {
        var source = new DictionaryPreviewSource(new() { ["strip-7"] = [1], ["Signature-Strip.naturale-v1.r7"] = [1], ["signature-strip.naturale-v1"] = [1] });

        Assert.Same(VirtualHostResponse.NotFound, VirtualHostResponder.Preview("GET", uri, source));
        Assert.Empty(source.Requested);
    }

    [Theory]
    [InlineData("https://preview.fotohavn/missing.png")]
    [InlineData("https://preview.fotohavn/signature-strip.naturale-v1.r7.jpg")]
    [InlineData("https://preview.fotohavn/signature-strip.naturale-v1.r7.PNG")]
    [InlineData("https://preview.fotohavn/.png")]
    [InlineData("https://preview.fotohavn/a/signature-strip.naturale-v1.r7.png")]
    [InlineData("https://preview.fotohavn/")]
    [InlineData("https://media.fotohavn/signature-strip.naturale-v1.r7.png")]
    public void Preview_answers_404_for_anything_else(string uri)
    {
        var source = new DictionaryPreviewSource(new() { ["signature-strip.naturale-v1.r7"] = [1] });

        Assert.Same(VirtualHostResponse.NotFound, VirtualHostResponder.Preview("GET", uri, source));
    }

    [Fact]
    public void Preview_until_M7_is_always_404()
    {
        Assert.Same(VirtualHostResponse.NotFound, VirtualHostResponder.Preview("GET", "https://preview.fotohavn/signature-strip.naturale-v1.r7.png", EmptyPreviewSource.Instance));
        Assert.Same(VirtualHostResponse.MethodNotAllowed, VirtualHostResponder.Preview("POST", "https://preview.fotohavn/signature-strip.naturale-v1.r7.png", EmptyPreviewSource.Instance));
    }

    [Theory]
    [InlineData("https://app.fotohavn/")]
    [InlineData("https://app.fotohavn/index.html")]
    public void App_fallback_serves_the_not_built_page(string uri)
    {
        var content = Assert.IsType<ContentResponse>(VirtualHostResponder.AppFallback("GET", uri));

        Assert.StartsWith("text/html", content.ContentType, StringComparison.Ordinal);
        Assert.Contains(FallbackPage.Message, System.Text.Encoding.UTF8.GetString(content.Content.Span), StringComparison.Ordinal);
    }

    [Fact]
    public void App_fallback_answers_404_for_other_paths()
    {
        Assert.Same(VirtualHostResponse.NotFound, VirtualHostResponder.AppFallback("GET", "https://app.fotohavn/assets/app.js"));
    }

    [Fact]
    public void Headers_carry_type_length_and_safety()
    {
        var headers = VirtualHostResponder.Headers("image/png", 1234);

        Assert.Equal(
            "Content-Type: image/png\r\nContent-Length: 1234\r\nX-Content-Type-Options: nosniff\r\nAccess-Control-Allow-Origin: https://app.fotohavn\r\nCache-Control: no-cache",
            headers);
        Assert.Equal("X-Content-Type-Options: nosniff\r\nAccess-Control-Allow-Origin: https://app.fotohavn", VirtualHostResponder.StatusHeaders);
        Assert.EndsWith("\r\nCache-Control: no-store", VirtualHostResponder.Headers("image/png", 1234, VirtualHostResponder.NoStore), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a.jpg", "image/jpeg")]
    [InlineData("a.JPEG", "image/jpeg")]
    [InlineData("a.png", "image/png")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.gif", "image/gif")]
    [InlineData("a.bmp", "image/bmp")]
    [InlineData("a.json", "application/json")]
    [InlineData("a.txt", "text/plain; charset=utf-8")]
    [InlineData("a.db", "application/octet-stream")]
    [InlineData("a", "application/octet-stream")]
    public void Content_types_follow_the_extension(string path, string expected)
    {
        Assert.Equal(expected, ContentTypes.ForPath(path));
    }

    private sealed class DictionaryPreviewSource(Dictionary<string, byte[]> previews) : IPreviewSource
    {
        public List<string> Requested { get; } = [];

        public bool TryGet(string key, out ReadOnlyMemory<byte> png)
        {
            Requested.Add(key);
            var found = previews.TryGetValue(key, out var bytes);
            png = bytes ?? ReadOnlyMemory<byte>.Empty;
            return found;
        }
    }
}
