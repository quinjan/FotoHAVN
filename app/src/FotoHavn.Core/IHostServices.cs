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

    /// <summary>Opens a folder in File Explorer (Event and Guest Cycle folders, from M6). The headless host logs it.</summary>
    void OpenFolder(string path);
}
