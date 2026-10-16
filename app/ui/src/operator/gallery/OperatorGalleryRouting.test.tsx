import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { BrainClientContext } from "../../brain/BrainClientContext.ts";
import { operatorGalleryFixtures } from "../../fixtures/operatorGalleryFixtures.ts";
import { operatorWelcome, runningEventPanel } from "../../fixtures/operatorSetupFixtures.ts";
import type { OperatorPresentation } from "../../protocol/generated.ts";
import { stubClient } from "../../test/operatorHarness.tsx";
import { OperatorRoot } from "../OperatorRoot.tsx";

function renderRoot(presentation: OperatorPresentation) {
  return render(
    <BrainClientContext value={stubClient()}>
      <OperatorRoot presentation={presentation} reconnecting={false} />
    </BrainClientContext>,
  );
}

function fixture(name: string): OperatorPresentation {
  const message = operatorGalleryFixtures[name];
  if (message === undefined) {
    throw new Error(`no fixture ${name}`);
  }
  return message.presentation;
}

describe("the operator gallery surfaces", () => {
  it("routes eventGallery to the Event Gallery surface with the Event's id", () => {
    renderRoot(fixture("operator-ui-gallery-mixed"));
    expect(document.querySelector("main")).toHaveAttribute("data-fotohavn-surface", "eventGallery");
    expect(screen.getByRole("heading", { level: 1, name: "Event Gallery." })).toBeVisible();
    expect(document.querySelector("[data-gallery-event]")).toHaveAttribute("data-gallery-event", "event-5");
  });

  it("routes guestCycleDetail to the Guest Cycle detail surface with the Guest Cycle's id", () => {
    renderRoot(fixture("operator-ui-gallery-detail-no-strip"));
    expect(document.querySelector("main")).toHaveAttribute("data-fotohavn-surface", "guestCycleDetail");
    expect(screen.getByRole("heading", { level: 1, name: "Guest Cycle." })).toBeVisible();
    expect(document.querySelector("[data-gallery-guest-cycle]")).toHaveAttribute("data-gallery-guest-cycle", "cycle-8");
  });

  it("passes the running-Event panel to the surface and keeps the delete confirmation in the shell", () => {
    const presentation = { ...fixture("operator-ui-gallery-detail-delete"), runningEvent: runningEventPanel() };
    renderRoot(presentation);
    expect(screen.getAllByRole("heading", { level: 1 }).length).toBeGreaterThan(0);
    expect(presentation.overlays.confirmation?.kind).toBe("deleteGuestCycle");
  });

  it.each(["eventGallery", "guestCycleDetail"] as const)("shows the placeholder when %s carries no view", (surface) => {
    renderRoot(operatorWelcome(surface).presentation);
    expect(screen.getByText("This view isn't ready yet.")).toBeVisible();
  });
});
