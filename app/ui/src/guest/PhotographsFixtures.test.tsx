import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import { generatedFixtureNames } from "../fixtures/fixtureLane.ts";
import { guestCaptureFixtures } from "../fixtures/guestCaptureFixtures.ts";
import { GuestRoot } from "./GuestRoot.tsx";
import { stubClient } from "./PhotographsTestKit.tsx";

describe("Template and Photographs fixtures", () => {
  const entries = Object.entries(guestCaptureFixtures);

  it("keep to the hand-written prefix and never share a name with a brain-generated fixture", () => {
    expect(entries.length).toBeGreaterThanOrEqual(20);
    for (const [name] of entries) {
      expect(name.startsWith("guest-ui-")).toBe(true);
      expect(generatedFixtureNames).not.toContain(name);
    }
  });

  it.each(entries)("%s renders its surface through the guest root", (name, fixture) => {
    const { container } = render(
      <BrainClientContext value={stubClient().client}>
        <GuestRoot presentation={fixture.presentation} reconnecting={false} />
      </BrainClientContext>,
    );
    const { surface, photographs } = fixture.presentation;

    expect(container.querySelector(`[data-fotohavn-surface='${surface}']`)).not.toBeNull();
    if (surface === "photographs") {
      expect(container.querySelector(`[data-photographs-tab='${photographs?.tab ?? ""}']`), name).not.toBeNull();
    }
  });

  it("describe only windows the Template has, with an image exactly where a Capture is", () => {
    for (const [, fixture] of entries) {
      const view = fixture.presentation.photographs;
      if (view === null) {
        continue;
      }
      expect(view.windows).toHaveLength(view.template.windows.length);
      for (const window of view.windows) {
        expect(window.captureId === null).toBe(window.imageUrl === null);
      }
    }
  });
});
