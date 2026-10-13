import { act, render } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CaptureFlash, flashWindowMs } from "./CaptureFlash.tsx";

// The component's stylesheet as written, read from disk relative to app/ui, where Vitest runs (it turns CSS imports into modules, even with ?raw).
const fsModule = "node:fs";
const { readFileSync } = (await import(/* @vite-ignore */ fsModule)) as {
  readFileSync: (path: string, encoding: "utf8") => string;
};
const css = readFileSync("src/components/CaptureFlash/CaptureFlash.module.css", "utf8");

function flash(container: HTMLElement): Element | null {
  return container.firstElementChild;
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

describe("CaptureFlash", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("is decorative and idle until a Capture arrives, including the key it mounts with", () => {
    const { container } = render(<CaptureFlash flashKey="capture-1" className="extra" />);
    expect(flash(container)).toHaveAttribute("aria-hidden", "true");
    expect(flash(container)).toHaveAttribute("data-state", "idle");
    expect(flash(container)?.className).toContain("extra");
  });

  it("plays one flash per new Capture, for the flash window", () => {
    stubReducedMotion(false);
    const { container, rerender } = render(<CaptureFlash />);
    rerender(<CaptureFlash flashKey="capture-1" />);
    const first = flash(container);
    expect(first).toHaveAttribute("data-state", "flash");
    expect(first).toHaveAttribute("data-motion", "animated");

    advance(flashWindowMs - 1);
    expect(flash(container)).toHaveAttribute("data-state", "flash");
    advance(1);
    expect(flash(container)).toHaveAttribute("data-state", "idle");

    rerender(<CaptureFlash flashKey="capture-2" />);
    expect(flash(container)).toHaveAttribute("data-state", "flash");
    // A new element per flash, so the fade restarts.
    expect(flash(container)).not.toBe(first);
  });

  it("never flashes again while a flash shows, so it can't strobe", () => {
    const { container, rerender } = render(<CaptureFlash flashKey={0} />);
    rerender(<CaptureFlash flashKey={1} />);
    const playing = flash(container);
    advance(100);
    rerender(<CaptureFlash flashKey={2} />);
    rerender(<CaptureFlash flashKey={3} />);
    expect(flash(container)).toBe(playing);
    advance(flashWindowMs);
    expect(flash(container)).toHaveAttribute("data-state", "idle");
    // The skipped keys don't play late.
    advance(1000);
    expect(flash(container)).toHaveAttribute("data-state", "idle");
  });

  it("ignores a cleared key", () => {
    const { container, rerender } = render(<CaptureFlash flashKey={1} />);
    rerender(<CaptureFlash flashKey={null} />);
    expect(flash(container)).toHaveAttribute("data-state", "idle");
  });

  it("is a static change under reduced motion", () => {
    stubReducedMotion(true);
    const { container, rerender } = render(<CaptureFlash />);
    rerender(<CaptureFlash flashKey="capture-1" />);
    expect(flash(container)).toHaveAttribute("data-state", "flash");
    expect(flash(container)).toHaveAttribute("data-motion", "static");
    advance(flashWindowMs);
    expect(flash(container)).toHaveAttribute("data-state", "idle");
  });

  it("holds its peak for fixtures", () => {
    const { container } = render(<CaptureFlash peak />);
    expect(flash(container)).toHaveAttribute("data-state", "peak");
    advance(5000);
    expect(flash(container)).toHaveAttribute("data-state", "peak");
  });

  it("animates opacity once, with the flash tokens and no transform", () => {
    expect(css).not.toMatch(/transform|translate|scale|infinite|alternate/);
    expect(css).toMatch(/animation: capture-flash var\(--motion-duration-flash\) var\(--motion-easing-editorial\) both;/);
  });

  it("stops its timer when unmounted mid-flash", () => {
    const { rerender, unmount } = render(<CaptureFlash flashKey={1} />);
    rerender(<CaptureFlash flashKey={2} />);
    unmount();
    expect(vi.getTimerCount()).toBe(0);
  });
});
