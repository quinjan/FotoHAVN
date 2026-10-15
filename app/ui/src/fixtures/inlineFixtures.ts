import {
  PROTOCOL_VERSION,
  type GuestOverlays,
  type GuestPresentation,
  type GuestWelcomeMessage,
  type OperatorWelcomeMessage,
  type PinDialogOverlay,
} from "../protocol/generated.ts";

/**
 * Hand-written snapshots for unit tests, typed against the generated protocol. The fixture lane serves the
 * brain-generated files in `presentations/` instead (`fixtureLane.ts`), plus `operatorEventListWithRows` until the
 * brain can generate Event rows (M4).
 */

export const operatorEventListEmpty: OperatorWelcomeMessage = {
  type: "welcome",
  role: "operator",
  protocolVersion: PROTOCOL_VERSION,
  revision: 1,
  presentation: {
    surface: "eventList",
    eventList: { events: [], newEventAvailable: true },
    eventSetup: null,
    runningEvent: null,
    overlays: { confirmation: null, identifyDisplays: null, notice: null },
    fatal: null,
    storageError: null,
    cameraDiagnostics: null,
  },
};

export const operatorEventListWithRows: OperatorWelcomeMessage = {
  ...operatorEventListEmpty,
  presentation: {
    ...operatorEventListEmpty.presentation,
    eventList: {
      events: [
        {
          id: "event-2",
          name: "Santos and Reyes wedding",
          date: "2026-11-21",
          venue: "The Glasshouse, Tagaytay",
          completedGuestCycles: 1,
          lastUsedAt: null,
          running: true,
          actions: { canStart: false, canEdit: false, canDelete: false, canOpenFolder: true, canOpenGallery: true },
          startDisabledReason: "eventRunning",
        },
        {
          id: "event-1",
          name: "Mara's 30th",
          date: "2026-10-03",
          venue: null,
          completedGuestCycles: 42,
          lastUsedAt: "2026-10-03T12:00:00Z",
          running: false,
          actions: { canStart: false, canEdit: true, canDelete: true, canOpenFolder: true, canOpenGallery: true },
          startDisabledReason: "eventRunning",
        },
      ],
      newEventAvailable: true,
    },
  },
};

export const guestStart: GuestWelcomeMessage = {
  type: "welcome",
  role: "guest",
  protocolVersion: PROTOCOL_VERSION,
  revision: 1,
  presentation: {
    surface: "start",
    start: { stepInsideAvailable: false, guestGalleryLinkVisible: false },
    startUnavailable: null,
    overlays: { exitHold: null, pinDialog: null, phaseTimerWarning: null, startOverConfirmation: null, notice: null },
    fatal: null,
  },
};

const noGuestOverlays: GuestOverlays = {
  exitHold: null,
  pinDialog: null,
  phaseTimerWarning: null,
  startOverConfirmation: null,
  notice: null,
};

function guestWelcome(presentation: Partial<GuestPresentation>): GuestWelcomeMessage {
  return {
    ...guestStart,
    presentation: { ...guestStart.presentation, ...presentation },
  };
}

function pinDialogWelcome(pinDialog: PinDialogOverlay): GuestWelcomeMessage {
  return guestWelcome({
    start: { stepInsideAvailable: true, guestGalleryLinkVisible: false },
    overlays: { ...noGuestOverlays, pinDialog },
  });
}

/**
 * Hand-written Guest Screen shell states for the fixture lane (`guest-shell-*`) and unit tests, typed against the
 * generated protocol. They stand beside the brain-generated `guest-*` files, which the brain's own fixture tests write.
 */
export const guestShellFixtures: Readonly<Record<string, GuestWelcomeMessage>> = {
  "guest-shell-start-ready": guestWelcome({ start: { stepInsideAvailable: true, guestGalleryLinkVisible: false } }),
  "guest-shell-start-gallery": guestWelcome({ start: { stepInsideAvailable: true, guestGalleryLinkVisible: true } }),
  "guest-shell-start-not-ready": guestWelcome({ start: { stepInsideAvailable: false, guestGalleryLinkVisible: false } }),
  "guest-shell-start-unavailable-camera": guestWelcome({
    surface: "startUnavailable",
    start: null,
    startUnavailable: { reason: "camera", guestGalleryLinkVisible: false },
  }),
  "guest-shell-start-unavailable-storage": guestWelcome({
    surface: "startUnavailable",
    start: null,
    startUnavailable: { reason: "storage", guestGalleryLinkVisible: true },
  }),
  "guest-shell-pin-dialog": pinDialogWelcome({ error: null, lockedRemainingSeconds: null, attemptsRemaining: 5 }),
  "guest-shell-pin-dialog-wrong": pinDialogWelcome({ error: "wrongPin", lockedRemainingSeconds: null, attemptsRemaining: 3 }),
  "guest-shell-pin-dialog-locked": pinDialogWelcome({ error: "wrongPin", lockedRemainingSeconds: 27, attemptsRemaining: null }),
};
