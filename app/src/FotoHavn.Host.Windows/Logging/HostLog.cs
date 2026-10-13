using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Logging;

internal static partial class HostLog
{
    [LoggerMessage(EventId = 1, EventName = "started", Level = LogLevel.Information, Message = "FOTOHAVN {Version} started; Data folder {DataFolder}; E2E hooks compiled {HooksCompiled}")]
    public static partial void Started(ILogger logger, string version, string dataFolder, bool hooksCompiled);

    [LoggerMessage(EventId = 2, EventName = "ready", Level = LogLevel.Information, Message = "UI signalled ready")]
    public static partial void UiReady(ILogger logger);

    [LoggerMessage(EventId = 3, EventName = "uiNotBuilt", Level = LogLevel.Warning, Message = "The FOTOHAVN UI is not built: {IndexFile} is missing")]
    public static partial void UiNotBuilt(ILogger logger, string indexFile);

    [LoggerMessage(EventId = 4, EventName = "webViewFailed", Level = LogLevel.Error, Message = "WebView2 failed to start")]
    public static partial void WebViewFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, EventName = "webMessageRejected", Level = LogLevel.Warning, Message = "Rejected a web message from {Source}")]
    public static partial void WebMessageRejected(ILogger logger, string source);

    [LoggerMessage(EventId = 6, EventName = "exitRequested", Level = LogLevel.Information, Message = "Exit requested by the test.exit hook")]
    public static partial void ExitRequested(ILogger logger);

    [LoggerMessage(EventId = 7, EventName = "exited", Level = LogLevel.Information, Message = "FOTOHAVN exited with code {ExitCode}")]
    public static partial void Exited(ILogger logger, int exitCode);
}
