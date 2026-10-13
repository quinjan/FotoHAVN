using System.Net;
using System.Text.Json;
using FotoHavn.Core.Displays;

namespace FotoHavn.Host.Headless.Tests;

public sealed class StaticUiTests
{
    [Theory]
    [InlineData("")]
    [InlineData("?token=0123")]
    [InlineData("index.html")]
    [InlineData("operator/events")]
    public async Task Serves_the_ui_and_falls_back_to_index_html(string path)
    {
        await using var host = await TestHost.StartAsync();

        using var response = await host.Http.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TestHost.IndexHtml, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Serves_assets_with_their_content_type()
    {
        await using var host = await TestHost.StartAsync();

        using var response = await host.Http.GetAsync(new Uri("assets/app.js", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TestHost.AppJs, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("GET", "/assets/missing.js")]
    [InlineData("GET", "/media/anything")]
    [InlineData("GET", "/preview/anything")]
    [InlineData("GET", "/bridge/anything")]
    [InlineData("POST", "/operator/events")]
    [InlineData("GET", "/../../Data/fotohavn.db")]
    public async Task Does_not_fall_back_for_missing_files_other_routes_or_writes(string method, string target)
    {
        await using var host = await TestHost.StartAsync();

        var (status, response) = await RawHttp.SendAsync(host.Endpoints.Port, method, target);

        Assert.Equal(404, status);
        Assert.DoesNotContain(TestHost.IndexHtml, response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Head_of_the_page_answers_without_a_body()
    {
        await using var host = await TestHost.StartAsync();

        var (status, response) = await RawHttp.SendAsync(host.Endpoints.Port, "HEAD", "/?token=0123");

        Assert.Equal(200, status);
        Assert.DoesNotContain(TestHost.IndexHtml, response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_built_ui_the_root_says_so()
    {
        await using var host = await TestHost.StartAsync(withUi: false);

        var body = await host.Http.GetStringAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Contains("not built", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("fotohavn.attacker.test")]
    [InlineData("127.0.0.2.nip.io")]
    public async Task Requests_for_other_hosts_are_refused(string hostHeader)
    {
        await using var host = await TestHost.StartAsync();

        var (status, response) = await RawHttp.SendAsync(host.Endpoints.Port, "GET", "/", hostHeader);

        Assert.Equal(400, status);
        Assert.DoesNotContain(TestHost.IndexHtml, response, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("[::1]")]
    public async Task Loopback_host_names_are_allowed(string hostHeader)
    {
        await using var host = await TestHost.StartAsync();

        var (status, _) = await RawHttp.SendAsync(host.Endpoints.Port, "GET", "/", hostHeader);

        Assert.Equal(200, status);
    }

    [Fact]
    public async Task Startup_prints_ready_to_open_urls_and_one_json_line()
    {
        await using var host = await TestHost.StartAsync();
        using var output = new StringWriter();

        host.App.WriteStartup(output, host.Endpoints);

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var port = host.Endpoints.Port;
        Assert.Contains($"operator: http://127.0.0.1:{port}/?token={host.App.Tokens.Operator}", lines);
        Assert.Contains($"guest: http://127.0.0.1:{port}/?token={host.App.Tokens.Guest}", lines);
        using var json = JsonDocument.Parse(lines[^1]);
        var listening = json.RootElement;
        Assert.Equal("listening", listening.GetProperty("event").GetString());
        Assert.Equal(port, listening.GetProperty("port").GetInt32());
        Assert.Equal($"http://127.0.0.1:{port}/", listening.GetProperty("url").GetString());
        Assert.Equal($"ws://127.0.0.1:{port}/bridge", listening.GetProperty("bridgeUrl").GetString());
        Assert.Equal($"http://127.0.0.1:{port}/?token={host.App.Tokens.Operator}", listening.GetProperty("operatorUrl").GetString());
        Assert.Equal($"http://127.0.0.1:{port}/?token={host.App.Tokens.Guest}", listening.GetProperty("guestUrl").GetString());
        Assert.Equal(host.App.Tokens.Operator, listening.GetProperty("operatorToken").GetString());
        Assert.Equal(host.App.Tokens.Guest, listening.GetProperty("guestToken").GetString());
        Assert.Equal($"http://127.0.0.1:{port}/media/", listening.GetProperty("mediaUrl").GetString());
        Assert.Equal($"http://127.0.0.1:{port}/preview/", listening.GetProperty("previewUrl").GetString());
        Assert.Equal(host.DataFolder, listening.GetProperty("dataFolder").GetString());
    }

    [Fact]
    public async Task The_real_composition_starts_and_serves_the_ui()
    {
        var root = TestHost.NewScratchFolder();
        var ui = Directory.CreateDirectory(Path.Combine(root, "ui")).FullName;
        await File.WriteAllTextAsync(Path.Combine(ui, "index.html"), TestHost.IndexHtml, TestContext.Current.CancellationToken);
        var settings = new HeadlessSettings(Path.Combine(root, "Data"), ui, 0) { ConfigureLogging = logging => logging.ClearProviders() };

        await using (var app = HeadlessApp.Create(settings))
        {
            var endpoints = await app.StartAsync(TestContext.Current.CancellationToken);
            using var http = new HttpClient();
            var body = await http.GetStringAsync(endpoints.OperatorUrl, TestContext.Current.CancellationToken);

            Assert.Equal(TestHost.IndexHtml, body);
            Assert.True(Directory.Exists(settings.DataFolder));
            Assert.Equal("fake-primary", app.HostServices.GetOperatorDisplayId());
            Assert.IsType<FotoHavn.Core.Brain>(app.Brain);
        }

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void The_data_folder_must_be_fully_qualified()
    {
        Assert.Throws<ArgumentException>(() => HeadlessApp.Create(new HeadlessSettings("Data", null, 0)));
    }

    [Fact]
    public void The_default_displays_are_a_primary_and_a_guest_display_side_by_side()
    {
        Assert.Collection(
            HeadlessSettings.DefaultDisplays,
            primary => Assert.Equal(("fake-primary", new DisplayRect(0, 0, 1920, 1080), 1.0, true), (primary.Id, primary.Bounds, primary.Scale, primary.IsPrimary)),
            guest => Assert.Equal(("fake-guest", new DisplayRect(1920, 0, 1920, 1080), 1.0, false), (guest.Id, guest.Bounds, guest.Scale, guest.IsPrimary)));
    }
}
