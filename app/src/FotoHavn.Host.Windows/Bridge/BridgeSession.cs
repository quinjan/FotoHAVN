using System.Runtime.InteropServices;
using System.Text.Json;
using FotoHavn.Core;
using FotoHavn.Host.Windows.Logging;
using FotoHavn.Protocol;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Bridge;

/// <summary>
/// One WebView2's bridge to the brain: one <see cref="IBrainConnection"/> with the role the host gave the window.
/// A window opens a new session for every WebView2 it creates (first load, and after <c>ProcessFailed</c>) and
/// disposes the old one, so the brain sees a new connection and the page's <c>hello</c> gets a fresh <c>welcome</c>.
/// </summary>
/// <remarks>
/// Threading: the brain calls <see cref="Send"/> on its executor; it only queues delivery onto the UI thread through
/// <c>postToUi</c> and never blocks. <see cref="Receive"/> and <see cref="Dispose"/> run on the UI thread. Messages for a
/// disposed session are dropped.
/// </remarks>
internal sealed class BridgeSession : IDisposable
{
    private const string WelcomePrefix = """{"type":"welcome",""";

    private readonly Action<Action> _postToUi;
    private readonly Action<string> _postWebMessage;
    private readonly ILogger _logger;
    private readonly IBrainConnection _connection;
    private readonly Func<string, bool>? _tryHandleFrameAck;
    private readonly Action<Role>? _uiReady;
    private volatile bool _closed;

    /// <param name="brain">The brain to connect to.</param>
    /// <param name="role">The window's role. The page can't choose or change it.</param>
    /// <param name="postToUi">Queues work on the window's UI thread (<c>Dispatcher.BeginInvoke</c>).</param>
    /// <param name="postWebMessage">Posts one JSON message to the page (<c>CoreWebView2.PostWebMessageAsJson</c>).</param>
    /// <param name="logger">The host logger.</param>
    /// <param name="tryHandleFrameAck">
    /// Takes a page message that is a <c>frameAck</c> (returns true) before the brain sees it: the frame channel's ack.
    /// Null passes every message on.
    /// </param>
    /// <param name="uiReady">Called on the UI thread when the page's <c>welcome</c> is delivered (the page finished its handshake).</param>
    public BridgeSession(IBrain brain, Role role, Action<Action> postToUi, Action<string> postWebMessage, ILogger logger, Func<string, bool>? tryHandleFrameAck = null, Action<Role>? uiReady = null)
    {
        ArgumentNullException.ThrowIfNull(brain);
        ArgumentNullException.ThrowIfNull(postToUi);
        ArgumentNullException.ThrowIfNull(postWebMessage);
        ArgumentNullException.ThrowIfNull(logger);
        Role = role;
        _postToUi = postToUi;
        _postWebMessage = postWebMessage;
        _logger = logger;
        _tryHandleFrameAck = tryHandleFrameAck;
        _uiReady = uiReady;
        _connection = brain.Connect(role, Send);
    }

    public Role Role { get; }

    public bool IsClosed => _closed;

    /// <summary>Hands one web message from the page to the brain, if it came from the app origin.</summary>
    /// <param name="source">The message's source URI (<c>CoreWebView2WebMessageReceivedEventArgs.Source</c>).</param>
    /// <param name="webMessageAsJson">The message as <c>WebMessageAsJson</c>.</param>
    public void Receive(string? source, string webMessageAsJson)
    {
        ArgumentNullException.ThrowIfNull(webMessageAsJson);
        if (_closed)
        {
            return;
        }

        if (!WebViewSettings.IsAppOrigin(source))
        {
            HostLog.WebMessageRejected(_logger, Role, source ?? string.Empty);
            return;
        }

        var text = WebMessageText.ToProtocolText(webMessageAsJson);
        if (_tryHandleFrameAck?.Invoke(text) == true)
        {
            return;
        }

        _connection.Receive(text);
    }

    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _connection.Dispose();
    }

    private void Send(string json)
    {
        if (!_closed)
        {
            _postToUi(() => Deliver(json));
        }
    }

    private void Deliver(string json)
    {
        if (_closed)
        {
            return;
        }

        if (json.StartsWith(WelcomePrefix, StringComparison.Ordinal))
        {
            HostLog.UiReady(_logger, Role);
            _uiReady?.Invoke(Role);
        }

        try
        {
            _postWebMessage(json);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException or COMException)
        {
            HostLog.WebMessageDropped(_logger, Role, exception);
        }
    }
}

/// <summary>Turns a WebView2 message into the protocol text the brain parses.</summary>
internal static class WebMessageText
{
    /// <summary>
    /// <c>postMessage(object)</c> arrives as that object's JSON and is passed on unchanged. <c>postMessage(string)</c>
    /// arrives as a JSON string literal; its content is the protocol text, so it is unwrapped. Anything unreadable is
    /// passed on as is, and the brain answers it with <c>invalidJson</c> or <c>invalidEnvelope</c>.
    /// </summary>
    public static string ToProtocolText(string webMessageAsJson)
    {
        ArgumentNullException.ThrowIfNull(webMessageAsJson);
        if (!webMessageAsJson.AsSpan().TrimStart().StartsWith("\"", StringComparison.Ordinal))
        {
            return webMessageAsJson;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(webMessageAsJson) ?? webMessageAsJson;
        }
        catch (JsonException)
        {
            return webMessageAsJson;
        }
    }
}
