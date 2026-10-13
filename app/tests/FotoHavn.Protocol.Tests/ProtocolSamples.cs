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

    public static OperatorPresentation EmptyEventList { get; } =
        new(OperatorSurface.EventList, new EventListView([]), null, OperatorOverlays.None, null, null, null);

    public static OperatorPresentation EventListWithOverlays { get; } = new(
        OperatorSurface.EventList,
        new EventListView(
        [
            new EventListRow("event-2", "Lindqvist wedding", new DateOnly(2026, 10, 24), "Harbour Hall", 0, null, false),
            new EventListRow("event-1", "Studio open day", new DateOnly(2026, 10, 13), null, 37, new DateTimeOffset(2026, 10, 13, 18, 5, 0, TimeSpan.FromHours(8)), true),
        ]),
        new RunningEventPanel(),
        new OperatorOverlays(new ConfirmationOverlay(), new IdentifyDisplaysOverlay(), new NoticeOverlay("The chosen guest display isn't connected.")),
        null,
        null,
        null);

    public static OperatorPresentation OperatorFatal { get; } =
        new(OperatorSurface.Fatal, null, null, OperatorOverlays.None, new FatalView("The Data folder can't be written."), null, null);

    public static OperatorPresentation OperatorStorageError { get; } =
        new(OperatorSurface.StorageError, null, null, OperatorOverlays.None, null, new StorageErrorView(StorageErrorReason.CloudSynced, @"C:\Users\Booth\OneDrive\FOTOHAVN\Data"), null);

    public static OperatorPresentation OperatorStarting { get; } =
        new(OperatorSurface.Starting, null, null, OperatorOverlays.None, null, null, null);

    public static OperatorPresentation OperatorCameraDiagnostics { get; } = new(
        OperatorSurface.CameraDiagnostics,
        null,
        null,
        OperatorOverlays.None,
        null,
        null,
        new CameraDiagnosticsView(
            [new CameraOption(@"\\?\USB#VID_046D&PID_085E#{e5323777-f976-4f5b-9b55-b94699c46e44}\GLOBAL", "Integrated Webcam"), new CameraOption("fake-camera", "Fake camera")],
            @"\\?\USB#VID_046D&PID_085E#{e5323777-f976-4f5b-9b55-b94699c46e44}\GLOBAL",
            CameraState.Ready,
            null,
            new CameraFormatView(1920, 1080, 30),
            true,
            new CameraCaptureView("capture-20261014-071910-123.jpg", 1920, 1080, 412_345, true)));

    public static OperatorPresentation OperatorCameraDiagnosticsInUse { get; } = new(
        OperatorSurface.CameraDiagnostics,
        null,
        null,
        OperatorOverlays.None,
        null,
        null,
        new CameraDiagnosticsView([new CameraOption("fake-camera", "Fake camera")], "fake-camera", CameraState.InUseByAnotherApp, CameraFailureKind.InUseByAnotherApp, null, false, null));

    public static GuestPresentation GuestStart { get; } =
        new(GuestSurface.Start, new StartView(false), GuestOverlays.None, null);

    public static GuestPresentation GuestStartWithOverlays { get; } = new(
        GuestSurface.StartUnavailable,
        new StartView(true),
        new GuestOverlays(new ExitHoldOverlay(), new PinDialogOverlay(), new PhaseTimerWarningOverlay(), new StartOverConfirmationOverlay(), new NoticeOverlay("One moment.")),
        null);

    public static GuestPresentation GuestFatal { get; } =
        new(GuestSurface.Fatal, null, GuestOverlays.None, new FatalView("Something went wrong."));

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
        ("presentation-guest-start", new GuestPresentationMessage(2, GuestStart)),
        ("presentation-guest-start-with-overlays", new GuestPresentationMessage(3, GuestStartWithOverlays)),
        ("presentation-guest-fatal", new GuestPresentationMessage(4, GuestFatal)),
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
            new CameraDiagnostics(CameraState.Ready, "fake-camera", new CameraFormatView(1280, 720, 30), 41, 29.5, 3, PreviewTarget.Operator))),
        ("camera-diagnostics-idle", new CameraDiagnostics(CameraState.Idle, null, null, null, null, null, PreviewTarget.None)),
        ("select-diagnostics-camera-args", new SelectDiagnosticsCameraArgs("fake-camera")),
        ("set-diagnostics-mirror-args", new SetDiagnosticsMirrorArgs(true)),
        ("inject-camera-fault-args", new InjectCameraFaultArgs(CameraFaultKind.Stale)),
        ("capture-diagnostics-result", new CameraCaptureView("capture-20261014-071910-123.jpg", 1280, 720, 98_304, false)),
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
