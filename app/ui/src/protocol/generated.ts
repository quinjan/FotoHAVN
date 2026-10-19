/*
 * generated — do not edit.
 * Generated from app/src/FotoHavn.Protocol/schema/protocol.schema.json by json-schema-to-typescript.
 * Change the C# records in FotoHavn.Protocol, then run app/scripts/generate-protocol.ps1.
 */

/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ClientMessage".
 */
export type ClientMessage = HelloMessage | Command | TestMessage | FrameAckMessage;
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "Command".
 */
export type Command =
  | OpenNewEventCommand
  | EditEventCommand
  | StartEventCommand
  | DeleteEventCommand
  | ConfirmDeleteEventCommand
  | CancelDeleteEventCommand
  | OpenEventFolderCommand
  | DismissNoticeCommand
  | ChangeEventFieldCommand
  | SelectCameraCommand
  | SelectGuestDisplayCommand
  | IdentifyDisplaysCommand
  | SetMirrorCommand
  | SetCountdownCommand
  | SetOfferedTemplatesCommand
  | SetDefaultTemplateCommand
  | SetOfferedLooksCommand
  | SetDefaultLookCommand
  | SetPhaseTimerCommand
  | SetGuestGalleryCommand
  | SetGuestSoundsCommand
  | SetPinCommand
  | SaveEventCommand
  | SaveAndStartEventCommand
  | LeaveEventSetupCommand
  | ConfirmLeaveCommand
  | OpenGalleryCommand
  | LoadMoreGuestCyclesCommand
  | OpenGuestCycleCommand
  | CloseGuestCycleCommand
  | OpenGuestCycleFolderCommand
  | DeleteGuestCycleCommand
  | ConfirmDeleteGuestCycleCommand
  | CancelDeleteGuestCycleCommand
  | CloseGalleryCommand
  | EndEventCommand
  | ConfirmEndEventCommand
  | KeepRunningCommand
  | RetryAssistanceCommand
  | ConfirmCloseAppCommand
  | CancelCloseAppCommand
  | RetryStorageCommand
  | BeginGuestCycleCommand
  | SelectTemplateCommand
  | GoToStepCommand
  | ShowCameraTabCommand
  | ShowArrangeTabCommand
  | TakePhotographsCommand
  | SelectPositionCommand
  | BeginRetakeCommand
  | AcceptRetakeCommand
  | TryAgainCommand
  | KeepOriginalCommand
  | BeginMoveCommand
  | MoveToCommand
  | CancelMoveCommand
  | DragPlaceCommand
  | SelectLookCommand
  | FinishStripCommand
  | RetryCompositionCommand
  | BeginExitHoldCommand
  | CompleteExitHoldCommand
  | SubmitPinCommand
  | CancelExitCommand
  | OpenGuestGalleryCommand
  | LoadMoreGuestStripsCommand
  | OpenGuestStripCommand
  | CloseGuestStripCommand
  | BackToStartCommand;
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventField".
 */
export type EventField = "name" | "date" | "venue" | "customer" | "notes";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PhaseTimer".
 */
export type PhaseTimer =
  "template" | "photographsCapture" | "photographsReview" | "filters" | "keepsake" | "guestGallery";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "LeaveChoice".
 */
export type LeaveChoice = "save" | "discardChanges" | "keepEditing";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestStep".
 */
export type GuestStep = "template" | "photographs" | "filters";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ServerMessage".
 */
export type ServerMessage =
  | OperatorWelcomeMessage
  | GuestWelcomeMessage
  | OperatorPresentationMessage
  | GuestPresentationMessage
  | CommandResultMessage
  | ProtocolMismatchMessage
  | ErrorMessage
  | TestResultMessage;
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OperatorSurface".
 */
export type OperatorSurface =
  | "eventList"
  | "eventSetup"
  | "eventGallery"
  | "guestCycleDetail"
  | "guestScreenActive"
  | "fatal"
  | "storageError"
  | "starting"
  | "cameraDiagnostics";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StartDisabledReason".
 */
export type StartDisabledReason = "eventRunning";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventSetupErrorCode".
 */
export type EventSetupErrorCode =
  | "required"
  | "tooLong"
  | "invalidDate"
  | "atLeastOneTemplate"
  | "defaultTemplateNotOffered"
  | "atLeastOneLook"
  | "defaultLookNotOffered"
  | "timerOutOfRange"
  | "pinFormat"
  | "pinMismatch"
  | "pinRequired"
  | "cameraRequired"
  | "displayRequired";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventSetupCameraState".
 */
export type EventSetupCameraState =
  | "none"
  | "checking"
  | "ready"
  | "notAvailable"
  | "inUseByAnotherApp"
  | "inUseByRunningEvent"
  | "missing"
  | "notChecked";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CameraFailureKind".
 */
export type CameraFailureKind =
  | "stale"
  | "failed"
  | "removed"
  | "exclusiveLost"
  | "inUseByAnotherApp"
  | "privacyDenied"
  | "noSupportedFormat"
  | "notFound"
  | "unknown";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestDisplayState".
 */
export type GuestDisplayState = "none" | "connected" | "missing";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PrinterChoice".
 */
export type PrinterChoice = "none";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StartFailureCode".
 */
export type StartFailureCode =
  | "eventRunning"
  | "guestDisplayMissing"
  | "cameraNotEligible"
  | "storageNotWritable"
  | "pinMissing"
  | "invalidSetup"
  | "guestScreenNotReady";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RunningDisplayState".
 */
export type RunningDisplayState = "connected" | "disconnected";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CameraState".
 */
export type CameraState = "idle" | "checking" | "ready" | "notAvailable" | "inUseByAnotherApp";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RunningEventStep".
 */
export type RunningEventStep =
  "start" | "template" | "photographs" | "filters" | "keepsake" | "guestGallery" | "operatorAssistance";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "AssistanceReason".
 */
export type AssistanceReason = "camera" | "storage" | "guestDisplay";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmationKind".
 */
export type ConfirmationKind = "deleteEvent" | "endEvent" | "leaveSetup" | "deleteGuestCycle" | "closeApp";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OperatorNoticeCode".
 */
export type OperatorNoticeCode =
  "saveFailed" | "deleteFailed" | "eventNotFound" | "guestCycleDeleteFailed" | "guestCycleNotFound";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StorageErrorReason".
 */
export type StorageErrorReason =
  | "diskFull"
  | "accessDenied"
  | "locked"
  | "notLocalDisk"
  | "cloudSynced"
  | "notWritable"
  | "integrityFailed"
  | "corrupt"
  | "unknown";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GalleryOutcome".
 */
export type GalleryOutcome = "completed" | "startedOver" | "endedByOperator" | "appClosed" | "unrecoverable";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestSurface".
 */
export type GuestSurface =
  | "start"
  | "startUnavailable"
  | "guestGallery"
  | "guestStripDetail"
  | "template"
  | "photographs"
  | "filters"
  | "keepsake"
  | "operatorAssistance"
  | "fatal";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StartUnavailableReason".
 */
export type StartUnavailableReason = "camera" | "storage";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PinDialogError".
 */
export type PinDialogError = "wrongPin";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestCycleStepName".
 */
export type GuestCycleStepName = "template" | "photographs" | "filters" | "keepsake";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestStepState".
 */
export type GuestStepState = "done" | "current" | "upcoming" | "skipped";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PhotographsTabName".
 */
export type PhotographsTabName = "camera" | "arrange";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PhotographSelectionMode".
 */
export type PhotographSelectionMode = "none" | "selected" | "moving" | "retaking";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CommandStatus".
 */
export type CommandStatus = "accepted" | "rejected";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RejectReason".
 */
export type RejectReason =
  | "unknownCommand"
  | "wrongRole"
  | "invalidPayload"
  | "notAvailable"
  | "eventRunning"
  | "protocolMismatch"
  | "payloadTooLarge"
  | "validationFailed"
  | "pinLocked";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EnvelopeError".
 */
export type EnvelopeError = "invalidJson" | "invalidEnvelope" | "unknownType" | "invalidId" | "payloadTooLarge";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TestHookError".
 */
export type TestHookError =
  | "hooksUnavailable"
  | "unknownHook"
  | "invalidArgs"
  | "hookFailed"
  | "notSupported"
  | "unknownDisplay"
  | "unknownCamera"
  | "compositionFailed";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "Role".
 */
export type Role = "operator" | "guest";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StorageDiagnosticsState".
 */
export type StorageDiagnosticsState = "opening" | "ready" | "error";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PreviewTarget".
 */
export type PreviewTarget = "none" | "operator" | "guest";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StorageFaultKind".
 */
export type StorageFaultKind = "diskFull" | "accessDenied" | "locked";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StorageFaultScope".
 */
export type StorageFaultScope = "nextWrite" | "allWrites";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CameraFaultKind".
 */
export type CameraFaultKind = "stale" | "failed" | "removed" | "exclusiveLost";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PreviewPixelFormat".
 */
export type PreviewPixelFormat = "rgba";

/**
 * The FOTOHAVN UI and brain protocol. Generated from FotoHavn.Protocol by scripts/generate-protocol.ps1; do not edit.
 */
export interface FotoHavnProtocol {
  protocolVersion: 12;
  clientMessage: ClientMessage;
  serverMessage: ServerMessage;
  commandPayloads: CommandPayloadMap;
  testHooks: TestHookMap;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "HelloMessage".
 */
export interface HelloMessage {
  type: "hello";
  protocolVersion: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenNewEventCommand".
 */
export interface OpenNewEventCommand {
  type: "command";
  id: string;
  name: "OpenNewEvent";
  payload: OpenNewEventPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenNewEventPayload".
 */
export interface OpenNewEventPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EditEventCommand".
 */
export interface EditEventCommand {
  type: "command";
  id: string;
  name: "EditEvent";
  payload: EditEventPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EditEventPayload".
 */
export interface EditEventPayload {
  eventId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StartEventCommand".
 */
export interface StartEventCommand {
  type: "command";
  id: string;
  name: "StartEvent";
  payload: StartEventPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StartEventPayload".
 */
export interface StartEventPayload {
  eventId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DeleteEventCommand".
 */
export interface DeleteEventCommand {
  type: "command";
  id: string;
  name: "DeleteEvent";
  payload: DeleteEventPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DeleteEventPayload".
 */
export interface DeleteEventPayload {
  eventId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmDeleteEventCommand".
 */
export interface ConfirmDeleteEventCommand {
  type: "command";
  id: string;
  name: "ConfirmDeleteEvent";
  payload: ConfirmDeleteEventPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmDeleteEventPayload".
 */
export interface ConfirmDeleteEventPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CancelDeleteEventCommand".
 */
export interface CancelDeleteEventCommand {
  type: "command";
  id: string;
  name: "CancelDeleteEvent";
  payload: CancelDeleteEventPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CancelDeleteEventPayload".
 */
export interface CancelDeleteEventPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenEventFolderCommand".
 */
export interface OpenEventFolderCommand {
  type: "command";
  id: string;
  name: "OpenEventFolder";
  payload: OpenEventFolderPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenEventFolderPayload".
 */
export interface OpenEventFolderPayload {
  eventId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DismissNoticeCommand".
 */
export interface DismissNoticeCommand {
  type: "command";
  id: string;
  name: "DismissNotice";
  payload: DismissNoticePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DismissNoticePayload".
 */
export interface DismissNoticePayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ChangeEventFieldCommand".
 */
export interface ChangeEventFieldCommand {
  type: "command";
  id: string;
  name: "ChangeEventField";
  payload: ChangeEventFieldPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ChangeEventFieldPayload".
 */
export interface ChangeEventFieldPayload {
  field: EventField;
  value: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SelectCameraCommand".
 */
export interface SelectCameraCommand {
  type: "command";
  id: string;
  name: "SelectCamera";
  payload: SelectCameraPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SelectCameraPayload".
 */
export interface SelectCameraPayload {
  deviceId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SelectGuestDisplayCommand".
 */
export interface SelectGuestDisplayCommand {
  type: "command";
  id: string;
  name: "SelectGuestDisplay";
  payload: SelectGuestDisplayPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SelectGuestDisplayPayload".
 */
export interface SelectGuestDisplayPayload {
  displayId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "IdentifyDisplaysCommand".
 */
export interface IdentifyDisplaysCommand {
  type: "command";
  id: string;
  name: "IdentifyDisplays";
  payload: IdentifyDisplaysPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "IdentifyDisplaysPayload".
 */
export interface IdentifyDisplaysPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetMirrorCommand".
 */
export interface SetMirrorCommand {
  type: "command";
  id: string;
  name: "SetMirror";
  payload: SetMirrorPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetMirrorPayload".
 */
export interface SetMirrorPayload {
  enabled: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetCountdownCommand".
 */
export interface SetCountdownCommand {
  type: "command";
  id: string;
  name: "SetCountdown";
  payload: SetCountdownPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetCountdownPayload".
 */
export interface SetCountdownPayload {
  seconds: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetOfferedTemplatesCommand".
 */
export interface SetOfferedTemplatesCommand {
  type: "command";
  id: string;
  name: "SetOfferedTemplates";
  payload: SetOfferedTemplatesPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetOfferedTemplatesPayload".
 */
export interface SetOfferedTemplatesPayload {
  templateIds: string[];
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetDefaultTemplateCommand".
 */
export interface SetDefaultTemplateCommand {
  type: "command";
  id: string;
  name: "SetDefaultTemplate";
  payload: SetDefaultTemplatePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetDefaultTemplatePayload".
 */
export interface SetDefaultTemplatePayload {
  templateId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetOfferedLooksCommand".
 */
export interface SetOfferedLooksCommand {
  type: "command";
  id: string;
  name: "SetOfferedLooks";
  payload: SetOfferedLooksPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetOfferedLooksPayload".
 */
export interface SetOfferedLooksPayload {
  lookIds: string[];
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetDefaultLookCommand".
 */
export interface SetDefaultLookCommand {
  type: "command";
  id: string;
  name: "SetDefaultLook";
  payload: SetDefaultLookPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetDefaultLookPayload".
 */
export interface SetDefaultLookPayload {
  lookId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetPhaseTimerCommand".
 */
export interface SetPhaseTimerCommand {
  type: "command";
  id: string;
  name: "SetPhaseTimer";
  payload: SetPhaseTimerPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetPhaseTimerPayload".
 */
export interface SetPhaseTimerPayload {
  phase: PhaseTimer;
  seconds: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetGuestGalleryCommand".
 */
export interface SetGuestGalleryCommand {
  type: "command";
  id: string;
  name: "SetGuestGallery";
  payload: SetGuestGalleryPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetGuestGalleryPayload".
 */
export interface SetGuestGalleryPayload {
  enabled: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetGuestSoundsCommand".
 */
export interface SetGuestSoundsCommand {
  type: "command";
  id: string;
  name: "SetGuestSounds";
  payload: SetGuestSoundsPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetGuestSoundsPayload".
 */
export interface SetGuestSoundsPayload {
  enabled: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetPinCommand".
 */
export interface SetPinCommand {
  type: "command";
  id: string;
  name: "SetPin";
  payload: SetPinPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetPinPayload".
 */
export interface SetPinPayload {
  pin: string;
  confirmation: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SaveEventCommand".
 */
export interface SaveEventCommand {
  type: "command";
  id: string;
  name: "SaveEvent";
  payload: SaveEventPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SaveEventPayload".
 */
export interface SaveEventPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SaveAndStartEventCommand".
 */
export interface SaveAndStartEventCommand {
  type: "command";
  id: string;
  name: "SaveAndStartEvent";
  payload: SaveAndStartEventPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SaveAndStartEventPayload".
 */
export interface SaveAndStartEventPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "LeaveEventSetupCommand".
 */
export interface LeaveEventSetupCommand {
  type: "command";
  id: string;
  name: "LeaveEventSetup";
  payload: LeaveEventSetupPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "LeaveEventSetupPayload".
 */
export interface LeaveEventSetupPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmLeaveCommand".
 */
export interface ConfirmLeaveCommand {
  type: "command";
  id: string;
  name: "ConfirmLeave";
  payload: ConfirmLeavePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmLeavePayload".
 */
export interface ConfirmLeavePayload {
  choice: LeaveChoice;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenGalleryCommand".
 */
export interface OpenGalleryCommand {
  type: "command";
  id: string;
  name: "OpenGallery";
  payload: OpenGalleryPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenGalleryPayload".
 */
export interface OpenGalleryPayload {
  eventId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "LoadMoreGuestCyclesCommand".
 */
export interface LoadMoreGuestCyclesCommand {
  type: "command";
  id: string;
  name: "LoadMoreGuestCycles";
  payload: LoadMoreGuestCyclesPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "LoadMoreGuestCyclesPayload".
 */
export interface LoadMoreGuestCyclesPayload {
  cursor: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenGuestCycleCommand".
 */
export interface OpenGuestCycleCommand {
  type: "command";
  id: string;
  name: "OpenGuestCycle";
  payload: OpenGuestCyclePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenGuestCyclePayload".
 */
export interface OpenGuestCyclePayload {
  guestCycleId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CloseGuestCycleCommand".
 */
export interface CloseGuestCycleCommand {
  type: "command";
  id: string;
  name: "CloseGuestCycle";
  payload: CloseGuestCyclePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CloseGuestCyclePayload".
 */
export interface CloseGuestCyclePayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenGuestCycleFolderCommand".
 */
export interface OpenGuestCycleFolderCommand {
  type: "command";
  id: string;
  name: "OpenGuestCycleFolder";
  payload: OpenGuestCycleFolderPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenGuestCycleFolderPayload".
 */
export interface OpenGuestCycleFolderPayload {
  guestCycleId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DeleteGuestCycleCommand".
 */
export interface DeleteGuestCycleCommand {
  type: "command";
  id: string;
  name: "DeleteGuestCycle";
  payload: DeleteGuestCyclePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DeleteGuestCyclePayload".
 */
export interface DeleteGuestCyclePayload {
  guestCycleId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmDeleteGuestCycleCommand".
 */
export interface ConfirmDeleteGuestCycleCommand {
  type: "command";
  id: string;
  name: "ConfirmDeleteGuestCycle";
  payload: ConfirmDeleteGuestCyclePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmDeleteGuestCyclePayload".
 */
export interface ConfirmDeleteGuestCyclePayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CancelDeleteGuestCycleCommand".
 */
export interface CancelDeleteGuestCycleCommand {
  type: "command";
  id: string;
  name: "CancelDeleteGuestCycle";
  payload: CancelDeleteGuestCyclePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CancelDeleteGuestCyclePayload".
 */
export interface CancelDeleteGuestCyclePayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CloseGalleryCommand".
 */
export interface CloseGalleryCommand {
  type: "command";
  id: string;
  name: "CloseGallery";
  payload: CloseGalleryPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CloseGalleryPayload".
 */
export interface CloseGalleryPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EndEventCommand".
 */
export interface EndEventCommand {
  type: "command";
  id: string;
  name: "EndEvent";
  payload: EndEventPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EndEventPayload".
 */
export interface EndEventPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmEndEventCommand".
 */
export interface ConfirmEndEventCommand {
  type: "command";
  id: string;
  name: "ConfirmEndEvent";
  payload: ConfirmEndEventPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmEndEventPayload".
 */
export interface ConfirmEndEventPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "KeepRunningCommand".
 */
export interface KeepRunningCommand {
  type: "command";
  id: string;
  name: "KeepRunning";
  payload: KeepRunningPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "KeepRunningPayload".
 */
export interface KeepRunningPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RetryAssistanceCommand".
 */
export interface RetryAssistanceCommand {
  type: "command";
  id: string;
  name: "RetryAssistance";
  payload: RetryAssistancePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RetryAssistancePayload".
 */
export interface RetryAssistancePayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmCloseAppCommand".
 */
export interface ConfirmCloseAppCommand {
  type: "command";
  id: string;
  name: "ConfirmCloseApp";
  payload: ConfirmCloseAppPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmCloseAppPayload".
 */
export interface ConfirmCloseAppPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CancelCloseAppCommand".
 */
export interface CancelCloseAppCommand {
  type: "command";
  id: string;
  name: "CancelCloseApp";
  payload: CancelCloseAppPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CancelCloseAppPayload".
 */
export interface CancelCloseAppPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RetryStorageCommand".
 */
export interface RetryStorageCommand {
  type: "command";
  id: string;
  name: "RetryStorage";
  payload: RetryStoragePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RetryStoragePayload".
 */
export interface RetryStoragePayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "BeginGuestCycleCommand".
 */
export interface BeginGuestCycleCommand {
  type: "command";
  id: string;
  name: "BeginGuestCycle";
  payload: BeginGuestCyclePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "BeginGuestCyclePayload".
 */
export interface BeginGuestCyclePayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SelectTemplateCommand".
 */
export interface SelectTemplateCommand {
  type: "command";
  id: string;
  name: "SelectTemplate";
  payload: SelectTemplatePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SelectTemplatePayload".
 */
export interface SelectTemplatePayload {
  templateId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GoToStepCommand".
 */
export interface GoToStepCommand {
  type: "command";
  id: string;
  name: "GoToStep";
  payload: GoToStepPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GoToStepPayload".
 */
export interface GoToStepPayload {
  step: GuestStep;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ShowCameraTabCommand".
 */
export interface ShowCameraTabCommand {
  type: "command";
  id: string;
  name: "ShowCameraTab";
  payload: ShowCameraTabPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ShowCameraTabPayload".
 */
export interface ShowCameraTabPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ShowArrangeTabCommand".
 */
export interface ShowArrangeTabCommand {
  type: "command";
  id: string;
  name: "ShowArrangeTab";
  payload: ShowArrangeTabPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ShowArrangeTabPayload".
 */
export interface ShowArrangeTabPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TakePhotographsCommand".
 */
export interface TakePhotographsCommand {
  type: "command";
  id: string;
  name: "TakePhotographs";
  payload: TakePhotographsPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TakePhotographsPayload".
 */
export interface TakePhotographsPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SelectPositionCommand".
 */
export interface SelectPositionCommand {
  type: "command";
  id: string;
  name: "SelectPosition";
  payload: SelectPositionPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SelectPositionPayload".
 */
export interface SelectPositionPayload {
  index: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "BeginRetakeCommand".
 */
export interface BeginRetakeCommand {
  type: "command";
  id: string;
  name: "BeginRetake";
  payload: BeginRetakePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "BeginRetakePayload".
 */
export interface BeginRetakePayload {
  index: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "AcceptRetakeCommand".
 */
export interface AcceptRetakeCommand {
  type: "command";
  id: string;
  name: "AcceptRetake";
  payload: AcceptRetakePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "AcceptRetakePayload".
 */
export interface AcceptRetakePayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TryAgainCommand".
 */
export interface TryAgainCommand {
  type: "command";
  id: string;
  name: "TryAgain";
  payload: TryAgainPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TryAgainPayload".
 */
export interface TryAgainPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "KeepOriginalCommand".
 */
export interface KeepOriginalCommand {
  type: "command";
  id: string;
  name: "KeepOriginal";
  payload: KeepOriginalPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "KeepOriginalPayload".
 */
export interface KeepOriginalPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "BeginMoveCommand".
 */
export interface BeginMoveCommand {
  type: "command";
  id: string;
  name: "BeginMove";
  payload: BeginMovePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "BeginMovePayload".
 */
export interface BeginMovePayload {
  index: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "MoveToCommand".
 */
export interface MoveToCommand {
  type: "command";
  id: string;
  name: "MoveTo";
  payload: MoveToPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "MoveToPayload".
 */
export interface MoveToPayload {
  index: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CancelMoveCommand".
 */
export interface CancelMoveCommand {
  type: "command";
  id: string;
  name: "CancelMove";
  payload: CancelMovePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CancelMovePayload".
 */
export interface CancelMovePayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DragPlaceCommand".
 */
export interface DragPlaceCommand {
  type: "command";
  id: string;
  name: "DragPlace";
  payload: DragPlacePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DragPlacePayload".
 */
export interface DragPlacePayload {
  sourceIndex: number;
  captureId: string;
  revision: number;
  targetIndex: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SelectLookCommand".
 */
export interface SelectLookCommand {
  type: "command";
  id: string;
  name: "SelectLook";
  payload: SelectLookPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SelectLookPayload".
 */
export interface SelectLookPayload {
  lookId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "FinishStripCommand".
 */
export interface FinishStripCommand {
  type: "command";
  id: string;
  name: "FinishStrip";
  payload: FinishStripPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "FinishStripPayload".
 */
export interface FinishStripPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RetryCompositionCommand".
 */
export interface RetryCompositionCommand {
  type: "command";
  id: string;
  name: "RetryComposition";
  payload: RetryCompositionPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RetryCompositionPayload".
 */
export interface RetryCompositionPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "BeginExitHoldCommand".
 */
export interface BeginExitHoldCommand {
  type: "command";
  id: string;
  name: "BeginExitHold";
  payload: BeginExitHoldPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "BeginExitHoldPayload".
 */
export interface BeginExitHoldPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CompleteExitHoldCommand".
 */
export interface CompleteExitHoldCommand {
  type: "command";
  id: string;
  name: "CompleteExitHold";
  payload: CompleteExitHoldPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CompleteExitHoldPayload".
 */
export interface CompleteExitHoldPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SubmitPinCommand".
 */
export interface SubmitPinCommand {
  type: "command";
  id: string;
  name: "SubmitPin";
  payload: SubmitPinPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SubmitPinPayload".
 */
export interface SubmitPinPayload {
  digits: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CancelExitCommand".
 */
export interface CancelExitCommand {
  type: "command";
  id: string;
  name: "CancelExit";
  payload: CancelExitPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CancelExitPayload".
 */
export interface CancelExitPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenGuestGalleryCommand".
 */
export interface OpenGuestGalleryCommand {
  type: "command";
  id: string;
  name: "OpenGuestGallery";
  payload: OpenGuestGalleryPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenGuestGalleryPayload".
 */
export interface OpenGuestGalleryPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "LoadMoreGuestStripsCommand".
 */
export interface LoadMoreGuestStripsCommand {
  type: "command";
  id: string;
  name: "LoadMoreGuestStrips";
  payload: LoadMoreGuestStripsPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "LoadMoreGuestStripsPayload".
 */
export interface LoadMoreGuestStripsPayload {
  cursor: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenGuestStripCommand".
 */
export interface OpenGuestStripCommand {
  type: "command";
  id: string;
  name: "OpenGuestStrip";
  payload: OpenGuestStripPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenGuestStripPayload".
 */
export interface OpenGuestStripPayload {
  guestCycleId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CloseGuestStripCommand".
 */
export interface CloseGuestStripCommand {
  type: "command";
  id: string;
  name: "CloseGuestStrip";
  payload: CloseGuestStripPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CloseGuestStripPayload".
 */
export interface CloseGuestStripPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "BackToStartCommand".
 */
export interface BackToStartCommand {
  type: "command";
  id: string;
  name: "BackToStart";
  payload: BackToStartPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "BackToStartPayload".
 */
export interface BackToStartPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TestMessage".
 */
export interface TestMessage {
  type: "test";
  id: string;
  name: string;
  args?: unknown;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "FrameAckMessage".
 */
export interface FrameAckMessage {
  type: "frameAck";
  seq: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OperatorWelcomeMessage".
 */
export interface OperatorWelcomeMessage {
  type: "welcome";
  role: "operator";
  protocolVersion: number;
  revision: number;
  presentation: OperatorPresentation;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OperatorPresentation".
 */
export interface OperatorPresentation {
  surface: OperatorSurface;
  eventList: EventListView | null;
  eventSetup: EventSetupView | null;
  runningEvent: RunningEventPanel | null;
  overlays: OperatorOverlays;
  fatal: FatalView | null;
  storageError: StorageErrorView | null;
  cameraDiagnostics: CameraDiagnosticsView | null;
  eventGallery: EventGalleryView | null;
  guestCycleDetail: GuestCycleDetailView | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventListView".
 */
export interface EventListView {
  events: EventListRow[];
  newEventAvailable: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventListRow".
 */
export interface EventListRow {
  id: string;
  name: string;
  date: string;
  venue: string | null;
  completedGuestCycles: number;
  lastUsedAt: string | null;
  running: boolean;
  actions: EventRowActions;
  startDisabledReason: StartDisabledReason | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventRowActions".
 */
export interface EventRowActions {
  canStart: boolean;
  canEdit: boolean;
  canDelete: boolean;
  canOpenFolder: boolean;
  canOpenGallery: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventSetupView".
 */
export interface EventSetupView {
  eventId: string | null;
  isNew: boolean;
  dirty: boolean;
  fields: EventSetupFieldValues;
  fieldErrors: EventSetupFieldError[];
  showErrors: boolean;
  camera: EventSetupCameraView;
  guestDisplay: EventSetupGuestDisplayView;
  mirror: boolean;
  countdownSeconds: number;
  printer: PrinterChoice;
  templates: EventSetupTemplateOption[];
  looks: EventSetupLookOption[];
  phaseTimers: PhaseTimerOption[];
  guestGalleryEnabled: boolean;
  guestSoundsEnabled: boolean;
  pin: EventSetupPinView;
  saveAndStartAvailable: boolean;
  saveAndStartDisabledReason: StartDisabledReason | null;
  startFailure: EventSetupStartFailure | null;
  saving: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventSetupFieldValues".
 */
export interface EventSetupFieldValues {
  name: string;
  date: string;
  venue: string;
  customer: string;
  notes: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventSetupFieldError".
 */
export interface EventSetupFieldError {
  field: string;
  code: EventSetupErrorCode;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventSetupCameraView".
 */
export interface EventSetupCameraView {
  cameras: CameraOption[];
  selectedDeviceId: string | null;
  selectedName: string | null;
  state: EventSetupCameraState;
  reason: CameraFailureKind | null;
  previewActive: boolean;
  mirrored: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CameraOption".
 */
export interface CameraOption {
  deviceId: string;
  name: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventSetupGuestDisplayView".
 */
export interface EventSetupGuestDisplayView {
  displays: GuestDisplayOption[];
  selectedId: string | null;
  selectedName: string | null;
  state: GuestDisplayState;
  coversOperatorScreen: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestDisplayOption".
 */
export interface GuestDisplayOption {
  id: string;
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
  number: number;
  isOperatorScreen: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventSetupTemplateOption".
 */
export interface EventSetupTemplateOption {
  id: string;
  name: string;
  description: string;
  width: number;
  height: number;
  landscape: boolean;
  offered: boolean;
  isDefault: boolean;
  blankPreviewKey: string | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventSetupLookOption".
 */
export interface EventSetupLookOption {
  id: string;
  name: string;
  description: string;
  offered: boolean;
  isDefault: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PhaseTimerOption".
 */
export interface PhaseTimerOption {
  phase: PhaseTimer;
  seconds: number;
  defaultSeconds: number;
  min: number;
  max: number;
  canBeOff: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventSetupPinView".
 */
export interface EventSetupPinView {
  isSet: boolean;
  changing: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventSetupStartFailure".
 */
export interface EventSetupStartFailure {
  code: StartFailureCode;
  cameraReason: CameraFailureKind | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RunningEventPanel".
 */
export interface RunningEventPanel {
  eventId: string;
  name: string;
  guestDisplay: RunningEventDisplay;
  camera: RunningEventCamera;
  currentStep: RunningEventStep;
  completedGuestCycles: number;
  startedAt: string;
  assistance: RunningEventAssistance | null;
  singleScreen: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RunningEventDisplay".
 */
export interface RunningEventDisplay {
  name: string;
  state: RunningDisplayState;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RunningEventCamera".
 */
export interface RunningEventCamera {
  state: CameraState;
  reason: CameraFailureKind | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RunningEventAssistance".
 */
export interface RunningEventAssistance {
  reason: AssistanceReason;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OperatorOverlays".
 */
export interface OperatorOverlays {
  confirmation: ConfirmationOverlay | null;
  identifyDisplays: IdentifyDisplaysOverlay | null;
  notice: OperatorNoticeOverlay | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmationOverlay".
 */
export interface ConfirmationOverlay {
  kind: ConfirmationKind;
  eventName: string | null;
  counts: ConfirmationCounts | null;
  guestCycle: DeleteGuestCycleConfirmation | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmationCounts".
 */
export interface ConfirmationCounts {
  guestCycles: number;
  photoStrips: number;
  captures: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DeleteGuestCycleConfirmation".
 */
export interface DeleteGuestCycleConfirmation {
  guestCycleId: string;
  startedAt: string;
  fileCount: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "IdentifyDisplaysOverlay".
 */
export interface IdentifyDisplaysOverlay {
  displays: IdentifyDisplayEntry[];
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "IdentifyDisplayEntry".
 */
export interface IdentifyDisplayEntry {
  id: string;
  number: number;
  name: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OperatorNoticeOverlay".
 */
export interface OperatorNoticeOverlay {
  code: OperatorNoticeCode;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "FatalView".
 */
export interface FatalView {
  reason: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StorageErrorView".
 */
export interface StorageErrorView {
  kind: StorageErrorReason;
  dataPath: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CameraDiagnosticsView".
 */
export interface CameraDiagnosticsView {
  cameras: CameraOption[];
  selectedDeviceId: string | null;
  state: CameraState;
  reason: CameraFailureKind | null;
  format: CameraFormatView | null;
  mirrored: boolean;
  lastCapture: CameraCaptureView | null;
  lastComposition: CompositionView | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CameraFormatView".
 */
export interface CameraFormatView {
  width: number;
  height: number;
  fps: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CameraCaptureView".
 */
export interface CameraCaptureView {
  fileName: string;
  width: number;
  height: number;
  byteLength: number;
  mirrored: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CompositionView".
 */
export interface CompositionView {
  previewKey: string;
  templateId: string;
  lookId: string;
  width: number;
  height: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventGalleryView".
 */
export interface EventGalleryView {
  eventId: string;
  eventName: string;
  running: boolean;
  items: GalleryItemView[];
  nextCursor: string | null;
  loadingMore: boolean;
  loadFailed: boolean;
  empty: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GalleryItemView".
 */
export interface GalleryItemView {
  guestCycleId: string;
  startedAt: string;
  outcome: GalleryOutcome;
  templateName: string | null;
  lookName: string | null;
  strip: GalleryStripThumbnailView | null;
  captures: GalleryCaptureThumbnailView[];
  unavailable: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GalleryStripThumbnailView".
 */
export interface GalleryStripThumbnailView {
  thumbnailUrl: string;
  width: number;
  height: number;
  landscape: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GalleryCaptureThumbnailView".
 */
export interface GalleryCaptureThumbnailView {
  thumbnailUrl: string;
  width: number;
  height: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestCycleDetailView".
 */
export interface GuestCycleDetailView {
  eventId: string;
  eventName: string;
  guestCycleId: string;
  startedAt: string;
  outcome: GalleryOutcome;
  templateName: string | null;
  lookName: string | null;
  strip: GalleryStripImageView | null;
  captures: GalleryCaptureImageView[];
  unavailable: boolean;
  canOpenFolder: boolean;
  canDelete: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GalleryStripImageView".
 */
export interface GalleryStripImageView {
  imageUrl: string;
  width: number;
  height: number;
  landscape: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GalleryCaptureImageView".
 */
export interface GalleryCaptureImageView {
  imageUrl: string;
  width: number;
  height: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestWelcomeMessage".
 */
export interface GuestWelcomeMessage {
  type: "welcome";
  role: "guest";
  protocolVersion: number;
  revision: number;
  presentation: GuestPresentation;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestPresentation".
 */
export interface GuestPresentation {
  surface: GuestSurface;
  start: StartView | null;
  startUnavailable: StartUnavailableView | null;
  overlays: GuestOverlays;
  fatal: FatalView | null;
  guestCycle: GuestCycleView | null;
  template: TemplateView | null;
  photographs: PhotographsView | null;
  filters: FiltersView | null;
  keepsake: KeepsakeView | null;
  operatorAssistance: OperatorAssistanceView | null;
  guestGallery: GuestGalleryView | null;
  guestStripDetail: GuestStripDetailView | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StartView".
 */
export interface StartView {
  stepInsideAvailable: boolean;
  guestGalleryLinkVisible: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StartUnavailableView".
 */
export interface StartUnavailableView {
  reason: StartUnavailableReason;
  guestGalleryLinkVisible: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestOverlays".
 */
export interface GuestOverlays {
  exitHold: ExitHoldOverlay | null;
  pinDialog: PinDialogOverlay | null;
  phaseTimer: PhaseTimerOverlay | null;
  notice: NoticeOverlay | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ExitHoldOverlay".
 */
export interface ExitHoldOverlay {
  active: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PinDialogOverlay".
 */
export interface PinDialogOverlay {
  error: PinDialogError | null;
  lockedRemainingSeconds: number | null;
  attemptsRemaining: number | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PhaseTimerOverlay".
 */
export interface PhaseTimerOverlay {
  phase: PhaseTimer;
  remainingSeconds: number;
  paused: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "NoticeOverlay".
 */
export interface NoticeOverlay {
  message: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestCycleView".
 */
export interface GuestCycleView {
  id: string;
  step: GuestCycleStepName;
  steps: GuestCycleStepEntry[];
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestCycleStepEntry".
 */
export interface GuestCycleStepEntry {
  step: GuestCycleStepName;
  state: GuestStepState;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TemplateView".
 */
export interface TemplateView {
  templates: TemplateOptionView[];
  selectedId: string;
  canContinue: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TemplateOptionView".
 */
export interface TemplateOptionView {
  id: string;
  name: string;
  description: string;
  width: number;
  height: number;
  landscape: boolean;
  windows: TemplateWindowView[];
  blankPreviewKey: string | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TemplateWindowView".
 */
export interface TemplateWindowView {
  x: number;
  y: number;
  width: number;
  height: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PhotographsView".
 */
export interface PhotographsView {
  tab: PhotographsTabName;
  template: PhotographsTemplateView;
  windows: PhotographsWindowView[];
  sequence: CaptureSequenceView;
  mirrored: boolean;
  cropAspect: number;
  selection: PhotographSelectionView;
  retakeCandidate: RetakeCandidateView | null;
  revision: number;
  canTakePhotographs: boolean;
  canReview: boolean;
  canContinue: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PhotographsTemplateView".
 */
export interface PhotographsTemplateView {
  id: string;
  width: number;
  height: number;
  landscape: boolean;
  windows: TemplateWindowView[];
  blankPreviewKey: string | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PhotographsWindowView".
 */
export interface PhotographsWindowView {
  index: number;
  captureId: string | null;
  imageUrl: string | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CaptureSequenceView".
 */
export interface CaptureSequenceView {
  running: boolean;
  targetWindow: number | null;
  countdownRemaining: number | null;
  flashSequence: number;
  capturedInSequence: number;
  totalInSequence: number;
  pausing: boolean;
  soundsOn: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PhotographSelectionView".
 */
export interface PhotographSelectionView {
  index: number | null;
  mode: PhotographSelectionMode;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RetakeCandidateView".
 */
export interface RetakeCandidateView {
  index: number;
  imageUrl: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "FiltersView".
 */
export interface FiltersView {
  looks: LookOptionView[];
  selectedLookId: string;
  width: number;
  height: number;
  landscape: boolean;
  stripPreviewKey: string | null;
  composing: boolean;
  compositionFailed: boolean;
  canFinish: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "LookOptionView".
 */
export interface LookOptionView {
  id: string;
  name: string;
  description: string;
  thumbnailKey: string | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "KeepsakeView".
 */
export interface KeepsakeView {
  stripUrl: string;
  width: number;
  height: number;
  landscape: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OperatorAssistanceView".
 */
export interface OperatorAssistanceView {
  reason: AssistanceReason;
  retrying: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestGalleryView".
 */
export interface GuestGalleryView {
  strips: GuestStripView[];
  nextCursor: string | null;
  loadingMore: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestStripView".
 */
export interface GuestStripView {
  guestCycleId: string;
  thumbnailUrl: string;
  width: number;
  height: number;
  landscape: boolean;
  alt: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestStripDetailView".
 */
export interface GuestStripDetailView {
  strip: GuestStripImageView;
  captures: GuestCaptureImageView[];
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestStripImageView".
 */
export interface GuestStripImageView {
  imageUrl: string;
  width: number;
  height: number;
  landscape: boolean;
  alt: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestCaptureImageView".
 */
export interface GuestCaptureImageView {
  imageUrl: string;
  width: number;
  height: number;
  alt: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OperatorPresentationMessage".
 */
export interface OperatorPresentationMessage {
  type: "presentation";
  role: "operator";
  revision: number;
  presentation: OperatorPresentation;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestPresentationMessage".
 */
export interface GuestPresentationMessage {
  type: "presentation";
  role: "guest";
  revision: number;
  presentation: GuestPresentation;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CommandResultMessage".
 */
export interface CommandResultMessage {
  type: "commandResult";
  id: string;
  status: CommandStatus;
  reason: RejectReason | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ProtocolMismatchMessage".
 */
export interface ProtocolMismatchMessage {
  type: "protocolMismatch";
  brainVersion: number;
  uiVersion: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ErrorMessage".
 */
export interface ErrorMessage {
  type: "error";
  id: string | null;
  reason: EnvelopeError;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TestResultMessage".
 */
export interface TestResultMessage {
  type: "testResult";
  id: string;
  ok: boolean;
  result: unknown;
  error: TestHookError | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CommandPayloadMap".
 */
export interface CommandPayloadMap {
  OpenNewEvent: OpenNewEventPayload;
  EditEvent: EditEventPayload;
  StartEvent: StartEventPayload;
  DeleteEvent: DeleteEventPayload;
  ConfirmDeleteEvent: ConfirmDeleteEventPayload;
  CancelDeleteEvent: CancelDeleteEventPayload;
  OpenEventFolder: OpenEventFolderPayload;
  DismissNotice: DismissNoticePayload;
  ChangeEventField: ChangeEventFieldPayload;
  SelectCamera: SelectCameraPayload;
  SelectGuestDisplay: SelectGuestDisplayPayload;
  IdentifyDisplays: IdentifyDisplaysPayload;
  SetMirror: SetMirrorPayload;
  SetCountdown: SetCountdownPayload;
  SetOfferedTemplates: SetOfferedTemplatesPayload;
  SetDefaultTemplate: SetDefaultTemplatePayload;
  SetOfferedLooks: SetOfferedLooksPayload;
  SetDefaultLook: SetDefaultLookPayload;
  SetPhaseTimer: SetPhaseTimerPayload;
  SetGuestGallery: SetGuestGalleryPayload;
  SetGuestSounds: SetGuestSoundsPayload;
  SetPin: SetPinPayload;
  SaveEvent: SaveEventPayload;
  SaveAndStartEvent: SaveAndStartEventPayload;
  LeaveEventSetup: LeaveEventSetupPayload;
  ConfirmLeave: ConfirmLeavePayload;
  OpenGallery: OpenGalleryPayload;
  LoadMoreGuestCycles: LoadMoreGuestCyclesPayload;
  OpenGuestCycle: OpenGuestCyclePayload;
  CloseGuestCycle: CloseGuestCyclePayload;
  OpenGuestCycleFolder: OpenGuestCycleFolderPayload;
  DeleteGuestCycle: DeleteGuestCyclePayload;
  ConfirmDeleteGuestCycle: ConfirmDeleteGuestCyclePayload;
  CancelDeleteGuestCycle: CancelDeleteGuestCyclePayload;
  CloseGallery: CloseGalleryPayload;
  EndEvent: EndEventPayload;
  ConfirmEndEvent: ConfirmEndEventPayload;
  KeepRunning: KeepRunningPayload;
  RetryAssistance: RetryAssistancePayload;
  ConfirmCloseApp: ConfirmCloseAppPayload;
  CancelCloseApp: CancelCloseAppPayload;
  RetryStorage: RetryStoragePayload;
  BeginGuestCycle: BeginGuestCyclePayload;
  SelectTemplate: SelectTemplatePayload;
  GoToStep: GoToStepPayload;
  ShowCameraTab: ShowCameraTabPayload;
  ShowArrangeTab: ShowArrangeTabPayload;
  TakePhotographs: TakePhotographsPayload;
  SelectPosition: SelectPositionPayload;
  BeginRetake: BeginRetakePayload;
  AcceptRetake: AcceptRetakePayload;
  TryAgain: TryAgainPayload;
  KeepOriginal: KeepOriginalPayload;
  BeginMove: BeginMovePayload;
  MoveTo: MoveToPayload;
  CancelMove: CancelMovePayload;
  DragPlace: DragPlacePayload;
  SelectLook: SelectLookPayload;
  FinishStrip: FinishStripPayload;
  RetryComposition: RetryCompositionPayload;
  BeginExitHold: BeginExitHoldPayload;
  CompleteExitHold: CompleteExitHoldPayload;
  SubmitPin: SubmitPinPayload;
  CancelExit: CancelExitPayload;
  OpenGuestGallery: OpenGuestGalleryPayload;
  LoadMoreGuestStrips: LoadMoreGuestStripsPayload;
  OpenGuestStrip: OpenGuestStripPayload;
  CloseGuestStrip: CloseGuestStripPayload;
  BackToStart: BackToStartPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TestHookMap".
 */
export interface TestHookMap {
  "test.getDiagnostics": {
    args: null;
    result: DiagnosticsResult;
  };
  "test.useVirtualDisplays": {
    args: UseVirtualDisplaysArgs;
    result: UseVirtualDisplaysResult;
  };
  "test.disconnectDisplay": {
    args: DisconnectDisplayArgs;
    result: EmptyHookResult;
  };
  "test.connectDisplay": {
    args: ConnectDisplayArgs;
    result: EmptyHookResult;
  };
  "test.openGuestScreen": {
    args: OpenGuestScreenArgs;
    result: EmptyHookResult;
  };
  "test.closeGuestScreen": {
    args: null;
    result: EmptyHookResult;
  };
  "test.setWindowBounds": {
    args: SetWindowBoundsArgs;
    result: TestWindow;
  };
  "test.crashUi": {
    args: CrashUiArgs;
    result: EmptyHookResult;
  };
  "test.listCameras": {
    args: null;
    result: ListCamerasResult;
  };
  "test.exit": {
    args: null;
    result: EmptyHookResult;
  };
  "test.injectStorageFault": {
    args: InjectStorageFaultArgs;
    result: EmptyHookResult;
  };
  "test.clearStorageFault": {
    args: null;
    result: EmptyHookResult;
  };
  "test.openCameraDiagnostics": {
    args: null;
    result: EmptyHookResult;
  };
  "test.selectDiagnosticsCamera": {
    args: SelectDiagnosticsCameraArgs;
    result: EmptyHookResult;
  };
  "test.setDiagnosticsMirror": {
    args: SetDiagnosticsMirrorArgs;
    result: EmptyHookResult;
  };
  "test.captureDiagnostics": {
    args: null;
    result: CameraCaptureView;
  };
  "test.injectCameraFault": {
    args: InjectCameraFaultArgs;
    result: EmptyHookResult;
  };
  "test.clearCameraFault": {
    args: null;
    result: EmptyHookResult;
  };
  "test.composeDiagnostics": {
    args: ComposeDiagnosticsArgs;
    result: ComposePreviewResult;
  };
  "test.composeBlankPreview": {
    args: ComposeBlankPreviewArgs;
    result: ComposePreviewResult;
  };
  "test.setClockScale": {
    args: SetClockScaleArgs;
    result: EmptyHookResult;
  };
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DiagnosticsResult".
 */
export interface DiagnosticsResult {
  protocolVersion: number;
  connectedRoles: Role[];
  presentations: PresentationDiagnostics[];
  lastCommandResults: CommandResultDiagnostics[];
  windows: TestWindow[];
  displays: DisplayDiagnostics;
  guestScreenOpen: boolean;
  storage: StorageDiagnostics;
  camera: CameraDiagnostics;
  openFolderRequests: OpenFolderRequest[];
  identifyOverlays: IdentifyOverlayDiagnostics[];
  wakeLock: WakeLockDiagnostics;
  clockScale: number;
  guestCycle: GuestCycleDiagnostics | null;
  gallery: GalleryDiagnostics | null;
  instance: InstanceDiagnostics;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PresentationDiagnostics".
 */
export interface PresentationDiagnostics {
  role: Role;
  revision: number;
  surface: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CommandResultDiagnostics".
 */
export interface CommandResultDiagnostics {
  id: string;
  name: string;
  role: Role;
  status: CommandStatus;
  reason: RejectReason | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TestWindow".
 */
export interface TestWindow {
  role: Role;
  bounds: PixelRect;
  displayId: string | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PixelRect".
 */
export interface PixelRect {
  x: number;
  y: number;
  width: number;
  height: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DisplayDiagnostics".
 */
export interface DisplayDiagnostics {
  virtual: boolean;
  displays: TestDisplay[];
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TestDisplay".
 */
export interface TestDisplay {
  id: string;
  name: string;
  bounds: PixelRect;
  workArea: PixelRect;
  scale: number;
  isPrimary: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StorageDiagnostics".
 */
export interface StorageDiagnostics {
  state: StorageDiagnosticsState;
  kind: StorageErrorReason | null;
  schemaVersion: number | null;
  reconcile: ReconcileDiagnostics | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ReconcileDiagnostics".
 */
export interface ReconcileDiagnostics {
  stagingFilesDeleted: number;
  orphanFilesDeleted: number;
  artifactProblems: number;
  cyclesMarkedUnrecoverable: number;
  cyclesClosedAsAppClosed: number;
  guestCyclesDeleted: number;
  eventsDeleted: number;
  discardedFilesDeleted: number;
  orphanThumbnailsDeleted: number;
  problems: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CameraDiagnostics".
 */
export interface CameraDiagnostics {
  state: CameraState;
  deviceId: string | null;
  format: CameraFormatView | null;
  frameAgeMs: number | null;
  deliveredFps: number | null;
  droppedFrames: number | null;
  previewTarget: PreviewTarget;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenFolderRequest".
 */
export interface OpenFolderRequest {
  path: string;
  at: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "IdentifyOverlayDiagnostics".
 */
export interface IdentifyOverlayDiagnostics {
  displayId: string;
  number: number;
  visible: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "WakeLockDiagnostics".
 */
export interface WakeLockDiagnostics {
  held: boolean;
  reason: string | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestCycleDiagnostics".
 */
export interface GuestCycleDiagnostics {
  id: string;
  step: GuestCycleStepName;
  tab: PhotographsTabName | null;
  windowsFilled: number;
  windowCount: number;
  sequenceRunning: boolean;
  countdownRemaining: number | null;
  timer: GuestCycleTimerDiagnostics;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestCycleTimerDiagnostics".
 */
export interface GuestCycleTimerDiagnostics {
  phase: PhaseTimer | null;
  remainingMs: number | null;
  paused: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GalleryDiagnostics".
 */
export interface GalleryDiagnostics {
  operator: OperatorGalleryDiagnostics | null;
  guest: GuestGalleryDiagnostics | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OperatorGalleryDiagnostics".
 */
export interface OperatorGalleryDiagnostics {
  eventId: string;
  itemCount: number;
  nextCursor: string | null;
  detailId: string | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestGalleryDiagnostics".
 */
export interface GuestGalleryDiagnostics {
  stripCount: number;
  nextCursor: string | null;
  detailId: string | null;
  timer: GuestCycleTimerDiagnostics;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "InstanceDiagnostics".
 */
export interface InstanceDiagnostics {
  mutexName: string | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "UseVirtualDisplaysArgs".
 */
export interface UseVirtualDisplaysArgs {
  displays: VirtualDisplaySpec[];
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "VirtualDisplaySpec".
 */
export interface VirtualDisplaySpec {
  id: string;
  name: string;
  bounds: PixelRect;
  isPrimary: boolean;
  scale?: number | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "UseVirtualDisplaysResult".
 */
export interface UseVirtualDisplaysResult {
  displays: TestDisplay[];
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "DisconnectDisplayArgs".
 */
export interface DisconnectDisplayArgs {
  displayId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EmptyHookResult".
 */
export interface EmptyHookResult {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConnectDisplayArgs".
 */
export interface ConnectDisplayArgs {
  displayId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OpenGuestScreenArgs".
 */
export interface OpenGuestScreenArgs {
  displayId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetWindowBoundsArgs".
 */
export interface SetWindowBoundsArgs {
  role: Role;
  width: number;
  height: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "CrashUiArgs".
 */
export interface CrashUiArgs {
  role: Role;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ListCamerasResult".
 */
export interface ListCamerasResult {
  cameras: TestCamera[];
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "TestCamera".
 */
export interface TestCamera {
  id: string;
  name: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "InjectStorageFaultArgs".
 */
export interface InjectStorageFaultArgs {
  kind: StorageFaultKind;
  scope: StorageFaultScope;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SelectDiagnosticsCameraArgs".
 */
export interface SelectDiagnosticsCameraArgs {
  deviceId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetDiagnosticsMirrorArgs".
 */
export interface SetDiagnosticsMirrorArgs {
  mirrored: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "InjectCameraFaultArgs".
 */
export interface InjectCameraFaultArgs {
  kind: CameraFaultKind;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ComposeDiagnosticsArgs".
 */
export interface ComposeDiagnosticsArgs {
  templateId: string;
  lookId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ComposePreviewResult".
 */
export interface ComposePreviewResult {
  previewKey: string;
  width: number;
  height: number;
  byteLength: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ComposeBlankPreviewArgs".
 */
export interface ComposeBlankPreviewArgs {
  templateId: string;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "SetClockScaleArgs".
 */
export interface SetClockScaleArgs {
  factor: number;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PreviewFrameMetadata".
 */
export interface PreviewFrameMetadata {
  type: "previewFrame";
  seq: number;
  width: number;
  height: number;
  format: PreviewPixelFormat;
  timestampMs: number;
}

/** The protocol version this UI build speaks; it sends it in `hello`. */
export const PROTOCOL_VERSION = 12;
