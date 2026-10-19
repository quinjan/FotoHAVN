import { cleanup, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient } from "../brain/types.ts";
import type { GuestCycleStepEntry, GuestCycleView, GuestOverlays, PhaseTimer } from "../protocol/generated.ts";
import { sentCommands, stubClient } from "../test/operatorHarness.tsx";
import { GuestCycleChrome } from "./GuestCycleChrome.tsx";

type StepName = GuestCycleStepEntry["step"];

const noOverlays: GuestOverlays = { exitHold: null, pinDialog: null, phaseTimer: null, notice: null };

function cycle(current: StepName, entries?: readonly GuestCycleStepEntry[]): GuestCycleView {
  const order: readonly StepName[] = ["template", "photographs", "filters", "keepsake"];
  const at = order.indexOf(current);
  const steps =
    entries ??
    order.map((step, index): GuestCycleStepEntry => ({ step, state: index < at ? "done" : index === at ? "current" : "upcoming" }));
  return { id: "cycle-1", step: current, steps: [...steps] };
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

function timer(phase: PhaseTimer, remainingSeconds: number, paused = false): Partial<GuestOverlays> {
  return { phaseTimer: { phase, remainingSeconds, paused } };
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
  });

  it("offers no START OVER, no MORE TIME, and no confirmation on any step", () => {
    for (const step of ["template", "photographs", "filters", "keepsake"] as const) {
      const { container, unmount } = render(ui(stubClient(), cycle(step), timer("filters", 5)));
      expect(screen.queryAllByRole("button"), step).toHaveLength(0);
      expect(screen.queryByRole("alertdialog"), step).toBeNull();
      expect(container.querySelector("[inert]"), step).toBeNull();
      expect(container.querySelector("footer"), step).toBeNull();
      unmount();
    }
  });

  describe("step progress", () => {
    it("lists the four steps with the current one marked", () => {
      render(ui(stubClient(), cycle("photographs")));
      expect(steps()).toEqual(["01 Template, completed", "02 Photographs", "03 Filters", "04 Keepsake"]);
      expect(screen.getByText("Photographs").parentElement).toHaveAttribute("aria-current", "step");
    });

    it("Q26: leaves out skipped steps and renumbers the rest", () => {
      const view = cycle("filters", [
        { step: "template", state: "skipped" },
        { step: "photographs", state: "done" },
        { step: "filters", state: "current" },
        { step: "keepsake", state: "upcoming" },
      ]);
      const { rerender } = render(ui(stubClient(), view));
      expect(steps()).toEqual(["01 Photographs, completed", "02 Filters", "03 Keepsake"]);

      rerender(
        ui(
          stubClient(),
          cycle("keepsake", [
            { step: "template", state: "skipped" },
            { step: "photographs", state: "done" },
            { step: "filters", state: "skipped" },
            { step: "keepsake", state: "current" },
          ]),
        ),
      );
      expect(steps()).toEqual(["01 Photographs, completed", "02 Keepsake"]);
    });

    it("never goes back: completed steps are not controls and send nothing", () => {
      const client = stubClient();
      render(ui(client, cycle("filters")));
      const list = screen.getByRole("list", { name: "Steps" });
      expect(within(list).queryAllByRole("button")).toHaveLength(0);
      expect(list.querySelector("[tabindex], button, a")).toBeNull();
      expect(sentCommands(client)).toEqual([]);
    });
  });

  describe("the Phase Timer", () => {
    it.each(["template", "photographsCapture", "photographsReview", "filters", "keepsake"] as const)(
      "shows the %s timer in the top band, as m:ss",
      (phase) => {
        render(ui(stubClient(), cycle("filters"), timer(phase, 45)));
        const pill = screen.getByRole("timer");
        expect(pill).toHaveTextContent("0:45");
        expect(pill.closest("header")).not.toBeNull();
      },
    );

    it("shows on Keepsake too, where its time is all there is", () => {
      render(ui(stubClient(), cycle("keepsake"), timer("keepsake", 12)));
      expect(screen.getByRole("timer")).toHaveTextContent("0:12");
    });

    it("shows nothing when the brain sends no timer (the operator turned it off)", () => {
      render(ui(stubClient(), cycle("filters")));
      expect(screen.queryByRole("timer")).toBeNull();
    });

    it("ticks without announcing, and says the last seconds once", () => {
      const client = stubClient();
      const { rerender } = render(ui(client, cycle("filters"), timer("filters", 14)));
      const live = screen.getByRole("status");
      expect(live).toBeEmptyDOMElement();
      for (const seconds of [13, 12, 11]) {
        rerender(ui(client, cycle("filters"), timer("filters", seconds)));
      }
      expect(live).toBeEmptyDOMElement();
      rerender(ui(client, cycle("filters"), timer("filters", 10)));
      expect(live).toHaveTextContent("10 seconds left.");
      for (const seconds of [9, 5, 1]) {
        rerender(ui(client, cycle("filters"), timer("filters", seconds)));
      }
      expect(screen.getAllByRole("status")).toEqual([live]);
      expect(live).toHaveTextContent("10 seconds left.");
      expect(screen.getByRole("timer")).toHaveAttribute("data-low", "true");
    });

    it("starts quiet again in a new phase", () => {
      const client = stubClient();
      const { rerender } = render(ui(client, cycle("photographs"), timer("photographsCapture", 6)));
      expect(screen.getByRole("status")).toHaveTextContent("6 seconds left.");
      rerender(ui(client, cycle("photographs"), timer("photographsReview", 60)));
      expect(screen.getByRole("status")).toBeEmptyDOMElement();
      expect(screen.getByRole("timer")).toHaveTextContent("1:00");
    });

    it("stands still while the brain pauses it", () => {
      render(ui(stubClient(), cycle("photographs"), timer("photographsCapture", 32, true)));
      expect(screen.getByRole("timer")).toHaveAttribute("data-paused", "true");
      expect(screen.getByRole("timer")).toHaveAccessibleName("Time left: 32 seconds, paused");
    });

    it("does not block the step: nothing behind it is inert", () => {
      const { container } = render(ui(stubClient(), cycle("filters"), timer("filters", 3)));
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
  });
});
