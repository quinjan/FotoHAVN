using System.Text.Json;
using FotoHavn.Host.Windows.TestHooks;

namespace FotoHavn.Host.Windows.Bridge;

/// <summary>What the window does after the router handles one web message.</summary>
internal sealed record WebMessageOutcome(string? ReplyJson, bool ExitRequested)
{
    public static WebMessageOutcome None { get; } = new(null, false);
}

/// <summary>
/// Routes messages from the page (M1 interim channel; M2 replaces it with the protocol envelope).
/// <c>ready</c> signals the UI mounted; every <c>test.*</c> message goes to the test hook dispatcher, which answers
/// <c>hooksUnavailable</c> unless the build has E2E hooks.
/// </summary>
internal sealed class WebMessageRouter(ITestHookDispatcher testHooks, Action onReady)
{
    public const string TestMessagePrefix = "test.";

    public async Task<WebMessageOutcome> HandleAsync(string messageJson)
    {
        JsonElement message;
        try
        {
            using var document = JsonDocument.Parse(messageJson);
            message = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return WebMessageOutcome.None;
        }

        if (message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("type", out var typeElement)
            || typeElement.ValueKind != JsonValueKind.String)
        {
            return WebMessageOutcome.None;
        }

        var type = typeElement.GetString()!;
        if (type == "ready")
        {
            onReady();
            return WebMessageOutcome.None;
        }

        if (!type.StartsWith(TestMessagePrefix, StringComparison.Ordinal))
        {
            return WebMessageOutcome.None;
        }

        var id = message.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()!
            : string.Empty;
        JsonElement? args = message.TryGetProperty("args", out var argsElement) ? argsElement : null;

        var reply = await testHooks.DispatchAsync(new TestHookRequest(type, id, args));
        return new WebMessageOutcome(reply.ToJson(), reply.ExitAfterReply);
    }
}
