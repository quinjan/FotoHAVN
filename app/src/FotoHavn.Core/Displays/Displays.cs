namespace FotoHavn.Core.Displays;

/// <summary>A rectangle in physical pixels, in Windows virtual-screen coordinates.</summary>
public readonly record struct DisplayRect(int X, int Y, int Width, int Height);

/// <summary>An Available Display, as the display adapter reports it. The adapter only reports facts; rules are in the brain.</summary>
/// <param name="Id">The stable identity: the monitor device path. Matching uses only this, never position or index.</param>
/// <param name="Name">The friendly name, for display only.</param>
/// <param name="Bounds">The full display bounds, in physical pixels.</param>
/// <param name="WorkArea">The bounds minus the taskbar, in physical pixels.</param>
/// <param name="Scale">The Windows display scale: DPI / 96 (1.0, 1.25, 1.5, …).</param>
/// <param name="IsPrimary">Whether this is the Windows primary display.</param>
public sealed record DisplayInfo(string Id, string Name, DisplayRect Bounds, DisplayRect WorkArea, double Scale, bool IsPrimary);

/// <summary>
/// Lists the active displays and reports changes. The Windows host implements it with <c>QueryDisplayConfig</c> and
/// <c>DisplayConfigGetDeviceInfo</c>, watching <c>WM_DISPLAYCHANGE</c>; tests and the headless host use
/// <c>FotoHavn.Core.Fakes.FakeDisplayAdapter</c>.
/// </summary>
public interface IDisplayAdapter
{
    /// <summary>
    /// The current displays. The brain calls it on its executor thread, so it must be thread-safe and must not block on
    /// the UI thread: return the latest cached snapshot.
    /// </summary>
    IReadOnlyList<DisplayInfo> GetDisplays();

    /// <summary>Raised on any thread after the list may have changed. The brain posts a re-read to its executor.</summary>
    event EventHandler? DisplaysChanged;
}
