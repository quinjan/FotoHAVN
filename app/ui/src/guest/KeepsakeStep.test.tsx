import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import type { GuestCycleView, KeepsakeView } from "../protocol/generated.ts";
import { KeepsakeStep } from "./KeepsakeStep.tsx";

const cycle: GuestCycleView = {
  id: "cycle-1",
  step: "keepsake",
  steps: [
    { step: "template", state: "done" },
    { step: "photographs", state: "done" },
    { step: "filters", state: "done" },
    { step: "keepsake", state: "current" },
  ],
};

const portrait: KeepsakeView = {
  stripUrl: "https://media.fotohavn/events/event-1/guest-cycles/cycle-1/photo-strip.png?v=0a1b2c3d",
  width: 600,
  height: 1800,
  landscape: false,
};

describe("KeepsakeStep", () => {
  afterEach(cleanup);

  it("shows the statement and the finished strip, lifted, at its native proportion", () => {
    render(<KeepsakeStep cycle={cycle} view={portrait} />);
    expect(screen.getByRole("heading", { level: 1, name: "A little moment. Yours." })).toBeVisible();
    expect(screen.getByText("Keep the moment close.")).toBeVisible();
    const strip = screen.getByRole("img", { name: "Your Photo Strip" });
    expect(strip).toHaveAttribute("src", portrait.stripUrl);
    expect(strip).toHaveAttribute("width", "600");
    expect(strip).toHaveAttribute("height", "1800");
    expect(strip.closest("[data-shadow]")).toHaveAttribute("data-shadow", "lifted");
    expect(strip.closest("[data-rotated]")).toBeNull();
  });

  it("sets the strip's footprint from its proportion, so the stage can give it the room", () => {
    const { container } = render(<KeepsakeStep cycle={cycle} view={portrait} />);
    const box = container.querySelector<HTMLElement>("[style*='--strip-ratio']");
    expect(Number(box?.style.getPropertyValue("--strip-ratio"))).toBeCloseTo(1 / 3);
  });

  it("turns Panorama Four a quarter turn, and its footprint turns with it", () => {
    const { container } = render(<KeepsakeStep cycle={cycle} view={{ ...portrait, width: 1800, height: 600, landscape: true }} />);
    expect(screen.getByRole("img", { name: "Your Photo Strip" }).closest("[data-rotated]")).not.toBeNull();
    expect(container.firstElementChild).toHaveAttribute("data-strip-landscape", "true");
    const box = container.querySelector<HTMLElement>("[style*='--strip-ratio']");
    expect(Number(box?.style.getPropertyValue("--strip-ratio"))).toBeCloseTo(1 / 3);
  });

  it("is a finished moment: no action, and no START OVER or MORE TIME", () => {
    render(<KeepsakeStep cycle={cycle} view={portrait} />);
    expect(screen.queryAllByRole("button")).toHaveLength(0);
    expect(screen.queryByText("START OVER")).toBeNull();
    expect(screen.queryByText("MORE TIME")).toBeNull();
    expect(document.querySelector("[data-fotohavn-action]")).toBeNull();
  });
});
