import { describe, expect, it } from "vitest";
import brainFixture from "../../fixtures/presentations/operator-camera-diagnostics-composed.json";
import landscapeFixture from "../../fixtures/presentations/operator-camera-diagnostics-composed-landscape.json";
import { lookChoices, lookName, templateChoices, templateName } from "./catalogNames.ts";

describe("catalog names", () => {
  it("lists the catalog's four Templates and seven Looks by id, as the brain has them", () => {
    expect(templateChoices.map((choice) => choice.id)).toEqual(["signature-strip", "panorama-four", "butter-gingham", "polka-keepsake"]);
    expect(lookChoices.map((choice) => choice.id)).toEqual([
      "monochrome-v1",
      "naturale-v1",
      "soft-monochrome-v1",
      "soft-color-v1",
      "sepia-v1",
      "warm-monochrome-v1",
      "classic-v1",
    ]);
  });

  it("names every id the brain's composed fixtures use", () => {
    for (const fixture of [brainFixture, landscapeFixture]) {
      const composition = fixture.presentation.cameraDiagnostics.lastComposition;
      expect(templateChoices.map((choice) => choice.id)).toContain(composition.templateId);
      expect(lookChoices.map((choice) => choice.id)).toContain(composition.lookId);
    }
  });

  it("names a Template or Look, and falls back to the id for one it doesn't know", () => {
    expect(templateName("panorama-four")).toBe("Panorama Four");
    expect(templateName("nope")).toBe("nope");
    expect(lookName("warm-monochrome-v1")).toBe("Warm Monochrome");
    expect(lookName("nope-v1")).toBe("nope-v1");
  });
});
