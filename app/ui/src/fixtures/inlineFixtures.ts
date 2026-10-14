import { PROTOCOL_VERSION, type GuestWelcomeMessage, type OperatorWelcomeMessage } from "../protocol/generated.ts";

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
