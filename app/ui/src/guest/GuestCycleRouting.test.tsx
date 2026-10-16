import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient } from "../brain/types.ts";
import { guestStart } from "../fixtures/inlineFixtures.ts";
import type { GuestCycleView, GuestPresentation, PhotographsView } from "../protocol/generated.ts";
import { GuestRoot } from "./GuestRoot.tsx";

const client: BrainClient = {
  kind: "fixture",
  connect: vi.fn(),
  getState: () => ({ status: "connected", session: null, mismatch: null }),
  subscribe: () => () => undefined,
  sendCommand: () => Promise.resolve({ status: "accepted" }),
  sendTest: vi.fn(),
  dispose: vi.fn(),
};

function cycle(step: GuestCycleView["step"]): GuestCycleView {
  return {
    id: "cycle-1",
    step,
    steps: [
      { step: "template", state: "done", canGoBack: true },
      { step: "photographs", state: "current", canGoBack: false },
      { step: "filters", state: "upcoming", canGoBack: false },
      { step: "keepsake", state: "upcoming", canGoBack: false },
    ],
    startOverAvailable: step !== "keepsake",
  };
}

function photographs(tab: PhotographsView["tab"]): PhotographsView {
  return {
    tab,
    template: { id: "signature-strip", width: 600, height: 1800, landscape: false, windows: [{ x: 0, y: 0, width: 600, height: 450 }], blankPreviewKey: null },
    windows: [{ index: 0, captureId: null, imageUrl: null }],
    retainedCount: 0,
    sequence: { running: false, targetWindow: null, countdownRemaining: null, flashSequence: 0, capturedInSequence: 0, totalInSequence: 0, pausing: false },
    mirrored: true,
    cropAspect: 600 / 450,
    selection: { index: null, mode: "none" },
    retakeCandidate: null,
    revision: 3,
    canTakePhotographs: true,
    canReview: false,
    canContinue: false,
  };
}

function renderGuest(presentation: Partial<GuestPresentation>) {
  return render(
    <BrainClientContext value={client}>
      <GuestRoot presentation={{ ...guestStart.presentation, ...presentation }} reconnecting={false} />
    </BrainClientContext>,
  );
}

describe("Guest Cycle routing", () => {
  it("routes the Template step to its surface inside the cycle chrome", () => {
    renderGuest({
      surface: "template",
      start: null,
      guestCycle: cycle("template"),
      template: { templates: [], selectedId: "signature-strip", canContinue: true },
    });

    expect(screen.getByRole("main")).toHaveAttribute("data-fotohavn-surface", "template");
    expect(screen.getByRole("heading", { level: 1, name: "Make a little Moment" })).toBeVisible();
    expect(document.querySelector("[data-fotohavn-cycle-step='template']")).not.toBeNull();
  });

  it("routes the Photographs step to the tab the brain names", () => {
    const view = renderGuest({ surface: "photographs", start: null, guestCycle: cycle("photographs"), photographs: photographs("camera") });
    expect(screen.getByRole("heading", { level: 1, name: "Find your light." })).toBeVisible();
    expect(document.querySelector("[data-photographs-tab='camera']")).not.toBeNull();
    view.unmount();

    renderGuest({ surface: "photographs", start: null, guestCycle: cycle("photographs"), photographs: photographs("arrange") });
    expect(document.querySelector("[data-photographs-tab='arrange']")).not.toBeNull();
  });

  it("routes Filters and Keepsake", () => {
    const filters = renderGuest({
      surface: "filters",
      start: null,
      guestCycle: cycle("filters"),
      filters: { looks: [], selectedLookId: "naturale-v1", width: 600, height: 1800, landscape: false, stripPreviewKey: null, composing: false, compositionFailed: false, canFinish: true },
    });
    expect(screen.getByRole("heading", { level: 1, name: "Find the feeling." })).toBeVisible();
    filters.unmount();

    renderGuest({
      surface: "keepsake",
      start: null,
      guestCycle: cycle("keepsake"),
      keepsake: { stripUrl: "https://media.fotohavn/events/e/guest-cycles/c/photo-strip.png?v=1", width: 600, height: 1800, landscape: false },
    });
    expect(screen.getByRole("heading", { level: 1, name: "A little moment. Yours." })).toBeVisible();
  });

  it("routes Operator Assistance with or without a cycle", () => {
    renderGuest({ surface: "operatorAssistance", start: null, operatorAssistance: { reason: "camera", retrying: false } });

    expect(screen.getByRole("heading", { level: 1, name: "A little interruption." })).toBeVisible();
    expect(document.querySelector("[data-assistance-reason='camera']")).not.toBeNull();
  });

  it("shows the wordmark when the brain sent a cycle surface without its view", () => {
    const view = renderGuest({ surface: "template", start: null, guestCycle: null, template: null });
    expect(screen.getByText("FOTOHAVN")).toBeVisible();
    view.unmount();

    renderGuest({ surface: "operatorAssistance", start: null, operatorAssistance: null });
    expect(screen.getByText("FOTOHAVN")).toBeVisible();
  });
});
