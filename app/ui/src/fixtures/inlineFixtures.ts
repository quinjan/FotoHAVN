import { PROTOCOL_VERSION, type GuestWelcomeMessage, type OperatorWelcomeMessage } from "../protocol/generated.ts";

/**
 * Hand-written snapshots for the fixture transport and unit tests, typed against the generated protocol. Wave C
 * replaces the gallery's use of these with the brain-generated files in `fixtures/presentations/` (contract §14).
 */

export const operatorEventListEmpty: OperatorWelcomeMessage = {
  type: "welcome",
  role: "operator",
  protocolVersion: PROTOCOL_VERSION,
  revision: 1,
  presentation: {
    surface: "eventList",
    eventList: { events: [] },
    runningEvent: null,
    overlays: { confirmation: null, identifyDisplays: null, notice: null },
    fatal: null,
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
          lastUsed: null,
          running: true,
        },
        {
          id: "event-1",
          name: "Mara's 30th",
          date: "2026-10-03",
          venue: null,
          completedGuestCycles: 42,
          lastUsed: "2026-10-03T12:00:00Z",
          running: false,
        },
      ],
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
    start: { guestGalleryAvailable: false },
    overlays: { exitHold: null, pinDialog: null, phaseTimerWarning: null, startOverConfirmation: null, notice: null },
    fatal: null,
  },
};

/** The fixtures the fixture transport serves, by their `?fixture=` name. */
export const inlineFixtures = {
  "operator-event-list-empty": operatorEventListEmpty,
  "operator-event-list": operatorEventListWithRows,
  "guest-start": guestStart,
} as const satisfies Record<string, OperatorWelcomeMessage | GuestWelcomeMessage>;

export type InlineFixtureName = keyof typeof inlineFixtures;

export const inlineFixtureNames = Object.keys(inlineFixtures) as InlineFixtureName[];

export function findInlineFixture(name: string): OperatorWelcomeMessage | GuestWelcomeMessage | undefined {
  return Object.hasOwn(inlineFixtures, name) ? inlineFixtures[name as InlineFixtureName] : undefined;
}
