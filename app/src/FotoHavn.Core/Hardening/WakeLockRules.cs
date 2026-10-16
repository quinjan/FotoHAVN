namespace FotoHavn.Core.Hardening;

/// <summary>The wake lock's reason (M10). The lock is held while an Event is held; the reason names the Event by its ID, never by its name.</summary>
public static class WakeLockRules
{
    /// <summary>The reason shown by <c>powercfg /requests</c> and <c>test.getDiagnostics</c> while Event <paramref name="eventId"/> keeps the booth awake.</summary>
    public static string ReasonFor(string eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        return $"FOTOHAVN Event {eventId} is running";
    }
}
