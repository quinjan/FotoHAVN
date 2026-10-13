import { act, fireEvent, render, screen } from "@testing-library/react";
import { StrictMode, useEffect, useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  PhaseTimerWarning,
  warningAnnouncement,
  wholeSeconds,
  type PhaseTimerWarningPhase,
} from "./PhaseTimerWarning.tsx";

/** A stand-in for the brain: counts the Phase Timer down once a second while the warning shows. */
function CountingDown({ from, onMoreTime = vi.fn() }: { readonly from: number; readonly onMoreTime?: () => void }) {
  const [seconds, setSeconds] = useState(from);
  useEffect(() => {
    const timer = setInterval(() => {
      setSeconds((value) => Math.max(0, value - 1));
    }, 1000);
    return () => {
      clearInterval(timer);
    };
  }, []);
  return <PhaseTimerWarning phase="template" secondsRemaining={seconds} onMoreTime={onMoreTime} />;
}

interface FakeMediaQueryList {
  matches: boolean;
  readonly listeners: Set<() => void>;
  addEventListener(type: string, listener: () => void): void;
  removeEventListener(type: string, listener: () => void): void;
}

function stubReducedMotion(matches: boolean): FakeMediaQueryList {
  const list: FakeMediaQueryList = {
    matches,
    listeners: new Set(),
    addEventListener(_type, listener) {
      this.listeners.add(listener);
    },
    removeEventListener(_type, listener) {
      this.listeners.delete(listener);
    },
  };
  vi.stubGlobal(
    "matchMedia",
    vi.fn((query: string) => {
      expect(query).toBe("(prefers-reduced-motion: reduce)");
      return list;
    }),
  );
  return list;
}

function warning(): HTMLElement {
  const element = screen.getByRole("button", { name: "MORE TIME" }).parentElement;
  if (element === null) {
    throw new Error("no warning");
  }
  return element;
}

describe("PhaseTimerWarning", () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("shows the remaining seconds with MORE TIME as a secondary action described by the count", () => {
    const onMoreTime = vi.fn();
    render(<PhaseTimerWarning phase="filters" secondsRemaining={18} onMoreTime={onMoreTime} className="extra" />);
    const button = screen.getByRole("button", { name: "MORE TIME" });
    expect(button).toHaveAttribute("data-fotohavn-action", "secondary");
    expect(button).toHaveAccessibleDescription("18 SECONDS LEFT");
    expect(document.querySelector('[data-fotohavn-action="primary"]')).toBeNull();
    expect(warning()).toHaveAttribute("data-phase", "filters");
    expect(warning()).toHaveAttribute("data-tone", "light");
    expect(warning().className).toContain("extra");

    fireEvent.click(button);
    expect(onMoreTime).toHaveBeenCalledOnce();
  });

  it("says SECOND for the last second and rounds partial seconds up", () => {
    const { rerender } = render(<PhaseTimerWarning phase="template" secondsRemaining={0.4} onMoreTime={vi.fn()} />);
    expect(screen.getByText("1").parentElement).toHaveTextContent("1 SECOND LEFT");
    rerender(<PhaseTimerWarning phase="template" secondsRemaining={-2} onMoreTime={vi.fn()} />);
    expect(screen.getByText("0").parentElement).toHaveTextContent("0 SECONDS LEFT");
    expect(wholeSeconds(19.01)).toBe(20);
    expect(wholeSeconds(19)).toBe(19);
    expect(warningAnnouncement(1)).toBe("1 second left. Choose MORE TIME to stay a little longer.");
  });

  it("announces once through a polite live region while the count updates every second", () => {
    vi.useFakeTimers();
    render(
      <StrictMode>
        <CountingDown from={20} />
      </StrictMode>,
    );
    const region = screen.getByRole("status");
    expect(region).toHaveTextContent("20 seconds left. Choose MORE TIME to stay a little longer.");

    const observer = new MutationObserver(() => undefined);
    observer.observe(region, { childList: true, characterData: true, subtree: true });
    const records: MutationRecord[] = [];
    for (let second = 19; second >= 1; second -= 1) {
      act(() => {
        vi.advanceTimersByTime(1000);
      });
      records.push(...observer.takeRecords());
      expect(screen.getByText(String(second))).toBeInTheDocument();
    }
    observer.disconnect();

    expect(records).toEqual([]);
    expect(screen.getAllByRole("status")).toHaveLength(1);
    expect(region).toHaveTextContent("20 seconds left. Choose MORE TIME to stay a little longer.");
  });

  it("fills its live region exactly once after mounting, so the region exists before its text", () => {
    const container = document.body.appendChild(document.createElement("div"));
    const observer = new MutationObserver(() => undefined);
    observer.observe(container, { childList: true, characterData: true, subtree: true });
    render(
      <StrictMode>
        <PhaseTimerWarning phase="photographs-review" secondsRemaining={20} onMoreTime={vi.fn()} />
      </StrictMode>,
      { container },
    );
    const region = screen.getByRole("status");
    const fills = observer.takeRecords().filter((record) => record.target === region);
    observer.disconnect();
    expect(fills).toHaveLength(1);
    expect(fills[0]?.addedNodes[0]?.textContent).toBe("20 seconds left. Choose MORE TIME to stay a little longer.");
  });

  it("announces again when a later warning mounts", () => {
    const { rerender } = render(<PhaseTimerWarning key="first" phase="template" secondsRemaining={20} onMoreTime={vi.fn()} />);
    expect(screen.getByRole("status")).toHaveTextContent("20 seconds left.");
    rerender(<PhaseTimerWarning key="second" phase="template" secondsRemaining={19} onMoreTime={vi.fn()} />);
    expect(screen.getByRole("status")).toHaveTextContent("19 seconds left.");
  });

  it("never appears on Keepsake", () => {
    const { container } = render(
      <PhaseTimerWarning phase={"keepsake" as PhaseTimerWarningPhase} secondsRemaining={10} onMoreTime={vi.fn()} />,
    );
    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByRole("button", { name: "MORE TIME" })).toBeNull();
  });

  it.each(["template", "photographs-capture", "photographs-review", "filters", "guest-gallery"] as const)(
    "warns on %s",
    (phase) => {
      render(<PhaseTimerWarning phase={phase} secondsRemaining={20} onMoreTime={vi.fn()} />);
      expect(warning()).toHaveAttribute("data-phase", phase);
    },
  );

  it("arrives with motion by default and simply appears under reduced motion", () => {
    const list = stubReducedMotion(false);
    render(<PhaseTimerWarning phase="template" secondsRemaining={20} onMoreTime={vi.fn()} />);
    expect(warning()).toHaveAttribute("data-motion", "full");

    act(() => {
      list.matches = true;
      for (const listener of list.listeners) {
        listener();
      }
    });
    expect(warning()).toHaveAttribute("data-motion", "reduced");
  });

  it("starts without motion when reduced motion is already preferred, and unsubscribes on unmount", () => {
    const list = stubReducedMotion(true);
    const { unmount } = render(<PhaseTimerWarning phase="template" secondsRemaining={20} onMoreTime={vi.fn()} />);
    expect(warning()).toHaveAttribute("data-motion", "reduced");
    expect(list.listeners.size).toBe(1);
    unmount();
    expect(list.listeners.size).toBe(0);
  });

  it("allows motion where the host has no matchMedia", () => {
    vi.stubGlobal("matchMedia", undefined);
    render(<PhaseTimerWarning phase="template" secondsRemaining={20} onMoreTime={vi.fn()} />);
    expect(warning()).toHaveAttribute("data-motion", "full");
  });

  it("doesn't arrive again when it is already under way as the view mounts", () => {
    stubReducedMotion(false);
    render(<PhaseTimerWarning phase="template" secondsRemaining={9} onMoreTime={vi.fn()} arrive={false} />);
    expect(warning()).toHaveAttribute("data-motion", "static");
    expect(screen.getByRole("status")).toHaveTextContent("9 seconds left.");
  });
});
