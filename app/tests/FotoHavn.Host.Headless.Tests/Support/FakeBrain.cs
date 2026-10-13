using System.Collections.Concurrent;
using FotoHavn.Core;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Host.Headless.Tests.Support;

/// <summary>
/// A test double of the brain that answers like the M2 brain: <c>hello</c> → <c>welcome</c> (or <c>protocolMismatch</c>),
/// valid commands → <c>rejected notAvailable</c>, parser rejections and envelope errors as the parser decides, and every
/// test hook → <c>hooksUnavailable</c>. It answers on the caller's thread and records what it saw.
/// </summary>
internal sealed class FakeBrain : IBrain
{
    private readonly ConcurrentQueue<FakeConnection> _connections = new();

    public bool FailConnect { get; init; }

    public bool Disposed { get; private set; }

    public IReadOnlyList<FakeConnection> Connections => [.. _connections];

    public static OperatorWelcomeMessage OperatorWelcome { get; } = new(
        ProtocolVersion.Current,
        1,
        new OperatorPresentation(OperatorSurface.EventList, new EventListView([]), null, OperatorOverlays.None, null, null));

    public static GuestWelcomeMessage GuestWelcome { get; } = new(
        ProtocolVersion.Current,
        1,
        new GuestPresentation(GuestSurface.Start, new StartView(false), GuestOverlays.None, null));

    public IBrainConnection Connect(Role role, Action<string> send)
    {
        if (FailConnect)
        {
            throw new InvalidOperationException("The fake brain refuses connections.");
        }

        var connection = new FakeConnection(role, send);
        _connections.Enqueue(connection);
        return connection;
    }

    /// <summary>Pushes a message to every open connection of <paramref name="role"/>, as a presentation change would.</summary>
    public void Push(Role role, ServerMessage message)
    {
        foreach (var connection in _connections.Where(c => c.Role == role && !c.IsDisposed))
        {
            connection.Send(ProtocolJson.Serialize(message));
        }
    }

    public void Dispose() => Disposed = true;
}

internal sealed class FakeConnection(Role role, Action<string> send) : IBrainConnection
{
    private readonly ConcurrentQueue<string> _received = new();
    private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Role Role { get; } = role;

    public IReadOnlyList<string> Received => [.. _received];

    public bool IsDisposed => _disposed.Task.IsCompleted;

    public Task WhenDisposed => _disposed.Task;

    public void Send(string json) => send(json);

    public void Receive(string json)
    {
        _received.Enqueue(json);
        ServerMessage reply = ClientMessageParser.Parse(json, Role) switch
        {
            ParsedHello hello when hello.Message.ProtocolVersion == ProtocolVersion.Current =>
                Role == Role.Operator ? FakeBrain.OperatorWelcome : FakeBrain.GuestWelcome,
            ParsedHello hello => new ProtocolMismatchMessage(ProtocolVersion.Current, hello.Message.ProtocolVersion),
            ParsedCommand command => CommandResultMessage.Rejected(command.Id, RejectReason.NotAvailable),
            RejectedCommand rejected => CommandResultMessage.Rejected(rejected.Id, rejected.Reason),
            ParsedTest test => TestResultMessage.Failure(test.Message.Id, TestHookError.HooksUnavailable),
            InvalidMessage invalid => invalid.Reply,
            var other => throw new InvalidOperationException($"Unexpected parse result {other}."),
        };
        send(ProtocolJson.Serialize(reply));
    }

    public void Dispose() => _disposed.TrySetResult();
}
