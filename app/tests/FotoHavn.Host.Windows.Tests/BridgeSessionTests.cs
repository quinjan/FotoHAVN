using FotoHavn.Host.Windows.Bridge;
using FotoHavn.Protocol;

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

    private BridgeSession CreateSession(Role role) =>
        new(_brain, role, _uiQueue.Enqueue, _posted.Add, _logger);

    private void RunUi()
    {
        while (_uiQueue.TryDequeue(out var work))
        {
            work();
        }
    }
}
