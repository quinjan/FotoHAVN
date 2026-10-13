using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using FotoHavn.Core;
using FotoHavn.Protocol;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Headless;

/// <summary>
/// One WebSocket client and its brain connection. Text frames in are reassembled into one message each (at most
/// <see cref="ProtocolLimits.MaxMessageBytes"/> UTF-8 bytes) and handed to <see cref="IBrainConnection.Receive"/>; the
/// brain's messages go out through an ordered queue with a single writer, which also sends the close frame. A message
/// over the limit is answered with <c>error payloadTooLarge</c> and the socket stays open. Binary frames and invalid
/// UTF-8 close the socket; the WebSocket stack itself rejects invalid UTF-8 (see <see cref="LingerAfterProtocolErrorAsync"/>).
/// </summary>
internal sealed partial class BridgeSession
{
    internal static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    private const int ReceiveBufferBytes = 16 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly string PayloadTooLargeReply = ProtocolJson.Serialize(new ErrorMessage(null, EnvelopeError.PayloadTooLarge));

    private readonly WebSocket _socket;
    private readonly ILogger _logger;
    private readonly Channel<string> _outgoing = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private int _closeRequested;
    private int _abortedByHost;
    private WebSocketCloseStatus _closeStatus;
    private string? _closeDescription;

    public BridgeSession(WebSocket socket, Role role, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(logger);
        _socket = socket;
        Role = role;
        _logger = logger;
    }

    public Role Role { get; }

    /// <summary>Queues one message for the client. Messages queued after the session started closing are dropped.</summary>
    public void Send(string json) => _outgoing.Writer.TryWrite(json);

    /// <summary>Ends the connection at once without a close handshake (the headless <c>test.crashUi</c>).</summary>
    public void Abort()
    {
        Volatile.Write(ref _abortedByHost, 1);
        _socket.Abort();
    }

    /// <summary>
    /// Starts a server-initiated close: the writer sends what is queued, then the close frame. The first request wins.
    /// </summary>
    public void RequestClose(WebSocketCloseStatus status, string? description)
    {
        if (Interlocked.Exchange(ref _closeRequested, 1) != 0)
        {
            return;
        }

        _closeStatus = status;
        _closeDescription = status == WebSocketCloseStatus.Empty ? null : description;
        _outgoing.Writer.TryComplete();
    }

    /// <summary>
    /// Connects to the brain and runs the session until the socket closes or aborts. <paramref name="clientGone"/> fires
    /// when the client's connection ends.
    /// </summary>
    public async Task RunAsync(IBrain brain, CancellationToken stopping, CancellationToken clientGone)
    {
        ArgumentNullException.ThrowIfNull(brain);

        var writer = WriteLoopAsync();
        IBrainConnection? connection = null;
        try
        {
            connection = brain.Connect(Role, Send);
        }
#pragma warning disable CA1031 // A brain that can't connect closes this socket; the host keeps serving.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogConnectFailed(_logger, exception, Role);
            RequestClose(WebSocketCloseStatus.InternalServerError, "The brain refused the connection.");
        }

        using var stoppingRegistration = stopping.Register(
            () => RequestClose(WebSocketCloseStatus.EndpointUnavailable, "The headless host is stopping."));
        using var linger = CancellationTokenSource.CreateLinkedTokenSource(stopping, clientGone);
        try
        {
            await ReceiveLoopAsync(connection, linger.Token).ConfigureAwait(false);
        }
        finally
        {
            connection?.Dispose();
            _outgoing.Writer.TryComplete();
            await writer.ConfigureAwait(false);
        }
    }

    private bool IsClosing => Volatile.Read(ref _closeRequested) != 0;

    private async Task ReceiveLoopAsync(IBrainConnection? connection, CancellationToken lingerEnd)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferBytes);
        var message = new ArrayBufferWriter<byte>(ReceiveBufferBytes);
        var tooLarge = false;
        try
        {
            while (true)
            {
                ValueWebSocketReceiveResult result;
                try
                {
                    result = await _socket.ReceiveAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
                {
                    if (exception is WebSocketException { InnerException: null } && Volatile.Read(ref _abortedByHost) == 0)
                    {
                        await LingerAfterProtocolErrorAsync(lingerEnd).ConfigureAwait(false);
                    }

                    return;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    RequestClose(_socket.CloseStatus ?? WebSocketCloseStatus.NormalClosure, _socket.CloseStatusDescription);
                    return;
                }

                if (IsClosing || connection is null)
                {
                    continue;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    RequestClose(WebSocketCloseStatus.InvalidMessageType, "FOTOHAVN bridge messages are text frames.");
                    continue;
                }

                if (!tooLarge && (long)message.WrittenCount + result.Count > ProtocolLimits.MaxMessageBytes)
                {
                    tooLarge = true;
                    message.ResetWrittenCount();
                }

                if (!tooLarge)
                {
                    message.Write(buffer.AsSpan(0, result.Count));
                }

                if (!result.EndOfMessage)
                {
                    continue;
                }

                if (tooLarge)
                {
                    Send(PayloadTooLargeReply);
                }
                else if (TryDecode(message.WrittenSpan, out var json))
                {
                    connection.Receive(json);
                }
                else
                {
                    RequestClose(WebSocketCloseStatus.InvalidPayloadData, "FOTOHAVN bridge messages are UTF-8 text.");
                }

                tooLarge = false;
                message.ResetWrittenCount();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var json in _outgoing.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (_socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
                {
                    continue;
                }

                await _socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            if (IsClosing && _socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var timeout = new CancellationTokenSource(CloseTimeout);
                await _socket.CloseOutputAsync(_closeStatus, _closeDescription, timeout.Token).ConfigureAwait(false);
                if (_socket.State == WebSocketState.CloseSent)
                {
                    _ = AbortUnlessClosedAsync();
                }
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            _socket.Abort();
        }
    }

    /// <summary>
    /// The WebSocket stack validates incoming text as UTF-8 and breaks the frame rules itself: it sends the close frame
    /// (1007 for invalid UTF-8), aborts the socket, and throws without an inner exception. Ending the request at once
    /// would reset the TCP connection, and on Windows a reset discards the close frame the client hasn't read yet. So the
    /// request stays open until the client's connection ends, for at most <see cref="CloseTimeout"/>.
    /// </summary>
    private static async Task LingerAfterProtocolErrorAsync(CancellationToken lingerEnd)
    {
        try
        {
            await Task.Delay(CloseTimeout, lingerEnd).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The client's connection ended or the host is stopping: nothing is left to wait for.
        }
    }

    /// <summary>After a server-initiated close, gives the client <see cref="CloseTimeout"/> to answer, then aborts.</summary>
    private async Task AbortUnlessClosedAsync()
    {
        await Task.Delay(CloseTimeout).ConfigureAwait(false);
        if (_socket.State != WebSocketState.Closed)
        {
            _socket.Abort();
        }
    }

    private static bool TryDecode(ReadOnlySpan<byte> bytes, out string json)
    {
        try
        {
            json = StrictUtf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            json = string.Empty;
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The brain refused a {Role} connection.")]
    private static partial void LogConnectFailed(ILogger logger, Exception exception, Role role);
}
