using Microsoft.Web.WebView2.Core;

namespace FotoHavn.Host.Windows.WebView;

/// <summary>
/// The browser features each window's WebView2 runs with. Both windows use the same guardrails: no context menu,
/// browser accelerator keys, pinch or Ctrl+wheel zoom, swipe navigation, status bar, autofill, password saving, or
/// host objects. DevTools are on only in E2E and Debug builds. Navigation, new windows, downloads, external schemes, and
/// permissions are guarded by event handlers in <see cref="Screens.BoothWindow"/>.
/// </summary>
internal sealed record WebViewGuardrails(
    bool DefaultContextMenus,
    bool DevTools,
    bool BrowserAcceleratorKeys,
    bool PinchZoom,
    bool ZoomControl,
    bool SwipeNavigation,
    bool StatusBar,
    bool GeneralAutofill,
    bool PasswordAutosave,
    bool HostObjects,
    bool WebMessages)
{
    public static WebViewGuardrails For(bool allowDevTools) => new(
        DefaultContextMenus: false,
        DevTools: allowDevTools,
        BrowserAcceleratorKeys: false,
        PinchZoom: false,
        ZoomControl: false,
        SwipeNavigation: false,
        StatusBar: false,
        GeneralAutofill: false,
        PasswordAutosave: false,
        HostObjects: false,
        WebMessages: true);

    /// <summary>The guardrails for this build.</summary>
    public static WebViewGuardrails Current => For(HostBuild.AllowsDevTools);

    public void Apply(CoreWebView2Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.AreDefaultContextMenusEnabled = DefaultContextMenus;
        settings.AreDevToolsEnabled = DevTools;
        settings.AreBrowserAcceleratorKeysEnabled = BrowserAcceleratorKeys;
        settings.IsPinchZoomEnabled = PinchZoom;
        settings.IsZoomControlEnabled = ZoomControl;
        settings.IsSwipeNavigationEnabled = SwipeNavigation;
        settings.IsStatusBarEnabled = StatusBar;
        settings.IsGeneralAutofillEnabled = GeneralAutofill;
        settings.IsPasswordAutosaveEnabled = PasswordAutosave;
        settings.AreHostObjectsAllowed = HostObjects;
        settings.IsWebMessageEnabled = WebMessages;
    }
}
