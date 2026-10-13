using System.Text.Json;
using FotoHavn.Host.Windows.Bridge;
using FotoHavn.Host.Windows.TestHooks;

namespace FotoHavn.Host.Windows.Tests;

public sealed class WebMessageRouterTests
{
    [Theory]
    [InlineData("test.listCameras")]
    [InlineData("test.exit")]
    [InlineData("test.anythingElse")]
    public async Task Field_build_answers_every_test_hook_with_hooksUnavailable(string hook)
    {
        var router = new WebMessageRouter(TestHookChannel.Create(), () => { });

        var outcome = await router.HandleAsync($$"""{"type":"{{hook}}","id":"abc-123"}""");

        Assert.False(outcome.ExitRequested);
        using var reply = JsonDocument.Parse(outcome.ReplyJson!);
        Assert.Equal("test.result", reply.RootElement.GetProperty("type").GetString());
        Assert.Equal("abc-123", reply.RootElement.GetProperty("id").GetString());
        Assert.False(reply.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("hooksUnavailable", reply.RootElement.GetProperty("error").GetString());
        Assert.False(reply.RootElement.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task Ready_signals_the_host_without_a_reply()
    {
        var readyCount = 0;
        var router = new WebMessageRouter(UnavailableTestHooks.Instance, () => readyCount++);

        var outcome = await router.HandleAsync("""{"type":"ready"}""");

        Assert.Equal(1, readyCount);
        Assert.Same(WebMessageOutcome.None, outcome);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("\"ready\"")]
    [InlineData("""{"id":"x"}""")]
    [InlineData("""{"type":7}""")]
    [InlineData("""{"type":"command"}""")]
    public async Task Ignores_messages_it_does_not_know(string message)
    {
        var readyCount = 0;
        var router = new WebMessageRouter(UnavailableTestHooks.Instance, () => readyCount++);

        var outcome = await router.HandleAsync(message);

        Assert.Same(WebMessageOutcome.None, outcome);
        Assert.Equal(0, readyCount);
    }

    [Fact]
    public async Task Test_hook_without_an_id_replies_with_an_empty_id_and_passes_args()
    {
        var hooks = new RecordingHooks();
        var router = new WebMessageRouter(hooks, () => { });

        var outcome = await router.HandleAsync("""{"type":"test.exit","args":{"code":0}}""");

        Assert.True(outcome.ExitRequested);
        Assert.Equal(string.Empty, hooks.Request!.Id);
        Assert.Equal(0, hooks.Request.Args!.Value.GetProperty("code").GetInt32());
        Assert.Equal("""{"type":"test.result","id":"","ok":true,"result":{}}""", outcome.ReplyJson);
    }

    private sealed class RecordingHooks : ITestHookDispatcher
    {
        public TestHookRequest? Request { get; private set; }

        public Task<TestHookReply> DispatchAsync(TestHookRequest request)
        {
            Request = request;
            return Task.FromResult(TestHookReply.Success(request.Id, [], exitAfterReply: true));
        }
    }
}
