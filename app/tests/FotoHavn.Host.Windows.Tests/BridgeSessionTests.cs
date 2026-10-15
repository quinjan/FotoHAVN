using FotoHavn.Core.Camera;
using FotoHavn.Host.Windows.Bridge;
using FotoHavn.Host.Windows.Preview;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Host.Windows.Tests;

public sealed class BridgeSessionTests : IDisposable
{
    private const string AppSource = "https://app.fotohavn/index.html";

    private readonly FakeBrain _brain = new();
    private readonly Queue<Action> _uiQueue = new();
    private readonly List<string> _posted = [];
    private readonly RecordingLogger _logger = new();

    [Theory]
    [InlineData(Role.Operator)]
    [InlineData(Role.Guest)]
    public void Connects_once_with_the_role_the_host_assigned(Role role)
    {
        using var session = CreateSession(role);

        var connection = Assert.Single(_brain.Connections);
        Assert.Equal(role, connection.Role);
        Assert.Equal(role, session.Role);
    }

    [Fact]
    public void A_frame_ack_goes_to_the_frame_channel_and_never_to_the_brain()
    {
        var acks = new List<string>();
        using var session = CreateSession(Role.Operator, text => text.Contains("frameAck", StringComparison.Ordinal) && Record(acks, text));
        const string ack = """{"type":"frameAck","seq":42}""";

        session.Receive(AppSource, ack);
        session.Receive(AppSource, """{"type":"hello","protocolVersion":8}""");

        Assert.Equal([ack], acks);
        Assert.Equal(["""{"type":"hello","protocolVersion":8}"""], _brain.Connections[0].Received);
    }

    [Fact]
    public void A_frame_ack_posted_as_a_string_is_unwrapped_before_the_channel_sees_it()
    {
        var seen = new List<string>();
        using var session = CreateSession(Role.Guest, text => Record(seen, text));

        session.Receive(AppSource, "\"{\\\"type\\\":\\\"frameAck\\\",\\\"seq\\\":1}\"");

        Assert.Equal(["""{"type":"frameAck","seq":1}"""], seen);
        Assert.Empty(_brain.Connections[0].Received);
    }

    [Fact]
    public void A_message_the_channel_declines_reaches_the_brain()
    {
        using var session = CreateSession(Role.Operator, _ => false);
        const string malformed = """{"type":"frameAck","seq":-1}""";

        session.Receive(AppSource, malformed);

        Assert.Equal([malformed], _brain.Connections[0].Received);
    }

    [Fact]
    public void A_frame_ack_from_another_origin_is_dropped_before_the_channel()
    {
        var seen = new List<string>();
        using var session = CreateSession(Role.Operator, text => Record(seen, text));

        session.Receive("https://example.com/", """{"type":"frameAck","seq":1}""");

        Assert.Empty(seen);
        Assert.Empty(_brain.Connections[0].Received);
    }

    [Fact]
    public void The_ack_of_a_posted_frame_frees_the_channels_slot_through_the_bridge()
    {
        var uiWork = new Queue<Action>();
        var surface = new FakeSurface();
        var channel = new SharedBufferPreviewChannel(uiWork.Enqueue, TimeProvider.System, _logger);
        channel.Attach(Role.Guest, surface);
        using var session = CreateSession(Role.Guest, text => channel.TryHandleAck(Role.Guest, text));
        channel.TryPost(PreviewTarget.Guest, new PreviewFrame(5, 2, 2, new byte[16], TimeSpan.Zero));

        session.Receive(AppSource, """{"type":"frameAck","seq":5}""");

        Assert.Equal(1, channel.GetStats(PreviewTarget.Guest).Delivered);
        Assert.Empty(_brain.Connections[0].Received);
    }

    [Fact]
    public void Passes_app_messages_to_the_brain_unchanged()
    {
        using var session = CreateSession(Role.Guest);
        const string hello = """{"type":"hello","protocolVersion":1}""";

        session.Receive(AppSource, hello);

        Assert.Equal([hello], _brain.Connections[0].Received);
    }

    [Fact]
    public void A_page_cannot_change_its_role()
    {
        using var session = CreateSession(Role.Guest);
        const string claim = """{"type":"hello","protocolVersion":1,"role":"operator"}""";

        session.Receive(AppSource, claim);

        // The message reaches the brain as is (the brain answers invalidEnvelope); the connection keeps the host's role.
        var connection = Assert.Single(_brain.Connections);
        Assert.Equal(Role.Guest, connection.Role);
        Assert.Equal([claim], connection.Received);
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("http://app.fotohavn/index.html")]
    [InlineData("https://media.fotohavn/thumbnails/a.jpg")]
    [InlineData("about:blank")]
    [InlineData(null)]
    public void Drops_messages_from_other_origins(string? source)
    {
        using var session = CreateSession(Role.Operator);

        session.Receive(source, """{"type":"hello","protocolVersion":1}""");

        Assert.Empty(_brain.Connections[0].Received);
        Assert.Contains(_logger.Entries, entry => entry.EventId.Name == "webMessageRejected");
    }

    [Fact]
    public void Unwraps_messages_posted_as_strings()
    {
        using var session = CreateSession(Role.Operator);

        session.Receive(AppSource, "\"{\\\"type\\\":\\\"hello\\\",\\\"protocolVersion\\\":1}\"");

        Assert.Equal(["""{"type":"hello","protocolVersion":1}"""], _brain.Connections[0].Received);
    }

    [Fact]
    public void Brain_messages_are_posted_on_the_UI_thread_in_order()
    {
        using var session = CreateSession(Role.Operator);
        var connection = _brain.Connections[0];

        connection.Send("""{"type":"commandResult","id":"1"}""");
        connection.Send("""{"type":"commandResult","id":"2"}""");

        Assert.Empty(_posted);
        RunUi();
        Assert.Equal(["""{"type":"commandResult","id":"1"}""", """{"type":"commandResult","id":"2"}"""], _posted);
    }

    [Fact]
    public void Logs_ready_when_a_welcome_passes()
    {
        using var session = CreateSession(Role.Guest);

        _brain.Connections[0].Send("""{"type":"welcome","role":"guest","protocolVersion":1,"revision":1,"presentation":{}}""");
        RunUi();

        Assert.Contains(_logger.Entries, entry => entry.EventId.Name == "ready" && entry.Message.Contains("Guest", StringComparison.Ordinal));
    }

    [Fact]
    public void Dispose_closes_the_connection_and_drops_later_traffic()
    {
        var session = CreateSession(Role.Guest);
        var connection = _brain.Connections[0];
        connection.Send("queued before dispose");

        session.Dispose();
        session.Dispose();
        connection.Send("sent after dispose");
        RunUi();
        session.Receive(AppSource, """{"type":"hello","protocolVersion":1}""");

        Assert.True(connection.Disposed);
        Assert.True(session.IsClosed);
        Assert.Empty(_posted);
        Assert.Empty(_uiQueue);
        Assert.Empty(connection.Received);
    }

    [Fact]
    public void A_failed_post_is_dropped_and_logged()
    {
        using var session = new BridgeSession(
            _brain,
            Role.Operator,
            _uiQueue.Enqueue,
            _ => throw new InvalidOperationException("WebView2 is gone"),
            _logger);

        _brain.Connections[0].Send("{}");
        RunUi();

        Assert.Contains(_logger.Entries, entry => entry.EventId.Name == "webMessageDropped");
    }

    [Theory]
    [InlineData("""{"type":"hello"}""", """{"type":"hello"}""")]
    [InlineData("  \"text\"", "text")]
    [InlineData("\"unterminated", "\"unterminated")]
    [InlineData("42", "42")]
    [InlineData("null", "null")]
    public void Web_message_text_is_unwrapped_only_for_string_literals(string webMessageAsJson, string expected)
    {
        Assert.Equal(expected, WebMessageText.ToProtocolText(webMessageAsJson));
    }

    public void Dispose() => _brain.Dispose();

    private BridgeSession CreateSession(Role role, Func<string, bool>? tryHandleFrameAck = null) =>
        new(_brain, role, _uiQueue.Enqueue, _posted.Add, _logger, tryHandleFrameAck);

    private static bool Record(List<string> sink, string text)
    {
        sink.Add(text);
        return true;
    }

    private void RunUi()
    {
        while (_uiQueue.TryDequeue(out var work))
        {
            work();
        }
    }
}
