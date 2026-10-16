import { describe, expect, it } from "vitest";
import type { GuestWelcomeMessage, PhotographsView, ServerMessage } from "../protocol/generated.ts";
import { fixtureSnapshots } from "./fixtureLane.ts";
import { guestStart } from "./inlineFixtures.ts";
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
