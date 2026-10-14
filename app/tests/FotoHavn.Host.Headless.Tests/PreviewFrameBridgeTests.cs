using FotoHavn.Core;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Fakes;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Host.Headless.Tests;

/// <summary>The preview frame channel over real WebSockets: binary frames out, text <c>frameAck</c> in.</summary>
public sealed class PreviewFrameBridgeTests
{
    private static readonly string Hello = $$"""{"type":"hello","protocolVersion":{{ProtocolVersion.Current}}}""";

    [Fact]
    public async Task A_frame_reaches_the_operator_socket_as_a_binary_message()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);
        var pixels = Enumerable.Range(0, 5 * 3 * 4).Select(index => (byte)index).ToArray();

        await PostWhenConnectedAsync(host, PreviewTarget.Operator, new PreviewFrame(9, 5, 3, pixels, TimeSpan.FromMilliseconds(1234)));

        var message = await client.ReceiveBinaryAsync();
        Assert.True(PreviewFrameHeader.TryRead(message, out var header));
        Assert.Equal(new PreviewFrameHeader(9, 5, 3, 1234), header);
        Assert.Equal(pixels, message[PreviewFrameHeader.Size..]);
    }

    [Fact]
    public async Task The_text_ack_frees_the_slot_and_never_reaches_the_brain()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);
        await PostWhenConnectedAsync(host, PreviewTarget.Operator, Frame(7));
        await client.ReceiveBinaryAsync();
        Assert.False(host.App.Previews.TryPost(PreviewTarget.Operator, Frame(8)));

        await client.SendAsync("""{"type":"frameAck","seq":7}""");
        await WaitUntilAsync(() => host.App.Previews.GetStats(PreviewTarget.Operator).Delivered == 1);
        await client.SendAsync(Hello);
        await client.ReceiveMessageAsync();

        Assert.True(host.App.Previews.TryPost(PreviewTarget.Operator, Frame(8)));
        Assert.Equal([Hello], host.Brain.Connections[0].Received);
        Assert.Equal(1, host.App.Previews.GetStats(PreviewTarget.Operator).Delivered);
    }

    [Fact]
    public async Task A_wrong_sequence_ack_leaves_the_frame_in_flight()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);
        await PostWhenConnectedAsync(host, PreviewTarget.Operator, Frame(7));
        await client.ReceiveBinaryAsync();

        await client.SendAsync("""{"type":"frameAck","seq":6}""");
        await client.SendAsync(Hello);
        await client.ReceiveMessageAsync();

        Assert.False(host.App.Previews.TryPost(PreviewTarget.Operator, Frame(8)));
        Assert.Equal(0, host.App.Previews.GetStats(PreviewTarget.Operator).Delivered);
    }

    [Fact]
    public async Task A_malformed_ack_goes_to_the_brain_which_answers_it()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);
        const string malformed = """{"type":"frameAck","seq":"seven"}""";

        await client.SendAsync(malformed);
        var reply = await client.ReceiveAsync();

        Assert.Equal([malformed], host.Brain.Connections[0].Received);
        Assert.Contains("invalidEnvelope", reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Frames_go_only_to_the_socket_of_their_role()
    {
        await using var host = await TestHost.StartAsync();
        using var guest = await host.ConnectAsync(Role.Guest);
        using var op = await host.ConnectAsync(Role.Operator);

        await PostWhenConnectedAsync(host, PreviewTarget.Guest, Frame(1));
        await PostWhenConnectedAsync(host, PreviewTarget.Operator, Frame(2));

        Assert.True(PreviewFrameHeader.TryRead(await guest.ReceiveBinaryAsync(), out var guestHeader));
        Assert.True(PreviewFrameHeader.TryRead(await op.ReceiveBinaryAsync(), out var operatorHeader));
        Assert.Equal(1u, guestHeader.Sequence);
        Assert.Equal(2u, operatorHeader.Sequence);
    }

    [Fact]
    public async Task A_disconnected_page_has_no_target()
    {
        await using var host = await TestHost.StartAsync();
        var client = await host.ConnectAsync(Role.Operator);
        await PostWhenConnectedAsync(host, PreviewTarget.Operator, Frame(1));
        await client.ReceiveBinaryAsync();
        await client.SendAsync("""{"type":"frameAck","seq":1}""");
        await WaitUntilAsync(() => host.App.Previews.GetStats(PreviewTarget.Operator).Delivered == 1);

        client.Dispose();

        await WaitUntilAsync(() => host.App.Sessions.Count(Role.Operator) == 0);
        Assert.False(host.App.Previews.TryPost(PreviewTarget.Operator, Frame(2)));
        Assert.False(host.App.Previews.TryPost(PreviewTarget.Guest, Frame(2)));
        Assert.Equal(0, host.App.Previews.GetStats(PreviewTarget.Operator).Dropped);
    }

    [Fact]
    public async Task A_reconnected_page_starts_with_a_free_slot()
    {
        await using var host = await TestHost.StartAsync();
        var first = await host.ConnectAsync(Role.Operator);
        await PostWhenConnectedAsync(host, PreviewTarget.Operator, Frame(1));
        await first.ReceiveBinaryAsync();
        first.Dispose();
        using var second = await host.ConnectAsync(Role.Operator);

        await PostWhenConnectedAsync(host, PreviewTarget.Operator, Frame(2));

        Assert.True(PreviewFrameHeader.TryRead(await second.ReceiveBinaryAsync(), out var header));
        Assert.Equal(2u, header.Sequence);
    }

    [Fact]
    public async Task The_host_hands_the_same_channel_to_the_camera_adapter_and_the_brain()
    {
        IPreviewChannel? given = null;
        await using var host = await TestHost.StartAsync(cameraFactory: channel =>
        {
            given = channel;
            return new FakeCameraAdapter(SystemClock.Instance, channel);
        });

        Assert.Same(host.App.Previews, given);
        Assert.Same(host.App.Previews, host.Dependencies!.PreviewStats);
        Assert.IsType<FakeCameraAdapter>(host.Dependencies.Camera);
    }

    [Fact]
    public async Task The_default_camera_adapter_delivers_its_test_pattern_to_the_page_one_frame_at_a_time()
    {
        await using var host = await TestHost.StartAsync();
        using var client = await host.ConnectAsync(Role.Operator);
        await using var session = await host.Dependencies!.Camera.OpenAsync("fake-camera", TestContext.Current.CancellationToken);
        session.SetPreviewTarget(PreviewTarget.Operator);

        var sequences = new List<uint>();
        for (var received = 0; received < 4; received++)
        {
            var message = await client.ReceiveBinaryAsync();
            Assert.True(PreviewFrameHeader.TryRead(message, out var header));
            Assert.Equal(1280, header.Width);
            Assert.Equal(720, header.Height);
            sequences.Add(header.Sequence);
            await client.SendAsync($$"""{"type":"frameAck","seq":{{header.Sequence}}}""");
        }

        session.SetPreviewTarget(PreviewTarget.None);
        Assert.Equal(sequences.OrderBy(sequence => sequence), sequences);
        Assert.Equal(sequences.Count, sequences.Distinct().Count());
        await WaitUntilAsync(() => host.App.Previews.GetStats(PreviewTarget.Operator).Delivered >= 4);
        Assert.True(host.App.Previews.GetStats(PreviewTarget.Operator).DeliveredPerSecond > 0);
    }

    private static PreviewFrame Frame(long sequence) => new(sequence, 2, 2, new byte[16], TimeSpan.Zero);

    /// <summary>The socket is attached a moment after the client's connect completes: retry until the channel accepts the frame.</summary>
    private static async Task PostWhenConnectedAsync(TestHost host, PreviewTarget target, PreviewFrame frame)
    {
        await WaitUntilAsync(() => host.App.Previews.TryPost(target, frame));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + BridgeClient.Timeout;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold in time.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
