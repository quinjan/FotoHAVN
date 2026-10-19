import {
  PROTOCOL_VERSION,
  type CameraDiagnosticsView,
  type CameraFailureKind,
  type EventListRow,
  type OperatorPresentation,
  type OperatorWelcomeMessage,
  type RunningEventPanel,
  type StorageErrorReason,
} from "../protocol/generated.ts";
import { operatorEventListEmpty } from "./inlineFixtures.ts";
import { operatorSetupFixtures, operatorWelcome, overlays, runningEventPanel } from "./operatorSetupFixtures.ts";

/**
 * Hand-written operator snapshots for the fixture lane: Event lists of every size the surface tests need, and the
 * storage error screen for every kind. Deterministic, so screenshots stay comparable.
 */

const venues = ["The Glasshouse, Tagaytay", "Manila Hotel, Ballroom B", null, "Casa Rosa Garden, Alfonso", "Fairmont Makati, Salon 2"] as const;

function eventRow(index: number, count: number, running: boolean): EventListRow {
  const day = 28 - (index % 28);
  const isRunning = running && index === 0;
  return {
    id: `event-${String(count - index)}`,
    name: `Event ${String(count - index)}: Santos and Reyes celebration`,
    date: `2026-11-${String(day).padStart(2, "0")}`,
    venue: venues[index % venues.length] ?? null,
    completedGuestCycles: (index * 37) % 260,
    lastUsedAt: index % 4 === 3 ? null : `2026-10-${String(1 + (index % 28)).padStart(2, "0")}T10:30:00Z`,
    running: isRunning,
    actions: { canStart: !running, canEdit: !isRunning, canDelete: !isRunning, canOpenFolder: true, canOpenGallery: true },
    startDisabledReason: running ? "eventRunning" : null,
  };
}

/** `count` Events, newest first, with varied venues, counts, and last-used times; the first is running when asked. */
export function eventRows(count: number, running = true): EventListRow[] {
  return Array.from({ length: count }, (_, index) => eventRow(index, count, running));
}

export const longEventName = "Alcantara-Villanueva and de los Reyes 50th anniversary celebration, Manila Hotel";

export const longEventVenue =
  "The Grand Ballroom of the Manila Hotel, One Rizal Park, Ermita, Metro Manila, Philippines (enter through the south wing)";

export function operatorEventList(events: EventListRow[]): OperatorWelcomeMessage {
  return { ...operatorEventListEmpty, presentation: { ...operatorEventListEmpty.presentation, eventList: { events, newEventAvailable: true } } };
}

export function operatorStorageError(kind: StorageErrorReason, dataPath: string): OperatorWelcomeMessage {
  return {
    type: "welcome",
    role: "operator",
    protocolVersion: PROTOCOL_VERSION,
    revision: 1,
    presentation: {
      surface: "storageError",
      eventList: null,
      eventSetup: null,
      runningEvent: null,
      overlays: { confirmation: null, identifyDisplays: null, notice: null },
      fatal: null,
      storageError: { kind, dataPath },
      cameraDiagnostics: null,
      eventGallery: null,
      guestCycleDetail: null,
    },
  };
}

export const storageErrorKinds: readonly StorageErrorReason[] = [
  "diskFull",
  "accessDenied",
  "locked",
  "notLocalDisk",
  "cloudSynced",
  "notWritable",
  "integrityFailed",
  "corrupt",
  "unknown",
];

const kebab = (kind: string): string => kind.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`);

/** The storage errors the brain's fixture tests write as files, so no hand-written fixture may share their names. */
export const storageErrorKindsFromBrain: readonly StorageErrorReason[] = ["diskFull"];

/**
 * The camera diagnostics view (M5) for a fixture, on the revision the brain-generated ones use. The brain writes the
 * fixtures for ready, checking, privacy denied, in use, and no cameras; the other not-available reasons are here.
 */
export function operatorCameraDiagnostics(view: CameraDiagnosticsView): OperatorWelcomeMessage {
  return {
    type: "welcome",
    role: "operator",
    protocolVersion: PROTOCOL_VERSION,
    revision: 3,
    presentation: {
      surface: "cameraDiagnostics",
      eventList: null,
      eventSetup: null,
      runningEvent: null,
      overlays: { confirmation: null, identifyDisplays: null, notice: null },
      fatal: null,
      storageError: null,
      cameraDiagnostics: view,
      eventGallery: null,
      guestCycleDetail: null,
    },
  };
}

/** The not-available reasons the brain's fixtures don't cover (privacy denied and in use are generated). */
export const cameraReasonsWithFixtures: readonly CameraFailureKind[] = [
  "stale",
  "failed",
  "removed",
  "exclusiveLost",
  "noSupportedFormat",
  "notFound",
  "unknown",
];

const fixtureCamera = { deviceId: "fake-camera", name: "Fake camera" };

/** The Event list with the running Event first (its panel above the rows), `panel` overriding the panel's facts. */
function runningList(panel: Partial<RunningEventPanel> = {}, parts: Partial<OperatorPresentation> = {}): OperatorWelcomeMessage {
  const events = eventRows(5);
  return operatorWelcome("eventList", {
    eventList: { events, newEventAvailable: true },
    runningEvent: runningEventPanel({ eventId: events[0]?.id ?? "event-5", name: events[0]?.name ?? "Event", ...panel }),
    ...parts,
  });
}

/** The operator fixtures for the running-Event panel, its confirmations, and notices (typed here until the brain writes them). */
function operatorListFixtures(): Record<string, OperatorWelcomeMessage> {
  const idle = eventRows(3, false);
  return {
    "operator-list-running": runningList(),
    "operator-list-running-display-disconnected": runningList({ guestDisplay: { name: "DELL P2419H", state: "disconnected" } }),
    "operator-list-running-camera-not-available": runningList({ camera: { state: "notAvailable", reason: "stale" }, completedGuestCycles: 0 }),
    "operator-list-running-camera-checking": runningList({ camera: { state: "checking", reason: null }, completedGuestCycles: 0 }),
    "operator-list-running-assistance": runningList({
      currentStep: "operatorAssistance",
      assistance: { reason: "guestDisplay" },
      guestDisplay: { name: "DELL P2419H", state: "disconnected" },
    }),
    "operator-list-running-assistance-camera-removed": runningList({
      currentStep: "operatorAssistance",
      assistance: { reason: "camera" },
      camera: { state: "notAvailable", reason: "removed" },
    }),
    "operator-list-confirm-delete": operatorWelcome("eventList", {
      eventList: { events: idle, newEventAvailable: true },
      overlays: overlays({ confirmation: { kind: "deleteEvent", eventName: idle[1]?.name ?? "", counts: { guestCycles: 12, photoStrips: 11, captures: 41 }, guestCycle: null } }),
    }),
    "operator-list-confirm-end": runningList({}, { overlays: overlays({ confirmation: { kind: "endEvent", eventName: null, counts: null, guestCycle: null } }) }),
    "operator-list-confirm-close": runningList({}, { overlays: overlays({ confirmation: { kind: "closeApp", eventName: null, counts: null, guestCycle: null } }) }),
    "operator-list-notice": operatorWelcome("eventList", {
      eventList: { events: idle, newEventAvailable: true },
      overlays: overlays({ notice: "deleteFailed" }),
    }),
    "operator-single-screen": operatorWelcome("guestScreenActive", { runningEvent: runningEventPanel({ singleScreen: true }) }),
  };
}

/** Every hand-written operator fixture by name. */
export function operatorFixtures(): Record<string, OperatorWelcomeMessage> {
  const fixtures: Record<string, OperatorWelcomeMessage> = {
    ...operatorListFixtures(),
    ...operatorSetupFixtures(),
    "operator-event-list-1": operatorEventList(eventRows(1)),
    "operator-event-list-5": operatorEventList(eventRows(5)),
    "operator-event-list-30": operatorEventList(eventRows(30)),
    "operator-event-list-long": operatorEventList([
      { ...eventRow(0, 1, true), id: "event-long-1", name: longEventName, venue: longEventVenue },
      { ...eventRow(1, 2, false), id: "event-long-2", name: longEventName, venue: longEventVenue, completedGuestCycles: 1248 },
    ]),
    "operator-camera-diagnostics-choose-camera": operatorCameraDiagnostics({
      cameras: [fixtureCamera, { deviceId: "second-camera", name: "USB camera" }],
      selectedDeviceId: null,
      state: "idle",
      reason: null,
      format: null,
      mirrored: false,
      lastCapture: null,
      lastComposition: null,
    }),
  };
  for (const reason of cameraReasonsWithFixtures) {
    fixtures[`operator-camera-diagnostics-reason-${kebab(reason)}`] = operatorCameraDiagnostics({
      cameras: [fixtureCamera],
      selectedDeviceId: fixtureCamera.deviceId,
      state: "notAvailable",
      reason,
      format: null,
      mirrored: false,
      lastCapture: null,
      lastComposition: null,
    });
  }
  for (const kind of storageErrorKinds.filter((candidate) => !storageErrorKindsFromBrain.includes(candidate))) {
    fixtures[`operator-storage-error-${kebab(kind)}`] = operatorStorageError(
      kind,
      kind === "cloudSynced" ? "C:\\Users\\Booth\\OneDrive\\FOTOHAVN\\Data" : "C:\\FOTOHAVN\\Data",
    );
  }
  return fixtures;
}
