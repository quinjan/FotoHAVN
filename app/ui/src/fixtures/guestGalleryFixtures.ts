import { fixtureCapture, fixtureStrip } from "../components/shared/fixturePhotographs.ts";
import {
  PROTOCOL_VERSION,
  type GuestGalleryView,
  type GuestOverlays,
  type GuestPresentation,
  type GuestStripDetailView,
  type GuestStripView,
  type GuestWelcomeMessage,
} from "../protocol/generated.ts";
import { guestStart } from "./inlineFixtures.ts";

/**
 * Hand-written fixtures for the guest gallery (M9 UI-2 owns this file; wave A seeds it). The brain's own gallery fixtures
 * (`guest-gallery-*`, `guest-strip-detail-*`, `guest-start-gallery-link`) come from the .NET tests; these cover what those don't write:
 * four strips, a long row with more to load, a landscape strip, the Phase Timer in its last seconds on the gallery and on a detail, and the load
 * state. Names start with `guest-ui-gallery-` and never repeat a brain-generated name. The views carry only images, proportions, and alt
 * text, so nothing here can show metadata.
 */

/** The guest gallery's Phase Timer is always on, so every gallery view carries it. */
const noOverlays: GuestOverlays = { ...guestStart.presentation.overlays, phaseTimer: { phase: "guestGallery", remainingSeconds: 52, paused: false } };

const alt = "Photo Strip from this Event";
const captureAlt = "Photograph from this Event";

function strips(count: number, landscapeEvery = 0): GuestStripView[] {
  return Array.from({ length: count }, (_, index) => {
    const landscape = landscapeEvery > 0 && index % landscapeEvery === landscapeEvery - 1;
    const image = fixtureStrip(landscape ? "panorama-four" : "signature-strip", index);
    return { guestCycleId: `cycle-${String(count - index)}`, thumbnailUrl: image.src, width: landscape ? 1800 : 600, height: landscape ? 600 : 1800, landscape, alt };
  });
}

function welcome(presentation: Partial<GuestPresentation>): GuestWelcomeMessage {
  return {
    type: "welcome",
    role: "guest",
    protocolVersion: PROTOCOL_VERSION,
    revision: 1,
    presentation: { ...guestStart.presentation, start: null, ...presentation },
  };
}

function galleryWelcome(view: GuestGalleryView, overlays: Partial<GuestOverlays> = {}): GuestWelcomeMessage {
  return welcome({ surface: "guestGallery", guestGallery: view, overlays: { ...noOverlays, ...overlays } });
}

function detailWelcome(view: GuestStripDetailView, overlays: Partial<GuestOverlays> = {}): GuestWelcomeMessage {
  return welcome({ surface: "guestStripDetail", guestStripDetail: view, overlays: { ...noOverlays, ...overlays } });
}

const detailView: GuestStripDetailView = {
  strip: { imageUrl: fixtureStrip("signature-strip", 1).src, width: 600, height: 1800, landscape: false, alt },
  captures: Array.from({ length: 4 }, (_, index) => ({ imageUrl: fixtureCapture(index, 1640, 1230).src, width: 1640, height: 1230, alt: captureAlt })),
};

export const guestGalleryFixtures: Readonly<Record<string, GuestWelcomeMessage>> = {
  "guest-ui-gallery-four-strips": galleryWelcome({ strips: strips(4), nextCursor: null, loadingMore: false }),
  "guest-ui-gallery-many-strips": galleryWelcome({ strips: strips(40, 5), nextCursor: "cursor-1", loadingMore: false }),
  "guest-ui-gallery-loading-more": galleryWelcome({ strips: strips(40, 5), nextCursor: "cursor-1", loadingMore: true }),
  "guest-ui-gallery-loading-first": galleryWelcome({ strips: [], nextCursor: null, loadingMore: true }),
  "guest-ui-gallery-timer-low": galleryWelcome(
    { strips: strips(4), nextCursor: null, loadingMore: false },
    { phaseTimer: { phase: "guestGallery", remainingSeconds: 8, paused: false } },
  ),
  "guest-ui-strip-detail": detailWelcome(detailView),
  "guest-ui-strip-detail-landscape": detailWelcome({
    strip: { imageUrl: fixtureStrip("panorama-four", 2).src, width: 1800, height: 600, landscape: true, alt },
    captures: detailView.captures,
  }),
  "guest-ui-strip-detail-timer-low": detailWelcome(detailView, { phaseTimer: { phase: "guestGallery", remainingSeconds: 1, paused: false } }),
};
