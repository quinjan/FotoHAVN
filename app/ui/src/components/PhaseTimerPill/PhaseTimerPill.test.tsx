import { render, screen } from "@testing-library/react";
import { StrictMode } from "react";
import { describe, expect, it } from "vitest";
import { PhaseTimerPill, clockText, lowAnnouncement, wholeSeconds } from "./PhaseTimerPill.tsx";

function pill(): HTMLElement {
  return screen.getByRole("timer");
}

function status(): HTMLElement {
  return screen.getByRole("status");
}

describe("PhaseTimerPill", () => {
  it("shows the time as m:ss in whole seconds, rounded up and never negative", () => {
    expect(wholeSeconds(4.2)).toBe(5);
    expect(wholeSeconds(-3)).toBe(0);
    expect(clockText(45)).toBe("0:45");
    expect(clockText(8)).toBe("0:08");
    expect(clockText(125)).toBe("2:05");
    expect(clockText(0.4)).toBe("0:01");
    expect(lowAnnouncement(1)).toBe("1 second left.");
    expect(lowAnnouncement(10)).toBe("10 seconds left.");
  });

  it("is a quiet outlined timer, named with the time in words, while more than 10 seconds remain", () => {
    render(<PhaseTimerPill secondsRemaining={45} />);
    expect(pill()).toHaveTextContent("0:45");
    expect(pill()).toHaveAccessibleName("Time left: 45 seconds");
    expect(pill()).not.toHaveAttribute("data-low");
    expect(pill()).not.toHaveAttribute("data-paused");
    expect(status()).toBeEmptyDOMElement();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("fills in the last 10 seconds and says so once, never every second", () => {
    const { rerender } = render(<PhaseTimerPill secondsRemaining={12} />);
    expect(status()).toBeEmptyDOMElement();
    rerender(<PhaseTimerPill secondsRemaining={11} />);
    expect(status()).toBeEmptyDOMElement();
    rerender(<PhaseTimerPill secondsRemaining={10} />);
    expect(pill()).toHaveAttribute("data-low", "true");
    expect(status()).toHaveTextContent("10 seconds left.");
    rerender(<PhaseTimerPill secondsRemaining={9} />);
    rerender(<PhaseTimerPill secondsRemaining={1} />);
    expect(pill()).toHaveTextContent("0:01");
    expect(status()).toHaveTextContent("10 seconds left.");
  });

  it("announces once when it mounts inside the last seconds, and again only after the time goes back above them", () => {
    const { rerender } = render(
      <StrictMode>
        <PhaseTimerPill secondsRemaining={7} />
      </StrictMode>,
    );
    expect(status()).toHaveTextContent("7 seconds left.");
    rerender(
      <StrictMode>
        <PhaseTimerPill secondsRemaining={6} />
      </StrictMode>,
    );
    expect(status()).toHaveTextContent("7 seconds left.");
    rerender(
      <StrictMode>
        <PhaseTimerPill secondsRemaining={30} />
      </StrictMode>,
    );
    expect(status()).toBeEmptyDOMElement();
    expect(pill()).not.toHaveAttribute("data-low");
    rerender(
      <StrictMode>
        <PhaseTimerPill secondsRemaining={10} />
      </StrictMode>,
    );
    expect(status()).toHaveTextContent("10 seconds left.");
  });

  it("stands still with a pause mark while the brain pauses it", () => {
    render(<PhaseTimerPill secondsRemaining={32} paused />);
    expect(pill()).toHaveAttribute("data-paused", "true");
    expect(pill()).toHaveAccessibleName("Time left: 32 seconds, paused");
    expect(pill()).toHaveTextContent("0:32");
  });
});
