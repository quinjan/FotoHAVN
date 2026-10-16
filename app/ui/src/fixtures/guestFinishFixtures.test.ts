import { describe, expect, it } from "vitest";
import type { GuestPresentation, OperatorPresentation } from "../protocol/generated.ts";
import { generatedFixtureNames } from "./fixtureLane.ts";
import { guestFinishFixtures } from "./guestFinishFixtures.ts";

function guest(name: string): GuestPresentation {
  const fixture = guestFinishFixtures[name];
  if (fixture?.role !== "guest") {
    throw new Error(`No guest fixture ${name}`);
  }
  return fixture.presentation;
}

function operator(name: string): OperatorPresentation {
  const fixture = guestFinishFixtures[name];
  if (fixture?.role !== "operator") {
    throw new Error(`No operator fixture ${name}`);
  }
  return fixture.presentation;
}

const guestNames = Object.keys(guestFinishFixtures).filter((name) => name.startsWith("guest-ui-"));

describe("the Filters, Keepsake, and Assistance fixtures", () => {
  it("never share a name with a brain-generated fixture, and say which role they are for in it", () => {
    for (const name of Object.keys(guestFinishFixtures)) {
      expect(name.startsWith("guest-ui-") || name.startsWith("operator-ui-")).toBe(true);
      expect(generatedFixtureNames).not.toContain(name);
    }
  });

  it("are Guest welcomes whose surface has its view, and only its view, with a cycle wherever one runs", () => {
    expect(guestNames.length).toBeGreaterThan(15);
    for (const name of guestNames) {
      const presentation = guest(name);
      const views = { filters: presentation.filters, keepsake: presentation.keepsake, operatorAssistance: presentation.operatorAssistance };
      const present = Object.entries(views)
        .filter(([, view]) => view !== null)
        .map(([surface]) => surface);
      expect(present, name).toEqual([presentation.surface]);
      expect(presentation.template, name).toBeNull();
      expect(presentation.photographs, name).toBeNull();
      expect(presentation.start, name).toBeNull();
      if (presentation.surface !== "operatorAssistance") {
        expect(presentation.guestCycle?.step, name).toBe(presentation.surface);
        expect(presentation.guestCycle?.steps.map((entry) => entry.step), name).toEqual(["template", "photographs", "filters", "keepsake"]);
      }
    }
  });

  it("show Keepsake with no warning and no START OVER, and Filters on its way with both available", () => {
    for (const name of guestNames) {
      const presentation = guest(name);
      if (presentation.surface === "keepsake") {
        expect(presentation.guestCycle?.startOverAvailable, name).toBe(false);
        expect(presentation.overlays.phaseTimerWarning, name).toBeNull();
        expect(presentation.overlays.startOverConfirmation, name).toBeNull();
        expect(presentation.guestCycle?.steps.every((entry) => !entry.canGoBack), name).toBe(true);
      }
      if (presentation.surface === "filters") {
        expect(presentation.guestCycle?.startOverAvailable, name).toBe(true);
      }
    }
  });

  it("write each Filters state the brain can send: composing, failed, and every list size", () => {
    expect(guest("guest-ui-filters").filters).toMatchObject({ composing: false, compositionFailed: false, canFinish: true });
    expect(guest("guest-ui-filters-composing").filters).toMatchObject({ composing: true, canFinish: false });
    expect(guest("guest-ui-filters-first-composing").filters).toMatchObject({ composing: true, stripPreviewKey: null });
    expect(guest("guest-ui-filters-composition-failed").filters).toMatchObject({ compositionFailed: true, canFinish: false });
    expect(guest("guest-ui-filters-two-looks").filters?.looks).toHaveLength(2);
    expect(guest("guest-ui-filters-five-looks").filters?.looks).toHaveLength(5);
    expect(guest("guest-ui-filters-many-looks").filters?.looks).toHaveLength(7);
    expect(guest("guest-ui-filters-panorama").filters?.stripPreviewKey).toMatch(/^panorama-four\./);
    expect(guest("guest-ui-filters-no-thumbnails").filters?.looks.every((look) => look.thumbnailKey === null)).toBe(true);
  });

  it("write the chrome's states: a skipped step, the confirmation, and the warning", () => {
    expect(guest("guest-ui-filters-skipped-template").guestCycle?.steps[0]).toEqual({ step: "template", state: "skipped", canGoBack: false });
    expect(guest("guest-ui-keepsake-skipped-filters").guestCycle?.steps[2]).toEqual({ step: "filters", state: "skipped", canGoBack: false });
    expect(guest("guest-ui-filters-start-over").overlays.startOverConfirmation).toEqual({});
    expect(guest("guest-ui-filters-timer-warning").overlays.phaseTimerWarning).toEqual({ phase: "filters", remainingSeconds: 18 });
    expect(guest("guest-ui-filters-timer-warning-last-second").overlays.phaseTimerWarning?.remainingSeconds).toBe(1);
  });

  it("write Keepsake for a portrait strip and a landscape one, at the Template's native size", () => {
    expect(guest("guest-ui-keepsake").keepsake).toMatchObject({ width: 600, height: 1800, landscape: false });
    expect(guest("guest-ui-keepsake-panorama").keepsake).toMatchObject({ width: 1800, height: 600, landscape: true });
    expect(guest("guest-ui-keepsake").keepsake?.stripUrl).toMatch(/^data:image\/svg\+xml,/);
  });

  it("write Operator Assistance for every reason, retrying, and with no cycle", () => {
    expect(["camera", "storage", "display"].map((reason) => guest(`guest-ui-assistance-${reason}`).operatorAssistance?.reason)).toEqual(["camera", "storage", "guestDisplay"]);
    expect(guest("guest-ui-assistance-retrying").operatorAssistance).toEqual({ reason: "camera", retrying: true });
    expect(guest("guest-ui-assistance-no-cycle").guestCycle).toBeNull();
    expect(guest("guest-ui-assistance-camera").guestCycle?.step).toBe("filters");
  });

  it("write the operator's panel in Operator Assistance for the camera and for storage", () => {
    for (const [name, reason] of [
      ["operator-ui-list-assistance-camera", "camera"],
      ["operator-ui-list-assistance-storage", "storage"],
    ] as const) {
      const presentation = operator(name);
      expect(presentation.surface).toBe("eventList");
      expect(presentation.runningEvent).toMatchObject({ currentStep: "operatorAssistance", assistance: { reason } });
    }
  });
});
