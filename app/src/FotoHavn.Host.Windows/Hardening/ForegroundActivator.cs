namespace FotoHavn.Host.Windows.Hardening;

/// <summary>The window calls <see cref="ForegroundActivator"/> makes, for one window.</summary>
internal interface IForegroundWindowApi
{
    bool IsMinimized { get; }

    void Restore();

    /// <summary>Asks Windows to make the window the foreground window; false when Windows' foreground lock refused.</summary>
    bool SetForeground();

    /// <summary>The fallback when the foreground lock refuses: raises the window above every other for an instant, so it shows, then puts it back.</summary>
    void RaiseBriefly();
}

/// <summary>
/// Brings the Operator window forward when a second launch asks (M10): restore it if it is minimized, then make it the foreground window.
/// Windows only lets the process that has the foreground, or one the foreground process allowed, call <c>SetForegroundWindow</c>
/// (the foreground lock). The second launch is the process the person just started, so it calls <c>AllowSetForegroundWindow</c> for any
/// process before it signals; when the first instance is signalled some other way (a script with no foreground rights) the call can
/// still be refused, and the window is raised for an instant instead, so it is shown but may not take keyboard focus. The Guest window is
/// not touched: while an Event runs it stays where it is.
/// </summary>
internal static class ForegroundActivator
{
    /// <summary>Returns whether the window is the foreground window (the focus case); false when it was only raised.</summary>
    public static bool Bring(IForegroundWindowApi window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.IsMinimized)
        {
            window.Restore();
        }

        if (window.SetForeground())
        {
            return true;
        }

        window.RaiseBriefly();
        return false;
    }
}
