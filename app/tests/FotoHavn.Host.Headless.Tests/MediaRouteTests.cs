using System.Net;
using FotoHavn.Core.Media;

namespace FotoHavn.Host.Headless.Tests;

public sealed class MediaRouteTests
{
    private const string Strip = "strip-bytes";
    private const string Thumbnail = "thumbnail-bytes";
    private const string Secret = "secret-database-bytes";

    [Theory]
    [InlineData("media/events/event-1/guest-cycles/cycle-1/strip.jpg", Strip, "image/jpeg")]
    [InlineData("media/events/event-1/guest-cycles/cycle-1/strip.jpg?v=0123abcd", Strip, "image/jpeg")]
    [InlineData("media/thumbnails/source-1-0123abcd-480.jpg", Thumbnail, "image/jpeg")]
    public async Task Serves_files_inside_Events_and_the_thumbnail_cache(string path, string body, string contentType)
    {
        await using var host = await StartWithMediaAsync();

        using var response = await host.Http.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(body, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Head_answers_without_a_body()
    {
        await using var host = await StartWithMediaAsync();

        var (status, response) = await RawHttp.SendAsync(host.Endpoints.Port, "HEAD", "/media/thumbnails/source-1-0123abcd-480.jpg");

        Assert.Equal(200, status);
        Assert.Contains($"Content-Length: {Thumbnail.Length}", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Thumbnail, response, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/media/events/event-1/guest-cycles/cycle-1/missing.jpg")]
    [InlineData("/media/fotohavn.db")]
    [InlineData("/media/events/event-1/guest-cycles/cycle-1/..%2F..%2F..%2F..%2Ffotohavn.db")]
    [InlineData("/media/events/event-1/guest-cycles/cycle-1/%2E%2E%2F%2E%2E%2F%2E%2E%2F%2E%2E%2Ffotohavn.db")]
    [InlineData("/media/thumbnails/..%5C..%5Cfotohavn.db")]
    [InlineData("/media/thumbnails/%2e%2e%2f%2e%2e%2ffotohavn.db")]
    [InlineData("/media/thumbnails/..\\..\\fotohavn.db")]
    [InlineData("/media/thumbnails/C:%5Csecret.txt")]
    [InlineData("/media/thumbnails/fotohavn.db:stream")]
    [InlineData("/media/events/event-1/fotohavn.db")]
    [InlineData("/media/Thumbnails/source-1-0123abcd-480.jpg")]
    [InlineData("/MEDIA/thumbnails/source-1-0123abcd-480.jpg")]
    [InlineData("/media/thumbnails/../../fotohavn.db")]
    [InlineData("/media/events/../fotohavn.db")]
    [InlineData("/media/../fotohavn.db")]
    [InlineData("/media/thumbnails//source-1-0123abcd-480.jpg")]
    [InlineData("/media/thumbnails/CON.jpg")]
    [InlineData("/media")]
    [InlineData("/media/")]
    public async Task Refuses_everything_else_without_leaking_files(string target)
    {
        await using var host = await StartWithMediaAsync();

        var (status, response) = await RawHttp.SendAsync(host.Endpoints.Port, "GET", target);

        // 404 from the route, or 400 where Kestrel already refuses the request target (a raw backslash).
        Assert.True(status is 400 or 404, $"Expected 400 or 404, got {status}.");
        Assert.DoesNotContain(Secret, response, StringComparison.Ordinal);
        Assert.DoesNotContain(TestHost.IndexHtml, response, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Media_is_read_only(string method)
    {
        await using var host = await StartWithMediaAsync();

        var (status, response) = await RawHttp.SendAsync(host.Endpoints.Port, method, "/media/thumbnails/source-1-0123abcd-480.jpg");

        Assert.InRange(status, 400, 499);
        Assert.DoesNotContain(Thumbnail, response, StringComparison.Ordinal);
        Assert.Equal(Thumbnail, await File.ReadAllTextAsync(Path.Combine(host.DataFolder, "Cache", "thumbnails", "source-1-0123abcd-480.jpg"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Previews_come_from_the_preview_source()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47];
        await using var host = await TestHost.StartAsync(previews: new OnePreview("strip-preview", png));

        using var found = await host.Http.GetAsync(new Uri("preview/strip-preview.png?v=2", UriKind.Relative), TestContext.Current.CancellationToken);
        var (missing, _) = await RawHttp.SendAsync(host.Endpoints.Port, "GET", "/preview/other.png");
        var (wrongSuffix, _) = await RawHttp.SendAsync(host.Endpoints.Port, "GET", "/preview/strip-preview.jpg");
        var (nested, _) = await RawHttp.SendAsync(host.Endpoints.Port, "GET", "/preview/a/strip-preview.png");
        var (empty, _) = await RawHttp.SendAsync(host.Endpoints.Port, "GET", "/preview/.png");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal("image/png", found.Content.Headers.ContentType?.MediaType);
        Assert.Equal(png, await found.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal([404, 404, 404, 404], [missing, wrongSuffix, nested, empty]);
    }

    [Fact]
    public async Task The_default_preview_source_has_no_previews()
    {
        await using var host = await TestHost.StartAsync();

        var (status, _) = await RawHttp.SendAsync(host.Endpoints.Port, "GET", "/preview/anything.png");

        Assert.Equal(404, status);
    }

    private static async Task<TestHost> StartWithMediaAsync()
    {
        var host = await TestHost.StartAsync();
        var cancellation = TestContext.Current.CancellationToken;
        var cycle = Directory.CreateDirectory(Path.Combine(host.DataFolder, "Events", "event-1", "GuestCycles", "cycle-1"));
        var thumbnails = Directory.CreateDirectory(Path.Combine(host.DataFolder, "Cache", "thumbnails"));
        await File.WriteAllTextAsync(Path.Combine(cycle.FullName, "strip.jpg"), Strip, cancellation);
        await File.WriteAllTextAsync(Path.Combine(thumbnails.FullName, "source-1-0123abcd-480.jpg"), Thumbnail, cancellation);
        await File.WriteAllTextAsync(Path.Combine(host.DataFolder, "fotohavn.db"), Secret, cancellation);
        await File.WriteAllTextAsync(Path.Combine(host.DataFolder, "Events", "event-1", "fotohavn.db"), Secret, cancellation);
        return host;
    }

    private sealed class OnePreview(string key, byte[] png) : IPreviewSource
    {
        public bool TryGet(string requested, out ReadOnlyMemory<byte> found)
        {
            found = requested == key ? png : ReadOnlyMemory<byte>.Empty;
            return requested == key;
        }
    }
}
