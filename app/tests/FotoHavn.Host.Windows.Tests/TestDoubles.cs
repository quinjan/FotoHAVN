using FotoHavn.Core;
using FotoHavn.Protocol;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Tests;

/// <summary>A test double of the brain: it records connections and lets a test push messages through a connection's send.</summary>
internal sealed class FakeBrain : IBrain
{
    public List<FakeConnection> Connections { get; } = [];

    public IBrainConnection Connect(Role role, Action<string> send)
    {
        var connection = new FakeConnection(role, send);
        Connections.Add(connection);
        return connection;
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeConnection(Role role, Action<string> send) : IBrainConnection
{
    public Role Role { get; } = role;

    public List<string> Received { get; } = [];

    public bool Disposed { get; private set; }

    /// <summary>Calls the host's send callback, as the brain does on its executor.</summary>
    public void Send(string json) => send(json);

    public void Receive(string json) => Received.Add(json);

    public void Dispose() => Disposed = true;
}

/// <summary>Collects log entries.</summary>
internal sealed class RecordingLogger : ILogger
{
    public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, eventId, formatter(state, exception)));
}
