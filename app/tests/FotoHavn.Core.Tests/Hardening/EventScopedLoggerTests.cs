using FotoHavn.Core.Hardening;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core.Tests.Hardening;

/// <summary>Every brain log line carries the Event and Guest Cycle ids that are current, as a logging scope (M10, contract section 8).</summary>
public sealed class EventScopedLoggerTests
{
    private static (EventScopedLogger Logger, ScopeRecorder Recorder) Create(string? eventId, string? cycleId, LogLevel minimum = LogLevel.Trace)
    {
        var recorder = new ScopeRecorder(minimum);
        return (new EventScopedLogger(recorder, () => (eventId, cycleId)), recorder);
    }

    private static void Write(EventScopedLogger logger, LogLevel level, string message) => logger.Log(level, default, message, null, static (text, _) => text);

    [Fact]
    public void A_line_logged_during_a_Guest_Cycle_carries_both_ids()
    {
        var (logger, recorder) = Create("event-1", "cycle-1");

        Write(logger, LogLevel.Information, "hello");

        var line = Assert.Single(recorder.Lines);
        Assert.Equal([new("EventId", "event-1"), new("GuestCycleId", "cycle-1")], line.Scope);
        Assert.Equal("hello", line.Message);
    }

    [Fact]
    public void A_line_logged_between_Guest_Cycles_carries_only_the_Event()
    {
        var (logger, recorder) = Create("event-1", null);

        Write(logger, LogLevel.Warning, "between cycles");

        Assert.Equal([new("EventId", "event-1")], Assert.Single(recorder.Lines).Scope);
    }

    [Fact]
    public void A_line_logged_with_no_Event_has_no_scope()
    {
        var (logger, recorder) = Create(null, null);

        Write(logger, LogLevel.Information, "idle");

        Assert.Empty(Assert.Single(recorder.Lines).Scope);
        Assert.Equal(0, recorder.ScopesBegun);
    }

    [Fact]
    public void The_scope_ends_with_the_line_and_a_disabled_level_writes_nothing()
    {
        var (logger, recorder) = Create("event-1", null, LogLevel.Warning);

        Write(logger, LogLevel.Information, "too quiet");
        Write(logger, LogLevel.Error, "loud");

        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.Equal("loud", Assert.Single(recorder.Lines).Message);
        Assert.Equal(0, recorder.OpenScopes);
    }

    [Fact]
    public void A_scope_the_caller_begins_goes_to_the_inner_logger()
    {
        var (logger, recorder) = Create(null, null);

        using (logger.BeginScope("caller"))
        {
            Assert.Equal(1, recorder.OpenScopes);
        }

        Assert.Equal(0, recorder.OpenScopes);
    }

    private sealed class ScopeRecorder(LogLevel minimum) : ILogger
    {
        private readonly Stack<object> _scopes = new();

        public List<(string Message, List<KeyValuePair<string, object?>> Scope)> Lines { get; } = [];

        public int ScopesBegun { get; private set; }

        public int OpenScopes => _scopes.Count;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            ScopesBegun++;
            _scopes.Push(state);
            return new Scope(_scopes);
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimum;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scope = _scopes.Count == 0 ? new List<KeyValuePair<string, object?>>() : [.. (IEnumerable<KeyValuePair<string, object?>>)_scopes.Peek()];
            Lines.Add((formatter(state, exception), scope));
        }

        private sealed class Scope(Stack<object> scopes) : IDisposable
        {
            public void Dispose() => scopes.Pop();
        }
    }
}
