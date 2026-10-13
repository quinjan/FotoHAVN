using System.Text.Json;
using System.Text.Json.Serialization;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Protocol;

/// <summary>The wire values of every envelope's <c>type</c>.</summary>
public static class MessageTypes
{
    public const string Hello = "hello";
    public const string Command = "command";
    public const string Test = "test";
    public const string Welcome = "welcome";
    public const string Presentation = "presentation";
    public const string CommandResult = "commandResult";
    public const string ProtocolMismatch = "protocolMismatch";
    public const string Error = "error";
    public const string TestResult = "testResult";
}

/// <summary>
/// One envelope on the bridge. Serialize it with <see cref="ProtocolJson.Serialize(ProtocolMessage)"/> so its
/// <c>type</c> is written first.
/// </summary>
public abstract record ProtocolMessage
{
    [JsonPropertyOrder(-2)]
    public string Type => MessageType;

    /// <summary>The envelope's <c>type</c>, one of <see cref="MessageTypes"/>.</summary>
    protected abstract string MessageType { get; }
}

/// <summary>A message from the UI to the brain: <c>hello</c>, <c>command</c>, or <c>test</c>.</summary>
public abstract record ClientMessage : ProtocolMessage;

/// <summary>A message from the brain to the UI.</summary>
public abstract record ServerMessage : ProtocolMessage;

/// <summary>A message that belongs to one role; <c>role</c> is written after <c>type</c>.</summary>
public interface IRoleMessage
{
    Role Role { get; }
}

/// <summary>The UI's first message on every page load, with the protocol version it was built with.</summary>
public sealed record HelloMessage(int ProtocolVersion) : ClientMessage
{
    protected override string MessageType => MessageTypes.Hello;
}

/// <summary>
/// A command from the UI. <paramref name="Id"/> is a UUID chosen by the UI (lowercase, 8-4-4-4-12) and echoed in the
/// <see cref="CommandResultMessage"/>. <paramref name="Payload"/> is the command's payload record, always an object
/// (<c>{}</c> for commands without fields). Read incoming commands with <see cref="ClientMessageParser"/>.
/// </summary>
public sealed record CommandMessage(string Id, string Name, JsonElement Payload) : ClientMessage
{
    protected override string MessageType => MessageTypes.Command;

    /// <summary>Builds a command envelope for a typed payload; its name comes from <see cref="CommandRegistry"/>.</summary>
    public static CommandMessage Create<TPayload>(string id, TPayload payload)
        where TPayload : ICommandPayload =>
        new(id, CommandRegistry.For<TPayload>().Name, ProtocolJson.ToElement(payload));
}

/// <summary>
/// An E2E test hook call (<c>test.*</c>). Brains built without <c>E2E_HOOKS</c> answer every one with
/// <see cref="TestHookError.HooksUnavailable"/>. <paramref name="Args"/> is omitted or null for hooks without arguments.
/// </summary>
public sealed record TestMessage(string Id, string Name, JsonElement? Args = null) : ClientMessage
{
    protected override string MessageType => MessageTypes.Test;
}

/// <summary>The brain's answer to a matching <c>hello</c>: the connection's role and that role's current presentation.</summary>
public sealed record OperatorWelcomeMessage(int ProtocolVersion, long Revision, OperatorPresentation Presentation)
    : ServerMessage, IRoleMessage
{
    protected override string MessageType => MessageTypes.Welcome;

    [JsonPropertyOrder(-1)]
    public Role Role => Role.Operator;
}

/// <inheritdoc cref="OperatorWelcomeMessage"/>
public sealed record GuestWelcomeMessage(int ProtocolVersion, long Revision, GuestPresentation Presentation)
    : ServerMessage, IRoleMessage
{
    protected override string MessageType => MessageTypes.Welcome;

    [JsonPropertyOrder(-1)]
    public Role Role => Role.Guest;
}

/// <summary>
/// A complete snapshot of everything the operator UI may show, sent after every change. Revisions count per role and
/// only increase; the UI drops a snapshot older than the one it has.
/// </summary>
public sealed record OperatorPresentationMessage(long Revision, OperatorPresentation Presentation)
    : ServerMessage, IRoleMessage
{
    protected override string MessageType => MessageTypes.Presentation;

    [JsonPropertyOrder(-1)]
    public Role Role => Role.Operator;
}

/// <summary>A complete snapshot of everything the guest UI may show. It never contains operator data.</summary>
public sealed record GuestPresentationMessage(long Revision, GuestPresentation Presentation)
    : ServerMessage, IRoleMessage
{
    protected override string MessageType => MessageTypes.Presentation;

    [JsonPropertyOrder(-1)]
    public Role Role => Role.Guest;
}

public enum CommandStatus
{
    Accepted,
    Rejected,
}

/// <summary>Why the brain rejected a command. A closed set; the UI may show a rejection but never decides one.</summary>
public enum RejectReason
{
    /// <summary>No command has this name.</summary>
    UnknownCommand,

    /// <summary>The command exists but the sender's role may not send it.</summary>
    WrongRole,

    /// <summary>The payload is missing or doesn't match the command's payload record exactly.</summary>
    InvalidPayload,

    /// <summary>The command exists but isn't implemented yet or isn't valid in the current state.</summary>
    NotAvailable,

    /// <summary>The command would change, delete, or start an Event while an Event is running.</summary>
    EventRunning,

    /// <summary>The connection hasn't completed a <c>hello</c> with a matching protocol version.</summary>
    ProtocolMismatch,

    /// <summary>The message is larger than <see cref="ProtocolLimits.MaxMessageBytes"/>.</summary>
    PayloadTooLarge,
}

/// <summary>The brain's answer to one command, correlated by <see cref="Id"/>. <see cref="Reason"/> is null when accepted.</summary>
public sealed record CommandResultMessage(string Id, CommandStatus Status, RejectReason? Reason) : ServerMessage
{
    protected override string MessageType => MessageTypes.CommandResult;

    public static CommandResultMessage Accepted(string id) => new(id, CommandStatus.Accepted, null);

    public static CommandResultMessage Rejected(string id, RejectReason reason) => new(id, CommandStatus.Rejected, reason);
}

/// <summary>The brain's answer to a <c>hello</c> whose version differs. The UI shows its fixed error screen.</summary>
public sealed record ProtocolMismatchMessage(int BrainVersion, int UiVersion) : ServerMessage
{
    protected override string MessageType => MessageTypes.ProtocolMismatch;
}

/// <summary>Why the brain couldn't read an envelope at all.</summary>
public enum EnvelopeError
{
    /// <summary>The message isn't JSON, or is nested deeper than the brain reads.</summary>
    InvalidJson,

    /// <summary>The message isn't an object with a string <c>type</c>, or a field is missing, of the wrong kind, or unknown.</summary>
    InvalidEnvelope,

    /// <summary>The <c>type</c> isn't <c>hello</c>, <c>command</c>, or <c>test</c>.</summary>
    UnknownType,

    /// <summary>The <c>id</c> is missing or isn't a UUID.</summary>
    InvalidId,

    /// <summary>The message is larger than <see cref="ProtocolLimits.MaxMessageBytes"/>.</summary>
    PayloadTooLarge,
}

/// <summary>
/// The brain's answer to an envelope it couldn't read. <see cref="Id"/> is the message's <c>id</c> when it could be read
/// as a string, otherwise null.
/// </summary>
public sealed record ErrorMessage(string? Id, EnvelopeError Reason) : ServerMessage
{
    protected override string MessageType => MessageTypes.Error;
}

/// <summary>
/// The answer to one <see cref="TestMessage"/>. When <see cref="Ok"/>, <see cref="Result"/> holds the hook's result
/// (<c>{}</c> for hooks without one) and <see cref="Error"/> is null; otherwise <see cref="Result"/> is null.
/// </summary>
public sealed record TestResultMessage(string Id, bool Ok, JsonElement? Result, TestHookError? Error) : ServerMessage
{
    protected override string MessageType => MessageTypes.TestResult;

    public static TestResultMessage Success<TResult>(string id, TResult result) =>
        new(id, true, ProtocolJson.ToElement(result), null);

    public static TestResultMessage Failure(string id, TestHookError error) => new(id, false, null, error);
}

/// <summary>Why a test hook failed.</summary>
public enum TestHookError
{
    /// <summary>The build has no E2E hooks (any field build).</summary>
    HooksUnavailable,

    /// <summary>No hook has this name.</summary>
    UnknownHook,

    /// <summary>The arguments are missing or don't match the hook's arguments record.</summary>
    InvalidArgs,

    /// <summary>The hook ran and failed (for example, the host couldn't enumerate cameras).</summary>
    HookFailed,

    /// <summary>This host doesn't support the hook (for example, window bounds in the headless host).</summary>
    NotSupported,

    /// <summary>The display ID isn't in the current display list (or, for connect and disconnect, the virtual list).</summary>
    UnknownDisplay,
}
