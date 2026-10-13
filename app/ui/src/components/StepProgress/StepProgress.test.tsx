import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
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

  it("marks only the current step with aria-current=step, and never as a button", () => {
    render(<StepProgress current="photographs" onSelectStep={vi.fn()} />);
    const current = screen.getByText("Photographs").parentElement;
    expect(current).toHaveAttribute("aria-current", "step");
    expect(current).toHaveAttribute("data-state", "current");
    expect(current?.tagName).toBe("SPAN");
    expect(document.querySelectorAll("[aria-current]")).toHaveLength(1);
  });

  it("shows completed, current, and upcoming states", () => {
    render(<StepProgress current="filters" />);
    const states = items().map((item) => item.firstElementChild?.getAttribute("data-state"));
    expect(states).toEqual(["completed", "completed", "current", "upcoming"]);
    const [template, , , keepsake] = items();
    expect(template).toHaveTextContent("01 Template, completed");
    expect(keepsake).not.toHaveTextContent("completed");
  });

  it("offers completed steps as buttons that go back, and nothing else", () => {
    const onSelectStep = vi.fn();
    render(<StepProgress current="filters" onSelectStep={onSelectStep} />);
    const buttons = screen.getAllByRole("button");
    expect(buttons.map((button) => button.textContent)).toEqual(["01 Template, completed", "02 Photographs, completed"]);
    for (const button of buttons) {
      expect(button).toHaveAttribute("type", "button");
      expect(button).toHaveAttribute("data-revisit", "true");
    }

    fireEvent.click(screen.getByRole("button", { name: "02 Photographs, completed" }));
    expect(onSelectStep).toHaveBeenCalledExactlyOnceWith("photographs");
    fireEvent.click(screen.getByRole("button", { name: /Template/ }));
    expect(onSelectStep).toHaveBeenLastCalledWith("template");
  });

  it("is keyboard reachable only on the steps the guest can return to", () => {
    render(<StepProgress current="filters" onSelectStep={vi.fn()} />);
    const [template, photographs] = screen.getAllByRole("button");
    template?.focus();
    expect(template).toHaveFocus();
    photographs?.focus();
    expect(photographs).toHaveFocus();
    expect(screen.getByText("Filters").closest("[tabindex], button")).toBeNull();
    expect(screen.getByText("Keepsake").closest("[tabindex], button")).toBeNull();
  });

  it("offers no going back without a callback, as on Keepsake", () => {
    render(<StepProgress current="keepsake" />);
    expect(screen.queryAllByRole("button")).toHaveLength(0);
    expect(items().map((item) => item.firstElementChild?.getAttribute("data-state"))).toEqual([
      "completed",
      "completed",
      "completed",
      "current",
    ]);
  });

  it("doesn't offer a step the Event skipped", () => {
    render(<StepProgress current="filters" onSelectStep={vi.fn()} skipped={["template"]} />);
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["02 Photographs, completed"]);
    expect(items()[0]?.firstElementChild).toHaveAttribute("data-state", "completed");
  });

  it("takes an accessible name and a layout class", () => {
    render(<StepProgress current="template" label="Your steps" className="extra" />);
    const list = screen.getByRole("list", { name: "Your steps" });
    expect(list.className).toContain("extra");
  });
});
