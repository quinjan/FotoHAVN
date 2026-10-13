using System.Text;
using System.Text.Json;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Protocol;

/// <summary>What the brain received from one connection, after <see cref="ClientMessageParser.Parse"/>.</summary>
public abstract record ClientParseResult;

/// <summary>A well-formed <c>hello</c>. The brain compares its version with <see cref="ProtocolVersion.Current"/>.</summary>
public sealed record ParsedHello(HelloMessage Message) : ClientParseResult;

/// <summary>A command the sender's role may send, with its payload read into its record.</summary>
public sealed record ParsedCommand(string Id, CommandDescriptor Command, ICommandPayload Payload) : ClientParseResult;

/// <summary>A command the brain must reject: answer with <see cref="CommandResultMessage.Rejected"/>.</summary>
/// <param name="Id">The command's ID.</param>
/// <param name="Name">The command name as sent (it may be unknown).</param>
/// <param name="Reason"><see cref="RejectReason.UnknownCommand"/>, <see cref="RejectReason.WrongRole"/>, or <see cref="RejectReason.InvalidPayload"/>.</param>
public sealed record RejectedCommand(string Id, string Name, RejectReason Reason) : ClientParseResult;

/// <summary>A well-formed <c>test</c> message. Its name starts with <c>test.</c> but may be unknown; its args aren't read yet.</summary>
public sealed record ParsedTest(TestMessage Message) : ClientParseResult;

/// <summary>A well-formed <c>frameAck</c>. Hosts handle it before the brain; the brain ignores it.</summary>
public sealed record ParsedFrameAck(FrameAckMessage Message) : ClientParseResult;

/// <summary>An envelope the brain couldn't read: answer with <see cref="Reply"/>.</summary>
public sealed record InvalidMessage(ErrorMessage Reply) : ClientParseResult;

/// <summary>
/// Reads one message from the UI and validates its envelope, in this order:
/// <list type="number">
/// <item>Size: over <see cref="ProtocolLimits.MaxMessageBytes"/> UTF-8 bytes is <see cref="EnvelopeError.PayloadTooLarge"/>.</item>
/// <item>JSON: unreadable is <see cref="EnvelopeError.InvalidJson"/>; not an object with a string <c>type</c> is <see cref="EnvelopeError.InvalidEnvelope"/>.</item>
/// <item>Type: anything but <c>hello</c>, <c>command</c>, <c>test</c>, or <c>frameAck</c> is <see cref="EnvelopeError.UnknownType"/>.</item>
/// <item>ID (commands and tests): missing or not a UUID is <see cref="EnvelopeError.InvalidId"/>.</item>
/// <item>Fields: a missing, mistyped, or unknown field (a page sending its own <c>role</c>, say) is <see cref="EnvelopeError.InvalidEnvelope"/>.</item>
/// <item>Commands: an unknown name, a role that may not send it, or a payload that doesn't match its record is a <see cref="RejectedCommand"/>.</item>
/// </list>
/// It never throws for any input.
/// </summary>
public static class ClientMessageParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = ProtocolJson.Options.MaxDepth };

    public static ClientParseResult Parse(string json, Role sender)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (json.Length > ProtocolLimits.MaxMessageBytes || Encoding.UTF8.GetByteCount(json) > ProtocolLimits.MaxMessageBytes)
        {
            return Invalid(null, EnvelopeError.PayloadTooLarge);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException)
        {
            return Invalid(null, EnvelopeError.InvalidJson);
        }

        using (document)
        {
            return Parse(document.RootElement, sender);
        }
    }

    private static ClientParseResult Parse(JsonElement message, Role sender)
    {
        if (message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("type", out var typeElement)
            || typeElement.ValueKind != JsonValueKind.String)
        {
            return Invalid(ReadableId(message), EnvelopeError.InvalidEnvelope);
        }

        return typeElement.GetString() switch
        {
            MessageTypes.Hello => ParseHello(message),
            MessageTypes.Command => ParseCommand(message, sender),
            MessageTypes.Test => ParseTest(message),
            MessageTypes.FrameAck => ParseFrameAck(message),
            _ => Invalid(ReadableId(message), EnvelopeError.UnknownType),
        };
    }

    private static ClientParseResult ParseFrameAck(JsonElement message)
    {
        if (!HasOnly(message, "type", "seq")
            || !message.TryGetProperty("seq", out var seq)
            || seq.ValueKind != JsonValueKind.Number
            || !seq.TryGetInt64(out var value)
            || value is < 0 or > uint.MaxValue)
        {
            return Invalid(null, EnvelopeError.InvalidEnvelope);
        }

        return new ParsedFrameAck(new FrameAckMessage(value));
    }

    private static ClientParseResult ParseHello(JsonElement message)
    {
        if (!HasOnly(message, "type", "protocolVersion")
            || !message.TryGetProperty("protocolVersion", out var version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var protocolVersion))
        {
            return Invalid(null, EnvelopeError.InvalidEnvelope);
        }

        return new ParsedHello(new HelloMessage(protocolVersion));
    }

    private static ClientParseResult ParseCommand(JsonElement message, Role sender)
    {
        if (!TryReadId(message, out var id))
        {
            return Invalid(ReadableId(message), EnvelopeError.InvalidId);
        }

        if (!HasOnly(message, "type", "id", "name", "payload")
            || !message.TryGetProperty("name", out var nameElement)
            || nameElement.ValueKind != JsonValueKind.String)
        {
            return Invalid(id, EnvelopeError.InvalidEnvelope);
        }

        var name = nameElement.GetString()!;
        if (!CommandRegistry.TryGet(name, out var command))
        {
            return new RejectedCommand(id, name, RejectReason.UnknownCommand);
        }

        if (!command.IsAllowedFrom(sender))
        {
            return new RejectedCommand(id, name, RejectReason.WrongRole);
        }

        if (!message.TryGetProperty("payload", out var payloadElement) || payloadElement.ValueKind != JsonValueKind.Object)
        {
            return new RejectedCommand(id, name, RejectReason.InvalidPayload);
        }

        try
        {
            var payload = (ICommandPayload)payloadElement.Deserialize(command.PayloadType, ProtocolJson.Options)!;
            return new ParsedCommand(id, command, payload);
        }
        catch (JsonException)
        {
            return new RejectedCommand(id, name, RejectReason.InvalidPayload);
        }
    }

    private static ClientParseResult ParseTest(JsonElement message)
    {
        if (!TryReadId(message, out var id))
        {
            return Invalid(ReadableId(message), EnvelopeError.InvalidId);
        }

        if (!HasOnly(message, "type", "id", "name", "args")
            || !message.TryGetProperty("name", out var nameElement)
            || nameElement.ValueKind != JsonValueKind.String
            || !nameElement.GetString()!.StartsWith(Testing.TestHookNames.Prefix, StringComparison.Ordinal))
        {
            return Invalid(id, EnvelopeError.InvalidEnvelope);
        }

        JsonElement? args = message.TryGetProperty("args", out var argsElement) && argsElement.ValueKind != JsonValueKind.Null
            ? argsElement.Clone()
            : null;
        return new ParsedTest(new TestMessage(id, nameElement.GetString()!, args));
    }

    /// <summary>A command or test ID: a UUID in its 8-4-4-4-12 form, as <c>crypto.randomUUID()</c> makes.</summary>
    public static bool IsValidId(string? id) => id is not null && Guid.TryParseExact(id, "D", out _);

    private static bool TryReadId(JsonElement message, out string id)
    {
        id = ReadableId(message)!;
        return IsValidId(id);
    }

    private static string? ReadableId(JsonElement message) =>
        message.ValueKind == JsonValueKind.Object
        && message.TryGetProperty("id", out var idElement)
        && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()
            : null;

    private static bool HasOnly(JsonElement message, params ReadOnlySpan<string> allowed)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in message.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !seen.Add(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    private static InvalidMessage Invalid(string? id, EnvelopeError reason) => new(new ErrorMessage(id, reason));
}
