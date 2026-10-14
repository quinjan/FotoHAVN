#pragma warning disable CA1822 // Wave A stubs: the wave B owner's handlers use brain state, then delete this line.
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core;

// Event setup (plan section 8, Event setup; M6 contract sections 4 and 6). Owner: M6 wave B1a. Wave A defined the entry points and
// left every command a stub that answers rejected notAvailable; the rendering stub shows the Event list so the surface is
// never blank before B1a lands.
//
// B1a: keep the draft in a class under Events/Setup/ (pure rules in FotoHavn.Core.Events), build RenderEventSetup from it, and
// drive the camera only through the camera slot in Brain.Camera.cs (OpenCameraFor, ReleaseCamera, CameraSlot*). Start goes
// through TryStartEventAsync (Brain.ActiveEvent.cs, owner B1b); a failed start is shown in the draft's `StartFailure`. Leave the
// surface by setting `_operatorSurface` (and hide the identify overlays with HideIdentifyDisplays).
public sealed partial class Brain
{
    /// <summary>
    /// The Event setup form for the open draft, or null when none is open (then the operator shows the Event list). Only asked
    /// while <c>_operatorSurface</c> is <see cref="OperatorSurface.EventSetup"/>. Wave A's stub is always null.
    /// </summary>
    private EventSetupView? RenderEventSetup() => null;

    /// <summary>
    /// Opens Event setup for the saved Event <paramref name="eventId"/> showing <paramref name="failure"/>: how a failed
    /// <c>StartEvent</c> from the Event list ends (plan section 8, Start Event: "stay in Event setup with the reason"). Called by
    /// <c>StartEvent</c> (B1b) on the executor; the caller publishes. B1a implements it: load the Event into a draft off the
    /// executor, post back, set the surface, and keep the failure on the draft until it changes.
    /// </summary>
    internal void OpenEventSetupWithStartFailure(string eventId, EventSetupStartFailure failure)
    {
        // TODO(M6 wave B1a)
    }

    // TODO(M6 wave B1a): the Event setup commands.
    private CommandResultMessage ChangeEventField(string id, ChangeEventFieldPayload payload) => NotAvailable(id);

    private CommandResultMessage SelectCamera(string id, SelectCameraPayload payload) => NotAvailable(id);

    private CommandResultMessage SelectGuestDisplay(string id, SelectGuestDisplayPayload payload) => NotAvailable(id);

    /// <summary>
    /// <c>IdentifyDisplays</c>: allowed on the Event setup surface. Wave A implemented it: <see cref="ShowIdentifyDisplays"/>
    /// (Brain.Operator.cs) runs the sequence and the 3-second timer; B1a hides it with <see cref="HideIdentifyDisplays"/> when leaving setup.
    /// </summary>
    private CommandResultMessage IdentifyDisplays(string id)
    {
        if (_operatorSurface != OperatorSurface.EventSetup)
        {
            return NotAvailable(id);
        }

        ShowIdentifyDisplays();
        return CommandResultMessage.Accepted(id);
    }

    private CommandResultMessage SetMirror(string id, SetMirrorPayload payload) => NotAvailable(id);

    private CommandResultMessage SetCountdown(string id, SetCountdownPayload payload) => NotAvailable(id);

    private CommandResultMessage SetOfferedTemplates(string id, SetOfferedTemplatesPayload payload) => NotAvailable(id);

    private CommandResultMessage SetDefaultTemplate(string id, SetDefaultTemplatePayload payload) => NotAvailable(id);

    private CommandResultMessage SetOfferedLooks(string id, SetOfferedLooksPayload payload) => NotAvailable(id);

    private CommandResultMessage SetDefaultLook(string id, SetDefaultLookPayload payload) => NotAvailable(id);

    private CommandResultMessage SetPhaseTimer(string id, SetPhaseTimerPayload payload) => NotAvailable(id);

    private CommandResultMessage SetGuestGallery(string id, SetGuestGalleryPayload payload) => NotAvailable(id);

    /// <summary><c>SetPin</c>: the PIN and its confirmation are held in the draft only until saved, never echoed, logged, or kept in a presentation.</summary>
    private CommandResultMessage SetPin(string id, SetPinPayload payload) => NotAvailable(id);

    private CommandResultMessage SaveEvent(string id) => NotAvailable(id);

    /// <summary><c>SaveAndStartEvent</c>: validates and saves, then calls <see cref="TryStartEventAsync"/> (B1b).</summary>
    private CommandResultMessage SaveAndStartEvent(string id) => NotAvailable(id);

    private CommandResultMessage LeaveEventSetup(string id) => NotAvailable(id);

    private CommandResultMessage ConfirmLeave(string id, ConfirmLeavePayload payload) => NotAvailable(id);
}
