using FotoHavn.Protocol;

namespace FotoHavn.Core;

// Command dispatch (plan section 9, Commands; M6 contract section 3). One switch routes every command to the method in the
// file that owns it, so the milestone's parallel work never edits the same lines:
//   Brain.EventList.cs     Event list commands                       (M6 wave B1a)
//   Brain.EventSetup.cs    Event setup commands                      (M6 wave B1a)
//   Brain.ActiveEvent.cs   StartEvent, End Event, exit hold and PIN  (M6 wave B1b)
//   Brain.Operator.cs      DismissNotice
//   Brain.Storage.cs       RetryStorage
//   Brain.GuestCycle.cs    the Guest Cycle commands                  (M8 wave B1)
//   Brain.Assistance.cs    RetryAssistance                           (M8 wave B1)
// A command nothing implements yet (the galleries; the Guest Cycle until M8 wave B1) is rejected notAvailable. Handlers run on the executor,
// answer at once, and never wait for I/O: they start it and post the outcome back (contract section 9, Threading).
public sealed partial class Brain
{
    private static CommandResultMessage NotAvailable(string id) => CommandResultMessage.Rejected(id, RejectReason.NotAvailable);

    private CommandResultMessage DispatchCommand(string id, ICommandPayload payload) => payload switch
    {
        // Storage
        RetryStoragePayload => RetryStorage(id),

        // Operator overlays (Brain.Operator.cs)
        DismissNoticePayload => DismissNotice(id),

        // Event list (Brain.EventList.cs)
        OpenNewEventPayload => OpenNewEvent(id),
        EditEventPayload edit => EditEvent(id, edit),
        DeleteEventPayload delete => DeleteEvent(id, delete),
        ConfirmDeleteEventPayload => ConfirmDeleteEvent(id),
        CancelDeleteEventPayload => CancelDeleteEvent(id),
        OpenEventFolderPayload folder => OpenEventFolder(id, folder),

        // Event setup (Brain.EventSetup.cs)
        ChangeEventFieldPayload change => ChangeEventField(id, change),
        SelectCameraPayload camera => SelectCamera(id, camera),
        SelectGuestDisplayPayload display => SelectGuestDisplay(id, display),
        IdentifyDisplaysPayload => IdentifyDisplays(id),
        SetMirrorPayload mirror => SetMirror(id, mirror),
        SetCountdownPayload countdown => SetCountdown(id, countdown),
        SetOfferedTemplatesPayload templates => SetOfferedTemplates(id, templates),
        SetDefaultTemplatePayload template => SetDefaultTemplate(id, template),
        SetOfferedLooksPayload looks => SetOfferedLooks(id, looks),
        SetDefaultLookPayload look => SetDefaultLook(id, look),
        SetPhaseTimerPayload timer => SetPhaseTimer(id, timer),
        SetGuestGalleryPayload gallery => SetGuestGallery(id, gallery),
        SetPinPayload pin => SetPin(id, pin),
        SaveEventPayload => SaveEvent(id),
        SaveAndStartEventPayload => SaveAndStartEvent(id),
        LeaveEventSetupPayload => LeaveEventSetup(id),
        ConfirmLeavePayload leave => ConfirmLeave(id, leave),

        // The running Event and the Guest Screen's exit (Brain.ActiveEvent.cs)
        StartEventPayload start => StartEvent(id, start),
        EndEventPayload => EndEvent(id),
        ConfirmEndEventPayload => ConfirmEndEvent(id),
        KeepRunningPayload => KeepRunning(id),
        BeginExitHoldPayload => BeginExitHold(id),
        CompleteExitHoldPayload => CompleteExitHold(id),
        SubmitPinPayload pin => SubmitPin(id, pin),
        CancelExitPayload => CancelExit(id),

        // The Guest Cycle (Brain.GuestCycle.cs) and Operator Assistance (Brain.Assistance.cs)
        BeginGuestCyclePayload => BeginGuestCycle(id),
        SelectTemplatePayload template => SelectTemplate(id, template),
        GoToStepPayload step => GoToStep(id, step),
        ShowCameraTabPayload => ShowCameraTab(id),
        ShowArrangeTabPayload => ShowArrangeTab(id),
        TakePhotographsPayload => TakePhotographs(id),
        StopCapturePayload => StopCapture(id),
        SelectPositionPayload position => SelectPosition(id, position),
        BeginRetakePayload retake => BeginRetake(id, retake),
        AcceptRetakePayload => AcceptRetake(id),
        TryAgainPayload => TryAgain(id),
        KeepOriginalPayload => KeepOriginal(id),
        BeginMovePayload move => BeginMove(id, move),
        MoveToPayload moveTo => MoveTo(id, moveTo),
        CancelMovePayload => CancelMove(id),
        DragPlacePayload drag => DragPlace(id, drag),
        SelectLookPayload look => SelectLook(id, look),
        FinishStripPayload => FinishStrip(id),
        RetryCompositionPayload => RetryComposition(id),
        MoreTimePayload => MoreTime(id),
        StartOverPayload => StartOver(id),
        ConfirmStartOverPayload => ConfirmStartOver(id),
        KeepGoingPayload => KeepGoing(id),
        RetryAssistancePayload => RetryAssistance(id),

        _ => NotAvailable(id),
    };
}
