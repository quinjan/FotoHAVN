namespace FotoHavn.Core.Hardening;

/// <summary>Where a request to close FOTOHAVN comes from.</summary>
public enum CloseSource
{
    /// <summary>The Operator window's close button, Alt+F4 on it, or its system menu.</summary>
    OperatorWindow,

    /// <summary>The Guest window's close button, Alt+F4 on it, or its system menu (no brain involved: the host decides with <see cref="CloseRequestRules.Decide"/>).</summary>
    GuestWindow,

    /// <summary>Windows is ending the user's session (sign-out, shutdown, restart).</summary>
    SessionEnd,
}

/// <summary>What happens to a close request.</summary>
public enum CloseRequestOutcome
{
    /// <summary>Nothing is running. The window closes; closing the Operator window, or Windows ending the session, closes the app.</summary>
    CloseNow,

    /// <summary>The Operator Screen asks "End the running Event and close FOTOHAVN?"; the app stays open until the operator chooses.</summary>
    AskToEndEvent,

    /// <summary>The Event ends cleanly without asking, then the app closes (Windows will not wait for an answer).</summary>
    EndEventAndClose,

    /// <summary>The close is ignored: guests cannot close the Guest Screen.</summary>
    Refuse,
}

/// <summary>The close rule (plan section 8, M10): an Active Event is never ended by accident, and never by a guest.</summary>
public static class CloseRequestRules
{
    /// <summary>The outcome for a close request while an Event is (<paramref name="eventActive"/>) or is not running.</summary>
    public static CloseRequestOutcome Decide(bool eventActive, CloseSource source)
    {
        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Not a close source.");
        }

        if (!eventActive)
        {
            return CloseRequestOutcome.CloseNow;
        }

        return source switch
        {
            CloseSource.OperatorWindow => CloseRequestOutcome.AskToEndEvent,
            CloseSource.GuestWindow => CloseRequestOutcome.Refuse,
            _ => CloseRequestOutcome.EndEventAndClose,
        };
    }
}
