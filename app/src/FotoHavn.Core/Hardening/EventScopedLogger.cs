using Microsoft.Extensions.Logging;

namespace FotoHavn.Core.Hardening;

/// <summary>
/// Wraps the brain's logger so every line it writes while an Event is held carries the Event's ID, and while a Guest Cycle runs the
/// Guest Cycle's ID, as a logging scope with the properties <c>EventId</c> and <c>GuestCycleId</c> (both on the allow-list of
/// <see cref="LogRedaction"/>). The ids are read when each line is written, so a line logged from a pool thread still carries the ids
/// that are current; they are references the executor replaces, so a racing read sees the old or the new one, never a torn one.
/// </summary>
internal sealed class EventScopedLogger(ILogger inner, Func<(string? EventId, string? GuestCycleId)> currentIds) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!inner.IsEnabled(logLevel))
        {
            return;
        }

        var (eventScope, cycleScope) = currentIds();
        var ids = new List<KeyValuePair<string, object?>>(2);
        if (eventScope is not null)
        {
            ids.Add(new KeyValuePair<string, object?>("EventId", eventScope));
        }

        if (cycleScope is not null)
        {
            ids.Add(new KeyValuePair<string, object?>("GuestCycleId", cycleScope));
        }

        if (ids.Count == 0)
        {
            inner.Log(logLevel, eventId, state, exception, formatter);
            return;
        }

        using var scope = inner.BeginScope(ids);
        inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
