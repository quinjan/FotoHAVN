#pragma warning disable CA1822 // Wave A stubs: the wave B owner's handlers use brain state, then delete this line.
using FotoHavn.Core.Displays;
using FotoHavn.Core.Events.Active;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core;

// The running Event, the Start Event sequence, End Event, and the Guest Screen's exit hold and PIN (plan sections 7 and 8; M6
// contract sections 4 and 6). Owner: M6 wave B1b. Wave A defined the shared state, the read accessors Event setup uses, the
// rendering of the running-Event panel, and the entry points; every command is a stub that answers rejected notAvailable.
//
// The brain treats a guest `welcome` (the Guest Screen's UI finished its handshake) as "Guest Screen ready" for Start Event step 8.
// B1b: Start Event runs through TryStartEventAsync; the camera only through the camera slot (Brain.Camera.cs); the wake lock only
// through `_dependencies.WakeLock`; the guest window only through `_dependencies.Host`.
public sealed partial class Brain
{
    // The running Event; null while none runs. B1b sets it when Start Event succeeds and clears it when the Event ends.
    private ActiveEventState? _activeEvent;

    // The display the Guest Screen is on, or null while none is open (the test.openGuestScreen stand-in sets it too).
    private DisplayInfo? _guestScreenDisplay;

    /// <summary>
    /// Keeps the brain consistent with the current display list. M2 stand-in for D37 (M6 and M8 complete it): a Guest
    /// Screen whose display is gone is closed. The caller publishes the presentations afterwards.
    /// </summary>
    private void ApplyDisplayChange()
    {
        if (_guestScreenDisplay is { } display && DisplayMatching.Find(CurrentDisplays, display.Id) is null)
        {
            BrainLog.GuestDisplayLost(_logger, display.Id);
            _dependencies.Host.CloseGuestScreen();
            _guestScreenDisplay = null;
        }
    }

    /// <summary>The display containing the Operator window, for D38: the virtual primary while displays are virtual.</summary>
    private string? OperatorDisplayId() => _virtualDisplays is { } virtualDisplays
        ? virtualDisplays.PrimaryId
        : _dependencies.Host.GetOperatorDisplayId();

    /// <summary>The running Event's ID, or null while none runs. Event setup and the Event list read it to lock the running Event.</summary>
    internal string? ActiveEventId => _activeEvent?.Event.Id;

    /// <summary>The running Event's bound camera device, or null while none runs. Event setup shows "In use by the running Event" for it.</summary>
    internal string? ActiveEventCameraDeviceId => _activeEvent?.Event.CameraDeviceId;

    /// <summary>
    /// Makes <paramref name="state"/> the running Event (null: none runs) and publishes. M6 wave A's stand-in so the UI's fixtures and
    /// the render tests reach the running states from a real brain; B1b's Start and End Event sequences set it themselves.
    /// </summary>
    internal void ShowActiveEvent(ActiveEventState? state) =>
        Post(() =>
        {
            _activeEvent = state;
            PublishChanges();
        });

    /// <summary>The running-Event panel, or null while no Event runs. Rendered on every operator surface, so any view can show an Operator Assistance alert.</summary>
    private RunningEventPanel? RenderRunningEventPanel()
    {
        if (_activeEvent is not { } active)
        {
            return null;
        }

        var connected = DisplayMatching.Find(CurrentDisplays, active.GuestDisplay.Id) is not null;
        return new RunningEventPanel(
            active.Event.Id,
            active.Event.Name,
            new RunningEventDisplay(active.GuestDisplay.Name, connected ? RunningDisplayState.Connected : RunningDisplayState.Disconnected),
            new RunningEventCamera(_cameraState, _cameraReason),
            active.Step,
            active.CompletedGuestCycles,
            active.StartedAt,
            active.Assistance,
            active.SingleScreen);
    }

    /// <summary>
    /// The Guest Screen's presentation (the fatal case is handled by the caller): Start, Start unavailable, and the overlays of the exit
    /// hold and PIN dialog. Wave A's stub is the Start placeholder: nothing to step inside to until M8.
    /// </summary>
    private GuestPresentation RenderGuestShell() =>
        new(GuestSurface.Start, new StartView(StepInsideAvailable: false, GuestGalleryLinkVisible: false), null, GuestOverlays.None, null);

    private GuestPresentation RenderGuest() => _fatalReason is { } reason
        ? new GuestPresentation(GuestSurface.Fatal, null, null, GuestOverlays.None, new FatalView(reason))
        : RenderGuestShell();

    /// <summary>
    /// Runs Start Event steps 1 to 9 for the saved Event <paramref name="eventId"/> and completes with how it ended. Called on the
    /// executor and returns at once; the task completes on any thread, and the caller posts what it does with the result back to the
    /// executor (it never touches brain state from a continuation). B1a's <c>SaveAndStartEvent</c> calls it after a successful
    /// save; B1b's <c>StartEvent</c> calls it for the Event list's Start. On success the Event is running (<see cref="ActiveEventId"/>
    /// is set, the camera, wake lock, and Guest Screen are held); on failure nothing is left held. Wave A's stub fails with
    /// <see cref="StartFailureCode.InvalidSetup"/>.
    /// </summary>
    internal Task<StartEventResult> TryStartEventAsync(string eventId)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventId);
        return Task.FromResult(StartEventResult.Failed(StartFailureCode.InvalidSetup));
    }

    /// <summary>
    /// A guest connection completed its handshake and got its <c>welcome</c>: the brain treats that as "Guest Screen ready" (Start
    /// Event step 8). The headless host's guest client does the same. Wave A's stub does nothing; B1b completes the start sequence here.
    /// </summary>
    private void OnGuestScreenReady()
    {
        // TODO(M6 wave B1b)
    }

    // TODO(M6 wave B1b): the running Event and Guest Screen commands.
    private CommandResultMessage StartEvent(string id, StartEventPayload payload) => NotAvailable(id);

    private CommandResultMessage EndEvent(string id) => NotAvailable(id);

    private CommandResultMessage ConfirmEndEvent(string id) => NotAvailable(id);

    private CommandResultMessage KeepRunning(string id) => NotAvailable(id);

    private CommandResultMessage BeginExitHold(string id) => NotAvailable(id);

    private CommandResultMessage CompleteExitHold(string id) => NotAvailable(id);

    private CommandResultMessage SubmitPin(string id, SubmitPinPayload payload) => NotAvailable(id);

    private CommandResultMessage CancelExit(string id) => NotAvailable(id);
}
