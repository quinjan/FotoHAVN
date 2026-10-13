import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { StartOverAction, type StartOverStep } from "./StartOverAction.tsx";

function trigger(): HTMLElement {
  return screen.getByRole("button", { name: "START OVER" });
}

describe("StartOverAction", () => {
  it("is a quiet text action that announces it opens a dialog", () => {
    render(<StartOverAction step="template" onConfirm={vi.fn()} onKeepGoing={vi.fn()} className="extra" />);
    const action = trigger();
    expect(action).toHaveAttribute("data-fotohavn-action", "text");
    expect(action).toHaveAttribute("aria-haspopup", "dialog");
    expect(action.className).toContain("extra");
    expect(screen.queryByRole("alertdialog")).toBeNull();
  });

  it.each<StartOverStep>(["template", "photographs", "filters"])("appears on the %s step", (step) => {
    render(<StartOverAction step={step} onConfirm={vi.fn()} onKeepGoing={vi.fn()} />);
    expect(trigger()).toBeInTheDocument();
  });

  it("renders nothing on any other step, such as Keepsake", () => {
    const { container } = render(
      <StartOverAction step={"keepsake" as StartOverStep} onConfirm={vi.fn()} onKeepGoing={vi.fn()} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("opens the confirmation itself, and confirming closes it", () => {
    const onStartOver = vi.fn();
    const onConfirm = vi.fn();
    render(<StartOverAction step="photographs" onStartOver={onStartOver} onConfirm={onConfirm} onKeepGoing={vi.fn()} />);
    trigger().focus();
    fireEvent.click(trigger());
    expect(onStartOver).toHaveBeenCalledOnce();

    const dialog = screen.getByRole("alertdialog", { name: "Start over?" });
    expect(dialog).toHaveAccessibleDescription("Your photographs stay with the Event, but no strip will be made.");
    expect(within(dialog).getByRole("button", { name: "KEEP GOING" })).toHaveFocus();
    expect(within(dialog).getByRole("button", { name: "START OVER" })).toHaveAttribute("data-fotohavn-action", "primary");

    fireEvent.click(within(dialog).getByRole("button", { name: "START OVER" }));
    expect(onConfirm).toHaveBeenCalledOnce();
    expect(screen.queryByRole("alertdialog")).toBeNull();
  });

  it("keeps going from the action or Escape, returning focus to START OVER", () => {
    const onKeepGoing = vi.fn();
    render(<StartOverAction step="filters" onConfirm={vi.fn()} onKeepGoing={onKeepGoing} />);
    trigger().focus();
    fireEvent.click(trigger());
    fireEvent.click(screen.getByRole("button", { name: "KEEP GOING" }));
    expect(onKeepGoing).toHaveBeenCalledOnce();
    expect(screen.queryByRole("alertdialog")).toBeNull();
    expect(trigger()).toHaveFocus();

    fireEvent.click(trigger());
    fireEvent.keyDown(screen.getByRole("button", { name: "KEEP GOING" }), { key: "Escape" });
    expect(onKeepGoing).toHaveBeenCalledTimes(2);
    expect(screen.queryByRole("alertdialog")).toBeNull();
    expect(trigger()).toHaveFocus();
  });

  it("follows the brain's confirmation state when controlled", () => {
    const onStartOver = vi.fn();
    const { rerender } = render(
      <StartOverAction step="template" confirming={false} onStartOver={onStartOver} onConfirm={vi.fn()} onKeepGoing={vi.fn()} />,
    );
    trigger().focus();
    fireEvent.click(trigger());
    expect(onStartOver).toHaveBeenCalledOnce();
    expect(screen.queryByRole("alertdialog")).toBeNull();

    rerender(<StartOverAction step="template" confirming onStartOver={onStartOver} onConfirm={vi.fn()} onKeepGoing={vi.fn()} />);
    expect(screen.getByRole("alertdialog", { name: "Start over?" })).toBeInTheDocument();

    // The Phase Timer ran out: the brain closes the confirmation as KEEP GOING.
    rerender(
      <StartOverAction step="template" confirming={false} onStartOver={onStartOver} onConfirm={vi.fn()} onKeepGoing={vi.fn()} />,
    );
    expect(screen.queryByRole("alertdialog")).toBeNull();
    expect(trigger()).toHaveFocus();
  });
});
