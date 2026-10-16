import { describe, expect, it } from "vitest";
import type { GuestPresentation, OperatorPresentation } from "../protocol/generated.ts";
import { generatedFixtureNames, handWrittenFixtureNames } from "./fixtureLane.ts";
import { guestGalleryFixtures } from "./guestGalleryFixtures.ts";
import { operatorGalleryFixtures } from "./operatorGalleryFixtures.ts";

const operatorNames = Object.keys(operatorGalleryFixtures);
const guestNames = Object.keys(guestGalleryFixtures);

function operatorOf(name: string): OperatorPresentation {
  const message = operatorGalleryFixtures[name];
  if (message === undefined) {
    throw new Error(`no fixture ${name}`);
  }
  return message.presentation;
}

function guestOf(name: string): GuestPresentation {
  const message = guestGalleryFixtures[name];
  if (message === undefined) {
    throw new Error(`no fixture ${name}`);
  }
  return message.presentation;
}

describe("the hand-written gallery fixtures", () => {
  it("are served by the lane and never share a name with a brain-generated fixture", () => {
    for (const name of operatorNames) {
      expect(name.startsWith("operator-ui-gallery-"), name).toBe(true);
    }
    for (const name of guestNames) {
      expect(name.startsWith("guest-ui-"), name).toBe(true);
    }
    for (const name of [...operatorNames, ...guestNames]) {
      expect(generatedFixtureNames).not.toContain(name);
      expect(handWrittenFixtureNames).toContain(name);
    }
  });

  it("are operator welcomes whose surface has its view, and only its view", () => {
    expect(operatorNames.length).toBeGreaterThanOrEqual(6);
    for (const name of operatorNames) {
      const presentation = operatorOf(name);
      expect(presentation.surface === "eventGallery" ? presentation.eventGallery : presentation.guestCycleDetail, name).not.toBeNull();
      expect(presentation.surface === "eventGallery" ? presentation.guestCycleDetail : presentation.eventGallery, name).toBeNull();
      expect(presentation.eventList).toBeNull();
      expect(presentation.eventSetup).toBeNull();
    }
  });

  it("are guest welcomes whose surface has its view, and only its view", () => {
    expect(guestNames.length).toBeGreaterThanOrEqual(8);
    for (const name of guestNames) {
      const presentation = guestOf(name);
      expect(presentation.surface === "guestGallery" ? presentation.guestGallery : presentation.guestStripDetail, name).not.toBeNull();
      expect(presentation.surface === "guestGallery" ? presentation.guestStripDetail : presentation.guestGallery, name).toBeNull();
      expect(presentation.start).toBeNull();
      expect(presentation.guestCycle).toBeNull();
    }
  });

  it("give the guest views only images, proportions, and alt text", () => {
    const allowed = new Set(["strips", "nextCursor", "loadingMore", "guestCycleId", "thumbnailUrl", "width", "height", "landscape", "alt", "strip", "captures", "imageUrl"]);
    for (const name of guestNames) {
      const { guestGallery, guestStripDetail } = guestOf(name);
      const text = JSON.stringify(guestGallery ?? guestStripDetail);
      for (const key of text.matchAll(/"([A-Za-z]+)":/g)) {
        expect(allowed.has(key[1] ?? ""), `${name}: ${key[1] ?? ""}`).toBe(true);
      }
    }
  });

  it("cover the list states: more to load, loading, a failed load, the end, a Guest Cycle with no strip, and one whose files are gone", () => {
    const views = operatorNames.map((name) => operatorOf(name).eventGallery).filter((view) => view !== null);
    expect(views.some((view) => view.nextCursor !== null)).toBe(true);
    expect(views.some((view) => view.nextCursor === null)).toBe(true);
    expect(views.some((view) => view.loadingMore)).toBe(true);
    expect(views.some((view) => view.loadFailed)).toBe(true);
    const items = views.flatMap((view) => view.items);
    expect(items.some((item) => item.strip === null && item.captures.length > 0)).toBe(true);
    expect(items.some((item) => item.unavailable)).toBe(true);
    expect(items.some((item) => item.strip?.landscape === true)).toBe(true);
  });

  it("show the delete confirmation for a Guest Cycle, naming its time and counting its files", () => {
    const confirmation = operatorOf("operator-ui-gallery-detail-delete").overlays.confirmation;
    expect(confirmation).toMatchObject({ kind: "deleteGuestCycle", guestCycle: { guestCycleId: "cycle-9", fileCount: 5 } });
  });
});
