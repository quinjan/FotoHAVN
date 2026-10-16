import { describe, expect, it } from "vitest";
import type { GuestStripDetailView, GuestStripView } from "../protocol/generated.ts";
import { guestDetail, guestStrip } from "./guestGalleryMapping.ts";

describe("guestStrip", () => {
  it("maps a strip to its image, proportion, alternative text, and opaque id, and nothing else", () => {
    const view: GuestStripView = {
      guestCycleId: "6f1c2a8e",
      thumbnailUrl: "https://media.fotohavn/thumbnails/6f1c2a8e-0a1b2c3d-400.jpg",
      width: 1800,
      height: 600,
      landscape: true,
      alt: "Photo Strip from this Event",
    };
    expect(guestStrip(view)).toEqual({
      id: "6f1c2a8e",
      src: "https://media.fotohavn/thumbnails/6f1c2a8e-0a1b2c3d-400.jpg",
      alt: "Photo Strip from this Event",
      width: 1800,
      height: 600,
    });
  });
});

describe("guestDetail", () => {
  it("maps the strip and its arranged Captures in the brain's order, with the alternative text the brain sent", () => {
    const view: GuestStripDetailView = {
      strip: { imageUrl: "https://media.fotohavn/strip.png?v=0a", width: 600, height: 1800, landscape: false, alt: "Photo Strip from this Event" },
      captures: [
        { imageUrl: "https://media.fotohavn/capture-2.jpg?v=1b", width: 1640, height: 1230, alt: "Photograph from this Event" },
        { imageUrl: "https://media.fotohavn/capture-1.jpg?v=2c", width: 1640, height: 1230, alt: "Photograph from this Event" },
      ],
    };
    expect(guestDetail(view)).toEqual({
      strip: { src: "https://media.fotohavn/strip.png?v=0a", alt: "Photo Strip from this Event", width: 600, height: 1800 },
      captures: [
        { src: "https://media.fotohavn/capture-2.jpg?v=1b", alt: "Photograph from this Event", width: 1640, height: 1230 },
        { src: "https://media.fotohavn/capture-1.jpg?v=2c", alt: "Photograph from this Event", width: 1640, height: 1230 },
      ],
    });
  });
});
