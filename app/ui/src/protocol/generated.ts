/*
 * generated — do not edit.
 * Generated from app/src/FotoHavn.Protocol/schema/protocol.schema.json by json-schema-to-typescript.
 * Change the C# records in FotoHavn.Protocol, then run app/scripts/generate-protocol.ps1.
 */

/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ClientMessage".
 */
export type ClientMessage = HelloMessage | Command | TestMessage;
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
  | RetryStorageCommand
  | BeginGuestCycleCommand
  | SelectTemplateCommand
  | GoToStepCommand
  | ShowCameraTabCommand
  | ShowArrangeTabCommand
  | TakePhotographsCommand
  | StopCaptureCommand
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
  | MoreTimeCommand
  | StartOverCommand
  | ConfirmStartOverCommand
  | KeepGoingCommand
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
  "eventList" | "eventSetup" | "eventGallery" | "guestCycleDetail" | "guestScreenActive" | "fatal" | "storageError";
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
  | "payloadTooLarge";
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
  "hooksUnavailable" | "unknownHook" | "invalidArgs" | "hookFailed" | "notSupported" | "unknownDisplay";
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
 * via the `definition` "StorageFaultKind".
 */
export type StorageFaultKind = "diskFull" | "accessDenied" | "locked";
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StorageFaultScope".
 */
export type StorageFaultScope = "nextWrite" | "allWrites";

/**
 * The FOTOHAVN UI and brain protocol. Generated from FotoHavn.Protocol by scripts/generate-protocol.ps1; do not edit.
 */
export interface FotoHavnProtocol {
  protocolVersion: 2;
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
 * via the `definition` "StopCaptureCommand".
 */
export interface StopCaptureCommand {
  type: "command";
  id: string;
  name: "StopCapture";
  payload: StopCapturePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StopCapturePayload".
 */
export interface StopCapturePayload {}
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
 * via the `definition` "MoreTimeCommand".
 */
export interface MoreTimeCommand {
  type: "command";
  id: string;
  name: "MoreTime";
  payload: MoreTimePayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "MoreTimePayload".
 */
export interface MoreTimePayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StartOverCommand".
 */
export interface StartOverCommand {
  type: "command";
  id: string;
  name: "StartOver";
  payload: StartOverPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StartOverPayload".
 */
export interface StartOverPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmStartOverCommand".
 */
export interface ConfirmStartOverCommand {
  type: "command";
  id: string;
  name: "ConfirmStartOver";
  payload: ConfirmStartOverPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmStartOverPayload".
 */
export interface ConfirmStartOverPayload {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "KeepGoingCommand".
 */
export interface KeepGoingCommand {
  type: "command";
  id: string;
  name: "KeepGoing";
  payload: KeepGoingPayload;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "KeepGoingPayload".
 */
export interface KeepGoingPayload {}
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
  runningEvent: RunningEventPanel | null;
  overlays: OperatorOverlays;
  fatal: FatalView | null;
  storageError: StorageErrorView | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "EventListView".
 */
export interface EventListView {
  events: EventListRow[];
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
  lastUsed: string | null;
  running: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "RunningEventPanel".
 */
export interface RunningEventPanel {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "OperatorOverlays".
 */
export interface OperatorOverlays {
  confirmation: ConfirmationOverlay | null;
  identifyDisplays: IdentifyDisplaysOverlay | null;
  notice: NoticeOverlay | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ConfirmationOverlay".
 */
export interface ConfirmationOverlay {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "IdentifyDisplaysOverlay".
 */
export interface IdentifyDisplaysOverlay {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "NoticeOverlay".
 */
export interface NoticeOverlay {
  message: string;
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
  overlays: GuestOverlays;
  fatal: FatalView | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StartView".
 */
export interface StartView {
  guestGalleryAvailable: boolean;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "GuestOverlays".
 */
export interface GuestOverlays {
  exitHold: ExitHoldOverlay | null;
  pinDialog: PinDialogOverlay | null;
  phaseTimerWarning: PhaseTimerWarningOverlay | null;
  startOverConfirmation: StartOverConfirmationOverlay | null;
  notice: NoticeOverlay | null;
}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "ExitHoldOverlay".
 */
export interface ExitHoldOverlay {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PinDialogOverlay".
 */
export interface PinDialogOverlay {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "PhaseTimerWarningOverlay".
 */
export interface PhaseTimerWarningOverlay {}
/**
 * This interface was referenced by `FotoHavnProtocol`'s JSON-Schema
 * via the `definition` "StartOverConfirmationOverlay".
 */
export interface StartOverConfirmationOverlay {}
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
  RetryStorage: RetryStoragePayload;
  BeginGuestCycle: BeginGuestCyclePayload;
  SelectTemplate: SelectTemplatePayload;
  GoToStep: GoToStepPayload;
  ShowCameraTab: ShowCameraTabPayload;
  ShowArrangeTab: ShowArrangeTabPayload;
  TakePhotographs: TakePhotographsPayload;
  StopCapture: StopCapturePayload;
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
  MoreTime: MoreTimePayload;
  StartOver: StartOverPayload;
  ConfirmStartOver: ConfirmStartOverPayload;
  KeepGoing: KeepGoingPayload;
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

/** The protocol version this UI build speaks; it sends it in `hello`. */
export const PROTOCOL_VERSION = 2;
