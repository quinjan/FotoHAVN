import { cleanup, render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { StepProgress, guestSteps } from "./StepProgress.tsx";

function items(): HTMLElement[] {
  return within(screen.getByRole("list", { name: "Steps" })).getAllByRole("listitem");
}

describe("StepProgress", () => {
  it("lists the four numbered steps in order, as the copy table writes them", () => {
    render(<StepProgress current="template" />);
    expect(items().map((item) => item.textContent)).toEqual(["01 Template", "02 Photographs", "03 Filters", "04 Keepsake"]);
    expect(guestSteps.map((step) => `${step.number} ${step.label}`)).toEqual([
      "01 Template",
      "02 Photographs",
      "03 Filters",
      "04 Keepsake",
    ]);
  });

  it("marks only the current step with aria-current=step", () => {
    render(<StepProgress current="photographs" />);
    const current = screen.getByText("Photographs").parentElement;
    expect(current).toHaveAttribute("aria-current", "step");
    expect(current).toHaveAttribute("data-state", "current");
    expect(document.querySelectorAll("[aria-current]")).toHaveLength(1);
  });

  it("shows completed, current, and upcoming states, with completed said in words", () => {
    render(<StepProgress current="filters" />);
    const states = items().map((item) => item.firstElementChild?.getAttribute("data-state"));
    expect(states).toEqual(["completed", "completed", "current", "upcoming"]);
    const [template, , , keepsake] = items();
    expect(template).toHaveTextContent("01 Template, completed");
    expect(keepsake).not.toHaveTextContent("completed");
  });

  it("is never a control: no step is a button, a link, or a tab stop, so a guest can't go back by it", () => {
    render(<StepProgress current="keepsake" />);
    expect(screen.queryAllByRole("button")).toHaveLength(0);
    expect(screen.queryAllByRole("link")).toHaveLength(0);
    expect(document.querySelectorAll("[tabindex], button, a")).toHaveLength(0);
    expect(items().map((item) => item.firstElementChild?.getAttribute("data-state"))).toEqual([
      "completed",
      "completed",
      "completed",
      "current",
    ]);
  });

  it("leaves out the steps it isn't given and renumbers the rest from 01", () => {
    render(<StepProgress current="photographs" steps={["photographs", "keepsake"]} />);
    expect(items().map((item) => item.textContent)).toEqual(["01 Photographs", "02 Keepsake"]);
    expect(items()[0]?.firstElementChild).toHaveAttribute("aria-current", "step");

    cleanup();
    render(<StepProgress current="keepsake" steps={["template", "photographs", "keepsake"]} />);
    expect(items().map((item) => item.textContent)).toEqual(["01 Template, completed", "02 Photographs, completed", "03 Keepsake"]);
  });

  it("takes an accessible name and a layout class", () => {
    render(<StepProgress current="template" label="Your steps" className="extra" />);
    const list = screen.getByRole("list", { name: "Your steps" });
    expect(list.className).toContain("extra");
  });
});