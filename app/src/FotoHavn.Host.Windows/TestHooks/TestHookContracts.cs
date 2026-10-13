using System.Text.Json;
using System.Text.Json.Nodes;

namespace FotoHavn.Host.Windows.TestHooks;

internal sealed record TestHookRequest(string Type, string Id, JsonElement? Args);

/// <summary>A <c>test.result</c> reply, posted back to the page with <c>PostWebMessageAsJson</c>.</summary>
internal sealed record TestHookReply(string Id, bool Ok, JsonObject? Result, string? Error, bool ExitAfterReply)
{
    public static TestHookReply Success(string id, JsonObject result, bool exitAfterReply = false) =>
        new(id, true, result, null, exitAfterReply);

    public static TestHookReply Failure(string id, string error) => new(id, false, null, error, false);

    public string ToJson()
    {
        var reply = new JsonObject
        {
            ["type"] = "test.result",
            ["id"] = Id,
            ["ok"] = Ok,
        };

        if (Ok)
        {
            reply["result"] = Result?.DeepClone() ?? new JsonObject();
        }
        else
        {
            reply["error"] = Error;
        }

        return reply.ToJsonString();
    }
}

internal interface ITestHookDispatcher
{
    Task<TestHookReply> DispatchAsync(TestHookRequest request);
}

internal static class TestHookErrors
{
    public const string HooksUnavailable = "hooksUnavailable";
    public const string UnknownHook = "unknownHook";
    public const string HookFailed = "hookFailed";
}

/// <summary>The dispatcher in field builds: every <c>test.*</c> message is refused.</summary>
internal sealed class UnavailableTestHooks : ITestHookDispatcher
{
    public static UnavailableTestHooks Instance { get; } = new();

    public Task<TestHookReply> DispatchAsync(TestHookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(TestHookReply.Failure(request.Id, TestHookErrors.HooksUnavailable));
    }
}

internal static class TestHookChannel
{
#if E2E_HOOKS
    public static bool IsCompiled => true;

    public static ITestHookDispatcher Create() =>
        new E2E.TestHookDispatcher(FotoHavn.Camera.Windows.VideoCaptureDeviceList.ListAsync);
#else
    public static bool IsCompiled => false;

    public static ITestHookDispatcher Create() => UnavailableTestHooks.Instance;
#endif
}
