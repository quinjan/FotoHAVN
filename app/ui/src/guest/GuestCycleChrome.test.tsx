import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient } from "../brain/types.ts";
import type { GuestCycleStepEntry, GuestCycleView, GuestOverlays, PhaseTimer } from "../protocol/generated.ts";
import { sentCommands, stubClient } from "../test/operatorHarness.tsx";
import { GuestCycleChrome } from "./GuestCycleChrome.tsx";

type StepName = GuestCycleStepEntry["step"];

const noOverlays: GuestOverlays = { exitHold: null, pinDialog: null, phaseTimerWarning: null, startOverConfirmation: null, notice: null };

function cycle(current: StepName, entries?: readonly GuestCycleStepEntry[], startOverAvailable = current !== "keepsake"): GuestCycleView {
  const order: readonly StepName[] = ["template", "photographs", "filters", "keepsake"];
  const at = order.indexOf(current);
  const steps =
    entries ??
    order.map((step, index): GuestCycleStepEntry => ({
      step,
      state: index < at ? "done" : index === at ? "current" : "upcoming",
      canGoBack: index < at && current !== "keepsake",
    }));
  return { id: "cycle-1", step: current, steps: [...steps], startOverAvailable };
}

function ui(client: BrainClient, view: GuestCycleView, overlays: Partial<GuestOverlays> = {}, body = "The step") {
  return (
    <BrainClientContext value={client}>
      <GuestCycleChrome cycle={view} overlays={{ ...noOverlays, ...overlays }}>
        <p>{body}</p>
      </GuestCycleChrome>
    </BrainClientContext>
  );
}

function steps(): string[] {
  return within(screen.getByRole("list", { name: "Steps" }))
    .getAllByRole("listitem")
    .map((item) => item.textContent);
}

function warning(phase: PhaseTimer, remainingSeconds: number): Partial<GuestOverlays> {
  return { phaseTimerWarning: { phase, remainingSeconds } };
}

function stubReducedMotion(matches: boolean): void {
  vi.stubGlobal(
    "matchMedia",
    vi.fn(() => ({ matches, addEventListener: vi.fn(), removeEventListener: vi.fn() })),
  );
}

describe("GuestCycleChrome", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("wraps the step and names it, with the step's own content untouched", () => {
    const { container } = render(ui(stubClient(), cycle("filters")));
    expect(screen.getByText("The step")).toBeVisible();
    expect(container.firstElementChild).toHaveAttribute("data-fotohavn-cycle-step", "filters");
    expect(container.firstElementChild).toHaveAttribute("data-start-over-available", "true");
  });

  describe("step progress", () => {
    it("lists the four steps with the current one marked", () => {
      render(ui(stubClient(), cycle("photographs")));
      expect(steps()).toEqual(["01 Template, completed", "02 Photographs", "03 Filters", "04 Keepsake"]);
      expect(screen.getByText("Photographs").parentElement).toHaveAttribute("aria-current", "step");
    });

    it("Q26: leaves out skipped steps and renumbers the rest", () => {
      const view = cycle("filters", [
        { step: "template", state: "skipped", canGoBack: false },
        { step: "photographs", state: "done", canGoBack: true },
        { step: "filters", state: "current", canGoBack: false },
        { step: "keepsake", state: "upcoming", canGoBack: false },
      ]);
      const { rerender } = render(ui(stubClient(), view));
      expect(steps()).toEqual(["01 Photographs, completed", "02 Filters", "03 Keepsake"]);

      rerender(
        ui(
          stubClient(),
          cycle("keepsake", [
            { step: "template", state: "skipped", canGoBack: false },
            { step: "photographs", state: "done", canGoBack: false },
            { step: "filters", state: "skipped", canGoBack: false },
            { step: "keepsake", state: "current", canGoBack: false },
          ]),
        ),
      );
      expect(steps()).toEqual(["01 Photographs, completed", "02 Keepsake"]);
    });

    it("goes back with GoToStep on the steps the brain says can be revisited", () => {
      const client = stubClient();
      render(ui(client, cycle("filters")));
      expect(screen.getAllByRole("button").filter((button) => button.dataset.revisit === "true")).toHaveLength(2);
      fireEvent.click(screen.getByRole("button", { name: "02 Photographs, completed" }));
      fireEvent.click(screen.getByRole("button", { name: "01 Template, completed" }));
      expect(sentCommands(client)).toEqual([
        ["GoToStep", { step: "photographs" }],
        ["GoToStep", { step: "template" }],
      ]);
    });

    it("offers no going back to a step the brain says can't be revisited", () => {
      const view = cycle("filters", [
        { step: "template", state: "done", canGoBack: false },
        { step: "photographs", state: "done", canGoBack: true },
        { step: "filters", state: "current", canGoBack: false },
        { step: "keepsake", state: "upcoming", canGoBack: false },
      ]);
      render(ui(stubClient(), view));
      expect(screen.queryByRole("button", { name: /Template/ })).toBeNull();
      expect(screen.getByRole("button", { name: "02 Photographs, completed" })).toBeVisible();
    });

    it("offers no going back at all on Keepsake, or when nothing can be revisited", () => {
      const keepsake = render(ui(stubClient(), cycle("keepsake")));
      expect(steps()).toEqual(["01 Template, completed", "02 Photographs, completed", "03 Filters, completed", "04 Keepsake"]);
      expect(screen.queryAllByRole("button")).toHaveLength(0);
      keepsake.unmount();

      const view = cycle("photographs", [
        { step: "template", state: "skipped", canGoBack: false },
        { step: "photographs", state: "current", canGoBack: false },
        { step: "filters", state: "skipped", canGoBack: false },
        { step: "keepsake", state: "upcoming", canGoBack: false },
      ]);
      render(ui(stubClient(), view));
      expect(steps()).toEqual(["01 Photographs", "02 Keepsake"]);
      expect(screen.queryAllByRole("button")).toHaveLength(1);
    });
  });

  describe("START OVER", () => {
    it.each(["template", "photographs", "filters"] as const)("is offered on %s and sends StartOver", (step) => {
      const client = stubClient();
      render(ui(client, cycle(step)));
      fireEvent.click(screen.getByRole("button", { name: "START OVER" }));
      expect(sentCommands(client)).toEqual([["StartOver", {}]]);
      expect(screen.queryByRole("alertdialog")).toBeNull();
    });

    it("is not on Keepsake, where the Guest Cycle is already completed", () => {
      render(ui(stubClient(), cycle("keepsake")));
      expect(screen.queryByRole("button", { name: "START OVER" })).toBeNull();
    });

    it("is not offered when the brain says it isn't available", () => {
      render(ui(stubClient(), cycle("photographs", undefined, false)));
      expect(screen.queryByRole("button", { name: "START OVER" })).toBeNull();
    });

    it("opens the confirmation the brain shows, with the plan's words, and holds the step behind it inert", () => {
      const { container } = render(ui(stubClient(), cycle("filters"), { startOverConfirmation: {} }));
      const dialog = screen.getByRole("alertdialog", { name: "Start over?" });
      expect(dialog).toHaveAccessibleDescription("Your photographs stay with the Event, but no strip will be made.");
      expect(within(dialog).getByRole("button", { name: "START OVER" })).toHaveAttribute("data-fotohavn-action", "primary");
      expect(within(dialog).getByRole("button", { name: "KEEP GOING" })).toHaveFocus();
      expect(container.querySelector("[inert]")).toContainElement(screen.getByText("The step"));
      expect(dialog.closest("[inert]")).toBeNull();
    });

    it("sends ConfirmStartOver and KeepGoing, and Escape is KEEP GOING", () => {
      const client = stubClient();
      render(ui(client, cycle("photographs"), { startOverConfirmation: {} }));
      fireEvent.click(screen.getByRole("button", { name: "KEEP GOING" }));
      fireEvent.keyDown(screen.getByRole("alertdialog"), { key: "Escape" });
      fireEvent.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "START OVER" }));
      expect(sentCommands(client).map(([name]) => name)).toEqual(["KeepGoing", "KeepGoing", "ConfirmStartOver"]);
    });

    it("closes with the brain's overlay, as when the timer runs out, and lets the step be used again", () => {
      const client = stubClient();
      const { rerender, container } = render(ui(client, cycle("template"), { startOverConfirmation: {} }));
      expect(screen.getByRole("alertdialog")).toBeVisible();
      rerender(ui(client, cycle("template")));
      expect(screen.queryByRole("alertdialog")).toBeNull();
      expect(container.querySelector("[inert]")).toBeNull();
    });

    it("never holds Keepsake behind a confirmation that has no dialog to show", () => {
      const { container } = render(ui(stubClient(), cycle("keepsake"), { startOverConfirmation: {} }));
      expect(screen.queryByRole("alertdialog")).toBeNull();
      expect(container.querySelector("[inert]")).toBeNull();
    });
  });

  describe("the Phase Timer warning", () => {
    it.each([
      ["template", "template"],
      ["photographsCapture", "photographs-capture"],
      ["photographsReview", "photographs-review"],
      ["filters", "filters"],
      ["guestGallery", "guest-gallery"],
    ] as const)("shows the %s warning with the seconds left", (phase, id) => {
      const { container } = render(ui(stubClient(), cycle("filters"), warning(phase, 18)));
      expect(container.querySelector(`[data-phase="${id}"]`)).not.toBeNull();
      expect(screen.getByText("SECONDS LEFT")).toBeVisible();
      expect(screen.getByText("18")).toBeVisible();
    });

    it("shows nothing when the brain shows none", () => {
      render(ui(stubClient(), cycle("filters")));
      expect(screen.queryByRole("button", { name: "MORE TIME" })).toBeNull();
    });

    it("sends MoreTime, as a secondary action, so the step's own action stays the one primary", () => {
      const client = stubClient();
      render(ui(client, cycle("filters"), warning("filters", 12)));
      const more = screen.getByRole("button", { name: "MORE TIME" });
      expect(more).toHaveAttribute("data-fotohavn-action", "secondary");
      fireEvent.click(more);
      expect(sentCommands(client)).toEqual([["MoreTime", {}]]);
    });

    it("announces once, with the seconds at that moment, however many seconds pass", () => {
      const client = stubClient();
      const { rerender } = render(ui(client, cycle("filters"), warning("filters", 20)));
      const live = screen.getByRole("status");
      expect(live).toHaveTextContent("20 seconds left. Choose MORE TIME to stay a little longer.");
      for (const seconds of [19, 18, 17, 3, 1]) {
        rerender(ui(client, cycle("filters"), warning("filters", seconds)));
      }
      expect(screen.getAllByRole("status")).toEqual([live]);
      expect(live).toHaveTextContent("20 seconds left. Choose MORE TIME to stay a little longer.");
      expect(screen.getByText("SECOND LEFT")).toBeVisible();
    });

    it("announces again for a new warning after the first went away (MORE TIME restarted the phase)", () => {
      const client = stubClient();
      const { rerender } = render(ui(client, cycle("filters"), warning("filters", 6)));
      expect(screen.getByRole("status")).toHaveTextContent("6 seconds left.");
      rerender(ui(client, cycle("filters")));
      expect(screen.queryByRole("status")).toBeNull();
      rerender(ui(client, cycle("filters"), warning("filters", 20)));
      expect(screen.getByRole("status")).toHaveTextContent("20 seconds left.");
    });

    it("never shows on Keepsake, which has no warning and no MORE TIME", () => {
      render(ui(stubClient(), cycle("keepsake"), warning("keepsake", 5)));
      expect(screen.queryByRole("button", { name: "MORE TIME" })).toBeNull();
      cleanup();
      render(ui(stubClient(), cycle("keepsake"), warning("filters", 5)));
      expect(screen.queryByRole("button", { name: "MORE TIME" })).toBeNull();
      expect(screen.queryByRole("button", { name: "START OVER" })).toBeNull();
    });

    it("arrives when it begins during the cycle, and is shown at rest when it was already under way", () => {
      const client = stubClient();
      const { rerender, container } = render(ui(client, cycle("filters")));
      rerender(ui(client, cycle("filters"), warning("filters", 20)));
      expect(container.querySelector("[data-phase]")).toHaveAttribute("data-motion", "full");
      cleanup();

      const atMount = render(ui(client, cycle("filters"), warning("filters", 9)));
      expect(atMount.container.querySelector("[data-phase]")).toHaveAttribute("data-motion", "static");
      atMount.rerender(ui(client, cycle("filters")));
      atMount.rerender(ui(client, cycle("filters"), warning("filters", 20)));
      expect(atMount.container.querySelector("[data-phase]")).toHaveAttribute("data-motion", "full");
    });

    it("arrives without motion under reduced motion", () => {
      stubReducedMotion(true);
      const client = stubClient();
      const { rerender, container } = render(ui(client, cycle("filters")));
      rerender(ui(client, cycle("filters"), warning("filters", 20)));
      expect(container.querySelector("[data-phase]")).toHaveAttribute("data-motion", "reduced");
    });

    it("does not block the step behind it: the content stays available", () => {
      const { container } = render(ui(stubClient(), cycle("filters"), warning("filters", 20)));
      expect(container.querySelector("[inert]")).toBeNull();
    });
  });

  describe("the step crossfade", () => {
    it("shows the first step at once and fades each later step in", () => {
      const client = stubClient();
      const { rerender, container } = render(ui(client, cycle("template")));
      const stage = () => container.querySelector<HTMLElement>("[class*='stage']");
      expect(stage()).not.toHaveAttribute("data-fade");

      rerender(ui(client, cycle("photographs"), {}, "Photographs"));
      expect(stage()).toHaveAttribute("data-fade", "in");
      expect(container.firstElementChild).toHaveAttribute("data-fotohavn-cycle-step", "photographs");

      // A change inside a step (a tab, a selection) is not a step change: the stage is kept.
      const kept = stage();
      rerender(ui(client, cycle("photographs"), {}, "Photographs again"));
      expect(stage()).toBe(kept);
    });

    it("drops the warning with the step it belonged to", () => {
      const client = stubClient();
      const { rerender } = render(ui(client, cycle("filters"), warning("filters", 10)));
      const live = screen.getByRole("status");
      rerender(ui(client, cycle("keepsake")));
      expect(screen.queryByRole("status")).toBeNull();
      expect(live.isConnected).toBe(false);
    });
  });
});
