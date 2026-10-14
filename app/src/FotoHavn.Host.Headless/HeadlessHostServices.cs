using FotoHavn.Core;
using FotoHavn.Core.Displays;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Headless;

/// <summary>
/// The headless host's answers to the brain. There are no windows: opening the Guest Screen only records the request
/// (guest clients connect with the guest token whenever they like), and folders aren't opened. Every call returns at once.
/// </summary>
internal sealed partial class HeadlessHostServices(IDisplayAdapter displays, ILogger logger) : IHostServices
{
    private DisplayInfo? _guestScreen;
    private IReadOnlyList<IdentifyOverlaySpec> _identifyOverlays = [];

    /// <summary>The display of the last <see cref="OpenGuestScreen"/> not followed by <see cref="CloseGuestScreen"/>.</summary>
    public DisplayInfo? GuestScreen => Volatile.Read(ref _guestScreen);

    public void OpenGuestScreen(DisplayInfo display)
    {
        ArgumentNullException.ThrowIfNull(display);
        Volatile.Write(ref _guestScreen, display);
        LogGuestScreenOpened(logger, display.Id);
    }

    /// <summary>Closes the Guest Screen; a no-op when none is open.</summary>
    public void CloseGuestScreen()
    {
        if (Interlocked.Exchange(ref _guestScreen, null) is not null)
        {
            LogGuestScreenClosed(logger);
        }
    }

    public void OpenFolder(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        LogOpenFolder(logger, path);
    }

    /// <summary>The overlays of the last <see cref="ShowIdentifyOverlays"/> not followed by <see cref="HideIdentifyOverlays"/>; empty when none show.</summary>
    public IReadOnlyList<IdentifyOverlaySpec> IdentifyOverlays => Volatile.Read(ref _identifyOverlays);

    /// <summary>Records only: there are no displays to draw on. The brain's diagnostics carry the E2E check (<c>identifyOverlays</c>).</summary>
    public void ShowIdentifyOverlays(IReadOnlyList<IdentifyOverlaySpec> overlays)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        Volatile.Write(ref _identifyOverlays, [.. overlays]);
        LogIdentifyShown(logger, overlays.Count);
    }

    public void HideIdentifyOverlays()
    {
        Volatile.Write(ref _identifyOverlays, []);
        LogIdentifyHidden(logger);
    }

    /// <summary>There is no Operator window, so the operator's display is the primary display.</summary>
    public string? GetOperatorDisplayId() => displays.GetDisplays().FirstOrDefault(display => display.IsPrimary)?.Id;

    [LoggerMessage(Level = LogLevel.Information, Message = "Guest Screen requested on display {DisplayId}; connect a guest client with the guest token.")]
    private static partial void LogGuestScreenOpened(ILogger logger, string displayId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Guest Screen closed.")]
    private static partial void LogGuestScreenClosed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Identify overlays requested on {Count} displays; the headless host only records them.")]
    private static partial void LogIdentifyShown(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Identify overlays hidden.")]
    private static partial void LogIdentifyHidden(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Open folder is disabled in the headless host: {Path}")]
    private static partial void LogOpenFolder(ILogger logger, string path);
}
