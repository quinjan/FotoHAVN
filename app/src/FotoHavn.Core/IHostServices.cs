using FotoHavn.Core.Displays;

namespace FotoHavn.Core;

/// <summary>
/// What the brain asks the host to do. The brain calls these only on its executor thread; each must return at once and
/// do the work asynchronously (WPF: on the UI thread through the dispatcher). They report nothing back: the brain
/// learns the outcome from new connections, display changes, and connection disposal.
/// </summary>
public interface IHostServices
{
    /// <summary>
    /// Opens the Guest window: borderless, covering <paramref name="display"/>'s full <see cref="DisplayInfo.Bounds"/>,
    /// topmost in field builds, with a new WebView2 whose bridge connects with <see cref="Protocol.Role.Guest"/>. If a
    /// Guest window is already open, the host closes it (disposing its connection) and opens a new one. The headless host
    /// only records the request: guest clients may connect with the guest token at any time.
    /// </summary>
    void OpenGuestScreen(DisplayInfo display);

    /// <summary>Closes the Guest window, if open, and disposes its WebView2 and connection.</summary>
    void CloseGuestScreen();

    /// <summary>
    /// Opens a folder in File Explorer (Event and Guest Cycle folders). Fire-and-forget: it returns at once, reports nothing
    /// back, and a failure to open is the host's to log. The brain records what it asked in its diagnostics. The headless host
    /// records the request and does nothing else.
    /// </summary>
    void OpenFolder(string path);

    /// <summary>
    /// Shows <c>IDENTIFY DISPLAYS</c>: a borderless, non-activating overlay on each display in <paramref name="overlays"/>, with
    /// its number and name in the Display type role. A new call replaces overlays already showing. The brain owns the 3-second
    /// timer and calls <see cref="HideIdentifyOverlays"/>; the host never hides them itself. The headless host only records it.
    /// </summary>
    void ShowIdentifyOverlays(IReadOnlyList<IdentifyOverlaySpec> overlays);

    /// <summary>Closes every identify overlay, if any is showing.</summary>
    void HideIdentifyOverlays();

    /// <summary>
    /// The <see cref="DisplayInfo.Id"/> of the display that currently contains the Operator window, or null when unknown
    /// (no Operator window yet). Single-screen detection (D38) compares the Guest Display Binding to this id, not to the
    /// primary display, because the operator may move the Operator window. The Windows host maps
    /// <c>MonitorFromWindow(MONITOR_DEFAULTTONEAREST)</c> to the display's device path and keeps it cached (refreshed on
    /// window moves and display changes) so this returns at once without touching the UI thread. The headless host and
    /// the fakes return the primary display's id.
    /// </summary>
    string? GetOperatorDisplayId();
}
