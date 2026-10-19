import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { PinPad } from "./PinPad.tsx";

// The stylesheet as written, read from disk relative to app/ui, where Vitest runs (it turns CSS imports into modules).
const fsModule = "node:fs";
const { readFileSync } = (await import(/* @vite-ignore */ fsModule)) as {
  readFileSync: (path: string, encoding: "utf8") => string;
};
const padCss = readFileSync("src/components/PinPad/PinPad.module.css", "utf8");

function key(name: string): HTMLElement {
  return screen.getByRole("button", { name });
}

function press(...digits: string[]): void {
  for (const digit of digits) {
    fireEvent.click(key(digit));
  }
}

function filledDots(container: HTMLElement): number {
  return container.querySelectorAll("[data-filled]").length;
}

describe("PinPad", () => {
  it("offers ten digit keys, a backspace, and the submit action, with no text field", () => {
    const { container } = render(<PinPad onSubmit={vi.fn()} className="extra" />);
    const keys = within(screen.getByRole("group", { name: "PIN digits" })).getAllByRole("button");
    expect(keys.map((button) => button.getAttribute("aria-label") ?? button.textContent)).toEqual([
      "1",
      "2",
      "3",
      "4",
      "5",
      "6",
      "7",
      "8",
      "9",
      "0",
      "Delete last digit",
    ]);
    expect(container.querySelector("input")).toBeNull();
    expect(container.firstElementChild?.className).toContain("extra");
    expect(key("END EVENT")).toHaveAttribute("data-fotohavn-action", "primary");
    expect(key("END EVENT")).toHaveAttribute("aria-disabled", "true");
    expect(key("END EVENT")).toHaveAccessibleDescription("4 to 6 digits.");
    expect(key("Delete last digit")).toHaveAttribute("aria-disabled", "true");
    expect(screen.queryByRole("button", { name: "KEEP RUNNING" })).toBeNull();
    expect(screen.getByText("No digits entered.")).toBeInTheDocument();
  });

  it("masks the entry as dots and never shows the digits", () => {
    const { container } = render(<PinPad onSubmit={vi.fn()} />);
    press("7", "3", "1");
    const dots = container.querySelector("[aria-hidden='true']");
    expect(dots?.children).toHaveLength(6);
    expect(dots?.textContent).toBe("");
    expect(filledDots(container)).toBe(3);
    expect(screen.getByText("3 digits entered.")).toBeInTheDocument();
    expect(container.textContent).not.toContain("731");
  });

  it("submits at 4 to 6 digits, stops at 6, and clears after submitting", () => {
    const onSubmit = vi.fn();
    const { container } = render(<PinPad onSubmit={onSubmit} />);
    press("1", "2", "3");
    fireEvent.click(key("END EVENT"));
    expect(onSubmit).not.toHaveBeenCalled();

    press("4");
    expect(key("END EVENT")).toHaveAttribute("aria-disabled", "false");
    press("5", "6");
    expect(key("7")).toHaveAttribute("aria-disabled", "true");
    press("7");
    expect(filledDots(container)).toBe(6);

    fireEvent.click(key("END EVENT"));
    expect(onSubmit).toHaveBeenCalledExactlyOnceWith("123456");
    expect(filledDots(container)).toBe(0);
    expect(key("END EVENT")).toHaveAttribute("aria-disabled", "true");
  });

  it("deletes the last digit", () => {
    const onSubmit = vi.fn();
    const { container } = render(<PinPad onSubmit={onSubmit} />);
    press("9", "8", "7", "6", "5");
    fireEvent.click(key("Delete last digit"));
    expect(filledDots(container)).toBe(4);
    fireEvent.click(key("END EVENT"));
    expect(onSubmit).toHaveBeenCalledWith("9876");
  });

  it("takes typed digits and Backspace while focus is inside, and ignores other keys", () => {
    const onSubmit = vi.fn();
    const { container } = render(<PinPad onSubmit={onSubmit} />);
    const one = key("1");
    one.focus();
    expect(fireEvent.keyDown(one, { key: "2" })).toBe(false);
    fireEvent.keyDown(one, { key: "0" });
    fireEvent.keyDown(one, { key: "2" });
    fireEvent.keyDown(one, { key: "5" });
    fireEvent.keyDown(one, { key: "9" });
    expect(fireEvent.keyDown(one, { key: "a" })).toBe(true);
    expect(filledDots(container)).toBe(5);
    expect(fireEvent.keyDown(one, { key: "Backspace" })).toBe(false);
    expect(filledDots(container)).toBe(4);
    fireEvent.click(key("END EVENT"));
    expect(onSubmit).toHaveBeenCalledWith("2025");

    for (const digit of "1234567") {
      fireEvent.keyDown(one, { key: digit });
    }
    expect(filledDots(container)).toBe(6);
  });

  it("places the submit action under the keys and the cancel action opposite it, in that Tab order", () => {
    const { container } = render(<PinPad onSubmit={vi.fn()} onCancel={vi.fn()} />);
    const keys = screen.getByRole("group", { name: "PIN digits" });
    const submit = key("END EVENT");
    const cancel = key("KEEP RUNNING");
    expect(keys.compareDocumentPosition(submit) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(submit.compareDocumentPosition(cancel) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(container.firstElementChild).toContainElement(submit);
    expect(padCss).toMatch(/"status keys"\s+"cancel submit"/);
    expect(padCss).toMatch(/\.submit \{\s*grid-area: submit;/);
    expect(padCss).toMatch(/\.cancel \{\s*grid-area: cancel;/);
  });

  it("says a wrong PIN in text, and announces each new wrong attempt", () => {
    const { rerender } = render(<PinPad onSubmit={vi.fn()} wrongAttempts={1} />);
    const first = screen.getByRole("alert");
    expect(first).toHaveTextContent("That PIN isn't right. Try again.");
    expect(screen.queryByText("4 to 6 digits.")).toBeInTheDocument();

    rerender(<PinPad onSubmit={vi.fn()} wrongAttempts={2} />);
    const second = screen.getByRole("alert");
    expect(second).toHaveTextContent("That PIN isn't right. Try again.");
    expect(second).not.toBe(first);
  });

  it("says how many tries are left after a wrong PIN, and only then", () => {
    const { rerender } = render(<PinPad onSubmit={vi.fn()} attemptsRemaining={5} />);
    expect(screen.queryByText(/tries? left/)).toBeNull();

    rerender(<PinPad onSubmit={vi.fn()} wrongAttempts={1} attemptsRemaining={4} />);
    expect(screen.getByText("4 tries left.")).toBeInTheDocument();

    rerender(<PinPad onSubmit={vi.fn()} wrongAttempts={4} attemptsRemaining={1} />);
    expect(screen.getByText("1 try left.")).toBeInTheDocument();

    rerender(<PinPad onSubmit={vi.fn()} wrongAttempts={2} />);
    expect(screen.queryByText(/tries? left/)).toBeNull();

    rerender(<PinPad onSubmit={vi.fn()} wrongAttempts={5} lockedSeconds={30} attemptsRemaining={0} />);
    expect(screen.queryByText(/tries? left/)).toBeNull();
  });

  it("locks with a visible countdown, ignores every key, and recovers", () => {
    const onSubmit = vi.fn();
    const { container, rerender } = render(<PinPad onSubmit={onSubmit} defaultValue="12" lockedSeconds={30} wrongAttempts={5} />);
    expect(container.firstElementChild).toHaveAttribute("data-locked", "true");
    expect(screen.getByRole("alert")).toHaveTextContent("Too many tries. The pad is locked for now.");
    expect(screen.getByText("Try again in 30 seconds.")).toBeInTheDocument();
    expect(screen.queryByText("That PIN isn't right. Try again.")).toBeNull();
    expect(filledDots(container)).toBe(0);
    expect(key("5")).toHaveAttribute("aria-disabled", "true");
    expect(key("END EVENT")).toHaveAttribute("aria-disabled", "true");
    expect(key("Delete last digit")).toHaveAttribute("aria-disabled", "true");

    press("1", "2", "3", "4");
    fireEvent.keyDown(key("1"), { key: "4" });
    fireEvent.click(key("Delete last digit"));
    fireEvent.keyDown(key("1"), { key: "Backspace" });
    fireEvent.click(key("END EVENT"));
    expect(onSubmit).not.toHaveBeenCalled();

    rerender(<PinPad onSubmit={onSubmit} lockedSeconds={1} wrongAttempts={5} />);
    expect(screen.getByText("Try again in 1 second.")).toBeInTheDocument();

    rerender(<PinPad onSubmit={onSubmit} lockedSeconds={0} wrongAttempts={0} />);
    expect(container.firstElementChild).not.toHaveAttribute("data-locked");
    expect(screen.queryByRole("alert")).toBeNull();
    // The entry from before the lockout comes back; the lockout only hid it.
    expect(filledDots(container)).toBe(2);
    press("3", "4");
    fireEvent.click(key("END EVENT"));
    expect(onSubmit).toHaveBeenCalledWith("1234");
  });

  it("renders the cancel action, custom labels and lengths, and a cleaned default entry", () => {
    const onCancel = vi.fn();
    const { container } = render(
      <PinPad
        onSubmit={vi.fn()}
        onCancel={onCancel}
        minLength={5}
        maxLength={8}
        defaultValue="1a2b3c4d5e6f7g8h9"
        submitLabel="UNLOCK"
        cancelLabel="CANCEL"
      />,
    );
    expect(filledDots(container)).toBe(8);
    expect(screen.getByText("8 digits entered.")).toBeInTheDocument();
    for (let deleted = 0; deleted < 7; deleted += 1) {
      fireEvent.click(key("Delete last digit"));
    }
    expect(screen.getByText("1 digit entered.")).toBeInTheDocument();
    fireEvent.click(key("CANCEL"));
    expect(onCancel).toHaveBeenCalledOnce();
    expect(key("UNLOCK")).toHaveAccessibleDescription("5 to 8 digits.");
  });
});
