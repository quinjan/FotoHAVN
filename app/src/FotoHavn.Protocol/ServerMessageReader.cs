using System.Text.Json;

namespace FotoHavn.Protocol;

/// <summary>
/// Reads a message from the brain into its record, for .NET clients: the headless host's integration tests and the
/// protocol tests. The UI uses the generated TypeScript types instead.
/// </summary>
public static class ServerMessageReader
{
    /// <summary>Reads one server message; throws <see cref="JsonException"/> when it isn't one exactly.</summary>
    public static ServerMessage Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var message = document.RootElement;
        if (message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("type", out var typeElement)
            || typeElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("A server message is an object with a string type.");
        }

        var type = typeElement.GetString();
        return type switch
        {
            MessageTypes.Welcome => ReadForRole<OperatorWelcomeMessage, GuestWelcomeMessage>(message),
            MessageTypes.Presentation => ReadForRole<OperatorPresentationMessage, GuestPresentationMessage>(message),
            MessageTypes.CommandResult => ProtocolJson.Deserialize<CommandResultMessage>(message),
            MessageTypes.ProtocolMismatch => ProtocolJson.Deserialize<ProtocolMismatchMessage>(message),
            MessageTypes.Error => ProtocolJson.Deserialize<ErrorMessage>(message),
            MessageTypes.TestResult => ProtocolJson.Deserialize<TestResultMessage>(message),
            _ => throw new JsonException($"Unknown server message type '{type}'."),
        };
    }

    private static ServerMessage ReadForRole<TOperator, TGuest>(JsonElement message)
        where TOperator : ServerMessage
        where TGuest : ServerMessage
    {
        var role = message.TryGetProperty("role", out var roleElement) && roleElement.ValueKind == JsonValueKind.String ? roleElement.GetString() : null;
        return role switch
        {
            "operator" => ProtocolJson.Deserialize<TOperator>(message),
            "guest" => ProtocolJson.Deserialize<TGuest>(message),
            _ => throw new JsonException($"Unknown role '{role}'."),
        };
    }
}
