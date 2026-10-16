import { describe, expect, it } from "vitest";
import type { GalleryItemView, GuestCycleDetailView } from "../../protocol/generated.ts";
import { operatorDetail, operatorItem } from "./galleryMapping.ts";

const startedAt = new Date(2026, 9, 13, 20, 42).toISOString();

const item: GalleryItemView = {
  guestCycleId: "cycle-1",
  startedAt,
  outcome: "completed",
  templateName: "Signature Strip",
  lookName: "Naturale",
  strip: { thumbnailUrl: "https://media.fotohavn/thumbnails/s-400.jpg", width: 600, height: 1800, landscape: false },
  captures: [],
  unavailable: false,
};

const detail: GuestCycleDetailView = {
  eventId: "event-1",
  eventName: "Studio open day",
  guestCycleId: "cycle-1",
  startedAt,
  outcome: "completed",
  templateName: "Signature Strip",
  lookName: "Naturale",
  strip: { imageUrl: "https://media.fotohavn/events/event-1/guest-cycles/cycle-1/photo-strip.png?v=0a1b2c3d", width: 600, height: 1800, landscape: false },
  captures: [
    { imageUrl: "https://media.fotohavn/events/event-1/guest-cycles/cycle-1/capture-2.jpg?v=1b2c3d4e", width: 1640, height: 1230 },
    { imageUrl: "https://media.fotohavn/events/event-1/guest-cycles/cycle-1/capture-1.jpg?v=2c3d4e5f", width: 1640, height: 1230 },
  ],
  unavailable: false,
  canOpenFolder: true,
  canDelete: true,
};

describe("operatorItem", () => {
  it("maps a list item with the brain's thumbnail URL used as it is, and the time in the operator's local zone", () => {
    expect(operatorItem(item)).toEqual({
      id: "cycle-1",
      time: "13 Oct, 8:42 PM",
      outcome: "completed",
      templateName: "Signature Strip",
      lookName: "Naturale",
      strip: { src: "https://media.fotohavn/thumbnails/s-400.jpg", alt: "Photo Strip", width: 600, height: 1800 },
      captures: [],
    });
  });

  it.each([
    ["completed", "completed"],
    ["startedOver", "started-over"],
    ["endedByOperator", "ended-by-operator"],
    ["appClosed", "app-closed"],
    ["unrecoverable", "unrecoverable"],
  ] as const)("maps the outcome %s to %s", (wire, outcome) => {
    expect(operatorItem({ ...item, outcome: wire }).outcome).toBe(outcome);
  });

  it("keeps a Guest Cycle with no strip: its kept Captures in order, no Template or Look recorded", () => {
    const mapped = operatorItem({
      ...item,
      templateName: null,
      lookName: null,
      strip: null,
      captures: [
        { thumbnailUrl: "https://media.fotohavn/thumbnails/a-480.jpg", width: 1640, height: 1230 },
        { thumbnailUrl: "https://media.fotohavn/thumbnails/b-480.jpg", width: 1640, height: 1230 },
      ],
    });
    expect(mapped.strip).toBeNull();
    expect(mapped.captures.map((capture) => capture.src)).toEqual(["https://media.fotohavn/thumbnails/a-480.jpg", "https://media.fotohavn/thumbnails/b-480.jpg"]);
    expect(mapped.captures[0]?.alt).toBe("Photograph from this Event");
    expect("templateName" in mapped).toBe(false);
    expect("lookName" in mapped).toBe(false);
    expect("unavailable" in mapped).toBe(false);
  });

  it("keeps the names a Guest Cycle with a strip recorded when its files are gone, and marks it unavailable", () => {
    const mapped = operatorItem({ ...item, strip: null, unavailable: true, templateName: "Panorama Four", lookName: "Sepia" });
    expect(mapped).toMatchObject({ unavailable: true, strip: null, captures: [], templateName: "Panorama Four", lookName: "Sepia" });
  });

  it("keeps a landscape strip's real size, so it stays in native orientation", () => {
    const mapped = operatorItem({ ...item, strip: { thumbnailUrl: "u", width: 1800, height: 600, landscape: true } });
    expect(mapped.strip).toMatchObject({ width: 1800, height: 600 });
  });
});

describe("operatorDetail", () => {
  it("maps the full files and the arranged Captures in the brain's order", () => {
    const mapped = operatorDetail(detail);
    expect(mapped.strip?.src).toContain("photo-strip.png?v=0a1b2c3d");
    expect(mapped.captures.map((capture) => capture.src.slice(-14))).toEqual(["pture-2.jpg?v=1b2c3d4e".slice(-14), "pture-1.jpg?v=2c3d4e5f".slice(-14)]);
    expect(mapped.time).toBe("13 Oct, 8:42 PM");
    expect("unavailable" in mapped).toBe(false);
  });

  it("maps an unavailable Guest Cycle with no names", () => {
    const mapped = operatorDetail({ ...detail, strip: null, captures: [], templateName: null, lookName: null, unavailable: true, outcome: "appClosed" });
    expect(mapped).toMatchObject({ unavailable: true, strip: null, captures: [], outcome: "app-closed" });
    expect("templateName" in mapped).toBe(false);
  });
});
