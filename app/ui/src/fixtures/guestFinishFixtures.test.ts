import { describe, expect, it } from "vitest";
import type { GuestPresentation } from "../protocol/generated.ts";
import { generatedFixtureNames } from "./fixtureLane.ts";
import { guestFinishFixtures } from "./guestFinishFixtures.ts";

function guest(name: string): GuestPresentation {
  const fixture = guestFinishFixtures[name];
  if (fixture === undefined) {
    throw new Error(`No guest fixture ${name}`);
  }
  return fixture.presentation;
}

const names = Object.keys(guestFinishFixtures);

describe("the hand-written Filters, Keepsake, and Assistance fixtures", () => {
  it("never share a name with a brain-generated fixture", () => {
    for (const name of names) {
      expect(name.startsWith("guest-ui-"), name).toBe(true);
      expect(generatedFixtureNames).not.toContain(name);
    }
  });

  it("are Guest welcomes whose surface has its view, and only its view, with a cycle wherever one runs", () => {
    expect(names.length).toBeGreaterThanOrEqual(10);
    for (const name of names) {
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
    for (const name of names) {
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

  it("write the Filters states the brain's fixtures don't: a strip kept while composing or failing, and every list size", () => {
    expect(guest("guest-ui-filters-composing").filters).toMatchObject({ composing: true, canFinish: false });
    expect(guest("guest-ui-filters-composing").filters?.stripPreviewKey).not.toBeNull();
    expect(guest("guest-ui-filters-composition-failed").filters).toMatchObject({ compositionFailed: true, canFinish: false });
    expect(guest("guest-ui-filters-composition-failed").filters?.stripPreviewKey).not.toBeNull();
    expect(guest("guest-ui-filters-four-looks").filters?.looks).toHaveLength(4);
    expect(guest("guest-ui-filters-five-looks").filters?.looks).toHaveLength(5);
    expect(guest("guest-ui-filters-many-looks").filters?.looks).toHaveLength(7);
    expect(guest("guest-ui-filters-no-thumbnails").filters?.looks.every((look) => look.thumbnailKey === null)).toBe(true);
  });

  it("give every Filters view the Template's size, as the brain does", () => {
    for (const name of names) {
      const filters = guest(name).filters;
      if (filters !== null) {
        expect(filters, name).toMatchObject({ width: 600, height: 1800, landscape: false });
      }
    }
  });

  it("write the chrome's states: a skipped step and the last second of the warning", () => {
    expect(guest("guest-ui-filters-skipped-template").guestCycle?.steps[0]).toEqual({ step: "template", state: "skipped", canGoBack: false });
    expect(guest("guest-ui-keepsake-skipped-filters").guestCycle?.steps[2]).toEqual({ step: "filters", state: "skipped", canGoBack: false });
    expect(guest("guest-ui-filters-timer-warning-last-second").overlays.phaseTimerWarning).toEqual({ phase: "filters", remainingSeconds: 1 });
  });

  it("write Keepsake for a one-Look Event, at the Template's native size", () => {
    expect(guest("guest-ui-keepsake-skipped-filters").keepsake).toMatchObject({ width: 600, height: 1800, landscape: false });
    expect(guest("guest-ui-keepsake-skipped-filters").keepsake?.stripUrl).toMatch(/^data:image\/svg\+xml,/);
  });

  it("write Operator Assistance while it retries, with its cycle", () => {
    expect(guest("guest-ui-assistance-retrying").operatorAssistance).toEqual({ reason: "camera", retrying: true });
    expect(guest("guest-ui-assistance-retrying").guestCycle?.step).toBe("filters");
  });
});
