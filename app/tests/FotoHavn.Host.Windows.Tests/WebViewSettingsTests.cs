using FotoHavn.Host.Windows.Screens;
using FotoHavn.Host.Windows.WebView;
using FotoHavn.Protocol;
using Microsoft.Web.WebView2.Core;

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
    [InlineData("https://app.fotohavn:443/", true)]
    [InlineData("https://app.fotohavn:8443/", false)]
    [InlineData("https://user@app.fotohavn/", false)]
    [InlineData("http://app.fotohavn/index.html", false)]
    [InlineData("https://example.com/", false)]
    [InlineData("about:blank", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Accepts_messages_only_from_the_app_origin(string? source, bool expected)
    {
        Assert.Equal(expected, WebViewSettings.IsAppOrigin(source));
    }

    [Theory]
    [InlineData("https://app.fotohavn/index.html")]
    [InlineData("https://app.fotohavn/index.html?e2eProtocolVersion=0")]
    [InlineData("https://app.fotohavn/#/guest")]
    public void Allows_navigation_inside_the_app(string uri)
    {
        Assert.True(WebViewSettings.IsAllowedNavigation(uri));
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("https://app.fotohavn.example.com/")]
    [InlineData("https://media.fotohavn/thumbnails/a.jpg")]
    [InlineData("https://preview.fotohavn/a.png")]
    [InlineData("http://app.fotohavn/")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("about:blank")]
    [InlineData("data:text/html,<p>x</p>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("edge://settings")]
    [InlineData("mailto:a@b.c")]
    [InlineData("")]
    [InlineData(null)]
    public void Cancels_every_other_navigation(string? uri)
    {
        Assert.False(WebViewSettings.IsAllowedNavigation(uri));
    }

    [Fact]
    public void Guardrails_turn_off_every_browser_feature_but_web_messages()
    {
        var guardrails = WebViewGuardrails.For(allowDevTools: false);

        Assert.Equal(
            new WebViewGuardrails(
                DefaultContextMenus: false,
                DevTools: false,
                BrowserAcceleratorKeys: false,
                PinchZoom: false,
                ZoomControl: false,
                SwipeNavigation: false,
                StatusBar: false,
                GeneralAutofill: false,
                PasswordAutosave: false,
                HostObjects: false,
                WebMessages: true),
            guardrails);
    }

    [Fact]
    public void DevTools_follow_the_build()
    {
        Assert.True(WebViewGuardrails.For(allowDevTools: true).DevTools);
        Assert.Equal(HostBuild.AllowsDevTools, WebViewGuardrails.Current.DevTools);
        Assert.Equal(HostBuild.IsE2E || HostBuild.IsDebug, HostBuild.AllowsDevTools);
    }

    [Fact]
    public void Guest_window_is_topmost_outside_E2E_builds()
    {
        Assert.False(HostBuild.IsE2E);
        Assert.True(HostBuild.GuestWindowTopmost);
    }

    [Theory]
    [InlineData(Role.Guest, 1.0, 1.0)]
    [InlineData(Role.Guest, 1.25, 0.8)]
    [InlineData(Role.Guest, 1.5, 2.0 / 3.0)]
    [InlineData(Role.Guest, 2.0, 0.5)]
    [InlineData(Role.Guest, 0.0, 1.0)]
    [InlineData(Role.Operator, 1.5, 1.0)]
    public void Guest_zoom_is_the_inverse_of_the_display_scale(Role role, double scale, double expected)
    {
        Assert.Equal(expected, BoothWindow.ZoomFactorFor(role, scale), precision: 10);
    }

    [Theory]
    [InlineData(CoreWebView2ProcessFailedKind.BrowserProcessExited, true)]
    [InlineData(CoreWebView2ProcessFailedKind.RenderProcessExited, true)]
    [InlineData(CoreWebView2ProcessFailedKind.RenderProcessUnresponsive, true)]
    [InlineData(CoreWebView2ProcessFailedKind.FrameRenderProcessExited, false)]
    [InlineData(CoreWebView2ProcessFailedKind.GpuProcessExited, false)]
    [InlineData(CoreWebView2ProcessFailedKind.UtilityProcessExited, false)]
    public void Recreates_the_WebView_only_when_the_page_is_gone(CoreWebView2ProcessFailedKind kind, bool expected)
    {
        Assert.Equal(expected, ProcessFailurePolicy.RequiresRecreate(kind));
    }

    [Fact]
    public void Recovery_budget_allows_a_few_recoveries_per_window()
    {
        var budget = new RecoveryBudget(2, TimeSpan.FromMinutes(1));
        var start = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        Assert.True(budget.TryConsume(start));
        Assert.True(budget.TryConsume(start.AddSeconds(10)));
        Assert.False(budget.TryConsume(start.AddSeconds(20)));
        Assert.True(budget.TryConsume(start.AddSeconds(60)));
        Assert.False(budget.TryConsume(start.AddSeconds(65)));
        Assert.True(RecoveryBudget.Default().TryConsume(start));
    }
}
