using FotoHavn.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace FotoHavn.Host.Windows.Logging;

internal static partial class HostLog
{
    [LoggerMessage(EventId = 1, EventName = "started", Level = LogLevel.Information, Message = "FOTOHAVN {Version} started; Data folder {DataFolder}; E2E hooks compiled {HooksCompiled}")]
    public static partial void Started(ILogger logger, string version, string dataFolder, bool hooksCompiled);

    [LoggerMessage(EventId = 2, EventName = "ready", Level = LogLevel.Information, Message = "UI ready ({Role}): the brain welcomed the page")]
    public static partial void UiReady(ILogger logger, Role role);

    [LoggerMessage(EventId = 3, EventName = "uiNotBuilt", Level = LogLevel.Warning, Message = "The FOTOHAVN UI is not built: {IndexFile} is missing")]
    public static partial void UiNotBuilt(ILogger logger, string indexFile);

    [LoggerMessage(EventId = 4, EventName = "webViewFailed", Level = LogLevel.Error, Message = "WebView2 failed to start ({Role})")]
    public static partial void WebViewFailed(ILogger logger, Role role, Exception exception);

    [LoggerMessage(EventId = 5, EventName = "webMessageRejected", Level = LogLevel.Warning, Message = "Rejected a web message ({Role}) from {Source}")]
    public static partial void WebMessageRejected(ILogger logger, Role role, string source);

    [LoggerMessage(EventId = 6, EventName = "exitRequested", Level = LogLevel.Information, Message = "Exit requested by the test.exit hook")]
    public static partial void ExitRequested(ILogger logger);

    [LoggerMessage(EventId = 7, EventName = "exited", Level = LogLevel.Information, Message = "FOTOHAVN exited with code {ExitCode}")]
    public static partial void Exited(ILogger logger, int exitCode);

    [LoggerMessage(EventId = 8, EventName = "webMessageDropped", Level = LogLevel.Debug, Message = "Dropped a brain message for the {Role} page")]
    public static partial void WebMessageDropped(ILogger logger, Role role, Exception exception);

    [LoggerMessage(EventId = 9, EventName = "navigationBlocked", Level = LogLevel.Warning, Message = "Blocked navigation ({Role}) to {Uri}")]
    public static partial void NavigationBlocked(ILogger logger, Role role, string uri);

    [LoggerMessage(EventId = 10, EventName = "newWindowBlocked", Level = LogLevel.Warning, Message = "Blocked a new window ({Role}) for {Uri}")]
    public static partial void NewWindowBlocked(ILogger logger, Role role, string uri);

    [LoggerMessage(EventId = 11, EventName = "externalSchemeBlocked", Level = LogLevel.Warning, Message = "Blocked an external scheme ({Role}): {Uri}")]
    public static partial void ExternalSchemeBlocked(ILogger logger, Role role, string uri);

    [LoggerMessage(EventId = 12, EventName = "downloadBlocked", Level = LogLevel.Warning, Message = "Blocked a download ({Role}) of {Uri}")]
    public static partial void DownloadBlocked(ILogger logger, Role role, string uri);

    [LoggerMessage(EventId = 13, EventName = "permissionDenied", Level = LogLevel.Warning, Message = "Denied the {Kind} permission ({Role}) for {Uri}")]
    public static partial void PermissionDenied(ILogger logger, Role role, CoreWebView2PermissionKind kind, string uri);

    [LoggerMessage(EventId = 14, EventName = "processFailed", Level = LogLevel.Error, Message = "WebView2 process failed ({Role}): {Kind}, reason {Reason}, exit code {ExitCode}")]
    public static partial void ProcessFailed(ILogger logger, Role role, CoreWebView2ProcessFailedKind kind, CoreWebView2ProcessFailedReason reason, int exitCode);

    [LoggerMessage(EventId = 15, EventName = "webViewRecreating", Level = LogLevel.Warning, Message = "Recreating the {Role} WebView2 with a new brain connection")]
    public static partial void WebViewRecreating(ILogger logger, Role role);

    [LoggerMessage(EventId = 16, EventName = "recoveryAbandoned", Level = LogLevel.Error, Message = "The {Role} WebView2 failed too often; it stays closed")]
    public static partial void RecoveryAbandoned(ILogger logger, Role role);

    [LoggerMessage(EventId = 17, EventName = "guestScreenOpened", Level = LogLevel.Information, Message = "Guest Screen opened on {DisplayName} [{DisplayId}] at {Bounds}, scale {Scale}; window at {WindowBounds}")]
    public static partial void GuestScreenOpened(ILogger logger, string displayName, string displayId, FotoHavn.Core.Displays.DisplayRect bounds, double scale, FotoHavn.Core.Displays.DisplayRect windowBounds);

    [LoggerMessage(EventId = 18, EventName = "guestScreenClosed", Level = LogLevel.Information, Message = "Guest Screen closed")]
    public static partial void GuestScreenClosed(ILogger logger);

    [LoggerMessage(EventId = 19, EventName = "displays", Level = LogLevel.Information, Message = "{Count} display(s): {Displays}")]
    public static partial void DisplaysRead(ILogger logger, int count, FotoHavn.Host.Windows.Displays.DisplaySnapshot displays);

    [LoggerMessage(EventId = 20, EventName = "displayConfigFailed", Level = LogLevel.Warning, Message = "QueryDisplayConfig failed")]
    public static partial void DisplayConfigFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 21, EventName = "openFolder", Level = LogLevel.Information, Message = "Open folder requested (not implemented until M6): {Path}")]
    public static partial void OpenFolderRequested(ILogger logger, string path);

    [LoggerMessage(EventId = 31, EventName = "identifyOverlays", Level = LogLevel.Information, Message = "Identify overlays requested on {Count} displays (not implemented until M6 wave B2)")]
    public static partial void IdentifyOverlaysRequested(ILogger logger, int count);

    [LoggerMessage(EventId = 32, EventName = "identifyOverlaysHidden", Level = LogLevel.Information, Message = "Identify overlays hide requested (not implemented until M6 wave B2)")]
    public static partial void IdentifyOverlaysHideRequested(ILogger logger);

    [LoggerMessage(EventId = 22, EventName = "brainWorkFailed", Level = LogLevel.Critical, Message = "A brain work item threw")]
    public static partial void BrainWorkFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 23, EventName = "mediaReadFailed", Level = LogLevel.Warning, Message = "Could not read media file {Path}")]
    public static partial void MediaReadFailed(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 24, EventName = "webViewEnvironment", Level = LogLevel.Information, Message = "WebView2 environment: runtime {Runtime}, user data {UserDataFolder}")]
    public static partial void WebViewEnvironment(ILogger logger, string runtime, string userDataFolder);

    [LoggerMessage(EventId = 25, EventName = "crashUi", Level = LogLevel.Warning, Message = "Crashing the {Role} renderer for the test.crashUi hook")]
    public static partial void CrashUiRequested(ILogger logger, Role role);

    [LoggerMessage(EventId = 26, EventName = "guestZoom", Level = LogLevel.Information, Message = "Guest WebView2 zoom factor {ZoomFactor} for display scale {Scale}")]
    public static partial void GuestZoom(ILogger logger, double zoomFactor, double scale);

    [LoggerMessage(EventId = 27, EventName = "guestOpenFailed", Level = LogLevel.Error, Message = "The Guest Screen failed to open")]
    public static partial void GuestOpenFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 28, EventName = "storeDisposeTimedOut", Level = LogLevel.Warning, Message = "The Event store was still finishing background deletion at exit; the next start completes it")]
    public static partial void StoreDisposeTimedOut(ILogger logger);

    [LoggerMessage(EventId = 29, EventName = "previewBufferFailed", Level = LogLevel.Warning, Message = "The {Role} preview frame surface failed; the frame is dropped")]
    public static partial void PreviewBufferFailed(ILogger logger, Role role, Exception exception);

    [LoggerMessage(EventId = 30, EventName = "previewSurfaceRecovered", Level = LogLevel.Information, Message = "The {Role} preview frame surface works again")]
    public static partial void PreviewSurfaceRecovered(ILogger logger, Role role);
}
