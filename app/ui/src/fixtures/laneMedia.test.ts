import { describe, expect, it } from "vitest";
import type { GalleryItemView, GuestWelcomeMessage, OperatorWelcomeMessage, PhotographsView, ServerMessage } from "../protocol/generated.ts";
import { fixtureSnapshots } from "./fixtureLane.ts";
import { guestStart, operatorEventListEmpty } from "./inlineFixtures.ts";
import { withLaneMedia } from "./laneMedia.ts";

const media = "https://media.fotohavn/events/e/guest-cycles/c/capture-1.jpg?v=1";

function photographsOf(name: string): PhotographsView {
  const snapshot = fixtureSnapshots()[name];
  if (snapshot?.type !== "welcome" || snapshot.role !== "guest" || snapshot.presentation.photographs === null) {
    throw new Error(`No Photographs fixture ${name}`);
  }
  return snapshot.presentation.photographs;
}

function guestWith(parts: Partial<GuestWelcomeMessage["presentation"]>): GuestWelcomeMessage {
  return { ...guestStart, presentation: { ...guestStart.presentation, ...parts } };
}

describe("withLaneMedia", () => {
  it("serves the brain's Captures and Retake candidate as placeholder photographs at each window's proportion", () => {
    const view = photographsOf("guest-cycle-photographs-retaking");
    const urls = [...view.windows.map((window) => window.imageUrl), view.retakeCandidate?.imageUrl];
    expect(urls).toHaveLength(5);
    for (const url of urls) {
      expect(url).toMatch(/^data:image\/svg\+xml,/);
      expect(decodeURIComponent(url ?? "")).toContain('width="960" height="720"');
    }
  });

  it("keeps the Panorama windows' own proportion", () => {
    const first = photographsOf("guest-cycle-photographs-arrange-landscape").windows[0]?.imageUrl ?? "";
    expect(decodeURIComponent(first)).toContain('width="540" height="720"');
  });

  it("serves the Keepsake strip as a placeholder Photo Strip of its orientation", () => {
    const strips = ["guest-cycle-keepsake", "guest-cycle-keepsake-landscape"].map((name) => {
      const snapshot = fixtureSnapshots()[name];
      return snapshot?.type === "welcome" && snapshot.role === "guest" ? decodeURIComponent(snapshot.presentation.keepsake?.stripUrl ?? "") : "";
    });
    expect(strips[0]).toContain('width="600" height="1800"');
    expect(strips[1]).toContain('width="1800" height="600"');
  });

  it("leaves an image that is already local, an empty window, and a message that carries none as they were", () => {
    const local = "data:image/svg+xml,local";
    const view = photographsOf("guest-cycle-photographs-camera");
    const message = guestWith({
      photographs: {
        ...view,
        windows: [
          { index: 0, captureId: "a", imageUrl: local },
          { index: 1, captureId: null, imageUrl: null },
          { index: 2, captureId: "c", imageUrl: media },
          { index: 9, captureId: "z", imageUrl: media },
        ],
      },
      keepsake: { stripUrl: local, width: 600, height: 1800, landscape: false },
    });
    const served = withLaneMedia(message);
    expect(served).toMatchObject({ presentation: { keepsake: { stripUrl: local }, photographs: { windows: [{ imageUrl: local }, { imageUrl: null }, {}, {}] } } });
    // A window the Template doesn't have (never sent by the brain) falls back to a 4:3 photograph.
    const fallback = served.type === "welcome" && served.role === "guest" ? served.presentation.photographs?.windows[3]?.imageUrl : null;
    expect(decodeURIComponent(fallback ?? "")).toContain('width="960" height="720"');
    expect(withLaneMedia(guestStart)).toEqual(guestStart);
    const mismatch: ServerMessage = { type: "protocolMismatch", brainVersion: 1, uiVersion: 0 };
    expect(withLaneMedia(mismatch)).toBe(mismatch);
  });
});

describe("withLaneMedia, Event Gallery", () => {
  const gallery = `https://media.fotohavn/thumbnails/${"a".repeat(8)}.jpg`;
  const local = "data:image/svg+xml,local";

  function operator(parts: Partial<OperatorWelcomeMessage["presentation"]>): OperatorWelcomeMessage {
    return { ...operatorEventListEmpty, presentation: { ...operatorEventListEmpty.presentation, ...parts } };
  }

  function served(message: ServerMessage): OperatorWelcomeMessage["presentation"] {
    if (message.type !== "welcome" || message.role !== "operator") {
      throw new Error("not an operator welcome");
    }
    return message.presentation;
  }

  const item = (parts: Partial<GalleryItemView>): GalleryItemView => ({
    guestCycleId: "c",
    startedAt: "2026-10-13T12:00:00Z",
    outcome: "completed",
    templateName: null,
    lookName: null,
    strip: null,
    captures: [],
    unavailable: false,
    ...parts,
  });

  it("serves the operator list's strips and Capture thumbnails as placeholders at their own proportion", () => {
    const message = withLaneMedia(
      operator({
        surface: "eventGallery",
        eventGallery: {
          eventId: "e",
          eventName: "Gala",
          running: false,
          nextCursor: null,
          loadingMore: false,
          loadFailed: false,
          empty: false,
          items: [
            item({ strip: { thumbnailUrl: gallery, width: 600, height: 1800, landscape: false } }),
            item({ strip: { thumbnailUrl: gallery, width: 1800, height: 600, landscape: true } }),
            item({ captures: [{ thumbnailUrl: gallery, width: 1640, height: 1230 }, { thumbnailUrl: local, width: 1640, height: 1230 }] }),
          ],
        },
      }),
    );
    const items = served(message).eventGallery?.items ?? [];
    expect(decodeURIComponent(items[0]?.strip?.thumbnailUrl ?? "")).toContain('width="600" height="1800"');
    expect(decodeURIComponent(items[1]?.strip?.thumbnailUrl ?? "")).toContain('width="1800" height="600"');
    expect(decodeURIComponent(items[2]?.captures[0]?.thumbnailUrl ?? "")).toContain('width="960" height="720"');
    expect(items[2]?.captures[1]?.thumbnailUrl).toBe(local);
  });

  it("serves the operator detail's strip and Captures, and leaves local images and a missing strip alone", () => {
    const base = {
      eventId: "e",
      eventName: "Gala",
      guestCycleId: "c",
      startedAt: "2026-10-13T12:00:00Z",
      outcome: "completed",
      templateName: null,
      lookName: null,
      unavailable: false,
      canOpenFolder: true,
      canDelete: true,
    } as const;
    const withMedia = served(
      withLaneMedia(
        operator({
          surface: "guestCycleDetail",
          guestCycleDetail: { ...base, strip: { imageUrl: gallery, width: 600, height: 1800, landscape: false }, captures: [{ imageUrl: gallery, width: 1640, height: 1230 }] },
        }),
      ),
    ).guestCycleDetail;
    expect(decodeURIComponent(withMedia?.strip?.imageUrl ?? "")).toContain('width="600" height="1800"');
    expect(decodeURIComponent(withMedia?.captures[0]?.imageUrl ?? "")).toContain('width="960" height="720"');
    const untouched = served(
      withLaneMedia(
        operator({
          surface: "guestCycleDetail",
          guestCycleDetail: { ...base, strip: { imageUrl: local, width: 600, height: 1800, landscape: false }, captures: [{ imageUrl: local, width: 1640, height: 1230 }] },
        }),
      ),
    ).guestCycleDetail;
    expect(untouched?.strip?.imageUrl).toBe(local);
    expect(untouched?.captures[0]?.imageUrl).toBe(local);
    const none = served(withLaneMedia(operator({ surface: "guestCycleDetail", guestCycleDetail: { ...base, strip: null, captures: [] } }))).guestCycleDetail;
    expect(none?.strip).toBeNull();
  });

  it("serves the guest gallery's strips and the guest detail's strip and Captures as placeholders", () => {
    const alt = "Photo Strip from this Event";
    const message = withLaneMedia(
      guestWith({
        surface: "guestGallery",
        guestGallery: {
          strips: [
            { guestCycleId: "a", thumbnailUrl: gallery, width: 600, height: 1800, landscape: false, alt },
            { guestCycleId: "b", thumbnailUrl: gallery, width: 1800, height: 600, landscape: true, alt },
            { guestCycleId: "c", thumbnailUrl: local, width: 600, height: 1800, landscape: false, alt },
          ],
          nextCursor: null,
          loadingMore: false,
        },
        guestStripDetail: {
          strip: { imageUrl: gallery, width: 1800, height: 600, landscape: true, alt },
          captures: [
            { imageUrl: gallery, width: 1640, height: 1230, alt: "Photograph from this Event" },
            { imageUrl: local, width: 1640, height: 1230, alt: "Photograph from this Event" },
          ],
        },
      }),
    );
    const presentation = message.type === "welcome" && message.role === "guest" ? message.presentation : null;
    const strips = presentation?.guestGallery?.strips ?? [];
    expect(decodeURIComponent(strips[0]?.thumbnailUrl ?? "")).toContain('width="600" height="1800"');
    expect(decodeURIComponent(strips[1]?.thumbnailUrl ?? "")).toContain('width="1800" height="600"');
    expect(strips[2]?.thumbnailUrl).toBe(local);
    expect(decodeURIComponent(presentation?.guestStripDetail?.strip.imageUrl ?? "")).toContain('width="1800" height="600"');
    expect(decodeURIComponent(presentation?.guestStripDetail?.captures[0]?.imageUrl ?? "")).toContain('width="960" height="720"');
    expect(presentation?.guestStripDetail?.captures[1]?.imageUrl).toBe(local);
  });

  it("leaves a detail whose strip is already local alone", () => {
    const message = withLaneMedia(
      guestWith({
        surface: "guestStripDetail",
        guestStripDetail: { strip: { imageUrl: local, width: 600, height: 1800, landscape: false, alt: "Photo Strip from this Event" }, captures: [] },
      }),
    );
    expect(message.type === "welcome" && message.role === "guest" ? message.presentation.guestStripDetail?.strip.imageUrl : null).toBe(local);
  });
});
