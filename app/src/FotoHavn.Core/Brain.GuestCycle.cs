using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core;

// The Guest Cycle's commands, presentation, and diagnostics (plan sections 7 and 9; M8 contract). Owner: M8 wave B1. Wave A wired the
// dispatch (Brain.Commands.cs) to these stubs, which answer notAvailable until B1 implements them, and added the RenderGuestCycle hook
// that Brain.ActiveEvent.cs's RenderGuestShell calls. The rules live in the pure model (GuestCycles/GuestCycleRules.cs) and the timer
// engine (GuestCycles/PhaseTimerEngine.cs); this partial and its siblings only orchestrate them:
//   Brain.GuestCycle.cs          commands, state, presentation, diagnostics           (this file)
//   Brain.GuestCycleCapture.cs   countdown, flash, capture, commit, sequence, Retake
//   Brain.GuestCycleTimers.cs    the Phase Timer's scheduling, pause and resume, warning, and outcomes
//   Brain.Assistance.cs          Operator Assistance: entry, RetryAssistance, recovery
public sealed partial class Brain
{
    /// <summary>The Event record of the running Event, or null while none runs. The Guest Cycle reads the Event's Templates, Looks, countdown, mirror, and Phase Timers from it.</summary>
    internal Storage.EventRecord? ActiveEventRecord => _activeEvent?.Event;

    /// <summary>The open camera session, or null while the camera isn't open. Valid only on the executor; a Capture takes its frame through it (never off the executor beyond the awaited call).</summary>
    internal Camera.ICameraSession? CameraSlotSession => _cameraSession;

    // Commands. Each handler runs on the executor, answers at once (accepted or rejected), and starts any I/O off the executor
    // and posts its outcome back (M6 contract section 9, Threading). Until B1 fills them they answer notAvailable. They are static only because a stub reads no instance state (CA1822): B1 drops `static` as it implements each one, and the same for RenderGuestCycle and GuestCycleDiagnosticsNow below.
    private static CommandResultMessage BeginGuestCycle(string id) => NotAvailable(id);

    private static CommandResultMessage SelectTemplate(string id, SelectTemplatePayload payload) => NotAvailable(id);

    private static CommandResultMessage GoToStep(string id, GoToStepPayload payload) => NotAvailable(id);

    private static CommandResultMessage ShowCameraTab(string id) => NotAvailable(id);

    private static CommandResultMessage ShowArrangeTab(string id) => NotAvailable(id);

    private static CommandResultMessage TakePhotographs(string id) => NotAvailable(id);

    private static CommandResultMessage StopCapture(string id) => NotAvailable(id);

    private static CommandResultMessage SelectPosition(string id, SelectPositionPayload payload) => NotAvailable(id);

    private static CommandResultMessage BeginRetake(string id, BeginRetakePayload payload) => NotAvailable(id);

    private static CommandResultMessage AcceptRetake(string id) => NotAvailable(id);

    private static CommandResultMessage TryAgain(string id) => NotAvailable(id);

    private static CommandResultMessage KeepOriginal(string id) => NotAvailable(id);

    private static CommandResultMessage BeginMove(string id, BeginMovePayload payload) => NotAvailable(id);

    private static CommandResultMessage MoveTo(string id, MoveToPayload payload) => NotAvailable(id);

    private static CommandResultMessage CancelMove(string id) => NotAvailable(id);

    private static CommandResultMessage DragPlace(string id, DragPlacePayload payload) => NotAvailable(id);

    private static CommandResultMessage SelectLook(string id, SelectLookPayload payload) => NotAvailable(id);

    private static CommandResultMessage FinishStrip(string id) => NotAvailable(id);

    private static CommandResultMessage RetryComposition(string id) => NotAvailable(id);

    private static CommandResultMessage MoreTime(string id) => NotAvailable(id);

    private static CommandResultMessage StartOver(string id) => NotAvailable(id);

    private static CommandResultMessage ConfirmStartOver(string id) => NotAvailable(id);

    private static CommandResultMessage KeepGoing(string id) => NotAvailable(id);

    /// <summary>
    /// The Guest Screen's presentation while a Guest Cycle runs, or while Operator Assistance shows with or without one; null when the guest
    /// is on Start, Start unavailable, or the guest gallery, so <see cref="RenderGuestShell"/> renders those. <paramref name="overlays"/> holds
    /// the exit hold and PIN dialog M6 owns; the cycle adds <c>phaseTimerWarning</c>, <c>startOverConfirmation</c>, and <c>notice</c> with
    /// <c>overlays with { ... }</c>. Called on the executor.
    /// </summary>
    private static GuestPresentation? RenderGuestCycle(ActiveEventState active, GuestOverlays overlays) => null;

    /// <summary>The running Guest Cycle for <c>test.getDiagnostics</c>, or null while none runs.</summary>
    private static GuestCycleDiagnostics? GuestCycleDiagnosticsNow() => null;
}
