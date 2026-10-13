using System.Net.WebSockets;
using System.Text;

namespace FotoHavn.Host.Headless.Tests;

public sealed class BridgeTests
{
    private const string CommandId = "6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b";
    private static readonly string Hello = $$"""{"type":"hello","protocolVersion":{{ProtocolVersion.Current}}}""";

    [Theory]
    [InlineData(Role.Operator)]
    [InlineData(Role.Guest)]
    public async Task The_token_decides_the_role(Role role)
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(role);

        await client.SendAsync(Hello);
        var welcome = await client.ReceiveMessageAsync();

        Assert.Equal(role, Assert.IsAssignableFrom<IRoleMessage>(welcome).Role);
        Assert.Equal(role, Assert.Single(host.Brain.Connections).Role);
    }

    [Fact]
    public async Task Operator_and_guest_clients_each_get_their_own_role_and_pushes()
    {
        await using var host = await TestHost.StartAsync();
        using var @operator = await host.ConnectAsync(Role.Operator);
        using var guest = await host.ConnectAsync(Role.Guest);

        await @operator.SendAsync(Hello);
        await guest.SendAsync(Hello);
        Assert.Equal(Json(FakeBrain.OperatorWelcome), await @operator.ReceiveAsync());
        Assert.Equal(Json(FakeBrain.GuestWelcome), await guest.ReceiveAsync());

        var guestPresentation = new GuestPresentationMessage(2, FakeBrain.GuestWelcome.Presentation);
        host.Brain.Push(Role.Guest, guestPresentation);
        var operatorPresentation = new OperatorPresentationMessage(2, FakeBrain.OperatorWelcome.Presentation);
        host.Brain.Push(Role.Operator, operatorPresentation);

        Assert.Equal(Json(guestPresentation), await guest.ReceiveAsync());
        Assert.Equal(Json(operatorPresentation), await @operator.ReceiveAsync());
    }

    [Fact]
    public async Task Envelopes_round_trip_in_order_as_text_frames()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Guest);
        var command = $$$"""{"type":"command","id":"{{{CommandId}}}","name":"OpenNewEvent","payload":{}}""";
        var test = $$"""{"type":"test","id":"{{CommandId}}","name":"test.getDiagnostics"}""";

        await client.SendAsync(Hello);
        await client.SendAsync(command);
        await client.SendAsync(test);
        await client.SendAsync("not json");

        Assert.Equal(Json(FakeBrain.GuestWelcome), await client.ReceiveAsync());
        Assert.Equal(Json(CommandResultMessage.Rejected(CommandId, RejectReason.WrongRole)), await client.ReceiveAsync());
        var testResult = Assert.IsType<TestResultMessage>(await client.ReceiveMessageAsync());
        Assert.Equal(TestHookError.HooksUnavailable, testResult.Error);
        Assert.Equal(Json(new ErrorMessage(null, EnvelopeError.InvalidJson)), await client.ReceiveAsync());
        Assert.Equal([Hello, command, test, "not json"], Assert.Single(host.Brain.Connections).Received);
    }

    [Fact]
    public async Task A_message_split_into_frames_reaches_the_brain_whole()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);

        await client.SendFragmentsAsync(Encoding.UTF8.GetBytes(Hello), frameBytes: 5);

        Assert.Equal(Json(FakeBrain.OperatorWelcome), await client.ReceiveAsync());
        Assert.Equal([Hello], Assert.Single(host.Brain.Connections).Received);
    }

    [Fact]
    public async Task A_message_of_exactly_the_limit_is_delivered()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);
        var json = Hello.PadRight(ProtocolLimits.MaxMessageBytes);

        await client.SendFragmentsAsync(Encoding.UTF8.GetBytes(json), frameBytes: 64 * 1024);

        Assert.Equal(Json(FakeBrain.OperatorWelcome), await client.ReceiveAsync());
        Assert.Equal(ProtocolLimits.MaxMessageBytes, Assert.Single(Assert.Single(host.Brain.Connections).Received).Length);
    }

    [Theory]
    [InlineData(64 * 1024)]
    [InlineData(int.MaxValue)]
    public async Task A_message_over_the_limit_is_refused_and_the_socket_stays_open(int frameBytes)
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);
        var oversize = Encoding.UTF8.GetBytes(Hello.PadRight(ProtocolLimits.MaxMessageBytes + 1));

        await client.SendFragmentsAsync(oversize, frameBytes);
        Assert.Equal(Json(new ErrorMessage(null, EnvelopeError.PayloadTooLarge)), await client.ReceiveAsync());

        await client.SendAsync(Hello);
        Assert.Equal(Json(FakeBrain.OperatorWelcome), await client.ReceiveAsync());
        Assert.Equal([Hello], Assert.Single(host.Brain.Connections).Received);
    }

    [Fact]
    public async Task The_limit_counts_utf8_bytes()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);
        var multiByte = new string('ø', (ProtocolLimits.MaxMessageBytes / 2) + 1);

        await client.SendAsync(multiByte);

        Assert.Equal(Json(new ErrorMessage(null, EnvelopeError.PayloadTooLarge)), await client.ReceiveAsync());
        Assert.Empty(Assert.Single(host.Brain.Connections).Received);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task A_missing_or_unknown_token_is_refused_with_401(string? token)
    {
        await using var host = await TestHost.StartAsync();

        Assert.Equal(401, await BridgeClient.ConnectStatusAsync(host.Endpoints.BridgeUrl, token));
        Assert.Empty(host.Brain.Connections);
    }

    [Fact]
    public async Task Tokens_are_case_sensitive_and_exact()
    {
        await using var host = await TestHost.StartAsync();
        var token = host.App.Tokens.Operator;

        Assert.Equal(401, await BridgeClient.ConnectStatusAsync(host.Endpoints.BridgeUrl, token.ToUpperInvariant()));
        Assert.Equal(401, await BridgeClient.ConnectStatusAsync(host.Endpoints.BridgeUrl, token[..^1]));
        Assert.Equal(401, await BridgeClient.ConnectStatusAsync(host.Endpoints.BridgeUrl, token + "0"));
        Assert.Empty(host.Brain.Connections);
    }

    [Fact]
    public async Task Two_tokens_are_refused()
    {
        await using var host = await TestHost.StartAsync();
        var url = new Uri($"{host.Endpoints.BridgeUrl}?token={host.App.Tokens.Operator}&token={host.App.Tokens.Guest}");

        Assert.Equal(401, await BridgeClient.ConnectStatusAsync(url, token: null));
    }

    [Fact]
    public async Task A_plain_http_request_to_the_bridge_is_not_upgraded()
    {
        await using var host = await TestHost.StartAsync();

        using var withToken = await host.Http.GetAsync(new Uri($"bridge?token={host.App.Tokens.Guest}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var withoutToken = await host.Http.GetAsync(new Uri("bridge", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(400, (int)withToken.StatusCode);
        Assert.Equal(401, (int)withoutToken.StatusCode);
    }

    [Theory]
    [InlineData("https://example.com", 403)]
    [InlineData("null", 403)]
    [InlineData("http://localhost:5173", 101)]
    [InlineData("http://127.0.0.1:1234", 101)]
    public async Task Only_loopback_origins_may_connect(string origin, int status)
    {
        await using var host = await TestHost.StartAsync();

        Assert.Equal(status, await BridgeClient.ConnectStatusAsync(host.Endpoints.BridgeUrl, host.App.Tokens.Operator, origin));
    }

    [Fact]
    public async Task A_client_close_is_answered_and_disposes_the_connection()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Guest);
        await client.SendAsync(Hello);
        await client.ReceiveAsync();

        using var timeout = new CancellationTokenSource(BridgeClient.Timeout);
        await client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", timeout.Token);

        Assert.Equal(WebSocketState.Closed, client.Socket.State);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, client.Socket.CloseStatus);
        await Assert.Single(host.Brain.Connections).WhenDisposed.WaitAsync(BridgeClient.Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task An_aborted_client_disposes_the_connection()
    {
        await using var host = await TestHost.StartAsync();
        var client = await host.ConnectAsync(Role.Operator);
        await client.SendAsync(Hello);
        await client.ReceiveAsync();

        client.Socket.Abort();
        client.Dispose();

        await Assert.Single(host.Brain.Connections).WhenDisposed.WaitAsync(BridgeClient.Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Binary_frames_close_the_socket()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);

        await client.SendFragmentsAsync(Encoding.UTF8.GetBytes(Hello), int.MaxValue, WebSocketMessageType.Binary);

        Assert.Equal(WebSocketCloseStatus.InvalidMessageType, await client.ReceiveCloseAsync());
        Assert.Empty(Assert.Single(host.Brain.Connections).Received);
        await host.Brain.Connections[0].WhenDisposed.WaitAsync(BridgeClient.Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Invalid_utf8_closes_the_socket()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);

        await client.SendFragmentsAsync([0x7B, 0xFF, 0x7D], int.MaxValue);

        Assert.Equal(WebSocketCloseStatus.InvalidPayloadData, await client.ReceiveCloseAsync());
        Assert.Empty(Assert.Single(host.Brain.Connections).Received);
    }

    [Fact]
    public async Task Invalid_utf8_close_frame_reaches_a_client_that_reads_late()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);

        await client.SendFragmentsAsync([0x7B, 0xFF, 0x7D], int.MaxValue);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        Assert.Equal(WebSocketCloseStatus.InvalidPayloadData, await client.ReceiveCloseAsync());
    }

    [Fact]
    public async Task Binary_close_frame_reaches_a_client_that_reads_late()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);

        await client.SendFragmentsAsync(Encoding.UTF8.GetBytes(Hello), int.MaxValue, WebSocketMessageType.Binary);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        Assert.Equal(WebSocketCloseStatus.InvalidMessageType, await client.ReceiveCloseAsync());
    }

    [Fact]
    public async Task A_brain_that_refuses_the_connection_closes_the_socket()
    {
        await using var host = await TestHost.StartAsync(failConnect: true);
        using var client = await host.ConnectAsync(Role.Operator);

        Assert.Equal(WebSocketCloseStatus.InternalServerError, await client.ReceiveCloseAsync());
    }

    [Fact]
    public async Task Stopping_the_host_closes_every_socket_and_disposes_the_brain()
    {
        var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);
        await client.SendAsync(Hello);
        await client.ReceiveAsync();

        var disposing = host.DisposeAsync();
        Assert.Equal(WebSocketCloseStatus.EndpointUnavailable, await client.ReceiveCloseAsync());
        using var timeout = new CancellationTokenSource(BridgeClient.Timeout);
        await client.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
        await disposing;

        Assert.True(host.Brain.Connections[0].IsDisposed);
        Assert.True(host.Brain.Disposed);
    }

    [Fact]
    public async Task The_sessions_registry_tracks_and_aborts_a_role()
    {
        await using var host = await TestHost.StartAsync();
        using var guest = await host.ConnectAsync(Role.Guest);
        using var @operator = await host.ConnectAsync(Role.Operator);
        await guest.SendAsync(Hello);
        await guest.ReceiveAsync();
        await @operator.SendAsync(Hello);
        await @operator.ReceiveAsync();

        Assert.Equal(1, host.App.Sessions.Count(Role.Guest));
        Assert.Equal(1, host.App.Sessions.AbortRole(Role.Guest));

        await host.Brain.Connections.Single(c => c.Role == Role.Guest).WhenDisposed.WaitAsync(BridgeClient.Timeout, TestContext.Current.CancellationToken);
        Assert.False(host.Brain.Connections.Single(c => c.Role == Role.Operator).IsDisposed);
        await @operator.SendAsync(Hello);
        Assert.Equal(Json(FakeBrain.OperatorWelcome), await @operator.ReceiveAsync());
    }

    private static string Json(ServerMessage message) => ProtocolJson.Serialize(message);
}
