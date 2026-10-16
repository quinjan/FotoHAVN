using System.Text.Json;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Protocol.Tests;

/// <summary>One sample value per protocol record shape, each snapshotted under <c>Snapshots/&lt;group&gt;/&lt;name&gt;.json</c>.</summary>
internal static class ProtocolSamples
{
    public const string Id = "6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b";

    private static readonly Dictionary<Type, ICommandPayload> CommandPayloadsWithFields = new()
    {
        [typeof(EditEventPayload)] = new EditEventPayload("event-1"),
        [typeof(StartEventPayload)] = new StartEventPayload("event-1"),
        [typeof(DeleteEventPayload)] = new DeleteEventPayload("event-1"),
        [typeof(OpenEventFolderPayload)] = new OpenEventFolderPayload("event-1"),
        [typeof(ChangeEventFieldPayload)] = new ChangeEventFieldPayload(EventField.Date, "2026-10-13"),
        [typeof(SelectCameraPayload)] = new SelectCameraPayload(@"\\?\USB#VID_046D&PID_085E#{e5323777-f976-4f5b-9b55-b94699c46e44}\GLOBAL"),
        [typeof(SelectGuestDisplayPayload)] = new SelectGuestDisplayPayload(@"\\?\DISPLAY#DEL41A8#5&2a0b1c3d&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}"),
        [typeof(SetMirrorPayload)] = new SetMirrorPayload(true),
        [typeof(SetCountdownPayload)] = new SetCountdownPayload(5),
        [typeof(SetOfferedTemplatesPayload)] = new SetOfferedTemplatesPayload(["classic-strip", "grid-four"]),
        [typeof(SetDefaultTemplatePayload)] = new SetDefaultTemplatePayload("classic-strip"),
        [typeof(SetOfferedLooksPayload)] = new SetOfferedLooksPayload(["naturale", "noir"]),
        [typeof(SetDefaultLookPayload)] = new SetDefaultLookPayload("naturale"),
        [typeof(SetPhaseTimerPayload)] = new SetPhaseTimerPayload(PhaseTimer.PhotographsReview, 45),
        [typeof(SetGuestGalleryPayload)] = new SetGuestGalleryPayload(false),
        [typeof(SetPinPayload)] = new SetPinPayload("4826", "4826"),
        [typeof(ConfirmLeavePayload)] = new ConfirmLeavePayload(LeaveChoice.DiscardChanges),
        [typeof(OpenGalleryPayload)] = new OpenGalleryPayload("event-1"),
        [typeof(LoadMoreGuestCyclesPayload)] = new LoadMoreGuestCyclesPayload("cursor-40"),
        [typeof(OpenGuestCyclePayload)] = new OpenGuestCyclePayload("cycle-1"),
        [typeof(OpenGuestCycleFolderPayload)] = new OpenGuestCycleFolderPayload("cycle-1"),
        [typeof(DeleteGuestCyclePayload)] = new DeleteGuestCyclePayload("cycle-1"),
        [typeof(SelectTemplatePayload)] = new SelectTemplatePayload("classic-strip"),
        [typeof(GoToStepPayload)] = new GoToStepPayload(GuestStep.Photographs),
        [typeof(SelectPositionPayload)] = new SelectPositionPayload(2),
        [typeof(BeginRetakePayload)] = new BeginRetakePayload(1),
        [typeof(BeginMovePayload)] = new BeginMovePayload(0),
        [typeof(MoveToPayload)] = new MoveToPayload(3),
        [typeof(DragPlacePayload)] = new DragPlacePayload(0, "capture-7", 12, 2),
        [typeof(SelectLookPayload)] = new SelectLookPayload("noir"),
        [typeof(SubmitPinPayload)] = new SubmitPinPayload("4826"),
        [typeof(LoadMoreGuestStripsPayload)] = new LoadMoreGuestStripsPayload("cursor-40"),
        [typeof(OpenGuestStripPayload)] = new OpenGuestStripPayload("cycle-1"),
    };

    public static IReadOnlyDictionary<Type, ICommandPayload> PayloadsWithFields => CommandPayloadsWithFields;

    /// <summary>A sample payload for every command: the explicit one above, or the empty record.</summary>
    public static ICommandPayload Payload(CommandDescriptor command) =>
        CommandPayloadsWithFields.TryGetValue(command.PayloadType, out var payload)
            ? payload
            : (ICommandPayload)Activator.CreateInstance(command.PayloadType)!;

    private const string CameraId = @"\\?\USB#VID_046D&PID_085E#{e5323777-f976-4f5b-9b55-b94699c46e44}\GLOBAL";

    private const string GuestDisplayId = @"\\?\DISPLAY#DEL41A8#5&2a0b1c3d&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    private static readonly EventRowActions AllActions = new(true, true, true, true, true);

    public static OperatorPresentation EmptyEventList { get; } =
        new(OperatorSurface.EventList, new EventListView([], NewEventAvailable: true), null, null, OperatorOverlays.None, null, null, null);

    public static RunningEventPanel RunningPanel { get; } = new(
        "event-1",
        "Studio open day",
        new RunningEventDisplay("DELL P2419H", RunningDisplayState.Connected),
        new RunningEventCamera(CameraState.Ready, null),
        RunningEventStep.Start,
        37,
        new DateTimeOffset(2026, 10, 13, 9, 30, 0, TimeSpan.FromHours(8)),
        null,
        false);

    public static OperatorPresentation EventListWithOverlays { get; } = new(
        OperatorSurface.EventList,
        new EventListView(
        [
            new EventListRow("event-2", "Lindqvist wedding", new DateOnly(2026, 10, 24), "Harbour Hall", 0, null, false, new EventRowActions(false, true, true, true, true), StartDisabledReason.EventRunning),
            new EventListRow("event-1", "Studio open day", new DateOnly(2026, 10, 13), null, 37, new DateTimeOffset(2026, 10, 13, 18, 5, 0, TimeSpan.FromHours(8)), true, new EventRowActions(false, false, false, true, true), StartDisabledReason.EventRunning),
        ],
        NewEventAvailable: true),
        null,
        RunningPanel,
        new OperatorOverlays(
            new ConfirmationOverlay(ConfirmationKind.DeleteEvent, "Lindqvist wedding", new ConfirmationCounts(12, 11, 41)),
            new IdentifyDisplaysOverlay([new IdentifyDisplayEntry("virtual-operator", 1, "Virtual operator"), new IdentifyDisplayEntry("virtual-guest", 2, "Virtual guest")]),
            new OperatorNoticeOverlay(OperatorNoticeCode.DeleteFailed)),
        null,
        null,
        null);

    public static OperatorPresentation EventListIdle { get; } = new(
        OperatorSurface.EventList,
        new EventListView([new EventListRow("event-3", "Harbour gala", new DateOnly(2026, 6, 12), "Harbour Hall", 3, null, false, AllActions, null)], NewEventAvailable: true),
        null,
        null,
        new OperatorOverlays(new ConfirmationOverlay(ConfirmationKind.EndEvent, null, null), null, null),
        null,
        null,
        null);

    private static readonly IReadOnlyList<EventSetupTemplateOption> SampleTemplates =
    [
        new("signature-strip", "Signature Strip", "Four photographs on a tall strip.", 600, 1800, false, true, true, "signature-strip.naturale-v1.r1"),
        new("panorama-four", "Panorama Four", "Four photographs across a wide card.", 1800, 600, true, true, false, null),
        new("butter-gingham", "Butter Gingham", "Gingham frame.", 600, 1800, false, false, false, null),
        new("polka-keepsake", "Polka Keepsake", "Polka dots.", 600, 1800, false, false, false, null),
    ];

    private static readonly IReadOnlyList<EventSetupLookOption> SampleLooks =
    [
        new("monochrome-v1", "Monochrome", "Black and white.", false, false),
        new("naturale-v1", "Naturale", "True to the room.", true, true),
        new("sepia-v1", "Sepia", "Warm and old.", true, false),
    ];

    private static readonly IReadOnlyList<PhaseTimerOption> SampleTimers =
    [
        new(PhaseTimer.Template, 30, 30, 20, 300, true),
        new(PhaseTimer.PhotographsCapture, 30, 30, 20, 300, true),
        new(PhaseTimer.PhotographsReview, 0, 45, 20, 300, true),
        new(PhaseTimer.Filters, 30, 30, 20, 300, true),
        new(PhaseTimer.Keepsake, 12, 12, 5, 60, false),
        new(PhaseTimer.GuestGallery, 60, 60, 20, 300, false),
    ];

    private static readonly IReadOnlyList<GuestDisplayOption> SampleDisplays =
    [
        new("virtual-operator", "Virtual operator", 0, 0, 960, 1080, 1, true),
        new(GuestDisplayId, "DELL P2419H", 960, 0, 960, 1080, 2, false),
    ];

    /// <summary>A new Event after a save attempt: the errors show, the camera is checking, and no guest display is chosen.</summary>
    public static OperatorPresentation EventSetupNewWithErrors { get; } = new(
        OperatorSurface.EventSetup,
        null,
        new EventSetupView(
            null,
            true,
            true,
            new EventSetupFieldValues(string.Empty, "2026-10-13", string.Empty, string.Empty, string.Empty),
            [
                new EventSetupFieldError(EventSetupFieldNames.Name, EventSetupErrorCode.Required),
                new EventSetupFieldError(EventSetupFieldNames.GuestDisplay, EventSetupErrorCode.DisplayRequired),
                new EventSetupFieldError(EventSetupFieldNames.Timer(PhaseTimer.Keepsake), EventSetupErrorCode.TimerOutOfRange),
                new EventSetupFieldError(EventSetupFieldNames.Pin, EventSetupErrorCode.PinRequired),
            ],
            true,
            new EventSetupCameraView([new CameraOption(CameraId, "Integrated Webcam")], CameraId, "Integrated Webcam", EventSetupCameraState.Checking, null, true, true),
            new EventSetupGuestDisplayView(SampleDisplays, null, null, GuestDisplayState.None, false),
            true,
            5,
            PrinterChoice.None,
            SampleTemplates,
            SampleLooks,
            SampleTimers,
            false,
            new EventSetupPinView(false, false),
            true,
            null,
            null,
            false),
        null,
        OperatorOverlays.None,
        null,
        null,
        null);

    /// <summary>A saved Event edited while another runs: the camera is held by the running Event, the display is the operator's, and Save and start is disabled.</summary>
    public static OperatorPresentation EventSetupEditingWhileRunning { get; } = new(
        OperatorSurface.EventSetup,
        null,
        new EventSetupView(
            "event-3",
            false,
            false,
            new EventSetupFieldValues("Harbour gala", "2026-06-12", "Harbour Hall", "Lindqvist & Co", "Bring the props."),
            [],
            false,
            new EventSetupCameraView([new CameraOption(CameraId, "Integrated Webcam")], CameraId, "Integrated Webcam", EventSetupCameraState.InUseByRunningEvent, null, false, true),
            new EventSetupGuestDisplayView(SampleDisplays, "virtual-operator", "Virtual operator", GuestDisplayState.Connected, true),
            true,
            10,
            PrinterChoice.None,
            SampleTemplates,
            SampleLooks,
            SampleTimers,
            true,
            new EventSetupPinView(true, false),
            false,
            StartDisabledReason.EventRunning,
            new EventSetupStartFailure(StartFailureCode.EventRunning, null),
            false),
        RunningPanel,
        new OperatorOverlays(new ConfirmationOverlay(ConfirmationKind.LeaveSetup, "Harbour gala", null), null, null),
        null,
        null,
        null);

    /// <summary>The single-screen placeholder: the Guest Screen covers the Operator Screen, and the panel carries an alert.</summary>
    public static OperatorPresentation OperatorGuestScreenActive { get; } = new(
        OperatorSurface.GuestScreenActive,
        null,
        null,
        RunningPanel with
        {
            GuestDisplay = new RunningEventDisplay("DELL P2419H", RunningDisplayState.Disconnected),
            Camera = new RunningEventCamera(CameraState.NotAvailable, CameraFailureKind.Removed),
            CurrentStep = RunningEventStep.OperatorAssistance,
            Assistance = new RunningEventAssistance(AssistanceReason.Camera),
            SingleScreen = true,
        },
        OperatorOverlays.None,
        null,
        null,
        null);

    public static OperatorPresentation OperatorFatal { get; } =
        new(OperatorSurface.Fatal, null, null, null, OperatorOverlays.None, new FatalView("The Data folder can't be written."), null, null);

    public static OperatorPresentation OperatorStorageError { get; } =
        new(OperatorSurface.StorageError, null, null, null, OperatorOverlays.None, null, new StorageErrorView(StorageErrorReason.CloudSynced, @"C:\Users\Booth\OneDrive\FOTOHAVN\Data"), null);

    public static OperatorPresentation OperatorStarting { get; } =
        new(OperatorSurface.Starting, null, null, null, OperatorOverlays.None, null, null, null);

    public static OperatorPresentation OperatorCameraDiagnostics { get; } = new(
        OperatorSurface.CameraDiagnostics,
        null,
        null,
        null,
        OperatorOverlays.None,
        null,
        null,
        new CameraDiagnosticsView(
            [new CameraOption(CameraId, "Integrated Webcam"), new CameraOption("fake-camera", "Fake camera")],
            CameraId,
            CameraState.Ready,
            null,
            new CameraFormatView(1920, 1080, 30),
            true,
            new CameraCaptureView("capture-20261014-071910-123.jpg", 1920, 1080, 412_345, true),
            new CompositionView("signature-strip.naturale-v1.r1", "signature-strip", "naturale-v1", 600, 1800)));

    public static OperatorPresentation OperatorCameraDiagnosticsInUse { get; } = new(
        OperatorSurface.CameraDiagnostics,
        null,
        null,
        null,
        OperatorOverlays.None,
        null,
        null,
        new CameraDiagnosticsView([new CameraOption("fake-camera", "Fake camera")], "fake-camera", CameraState.InUseByAnotherApp, CameraFailureKind.InUseByAnotherApp, null, false, null, null));

    public static GuestPresentation GuestStart { get; } =
        new(GuestSurface.Start, new StartView(false, false), null, GuestOverlays.None, null, null, null, null, null, null, null);

    public static GuestPresentation GuestStartReady { get; } =
        new(GuestSurface.Start, new StartView(true, true), null, new GuestOverlays(new ExitHoldOverlay(true), null, null, null, null), null, null, null, null, null, null, null);

    public static GuestPresentation GuestStartUnavailable { get; } =
        new(GuestSurface.StartUnavailable, null, new StartUnavailableView(StartUnavailableReason.Camera, true), GuestOverlays.None, null, null, null, null, null, null, null);

    public static GuestPresentation GuestPinDialogWrong { get; } = new(
        GuestSurface.Start,
        new StartView(true, false),
        null,
        new GuestOverlays(null, new PinDialogOverlay(PinDialogError.WrongPin, null, 3), null, null, null),
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    public static GuestPresentation GuestPinDialogLocked { get; } = new(
        GuestSurface.Start,
        new StartView(true, false),
        null,
        new GuestOverlays(null, new PinDialogOverlay(PinDialogError.WrongPin, 27, null), null, null, null),
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    public static GuestPresentation GuestStartWithOverlays { get; } = new(
        GuestSurface.StartUnavailable,
        null,
        new StartUnavailableView(StartUnavailableReason.Storage, false),
        new GuestOverlays(new ExitHoldOverlay(true), new PinDialogOverlay(null, null, 5), new PhaseTimerWarningOverlay(PhaseTimer.Template, 14), new StartOverConfirmationOverlay(), new NoticeOverlay("One moment.")),
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    public static GuestPresentation GuestFatal { get; } =
        new(GuestSurface.Fatal, null, null, GuestOverlays.None, new FatalView("Something went wrong."), null, null, null, null, null, null);

    private static GuestCycleView Cycle(GuestCycleStepName step, bool startOverAvailable = true) => new(
        "cycle-1",
        step,
        [
            new GuestCycleStepEntry(GuestCycleStepName.Template, step == GuestCycleStepName.Template ? GuestStepState.Current : GuestStepState.Done, step != GuestCycleStepName.Template && step != GuestCycleStepName.Keepsake),
            new GuestCycleStepEntry(GuestCycleStepName.Photographs, step switch { GuestCycleStepName.Template => GuestStepState.Upcoming, GuestCycleStepName.Photographs => GuestStepState.Current, _ => GuestStepState.Done }, step is GuestCycleStepName.Filters),
            new GuestCycleStepEntry(GuestCycleStepName.Filters, step switch { GuestCycleStepName.Filters => GuestStepState.Current, GuestCycleStepName.Keepsake => GuestStepState.Done, _ => GuestStepState.Upcoming }, false),
            new GuestCycleStepEntry(GuestCycleStepName.Keepsake, step == GuestCycleStepName.Keepsake ? GuestStepState.Current : GuestStepState.Upcoming, false),
        ],
        startOverAvailable);

    private static readonly TemplateWindowView[] StripWindows = [.. Enumerable.Range(0, 4).Select(index => new TemplateWindowView(0, index * 450, 600, 450))];

    private static readonly TemplateOptionView[] TemplateOptions =
    [
        new("signature-strip", "Signature Strip", "Four edge-to-edge photographs with a signature across the middle.", 600, 1800, false, StripWindows, "signature-strip.blank"),
        new("panorama-four", "Panorama Four", "Four portraits side by side, with a quiet corner signature.", 1800, 600, true, [.. Enumerable.Range(0, 4).Select(index => new TemplateWindowView(index * 450, 0, 450, 600))], null),
    ];

    /// <summary>The Template step.</summary>
    public static GuestPresentation GuestTemplate { get; } = new(
        GuestSurface.Template, null, null, GuestOverlays.None, null, Cycle(GuestCycleStepName.Template), new TemplateView(TemplateOptions, "signature-strip", true), null, null, null, null);

    /// <summary>The Photographs step on the Camera tab, in the middle of a countdown, with the Phase Timer warning showing.</summary>
    public static GuestPresentation GuestPhotographsCountdown { get; } = new(
        GuestSurface.Photographs,
        null,
        null,
        new GuestOverlays(null, null, new PhaseTimerWarningOverlay(PhaseTimer.PhotographsCapture, 18), null, null),
        null,
        Cycle(GuestCycleStepName.Photographs),
        null,
        new PhotographsView(
            PhotographsTabName.Camera,
            new PhotographsTemplateView("signature-strip", 600, 1800, false, StripWindows, "signature-strip.blank"),
            [
                new PhotographsWindowView(0, "capture-1", "https://media.fotohavn/events/event-1/guest-cycles/cycle-1/capture-1.jpg"),
                new PhotographsWindowView(1, null, null),
                new PhotographsWindowView(2, null, null),
                new PhotographsWindowView(3, null, null),
            ],
            0,
            new CaptureSequenceView(true, 1, 3, 1, 1, 3, false),
            true,
            600.0 / 450,
            new PhotographSelectionView(null, PhotographSelectionMode.None),
            null,
            1,
            false,
            false,
            false),
        null,
        null,
        null);

    /// <summary>The Photographs step on the Arrange tab with a Retake candidate waiting.</summary>
    public static GuestPresentation GuestPhotographsRetake { get; } = new(
        GuestSurface.Photographs,
        null,
        null,
        GuestOverlays.None,
        null,
        Cycle(GuestCycleStepName.Photographs),
        null,
        new PhotographsView(
            PhotographsTabName.Arrange,
            new PhotographsTemplateView("panorama-four", 1800, 600, true, [.. Enumerable.Range(0, 4).Select(index => new TemplateWindowView(index * 450, 0, 450, 600))], null),
            [.. Enumerable.Range(0, 4).Select(index => new PhotographsWindowView(index, $"capture-{index + 1}", $"https://media.fotohavn/events/event-1/guest-cycles/cycle-1/capture-{index + 1}.jpg"))],
            1,
            new CaptureSequenceView(false, null, null, 4, 0, 0, false),
            false,
            450.0 / 600,
            new PhotographSelectionView(2, PhotographSelectionMode.Retaking),
            new RetakeCandidateView(2, "https://media.fotohavn/events/event-1/guest-cycles/cycle-1/capture-5.jpg"),
            6,
            false,
            true,
            false),
        null,
        null,
        null);

    /// <summary>The Filters step after a composition failed.</summary>
    public static GuestPresentation GuestFiltersFailed { get; } = new(
        GuestSurface.Filters,
        null,
        null,
        GuestOverlays.None,
        null,
        Cycle(GuestCycleStepName.Filters),
        null,
        null,
        new FiltersView(
            [new LookOptionView("naturale-v1", "Naturale", "Original colors. No filter.", "signature-strip.naturale-v1.thumb"), new LookOptionView("sepia-v1", "Sepia", "Soft brown tones with faded contrast.", null)],
            "naturale-v1",
            600,
            1800,
            false,
            "signature-strip.naturale-v1.r1",
            false,
            true,
            false),
        null,
        null);

    /// <summary>The Keepsake step, with the Start over confirmation absent and no warning.</summary>
    public static GuestPresentation GuestKeepsake { get; } = new(
        GuestSurface.Keepsake,
        null,
        null,
        GuestOverlays.None,
        null,
        Cycle(GuestCycleStepName.Keepsake, startOverAvailable: false),
        null,
        null,
        null,
        new KeepsakeView("https://media.fotohavn/events/event-1/guest-cycles/cycle-1/photo-strip.png?v=0a1b2c3d", 600, 1800, false),
        null);

    /// <summary>Operator Assistance during a cycle, with the Start over confirmation open.</summary>
    public static GuestPresentation GuestOperatorAssistance { get; } = new(
        GuestSurface.OperatorAssistance,
        null,
        null,
        new GuestOverlays(null, null, null, new StartOverConfirmationOverlay(), null),
        null,
        Cycle(GuestCycleStepName.Photographs),
        null,
        null,
        null,
        null,
        new OperatorAssistanceView(AssistanceReason.Camera, true));

    /// <summary>Every envelope, keyed by snapshot name.</summary>
    public static IReadOnlyList<(string Name, ProtocolMessage Message)> Messages { get; } =
    [
        ("hello", new HelloMessage(ProtocolVersion.Current)),
        ("test-without-args", new TestMessage(Id, TestHookNames.GetDiagnostics)),
        ("test-with-args", new TestMessage(Id, TestHookNames.OpenGuestScreen, ProtocolJson.ToElement(new OpenGuestScreenArgs("virtual-guest")))),
        ("welcome-operator", new OperatorWelcomeMessage(ProtocolVersion.Current, 1, EmptyEventList)),
        ("welcome-guest", new GuestWelcomeMessage(ProtocolVersion.Current, 1, GuestStart)),
        ("presentation-operator-empty-event-list", new OperatorPresentationMessage(2, EmptyEventList)),
        ("presentation-operator-event-list-with-overlays", new OperatorPresentationMessage(3, EventListWithOverlays)),
        ("presentation-operator-fatal", new OperatorPresentationMessage(4, OperatorFatal)),
        ("presentation-operator-storage-error", new OperatorPresentationMessage(5, OperatorStorageError)),
        ("presentation-operator-starting", new OperatorPresentationMessage(6, OperatorStarting)),
        ("presentation-operator-camera-diagnostics", new OperatorPresentationMessage(7, OperatorCameraDiagnostics)),
        ("presentation-operator-camera-diagnostics-in-use", new OperatorPresentationMessage(8, OperatorCameraDiagnosticsInUse)),
        ("presentation-operator-event-list-idle", new OperatorPresentationMessage(9, EventListIdle)),
        ("presentation-operator-event-setup-new-with-errors", new OperatorPresentationMessage(10, EventSetupNewWithErrors)),
        ("presentation-operator-event-setup-editing-while-running", new OperatorPresentationMessage(11, EventSetupEditingWhileRunning)),
        ("presentation-operator-guest-screen-active", new OperatorPresentationMessage(12, OperatorGuestScreenActive)),
        ("presentation-guest-start", new GuestPresentationMessage(2, GuestStart)),
        ("presentation-guest-start-with-overlays", new GuestPresentationMessage(3, GuestStartWithOverlays)),
        ("presentation-guest-fatal", new GuestPresentationMessage(4, GuestFatal)),
        ("presentation-guest-start-ready", new GuestPresentationMessage(5, GuestStartReady)),
        ("presentation-guest-start-unavailable", new GuestPresentationMessage(6, GuestStartUnavailable)),
        ("presentation-guest-pin-dialog-wrong", new GuestPresentationMessage(7, GuestPinDialogWrong)),
        ("presentation-guest-pin-dialog-locked", new GuestPresentationMessage(8, GuestPinDialogLocked)),
        ("presentation-guest-template", new GuestPresentationMessage(9, GuestTemplate)),
        ("presentation-guest-photographs-countdown", new GuestPresentationMessage(10, GuestPhotographsCountdown)),
        ("presentation-guest-photographs-retake", new GuestPresentationMessage(11, GuestPhotographsRetake)),
        ("presentation-guest-filters-failed", new GuestPresentationMessage(12, GuestFiltersFailed)),
        ("presentation-guest-keepsake", new GuestPresentationMessage(13, GuestKeepsake)),
        ("presentation-guest-operator-assistance", new GuestPresentationMessage(14, GuestOperatorAssistance)),
        ("command-result-accepted", CommandResultMessage.Accepted(Id)),
        ("command-result-rejected", CommandResultMessage.Rejected(Id, RejectReason.WrongRole)),
        ("protocol-mismatch", new ProtocolMismatchMessage(ProtocolVersion.Current, 0)),
        ("error-with-id", new ErrorMessage(Id, EnvelopeError.InvalidEnvelope)),
        ("error-without-id", new ErrorMessage(null, EnvelopeError.PayloadTooLarge)),
        ("test-result-success", TestResultMessage.Success(Id, EmptyHookResult.Instance)),
        ("test-result-failure", TestResultMessage.Failure(Id, TestHookError.HooksUnavailable)),
        ("test-result-failure-unknown-camera", TestResultMessage.Failure(Id, TestHookError.UnknownCamera)),
        ("frame-ack", new FrameAckMessage(4_294_967_295)),
        ("preview-frame-metadata", new PreviewFrameMetadata(42, 1280, 720, PreviewPixelFormat.Rgba, 18_304)),
        ("test-with-camera-args", new TestMessage(Id, TestHookNames.InjectCameraFault, ProtocolJson.ToElement(new InjectCameraFaultArgs(CameraFaultKind.ExclusiveLost)))),
    ];

    private static readonly PixelRect PrimaryBounds = new(0, 0, 1920, 1080);

    private static readonly TestDisplay OperatorDisplay =
        new("virtual-operator", "Virtual operator", new PixelRect(0, 0, 960, 1080), new PixelRect(0, 0, 960, 1080), 1.25, true);

    private static readonly TestDisplay GuestDisplay =
        new("virtual-guest", "Virtual guest", new PixelRect(960, 0, 960, 1080), new PixelRect(960, 0, 960, 1080), 1.25, false);

    /// <summary>Every hook's arguments and result, keyed by snapshot name.</summary>
    public static IReadOnlyList<(string Name, object Value)> HookValues { get; } =
    [
        ("get-diagnostics-result", new DiagnosticsResult(
            ProtocolVersion.Current,
            [Role.Operator, Role.Guest],
            [new PresentationDiagnostics(Role.Operator, 3, "eventList"), new PresentationDiagnostics(Role.Guest, 1, "start")],
            [new CommandResultDiagnostics(Id, "OpenNewEvent", Role.Guest, CommandStatus.Rejected, RejectReason.WrongRole)],
            [new TestWindow(Role.Operator, PrimaryBounds, OperatorDisplay.Id), new TestWindow(Role.Guest, GuestDisplay.Bounds, GuestDisplay.Id)],
            new DisplayDiagnostics(true, [OperatorDisplay, GuestDisplay]),
            true,
            new StorageDiagnostics(
                StorageDiagnosticsState.Error,
                StorageErrorReason.DiskFull,
                1,
                new ReconcileDiagnostics(1, 2, 3, 4, 5, 6, 7, 8, 9, 10)),
            new CameraDiagnostics(CameraState.Ready, "fake-camera", new CameraFormatView(1280, 720, 30), 41, 29.5, 3, PreviewTarget.Operator),
            [new OpenFolderRequest(@"C:\FOTOHAVN\Data\Events\6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b", new DateTimeOffset(2026, 10, 13, 9, 45, 0, TimeSpan.Zero))],
            [new IdentifyOverlayDiagnostics("virtual-operator", 1, false), new IdentifyOverlayDiagnostics("virtual-guest", 2, false)],
            new WakeLockDiagnostics(true),
            10,
            null)),
        ("get-diagnostics-result-guest-cycle", new DiagnosticsResult(
            ProtocolVersion.Current,
            [Role.Operator, Role.Guest],
            [new PresentationDiagnostics(Role.Operator, 5, "guestScreenActive"), new PresentationDiagnostics(Role.Guest, 12, "photographs")],
            [],
            [],
            new DisplayDiagnostics(false, []),
            true,
            new StorageDiagnostics(StorageDiagnosticsState.Ready, null, 1, null),
            new CameraDiagnostics(CameraState.Ready, "fake-camera", new CameraFormatView(1280, 720, 30), 12, 30, 0, PreviewTarget.Guest),
            [],
            [],
            new WakeLockDiagnostics(true),
            10,
            new GuestCycleDiagnostics("cycle-1", GuestCycleStepName.Photographs, PhotographsTabName.Camera, 1, 4, true, 3, new GuestCycleTimerDiagnostics(PhaseTimer.PhotographsCapture, 18_250, true, true)))),
        ("set-clock-scale-args", new SetClockScaleArgs(10)),
        ("camera-diagnostics-idle", new CameraDiagnostics(CameraState.Idle, null, null, null, null, null, PreviewTarget.None)),
        ("select-diagnostics-camera-args", new SelectDiagnosticsCameraArgs("fake-camera")),
        ("set-diagnostics-mirror-args", new SetDiagnosticsMirrorArgs(true)),
        ("inject-camera-fault-args", new InjectCameraFaultArgs(CameraFaultKind.Stale)),
        ("capture-diagnostics-result", new CameraCaptureView("capture-20261014-071910-123.jpg", 1280, 720, 98_304, false)),
        ("compose-diagnostics-args", new ComposeDiagnosticsArgs("signature-strip", "naturale-v1")),
        ("compose-blank-preview-args", new ComposeBlankPreviewArgs("panorama-four")),
        ("compose-preview-result", new ComposePreviewResult("signature-strip.naturale-v1.r1", 600, 1800, 4_096)),
        ("storage-diagnostics-ready", new StorageDiagnostics(StorageDiagnosticsState.Ready, null, 1, null)),
        ("inject-storage-fault-args", new InjectStorageFaultArgs(StorageFaultKind.DiskFull, StorageFaultScope.AllWrites)),
        ("use-virtual-displays-args", new UseVirtualDisplaysArgs(
        [
            new VirtualDisplaySpec("virtual-operator", "Virtual operator", new PixelRect(0, 0, 960, 1080), true),
            new VirtualDisplaySpec("virtual-guest", "Virtual guest", new PixelRect(960, 0, 960, 1080), false, 1.25),
        ])),
        ("use-virtual-displays-result", new UseVirtualDisplaysResult([OperatorDisplay, GuestDisplay])),
        ("disconnect-display-args", new DisconnectDisplayArgs("virtual-guest")),
        ("connect-display-args", new ConnectDisplayArgs("virtual-guest")),
        ("open-guest-screen-args", new OpenGuestScreenArgs("virtual-guest")),
        ("set-window-bounds-args", new SetWindowBoundsArgs(Role.Guest, 1366, 768)),
        ("set-window-bounds-result", new TestWindow(Role.Guest, new PixelRect(960, 0, 1366, 768), "virtual-guest")),
        ("crash-ui-args", new CrashUiArgs(Role.Guest)),
        ("list-cameras-result", new ListCamerasResult([new TestCamera(@"\\?\USB#VID_046D&PID_085E#{e5323777-f976-4f5b-9b55-b94699c46e44}\GLOBAL", "Integrated Webcam")])),
        ("empty-result", EmptyHookResult.Instance),
    ];

    /// <summary>Re-serializes a snapshot after reading it into <paramref name="type"/>, to prove it round-trips.</summary>
    public static string RoundTrip(string json, Type type) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize(json, type, ProtocolJson.Options), type, ProtocolJson.Options);
}
