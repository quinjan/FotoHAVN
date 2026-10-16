using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Protocol.Presentations;

// The operator views M6 adds (plan sections 8 and 9): the Event list rows with their actions, the Event setup form, the
// running-Event panel, and the overlays. Every field is always written (null when absent). The brain sends codes and
// facts; static copy lives in the UI (plan section 9, Copy). The Operator PIN never appears in any of these.

/// <summary>Why Start is disabled: another Event is running (plan section 8, Start Event step 1). The copy is "End the running Event to start another."</summary>
public enum StartDisabledReason
{
    EventRunning,
}

/// <summary>The Event list, sorted by date, newest first (plan section 8). Empty shows "No Events yet."</summary>
/// <param name="Events">The rows.</param>
/// <param name="NewEventAvailable">Whether <c>NEW EVENT →</c> works; always true (creating an Event is allowed while another runs).</param>
public sealed record EventListView(IReadOnlyList<EventListRow> Events, bool NewEventAvailable);

/// <summary>Which row actions work. The brain decides; the UI renders and never infers it from <see cref="EventListRow.Running"/>.</summary>
/// <param name="CanStart">Start; false on every row while any Event runs.</param>
/// <param name="CanEdit">Edit; false for the running Event.</param>
/// <param name="CanDelete">Delete; false for the running Event.</param>
/// <param name="CanOpenFolder">Open folder; true on every row.</param>
/// <param name="CanOpenGallery">Gallery; true on every row (<c>OpenGallery</c> answers <c>rejected notAvailable</c> only while the Event setup draft is open or storage is down).</param>
public sealed record EventRowActions(bool CanStart, bool CanEdit, bool CanDelete, bool CanOpenFolder, bool CanOpenGallery);

/// <summary>One Event list row (plan section 8, Event list).</summary>
/// <param name="Id">The Event ID.</param>
/// <param name="Name">The Event name.</param>
/// <param name="Date">The Event's local date (<c>yyyy-MM-dd</c>).</param>
/// <param name="Venue">The venue, or null when not set.</param>
/// <param name="CompletedGuestCycles">The number of completed Guest Cycles.</param>
/// <param name="LastUsedAt">When the Event last ran, or null if it never has.</param>
/// <param name="Running">Whether this is the Active Event (shown as <c>RUNNING</c>).</param>
/// <param name="Actions">Which row actions are available.</param>
/// <param name="StartDisabledReason">Why <see cref="EventRowActions.CanStart"/> is false; null when Start works.</param>
public sealed record EventListRow(
    string Id,
    string Name,
    DateOnly Date,
    string? Venue,
    int CompletedGuestCycles,
    DateTimeOffset? LastUsedAt,
    bool Running,
    EventRowActions Actions,
    StartDisabledReason? StartDisabledReason);

// Event setup

/// <summary>Why a field or section is invalid. The wire names are the codes in plan section 8; the UI owns the message for each.</summary>
public enum EventSetupErrorCode
{
    Required,
    TooLong,
    InvalidDate,
    AtLeastOneTemplate,
    DefaultTemplateNotOffered,
    AtLeastOneLook,
    DefaultLookNotOffered,
    TimerOutOfRange,
    PinFormat,
    PinMismatch,
    PinRequired,
    CameraRequired,
    DisplayRequired,
}

/// <summary>The wire names of the form fields an <see cref="EventSetupFieldError"/> points at.</summary>
public static class EventSetupFieldNames
{
    public const string Name = "name";
    public const string Date = "date";
    public const string Venue = "venue";
    public const string Customer = "customer";
    public const string Notes = "notes";
    public const string Camera = "camera";
    public const string GuestDisplay = "guestDisplay";
    public const string Templates = "templates";
    public const string DefaultTemplate = "defaultTemplate";
    public const string Looks = "looks";
    public const string DefaultLook = "defaultLook";
    public const string Pin = "pin";

    /// <summary>The prefix of a Phase Timer field: <c>timers.</c> plus the phase's wire name (<c>timers.photographsReview</c>).</summary>
    public const string TimersPrefix = "timers.";

    /// <summary>The field name for one Phase Timer, for example <c>timers.keepsake</c>.</summary>
    public static string Timer(PhaseTimer phase) => TimersPrefix + ProtocolNames.Of(phase);
}

/// <summary>One invalid field. <paramref name="Field"/> is one of <see cref="EventSetupFieldNames"/>.</summary>
public sealed record EventSetupFieldError(string Field, EventSetupErrorCode Code);

/// <summary>The text fields as typed: exactly what the draft holds, so a half-typed Date stays visible next to its error.</summary>
/// <param name="Name">The Event name.</param>
/// <param name="Date">The Date, <c>yyyy-MM-dd</c> when valid.</param>
/// <param name="Venue">The venue.</param>
/// <param name="Customer">The Customer or host.</param>
/// <param name="Notes">The notes.</param>
public sealed record EventSetupFieldValues(string Name, string Date, string Venue, string Customer, string Notes);

/// <summary>The Event setup camera field's state (plan section 8, Camera).</summary>
public enum EventSetupCameraState
{
    /// <summary>No camera is chosen.</summary>
    None,

    /// <summary>The chosen camera is opening or waiting for a fresh frame.</summary>
    Checking,

    /// <summary>A fresh frame arrived: an Eligible Camera.</summary>
    Ready,

    /// <summary>The camera can't be used; <see cref="EventSetupCameraView.Reason"/> says why (including <c>privacyDenied</c>).</summary>
    NotAvailable,

    /// <summary>Another app holds the camera.</summary>
    InUseByAnotherApp,

    /// <summary>The running Event holds this camera, so preview and the eligibility check wait until it ends. The binding can still be saved.</summary>
    InUseByRunningEvent,

    /// <summary>The Event's bound camera isn't listed now. The binding is kept until the operator chooses another; it is never swapped silently.</summary>
    Missing,

    /// <summary>An Event is running on a different camera: the brain keeps one camera session, so this camera isn't opened or checked until that Event ends.</summary>
    NotChecked,
}

/// <summary>The camera section of Event setup.</summary>
/// <param name="Cameras">The Available Cameras, in the adapter's order.</param>
/// <param name="SelectedDeviceId">The Camera Binding's device ID, or null when none is chosen.</param>
/// <param name="SelectedName">The bound camera's name (from the list, else the name saved with the binding), for display when it is missing.</param>
/// <param name="State">The field's state.</param>
/// <param name="Reason">Why <paramref name="State"/> is <see cref="EventSetupCameraState.NotAvailable"/> or <see cref="EventSetupCameraState.InUseByAnotherApp"/>; null otherwise.</param>
/// <param name="PreviewActive">Whether the operator connection receives live preview frames for this camera.</param>
/// <param name="Mirrored">Whether the preview is mirrored (the Event's Mirror setting).</param>
public sealed record EventSetupCameraView(
    IReadOnlyList<CameraOption> Cameras,
    string? SelectedDeviceId,
    string? SelectedName,
    EventSetupCameraState State,
    CameraFailureKind? Reason,
    bool PreviewActive,
    bool Mirrored);

/// <summary>An Available Display in the guest display picker (plan section 8, Guest display).</summary>
/// <param name="Id">The display's stable identity (the monitor device path).</param>
/// <param name="Name">The friendly name.</param>
/// <param name="X">The left edge in virtual-screen pixels.</param>
/// <param name="Y">The top edge in virtual-screen pixels.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Number">The number <c>IDENTIFY DISPLAYS</c> shows on this display: 1 up, left to right (then top to bottom, then by id).</param>
/// <param name="IsOperatorScreen">Whether the Operator window is on this display.</param>
public sealed record GuestDisplayOption(string Id, string Name, int X, int Y, int Width, int Height, int Number, bool IsOperatorScreen);

/// <summary>The Guest Display Binding's state.</summary>
public enum GuestDisplayState
{
    /// <summary>No display is chosen.</summary>
    None,

    /// <summary>The bound display is connected.</summary>
    Connected,

    /// <summary>The bound display isn't connected. The binding is kept until the operator chooses another.</summary>
    Missing,
}

/// <summary>The guest display section of Event setup.</summary>
/// <param name="Displays">The Available Displays, numbered for <c>IDENTIFY DISPLAYS</c>.</param>
/// <param name="SelectedId">The Guest Display Binding's display id, or null when none is chosen.</param>
/// <param name="SelectedName">The bound display's name (from the list, else the name saved with the binding).</param>
/// <param name="State">The binding's state.</param>
/// <param name="CoversOperatorScreen">True when the bound display is the one showing the Operator Screen (D38): the UI shows "The Guest Screen will cover this screen. Use the hold and PIN to return."</param>
public sealed record EventSetupGuestDisplayView(
    IReadOnlyList<GuestDisplayOption> Displays,
    string? SelectedId,
    string? SelectedName,
    GuestDisplayState State,
    bool CoversOperatorScreen);

/// <summary>The printer: v1 has none, stored as <c>none</c>.</summary>
public enum PrinterChoice
{
    None,
}

/// <summary>A Template in the picker.</summary>
/// <param name="Id">The Template ID.</param>
/// <param name="Name">The display name (brain copy).</param>
/// <param name="Description">The one-line description (brain copy).</param>
/// <param name="Width">The native paper width in pixels.</param>
/// <param name="Height">The native paper height in pixels.</param>
/// <param name="Landscape">Whether the paper is wider than tall (the UI rotates the preview a quarter turn).</param>
/// <param name="Offered">Whether the Event offers it.</param>
/// <param name="IsDefault">Whether it is the default Template.</param>
/// <param name="BlankPreviewKey">The preview cache key of the blank preview, or null until composed; the PNG is at <c>https://preview.fotohavn/&lt;key&gt;.png</c>.</param>
public sealed record EventSetupTemplateOption(
    string Id,
    string Name,
    string Description,
    int Width,
    int Height,
    bool Landscape,
    bool Offered,
    bool IsDefault,
    string? BlankPreviewKey);

/// <summary>A Look in the picker, in the brand's order.</summary>
/// <param name="Id">The Look ID.</param>
/// <param name="Name">The display name (brain copy).</param>
/// <param name="Description">The one-line description (brain copy).</param>
/// <param name="Offered">Whether the Event offers it.</param>
/// <param name="IsDefault">Whether it is the default Look.</param>
public sealed record EventSetupLookOption(string Id, string Name, string Description, bool Offered, bool IsDefault);

/// <summary>One Phase Timer field.</summary>
/// <param name="Phase">The phase.</param>
/// <param name="Seconds">The current length; 0 is off (only where <paramref name="CanBeOff"/>).</param>
/// <param name="DefaultSeconds">The default length.</param>
/// <param name="Min">The shortest length when the timer is on.</param>
/// <param name="Max">The longest length.</param>
/// <param name="CanBeOff">Whether 0 is allowed.</param>
public sealed record PhaseTimerOption(PhaseTimer Phase, int Seconds, int DefaultSeconds, int Min, int Max, bool CanBeOff);

/// <summary>The Operator PIN section. The digits are never sent back; a new Event or a changed PIN takes two entries in the UI.</summary>
/// <param name="IsSet">Whether the Event has a saved PIN (a PIN that was set in this draft counts once saved).</param>
/// <param name="Changing">Whether the draft holds a new PIN that replaces the saved one on save.</param>
public sealed record EventSetupPinView(bool IsSet, bool Changing);

/// <summary>Why starting stopped (plan section 8, Start Event). The Event stays in setup with this reason.</summary>
public enum StartFailureCode
{
    EventRunning,
    GuestDisplayMissing,
    CameraNotEligible,
    StorageNotWritable,
    PinMissing,
    InvalidSetup,
    GuestScreenNotReady,
}

/// <summary>The reason the last start attempt stopped. Cleared by the next change to the draft or the next attempt.</summary>
/// <param name="Code">Which step failed.</param>
/// <param name="CameraReason">Why the camera wasn't eligible; set only with <see cref="StartFailureCode.CameraNotEligible"/>.</param>
public sealed record EventSetupStartFailure(StartFailureCode Code, CameraFailureKind? CameraReason);

/// <summary>The Event setup form (plan section 8): one screen with grouped sections.</summary>
/// <param name="EventId">The Event ID once saved or when editing; null for a new Event not yet saved.</param>
/// <param name="IsNew">Whether this is a new Event.</param>
/// <param name="Dirty">Whether the draft differs from what is saved (leaving asks to <c>SAVE</c>, <c>DISCARD CHANGES</c>, or <c>KEEP EDITING</c>).</param>
/// <param name="Fields">The text fields.</param>
/// <param name="FieldErrors">The invalid fields, always computed; the UI shows them only when <paramref name="ShowErrors"/> is true.</param>
/// <param name="ShowErrors">True after a save attempt: errors then show next to their fields.</param>
/// <param name="Camera">The camera section.</param>
/// <param name="GuestDisplay">The guest display section.</param>
/// <param name="Mirror">Mirror on or off (default on).</param>
/// <param name="CountdownSeconds">3, 5, or 10 (default 5).</param>
/// <param name="Printer">Always <see cref="PrinterChoice.None"/>.</param>
/// <param name="Templates">Every built-in Template, in catalog order.</param>
/// <param name="Looks">Every Look, in the brand's order.</param>
/// <param name="PhaseTimers">The six Phase Timers, in the plan's order.</param>
/// <param name="GuestGalleryEnabled">Whether guests can open the guest gallery (default off).</param>
/// <param name="Pin">The Operator PIN section.</param>
/// <param name="SaveAndStartAvailable">Whether <c>SAVE AND START</c> works.</param>
/// <param name="SaveAndStartDisabledReason">Why it doesn't (<c>eventRunning</c>: "End the running Event to start another."); null when it works.</param>
/// <param name="StartFailure">Why the last start attempt stopped; null when none did.</param>
/// <param name="Saving">Whether a save (or save and start) is in flight.</param>
public sealed record EventSetupView(
    string? EventId,
    bool IsNew,
    bool Dirty,
    EventSetupFieldValues Fields,
    IReadOnlyList<EventSetupFieldError> FieldErrors,
    bool ShowErrors,
    EventSetupCameraView Camera,
    EventSetupGuestDisplayView GuestDisplay,
    bool Mirror,
    int CountdownSeconds,
    PrinterChoice Printer,
    IReadOnlyList<EventSetupTemplateOption> Templates,
    IReadOnlyList<EventSetupLookOption> Looks,
    IReadOnlyList<PhaseTimerOption> PhaseTimers,
    bool GuestGalleryEnabled,
    EventSetupPinView Pin,
    bool SaveAndStartAvailable,
    StartDisabledReason? SaveAndStartDisabledReason,
    EventSetupStartFailure? StartFailure,
    bool Saving);

// The running Event

/// <summary>The guest display's state in the running-Event panel.</summary>
public enum RunningDisplayState
{
    Connected,
    Disconnected,
}

/// <summary>The guest display as the running-Event panel shows it.</summary>
/// <param name="Name">The bound display's name.</param>
/// <param name="State">Connected or Disconnected.</param>
public sealed record RunningEventDisplay(string Name, RunningDisplayState State);

/// <summary>The camera as the running-Event panel shows it, whether or not a Guest Cycle is running.</summary>
/// <param name="State">The camera's state (<c>ready</c> or <c>notAvailable</c> in practice).</param>
/// <param name="Reason">Why it isn't available; null otherwise.</param>
public sealed record RunningEventCamera(CameraState State, CameraFailureKind? Reason);

/// <summary>The guest step the running-Event panel names. M6 sends <c>start</c> only; M8 fills the rest.</summary>
public enum RunningEventStep
{
    Start,
    Template,
    Photographs,
    Filters,
    Keepsake,
    GuestGallery,
    OperatorAssistance,
}

/// <summary>Why the Event is in Operator Assistance (M8 fills this).</summary>
public enum AssistanceReason
{
    Camera,
    Storage,
    GuestDisplay,
}

/// <summary>An Operator Assistance alert on the running-Event panel (M8 fills it).</summary>
public sealed record RunningEventAssistance(AssistanceReason Reason);

/// <summary>The running-Event panel (plan section 8): read-only facts with text labels, plus <c>OPEN GALLERY</c>, <c>RETRY</c>, and <c>END EVENT</c>.</summary>
/// <param name="EventId">The running Event's ID.</param>
/// <param name="Name">The Event name.</param>
/// <param name="GuestDisplay">The guest display and its state.</param>
/// <param name="Camera">The camera state.</param>
/// <param name="CurrentStep">The guest step.</param>
/// <param name="CompletedGuestCycles">Completed Guest Cycles since the Event started running, plus those before.</param>
/// <param name="StartedAt">When the Event started running; the UI computes the time since start from it.</param>
/// <param name="Assistance">The Operator Assistance alert; null when none.</param>
/// <param name="SingleScreen">Whether the Guest Screen covers the Operator Screen (D38).</param>
public sealed record RunningEventPanel(
    string EventId,
    string Name,
    RunningEventDisplay GuestDisplay,
    RunningEventCamera Camera,
    RunningEventStep CurrentStep,
    int CompletedGuestCycles,
    DateTimeOffset StartedAt,
    RunningEventAssistance? Assistance,
    bool SingleScreen);

// Overlays

/// <summary>Overlays on the Operator Screen; each is null when not shown.</summary>
public sealed record OperatorOverlays(
    ConfirmationOverlay? Confirmation,
    IdentifyDisplaysOverlay? IdentifyDisplays,
    OperatorNoticeOverlay? Notice)
{
    public static OperatorOverlays None { get; } = new(null, null, null);
}

/// <summary>Which confirmation dialog is open.</summary>
public enum ConfirmationKind
{
    /// <summary>"Delete this Event?" with what will be removed: <c>ConfirmDeleteEvent</c> or <c>CancelDeleteEvent</c>.</summary>
    DeleteEvent,

    /// <summary>"End this Event? Anyone using the booth will be stopped. Their photographs are kept.": <c>ConfirmEndEvent</c> or <c>KeepRunning</c>.</summary>
    EndEvent,

    /// <summary>Leaving Event setup with changes: <c>ConfirmLeave(save | discardChanges | keepEditing)</c>.</summary>
    LeaveSetup,

    /// <summary>"Delete this Guest Cycle?" naming its time and counting its files (protocol version 9): <c>ConfirmDeleteGuestCycle</c> or <c>CancelDeleteGuestCycle</c>.</summary>
    DeleteGuestCycle,

    /// <summary>
    /// "End the running Event and close FOTOHAVN?" (protocol version 10), asked when the operator closes the Operator window
    /// while an Event runs: <c>ConfirmCloseApp</c> or <c>CancelCloseApp</c>. It carries no Event name, counts, or Guest Cycle.
    /// </summary>
    CloseApp,
}

/// <summary>What a delete removes, for the confirmation.</summary>
/// <param name="GuestCycles">Guest Cycles.</param>
/// <param name="PhotoStrips">Photo Strips.</param>
/// <param name="Captures">Captures.</param>
public sealed record ConfirmationCounts(int GuestCycles, int PhotoStrips, int Captures);

/// <summary>A confirmation dialog. Exactly one is open at a time; opening another replaces it.</summary>
/// <param name="Kind">Which dialog.</param>
/// <param name="EventName">The Event the dialog is about, or null for none.</param>
/// <param name="Counts">What deleting removes; set only for <see cref="ConfirmationKind.DeleteEvent"/>.</param>
/// <param name="GuestCycle">The Guest Cycle a delete is for; set only for <see cref="ConfirmationKind.DeleteGuestCycle"/> (protocol version 9).</param>
public sealed record ConfirmationOverlay(ConfirmationKind Kind, string? EventName, ConfirmationCounts? Counts, DeleteGuestCycleConfirmation? GuestCycle);

/// <summary>A display <c>IDENTIFY DISPLAYS</c> numbers.</summary>
/// <param name="Id">The display's stable identity.</param>
/// <param name="Number">The number shown on it.</param>
/// <param name="Name">The friendly name shown with it.</param>
public sealed record IdentifyDisplayEntry(string Id, int Number, string Name);

/// <summary>
/// The Identify displays state: informational for the Operator Screen (the host draws the real overlays on each display).
/// Present for 3 seconds after <c>IdentifyDisplays</c>.
/// </summary>
public sealed record IdentifyDisplaysOverlay(IReadOnlyList<IdentifyDisplayEntry> Displays);

/// <summary>What an operator notice says (the UI owns the copy). A notice stays until <c>DismissNotice</c>.</summary>
public enum OperatorNoticeCode
{
    /// <summary>The Event couldn't be saved (storage failed); nothing was lost from the draft.</summary>
    SaveFailed,

    /// <summary>The Event couldn't be deleted.</summary>
    DeleteFailed,

    /// <summary>The Event no longer exists (for example it was deleted while it was open).</summary>
    EventNotFound,

    /// <summary>The Guest Cycle couldn't be deleted (storage failed); it is back in the gallery (protocol version 9).</summary>
    GuestCycleDeleteFailed,

    /// <summary>The Guest Cycle no longer exists (for example it was deleted already) (protocol version 9).</summary>
    GuestCycleNotFound,
}

/// <summary>A notice on the Operator Screen.</summary>
public sealed record OperatorNoticeOverlay(OperatorNoticeCode Code);
