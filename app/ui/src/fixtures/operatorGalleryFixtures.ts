import { fixtureCapture, fixtureStrip } from "../components/shared/fixturePhotographs.ts";
import type { EventGalleryView, GalleryItemView, GuestCycleDetailView, OperatorWelcomeMessage } from "../protocol/generated.ts";
import { operatorWelcome, overlays } from "./operatorSetupFixtures.ts";

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

export const operatorGalleryFixtures: Readonly<Record<string, OperatorWelcomeMessage>> = {
  "operator-ui-gallery-mixed": galleryWelcome(gallery()),
  "operator-ui-gallery-loading-more": galleryWelcome(gallery({ loadingMore: true })),
  "operator-ui-gallery-load-failed": galleryWelcome(gallery({ loadFailed: true })),
  "operator-ui-gallery-end": galleryWelcome(gallery({ nextCursor: null })),
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
  "operator-ui-gallery-detail-delete": operatorWelcome("guestCycleDetail", {
    guestCycleDetail: detail(),
    overlays: overlays({
      confirmation: { kind: "deleteGuestCycle", eventName: null, counts: null, guestCycle: { guestCycleId: "cycle-9", startedAt: at(2), fileCount: 5 } },
    }),
  }),
};
