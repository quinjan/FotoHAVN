import { fixtureCapture, fixtureStrip } from "../components/shared/fixturePhotographs.ts";
import type { EventGalleryView, GalleryItemView, GuestCycleDetailView, OperatorWelcomeMessage } from "../protocol/generated.ts";
import { operatorWelcome, overlays, runningEventPanel } from "./operatorSetupFixtures.ts";

/**
 * Hand-written fixtures for the operator's Event Gallery (M9 UI-1 owns this file; wave A seeds it). The brain's own gallery fixtures
 * (`operator-gallery-*`, `operator-guest-cycle-detail-*`) come from the .NET tests; these cover what those don't write: a long list, a
 * mix of outcomes, a landscape strip, an item whose files are gone, and the load states. Names start with `operator-ui-gallery-` and never
 * repeat a brain-generated name. Thumbnails are the lane's placeholder photographs, as `laneMedia.ts` serves for the brain's.
 */

const strip = fixtureStrip("signature-strip", 2);
const panorama = fixtureStrip("panorama-four", 3);
const capture = fixtureCapture(5, 1640, 1230);

const at = (minutes: number) => new Date(Date.UTC(2026, 9, 13, 12, 0, 0) - minutes * 60_000).toISOString();

const completed = (id: string, minutes: number, landscape = false): GalleryItemView => ({
  guestCycleId: id,
  startedAt: at(minutes),
  outcome: "completed",
  templateName: landscape ? "Panorama Four" : "Signature Strip",
  lookName: "Naturale",
  strip: { thumbnailUrl: (landscape ? panorama : strip).src, width: landscape ? 1800 : 600, height: landscape ? 600 : 1800, landscape },
  captures: [],
  unavailable: false,
});

const withoutStrip = (id: string, minutes: number, outcome: GalleryItemView["outcome"], count: number): GalleryItemView => ({
  guestCycleId: id,
  startedAt: at(minutes),
  outcome,
  templateName: null,
  lookName: null,
  strip: null,
  captures: Array.from({ length: count }, () => ({ thumbnailUrl: capture.src, width: capture.width, height: capture.height })),
  unavailable: false,
});

const items: readonly GalleryItemView[] = [
  completed("cycle-9", 2),
  withoutStrip("cycle-8", 6, "startedOver", 2),
  completed("cycle-7", 11, true),
  withoutStrip("cycle-6", 17, "endedByOperator", 3),
  { ...withoutStrip("cycle-5", 24, "appClosed", 0), templateName: "Signature Strip", lookName: "Sepia", unavailable: true },
  withoutStrip("cycle-4", 31, "unrecoverable", 0),
  completed("cycle-3", 40),
];

function gallery(parts: Partial<EventGalleryView> = {}): EventGalleryView {
  return {
    eventId: "event-5",
    eventName: "Event 5: Santos and Reyes celebration",
    running: false,
    items: [...items],
    nextCursor: "cursor-1",
    loadingMore: false,
    loadFailed: false,
    empty: false,
    ...parts,
  };
}

function galleryWelcome(view: EventGalleryView): OperatorWelcomeMessage {
  return operatorWelcome("eventGallery", { eventGallery: view });
}

function detail(parts: Partial<GuestCycleDetailView> = {}): GuestCycleDetailView {
  return {
    eventId: "event-5",
    eventName: "Event 5: Santos and Reyes celebration",
    guestCycleId: "cycle-9",
    startedAt: at(2),
    outcome: "completed",
    templateName: "Signature Strip",
    lookName: "Naturale",
    strip: { imageUrl: strip.src, width: 600, height: 1800, landscape: false },
    captures: Array.from({ length: 4 }, (_, index) => ({ imageUrl: fixtureCapture(index, 1640, 1230).src, width: 1640, height: 1230 })),
    unavailable: false,
    canOpenFolder: true,
    canDelete: true,
    ...parts,
  };
}

/** Three hundred and twenty Guest Cycles (eight pages of 40), newest first, in the mix of endings an Event has. */
function manyItems(count: number): GalleryItemView[] {
  return Array.from({ length: count }, (_, index) => {
    const id = `cycle-${String(count - index)}`;
    const minutes = 3 + index * 2;
    if (index % 9 === 4) {
      return withoutStrip(id, minutes, index % 2 === 0 ? "startedOver" : "endedByOperator", 1 + (index % 3));
    }
    return completed(id, minutes, index % 7 === 3);
  });
}

const confirmation = (fileCount: number) =>
  overlays({
    confirmation: { kind: "deleteGuestCycle", eventName: null, counts: null, guestCycle: { guestCycleId: "cycle-9", startedAt: at(2), fileCount } },
  });

export const operatorGalleryFixtures: Readonly<Record<string, OperatorWelcomeMessage>> = {
  "operator-ui-gallery-mixed": galleryWelcome(gallery()),
  "operator-ui-gallery-loading-more": galleryWelcome(gallery({ loadingMore: true })),
  "operator-ui-gallery-load-failed": galleryWelcome(gallery({ loadFailed: true })),
  "operator-ui-gallery-end": galleryWelcome(gallery({ nextCursor: null })),
  "operator-ui-gallery-large": galleryWelcome(gallery({ items: manyItems(320), nextCursor: null })),
  "operator-ui-gallery-large-more": galleryWelcome(gallery({ items: manyItems(320), nextCursor: "cursor-8" })),
  "operator-ui-gallery-running": operatorWelcome("eventGallery", {
    eventGallery: gallery({ running: true, items: items.slice(0, 3), nextCursor: null }),
    runningEvent: runningEventPanel({ currentStep: "photographs" }),
  }),
  "operator-ui-gallery-running-assistance": operatorWelcome("eventGallery", {
    eventGallery: gallery({ running: true, items: items.slice(0, 3), nextCursor: null }),
    runningEvent: runningEventPanel({ assistance: { reason: "camera" } }),
  }),
  "operator-ui-gallery-delete-list": operatorWelcome("eventGallery", { eventGallery: gallery(), overlays: confirmation(5) }),
  "operator-ui-gallery-detail": operatorWelcome("guestCycleDetail", { guestCycleDetail: detail() }),
  "operator-ui-gallery-detail-landscape": operatorWelcome("guestCycleDetail", {
    guestCycleDetail: detail({
      guestCycleId: "cycle-7",
      templateName: "Panorama Four",
      strip: { imageUrl: panorama.src, width: 1800, height: 600, landscape: true },
    }),
  }),
  "operator-ui-gallery-detail-no-strip": operatorWelcome("guestCycleDetail", {
    guestCycleDetail: detail({
      guestCycleId: "cycle-8",
      outcome: "startedOver",
      templateName: null,
      lookName: null,
      strip: null,
      captures: detail().captures.slice(0, 2),
    }),
  }),
  "operator-ui-gallery-detail-unavailable": operatorWelcome("guestCycleDetail", {
    guestCycleDetail: detail({ guestCycleId: "cycle-5", outcome: "appClosed", lookName: "Sepia", strip: null, captures: [], unavailable: true }),
  }),
  "operator-ui-gallery-detail-delete": operatorWelcome("guestCycleDetail", { guestCycleDetail: detail(), overlays: confirmation(5) }),
  "operator-ui-gallery-detail-delete-one-file": operatorWelcome("guestCycleDetail", {
    guestCycleDetail: detail({ guestCycleId: "cycle-9", outcome: "startedOver", templateName: null, lookName: null, strip: null, captures: detail().captures.slice(0, 1) }),
    overlays: confirmation(1),
  }),
  "operator-ui-gallery-detail-delete-failed": operatorWelcome("guestCycleDetail", {
    guestCycleDetail: detail(),
    overlays: overlays({ notice: "guestCycleDeleteFailed" }),
  }),
};
