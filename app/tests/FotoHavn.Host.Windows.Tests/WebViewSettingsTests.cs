namespace FotoHavn.Host.Windows.Tests;

public sealed class WebViewSettingsTests
{
    [Fact]
    public void Field_build_never_opens_a_remote_debugging_port()
    {
        Assert.Null(WebViewSettings.AdditionalBrowserArguments(new HostOptions(null, 9333, null)));
    }

    [Fact]
    public void Start_url_is_on_the_app_virtual_host()
    {
        Assert.Equal("https://app.fotohavn/index.html", WebViewSettings.StartUri.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://app.fotohavn/index.html", true)]
    [InlineData("https://APP.FOTOHAVN/", true)]
    [InlineData("http://app.fotohavn/index.html", false)]
    [InlineData("https://example.com/", false)]
    [InlineData("about:blank", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Accepts_messages_only_from_the_app_origin(string? source, bool expected)
    {
        Assert.Equal(expected, WebViewSettings.IsAppOrigin(source));
    }
}
