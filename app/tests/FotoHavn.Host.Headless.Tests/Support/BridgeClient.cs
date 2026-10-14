using System.Net.WebSockets;
using System.Text;

namespace FotoHavn.Host.Headless.Tests.Support;

/// <summary>A WebSocket client of the bridge, as the UI's WebSocket transport would be.</summary>
internal sealed class BridgeClient : IDisposable
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private BridgeClient(ClientWebSocket socket)
    {
        Socket = socket;
    }

    public ClientWebSocket Socket { get; }

    public static async Task<BridgeClient> ConnectAsync(Uri bridgeUrl, string? token, string? origin = null)
    {
        var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        if (origin is not null)
        {
            socket.Options.SetRequestHeader("Origin", origin);
        }

        var url = token is null ? bridgeUrl : new Uri(bridgeUrl + "?token=" + Uri.EscapeDataString(token));
        try
        {
            using var timeout = Linked();
            await socket.ConnectAsync(url, timeout.Token);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return new BridgeClient(socket);
    }

    /// <summary>The HTTP status of a refused upgrade.</summary>
    public static async Task<int> ConnectStatusAsync(Uri bridgeUrl, string? token, string? origin = null)
    {
        using var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        if (origin is not null)
        {
            socket.Options.SetRequestHeader("Origin", origin);
        }

        var url = token is null ? bridgeUrl : new Uri(bridgeUrl + "?token=" + Uri.EscapeDataString(token));
        try
        {
            using var timeout = Linked();
            await socket.ConnectAsync(url, timeout.Token);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
            return (int)socket.HttpStatusCode;
        }
        catch (WebSocketException)
        {
            return (int)socket.HttpStatusCode;
        }
    }

    public Task SendAsync(string json) => SendFragmentsAsync(Encoding.UTF8.GetBytes(json), int.MaxValue);

    /// <summary>Sends one text message split into frames of at most <paramref name="frameBytes"/> bytes.</summary>
    public async Task SendFragmentsAsync(byte[] utf8, int frameBytes, WebSocketMessageType type = WebSocketMessageType.Text)
    {
        using var timeout = Linked();
        var offset = 0;
        do
        {
            var count = Math.Min(frameBytes, utf8.Length - offset);
            await Socket.SendAsync(utf8.AsMemory(offset, count), type, offset + count == utf8.Length, timeout.Token);
            offset += count;
        }
        while (offset < utf8.Length);
    }

    /// <summary>Receives the next text message; throws when the server closes instead.</summary>
    public async Task<string> ReceiveAsync()
    {
        var (type, text) = await ReceiveAnyAsync();
        return type == WebSocketMessageType.Text
            ? text
            : throw new InvalidOperationException($"Expected a text message, got {type} ({Socket.CloseStatus}).");
    }

    public async Task<ServerMessage> ReceiveMessageAsync() => ServerMessageReader.Read(await ReceiveAsync());

    /// <summary>Receives the next message, which must be binary (a preview frame), and returns its bytes.</summary>
    public async Task<byte[]> ReceiveBinaryAsync()
    {
        using var timeout = Linked();
        using var message = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var result = await Socket.ReceiveAsync(buffer, timeout.Token);
            if (result.MessageType != WebSocketMessageType.Binary)
            {
                throw new InvalidOperationException($"Expected a binary message, got {result.MessageType}.");
            }

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return message.ToArray();
            }
        }
    }

    /// <summary>Receives the next message or close; for a close, returns its type with an empty text.</summary>
    public async Task<(WebSocketMessageType Type, string Text)> ReceiveAnyAsync()
    {
        using var timeout = Linked();
        using var message = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var result = await Socket.ReceiveAsync(buffer, timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return (WebSocketMessageType.Close, string.Empty);
            }

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return (result.MessageType, Encoding.UTF8.GetString(message.ToArray()));
            }
        }
    }

    /// <summary>Waits for the server's close frame, skipping messages, and returns its status.</summary>
    public async Task<WebSocketCloseStatus?> ReceiveCloseAsync()
    {
        while (true)
        {
            var (type, _) = await ReceiveAnyAsync();
            if (type == WebSocketMessageType.Close)
            {
                return Socket.CloseStatus;
            }
        }
    }

    public void Dispose() => Socket.Dispose();

    private static CancellationTokenSource Linked()
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        source.CancelAfter(Timeout);
        return source;
    }
}
