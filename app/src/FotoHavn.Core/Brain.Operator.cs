using FotoHavn.Core.Displays;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core;

// The operator presentation (plan sections 8 and 9; M6 contract section 4). RenderOperator is the one place that builds it: it
// picks the surface and calls the render method in the file that owns that surface, so the Event list, Event setup, and running
// Event work never edit the same lines. This file also holds what those owners share: the overlay slots, the notice, the
// Identify displays sequence, and Open folder.
public sealed partial class Brain
{
    /// <summary>How long Identify displays stays up before the brain asks the host to hide the overlays (plan section 11).</summary>
    internal static readonly TimeSpan IdentifyDuration = TimeSpan.FromSeconds(3);

    // The surface the operator navigated to. Fatal, Starting, StorageError, and CameraDiagnostics are not navigated to: they
    // override this one in RenderOperator, in that order, and the brain returns to it when they clear. Only EventList, EventSetup,
    // and (until M9) the gallery surfaces, which render as the Event list, are navigated to. GuestScreenActive isn't stored:
    // it shows while the running Event's Guest Screen covers the Operator Screen (D38).
    private OperatorSurface _operatorSurface = OperatorSurface.EventList;

    // One confirmation at a time: opening another replaces it. The owner of the kind sets and clears it.
    private ConfirmationOverlay? _confirmation;

    // The notice stays until DismissNotice (or until another notice replaces it).
    private OperatorNoticeCode? _operatorNotice;

    private IReadOnlyList<IdentifyDisplayEntry>? _identifyDisplays;
    private IDisposable? _identifyTimer;
    private IReadOnlyList<IdentifyOverlayDiagnostics> _identifyOverlays = [];
    private readonly Queue<OpenFolderRequest> _openFolderRequests = new();

    private OperatorPresentation RenderOperator()
    {
        if (_fatalReason is { } reason)
        {
            return new OperatorPresentation(OperatorSurface.Fatal, null, null, null, OperatorOverlays.None, new FatalView(reason), null, null);
        }

        if (RenderCameraDiagnostics() is { } diagnostics)
        {
            return new OperatorPresentation(OperatorSurface.CameraDiagnostics, null, null, null, OperatorOverlays.None, null, null, diagnostics);
        }

        if (!_storageResolved)
        {
            return new OperatorPresentation(OperatorSurface.Starting, null, null, null, OperatorOverlays.None, null, null, null);
        }

        if (_storageError is { } storageError)
        {
            return new OperatorPresentation(
                OperatorSurface.StorageError, null, null, null, OperatorOverlays.None, null, new StorageErrorView(storageError, _dependencies.Store.DataRoot), null);
        }

        var panel = RenderRunningEventPanel();
        var overlays = RenderOperatorOverlays();
        if (_activeEvent is { SingleScreen: true })
        {
            return new OperatorPresentation(OperatorSurface.GuestScreenActive, null, null, panel, overlays, null, null, null);
        }

        return _operatorSurface == OperatorSurface.EventSetup && RenderEventSetup() is { } setup
            ? new OperatorPresentation(OperatorSurface.EventSetup, null, setup, panel, overlays, null, null, null)
            : new OperatorPresentation(OperatorSurface.EventList, RenderEventList(), null, panel, overlays, null, null, null);
    }

    private OperatorOverlays RenderOperatorOverlays() => new(
        _confirmation,
        _identifyDisplays is { } displays ? new IdentifyDisplaysOverlay(displays) : null,
        _operatorNotice is { } code ? new OperatorNoticeOverlay(code) : null);

    /// <summary>
    /// Shows <paramref name="surface"/> (the Event list or Event setup) as the operator's screen. M6 wave A's stand-in so the UI's
    /// fixtures and the render tests reach every surface from a real brain; the Event list and Event setup owners navigate with
    /// their own commands.
    /// </summary>
    internal void ShowOperatorSurface(OperatorSurface surface) =>
        Post(() =>
        {
            _operatorSurface = surface;
            PublishChanges();
        });

    /// <summary>Opens (or, with null, closes) the confirmation dialog. Called on the executor; the caller publishes. Opening replaces any open one.</summary>
    internal void SetConfirmation(ConfirmationOverlay? overlay) => _confirmation = overlay;

    /// <summary>Shows a notice until <c>DismissNotice</c>. Called on the executor; the caller publishes.</summary>
    internal void ShowNotice(OperatorNoticeCode code) => _operatorNotice = code;

    /// <summary><c>DismissNotice</c>: closes the notice; accepted only while one is showing.</summary>
    private CommandResultMessage DismissNotice(string id)
    {
        if (_operatorNotice is null)
        {
            return NotAvailable(id);
        }

        _operatorNotice = null;
        return CommandResultMessage.Accepted(id);
    }

    // Identify displays (plan section 11): numbers on every display for 3 seconds. The brain owns the timer; the host draws.

    /// <summary>
    /// Asks the host to number every display and starts (or restarts) the 3-second timer that hides the overlays again.
    /// The numbers follow <see cref="DisplayRules.NumberingOrder"/>, which is also the order Event setup lists displays in.
    /// Called on the executor; the caller publishes.
    /// </summary>
    internal void ShowIdentifyDisplays()
    {
        var ordered = DisplayRules.NumberingOrder(CurrentDisplays);
        var specs = new List<IdentifyOverlaySpec>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            specs.Add(new IdentifyOverlaySpec(ordered[index], index + 1, ordered[index].Name));
        }

        _identifyTimer?.Dispose();
        _identifyDisplays = [.. specs.Select(spec => new IdentifyDisplayEntry(spec.Display.Id, spec.Number, spec.Name))];
        _identifyOverlays = [.. specs.Select(spec => new IdentifyOverlayDiagnostics(spec.Display.Id, spec.Number, Visible: true))];
        _dependencies.Host.ShowIdentifyOverlays(specs);
        _identifyTimer = _dependencies.Clock.Schedule(IdentifyDuration, () => Post(() =>
        {
            HideIdentifyDisplays();
            PublishChanges();
        }));
    }

    /// <summary>Hides the identify overlays now (their timer ran out, or the operator left Event setup). Called on the executor; the caller publishes.</summary>
    internal void HideIdentifyDisplays()
    {
        _identifyTimer?.Dispose();
        _identifyTimer = null;
        if (_identifyDisplays is null)
        {
            return;
        }

        _identifyDisplays = null;
        _identifyOverlays = [.. _identifyOverlays.Select(overlay => overlay with { Visible = false })];
        _dependencies.Host.HideIdentifyOverlays();
    }

    // Open folder (plan section 8): fire-and-forget through the host; the brain records what it asked for the E2E diagnostics.

    /// <summary>Asks the host to open <paramref name="path"/> in File Explorer and records the request (the last 10 are in <c>test.getDiagnostics</c>).</summary>
    internal void RequestOpenFolder(string path)
    {
        _openFolderRequests.Enqueue(new OpenFolderRequest(path, _dependencies.Clock.UtcNow));
        if (_openFolderRequests.Count > DiagnosticsResult.MaxOpenFolderRequests)
        {
            _openFolderRequests.Dequeue();
        }

        _dependencies.Host.OpenFolder(path);
    }
}
