import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HoldControl, HoldControlView } from "./HoldControl.tsx";

function control(): HTMLElement {
  return screen.getByRole("button", { name: "Operator exit" });
}

function status(): HTMLElement {
  return screen.getByRole("status");
}

function progressText(): string {
  return control().parentElement?.querySelector("[id]:not([role])")?.textContent ?? "";
}

function advance(ms: number): void {
  act(() => {
    vi.advanceTimersByTime(ms);
  });
}

function stubReducedMotion(reduce: boolean): void {
  vi.stubGlobal(
    "matchMedia",
    vi.fn((query: string) => ({
      matches: reduce && query === "(prefers-reduced-motion: reduce)",
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })),
  );
}

describe("HoldControl", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("is a button named Operator exit, described by its hint, and idle with no visible text", () => {
    render(<HoldControl onComplete={vi.fn()} className="extra" />);
    const button = control();
    expect(button).toHaveAttribute("type", "button");
    expect(button).toHaveAccessibleDescription("Press and hold for 3 seconds.");
    expect(button.parentElement).toHaveAttribute("data-phase", "idle");
    expect(button.parentElement?.className).toContain("extra");
    expect(status()).toHaveTextContent("");
    expect(progressText()).toBe("");
  });

  it("completes once after a 3-second pointer hold, with text progress", () => {
    const onComplete = vi.fn();
    const onHoldStart = vi.fn();
    const onHoldCancel = vi.fn();
    render(<HoldControl onComplete={onComplete} onHoldStart={onHoldStart} onHoldCancel={onHoldCancel} />);
    fireEvent.pointerDown(control(), { button: 0, pointerId: 1 });
    expect(onHoldStart).toHaveBeenCalledOnce();
    expect(control().parentElement).toHaveAttribute("data-phase", "holding");
    expect(status()).toHaveTextContent("Keep holding for 3 seconds.");
    expect(progressText()).toBe("Keep holding: 3 seconds");

    advance(1000);
    expect(progressText()).toBe("Keep holding: 2 seconds");
    advance(1000);
    expect(progressText()).toBe("Keep holding: 1 second");
    expect(onComplete).not.toHaveBeenCalled();

    advance(1000);
    expect(onComplete).toHaveBeenCalledOnce();
    expect(control().parentElement).toHaveAttribute("data-phase", "complete");
    expect(progressText()).toBe("");

    // Still held: nothing fires again.
    advance(10_000);
    expect(onComplete).toHaveBeenCalledOnce();

    fireEvent.pointerUp(control());
    expect(control().parentElement).toHaveAttribute("data-phase", "idle");
    expect(onHoldCancel).not.toHaveBeenCalled();
    expect(status()).toHaveTextContent("Keep holding for 3 seconds.");
  });

  it("cancels when released early, and a new hold starts from zero", () => {
    const onComplete = vi.fn();
    const onHoldCancel = vi.fn();
    render(<HoldControl onComplete={onComplete} onHoldCancel={onHoldCancel} />);
    fireEvent.pointerDown(control(), { button: 0, pointerId: 1 });
    advance(2900);
    fireEvent.pointerUp(control());
    expect(onHoldCancel).toHaveBeenCalledOnce();
    expect(status()).toHaveTextContent("Released. Hold for 3 seconds to exit.");
    expect(control().parentElement).toHaveAttribute("data-phase", "idle");
    advance(5000);
    expect(onComplete).not.toHaveBeenCalled();

    fireEvent.pointerDown(control(), { button: 0, pointerId: 1 });
    advance(2900);
    expect(onComplete).not.toHaveBeenCalled();
    advance(100);
    expect(onComplete).toHaveBeenCalledOnce();
  });

  it("captures the pointer, so sliding off the control doesn't cancel, and a second press doesn't restart", () => {
    const onHoldStart = vi.fn();
    const onComplete = vi.fn();
    render(<HoldControl onComplete={onComplete} onHoldStart={onHoldStart} />);
    const setPointerCapture = vi.fn();
    Object.defineProperty(control(), "setPointerCapture", { value: setPointerCapture });
    fireEvent.pointerDown(control(), { button: 0, pointerId: 7 });
    expect(setPointerCapture).toHaveBeenCalledWith(7);
    advance(2000);
    fireEvent.pointerDown(control(), { button: 0, pointerId: 8 });
    advance(1000);
    expect(onHoldStart).toHaveBeenCalledOnce();
    expect(onComplete).toHaveBeenCalledOnce();
  });

  it("cancels on pointer cancel and lost capture, and ignores secondary buttons and the context menu", () => {
    const onHoldStart = vi.fn();
    const onHoldCancel = vi.fn();
    render(<HoldControl onComplete={vi.fn()} onHoldStart={onHoldStart} onHoldCancel={onHoldCancel} />);
    fireEvent.pointerDown(control(), { button: 2, pointerId: 1 });
    expect(onHoldStart).not.toHaveBeenCalled();
    expect(fireEvent.contextMenu(control())).toBe(false);

    fireEvent.pointerDown(control(), { button: 0, pointerId: 1 });
    fireEvent.pointerCancel(control());
    expect(onHoldCancel).toHaveBeenCalledOnce();

    fireEvent.pointerDown(control(), { button: 0, pointerId: 1 });
    fireEvent.lostPointerCapture(control());
    expect(onHoldCancel).toHaveBeenCalledTimes(2);
    expect(onHoldStart).toHaveBeenCalledTimes(2);
  });

  it("completes when Enter is held for 3 seconds, ignoring key repeat", () => {
    const onComplete = vi.fn();
    const onHoldStart = vi.fn();
    render(<HoldControl onComplete={onComplete} onHoldStart={onHoldStart} />);
    const button = control();
    expect(fireEvent.keyDown(button, { key: "Enter" })).toBe(false);
    advance(1000);
    fireEvent.keyDown(button, { key: "Enter", repeat: true });
    fireEvent.keyUp(button, { key: "Tab" });
    advance(2000);
    expect(onHoldStart).toHaveBeenCalledOnce();
    expect(onComplete).toHaveBeenCalledOnce();
    expect(fireEvent.keyUp(button, { key: "Enter" })).toBe(false);
    expect(button.parentElement).toHaveAttribute("data-phase", "idle");
  });

  it("cancels when Space is released early, and ignores other keys", () => {
    const onComplete = vi.fn();
    const onHoldStart = vi.fn();
    const onHoldCancel = vi.fn();
    render(<HoldControl onComplete={onComplete} onHoldStart={onHoldStart} onHoldCancel={onHoldCancel} />);
    const button = control();
    expect(fireEvent.keyDown(button, { key: "a" })).toBe(true);
    expect(onHoldStart).not.toHaveBeenCalled();

    fireEvent.keyDown(button, { key: " " });
    // A second key while one is held doesn't take over the hold.
    fireEvent.keyDown(button, { key: "Enter" });
    advance(1500);
    fireEvent.keyUp(button, { key: "Enter" });
    expect(onHoldCancel).not.toHaveBeenCalled();
    fireEvent.keyUp(button, { key: " " });
    expect(onHoldCancel).toHaveBeenCalledOnce();
    advance(3000);
    expect(onComplete).not.toHaveBeenCalled();
  });

  it("cancels when focus leaves during a hold", () => {
    const onHoldCancel = vi.fn();
    render(<HoldControl onComplete={vi.fn()} onHoldCancel={onHoldCancel} />);
    const button = control();
    button.focus();
    fireEvent.keyDown(button, { key: "Enter" });
    fireEvent.blur(button);
    expect(onHoldCancel).toHaveBeenCalledOnce();
    fireEvent.blur(button);
    expect(onHoldCancel).toHaveBeenCalledOnce();
  });

  it("calls the latest onComplete, and never fires after unmounting", () => {
    const first = vi.fn();
    const latest = vi.fn();
    const { rerender, unmount } = render(<HoldControl onComplete={first} />);
    fireEvent.pointerDown(control(), { button: 0, pointerId: 1 });
    rerender(<HoldControl onComplete={latest} />);
    advance(3000);
    expect(first).not.toHaveBeenCalled();
    expect(latest).toHaveBeenCalledOnce();

    fireEvent.pointerUp(control());
    fireEvent.pointerDown(control(), { button: 0, pointerId: 1 });
    unmount();
    advance(5000);
    expect(latest).toHaveBeenCalledOnce();
  });

  it("draws the progress ring with full motion", () => {
    stubReducedMotion(false);
    render(<HoldControl onComplete={vi.fn()} />);
    fireEvent.pointerDown(control(), { button: 0, pointerId: 1 });
    advance(1500);
    const svg = control().querySelector("svg");
    expect(svg).toHaveAttribute("data-motion", "full");
    expect(svg?.querySelectorAll("circle")).toHaveLength(2);
    expect(svg?.querySelector("circle[style]")?.getAttribute("style")).toContain("--hold-progress: 0.5");
  });

  it("shows progress as text only under reduced motion", () => {
    stubReducedMotion(true);
    render(<HoldControl onComplete={vi.fn()} />);
    fireEvent.pointerDown(control(), { button: 0, pointerId: 1 });
    advance(1500);
    const svg = control().querySelector("svg");
    expect(svg).toHaveAttribute("data-motion", "reduced");
    expect(svg?.querySelectorAll("circle")).toHaveLength(1);
    expect(progressText()).toBe("Keep holding: 2 seconds");
  });
});

describe("HoldControlView", () => {
  it("renders a hold in progress without timers", () => {
    render(<HoldControlView phase="holding" progress={0.4} secondsLeft={2} />);
    expect(progressText()).toBe("Keep holding: 2 seconds");
    expect(status()).toHaveTextContent("");
  });
});
