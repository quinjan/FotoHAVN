import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { RunningEventPanel, type RunningEventPanelProps } from "./RunningEventPanel.tsx";

function props(overrides: Partial<RunningEventPanelProps> = {}): RunningEventPanelProps {
  return {
    eventName: "Santos and Reyes wedding",
    guestDisplay: "connected",
    camera: "ready",
    step: "photographs",
    completedGuestCycles: 27,
    timeSinceStart: "1 h 12 min",
    onOpenGallery: vi.fn(),
    onRetry: vi.fn(),
    onEndEvent: vi.fn(),
    ...overrides,
  };
}

function facts(): Record<string, string> {
  const terms = screen.getAllByRole("term");
  return Object.fromEntries(terms.map((term) => [term.textContent, term.nextElementSibling?.textContent ?? ""]));
}

describe("RunningEventPanel", () => {
  it("is a group named by the Event, on its own Warm Ivory light surface", () => {
    render(<RunningEventPanel {...props()} className="extra" />);
    const panel = screen.getByRole("group", { name: "Santos and Reyes wedding" });
    expect(panel).toHaveAttribute("data-tone", "light");
    expect(panel).not.toHaveAttribute("data-assistance");
    expect(panel.className).toContain("extra");
    expect(screen.getByRole("heading", { level: 2, name: "Santos and Reyes wedding" })).toBeInTheDocument();
    expect(within(panel).getByText("RUNNING").tagName).toBe("P");
  });

  it("lists every fact as a Meta label with a text value", () => {
    render(<RunningEventPanel {...props()} />);
    expect(facts()).toEqual({
      "GUEST DISPLAY": "Connected",
      CAMERA: "Ready",
      "CURRENT STEP": "Photographs",
      "COMPLETED GUEST CYCLES": "27",
      "TIME SINCE START": "1 h 12 min",
    });
  });

  it.each([
    ["start", "Start"],
    ["template", "Template"],
    ["photographs", "Photographs"],
    ["filters", "Filters"],
    ["keepsake", "Keepsake"],
    ["guest-gallery", "Guest gallery"],
    ["operator-assistance", "Operator Assistance"],
  ] as const)("says the %s step in text", (step, text) => {
    render(<RunningEventPanel {...props({ step })} />);
    expect(facts()["CURRENT STEP"]).toBe(text);
  });

  it("updates live from props, with the problem states in text", () => {
    const { rerender } = render(<RunningEventPanel {...props()} />);
    rerender(
      <RunningEventPanel
        {...props({ guestDisplay: "disconnected", camera: "not-available", step: "start", completedGuestCycles: 28, timeSinceStart: "1 h 13 min" })}
      />,
    );
    expect(facts()).toEqual({
      "GUEST DISPLAY": "Disconnected",
      CAMERA: "Not available",
      "CURRENT STEP": "Start",
      "COMPLETED GUEST CYCLES": "28",
      "TIME SINCE START": "1 h 13 min",
    });
  });

  it("normally offers OPEN GALLERY and END EVENT, with no primary action and no RETRY", () => {
    const onOpenGallery = vi.fn();
    const onEndEvent = vi.fn();
    render(<RunningEventPanel {...props({ onOpenGallery, onEndEvent })} />);
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["OPEN GALLERY", "END EVENT"]);
    expect(screen.getByRole("button", { name: "OPEN GALLERY" })).toHaveAttribute("data-fotohavn-action", "text");
    expect(screen.getByRole("button", { name: "END EVENT" })).toHaveAttribute("data-fotohavn-action", "secondary");
    expect(document.querySelector('[data-fotohavn-action="primary"]')).toBeNull();
    expect(screen.queryByRole("button", { name: "RETRY" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "OPEN GALLERY" }));
    fireEvent.click(screen.getByRole("button", { name: "END EVENT" }));
    expect(onOpenGallery).toHaveBeenCalledOnce();
    expect(onEndEvent).toHaveBeenCalledOnce();
  });

  it("keeps an empty alert live region until Operator Assistance", () => {
    render(<RunningEventPanel {...props()} />);
    expect(screen.getByRole("status")).toBeEmptyDOMElement();
  });

  it("during Operator Assistance shows the reason in an alert line and RETRY as the single primary action", () => {
    const onRetry = vi.fn();
    const { rerender } = render(<RunningEventPanel {...props({ onRetry })} />);
    const region = screen.getByRole("status");

    rerender(
      <RunningEventPanel
        {...props({ onRetry, step: "operator-assistance", assistanceReason: "The guest display is disconnected." })}
      />,
    );
    expect(screen.getByRole("status")).toBe(region);
    expect(region).toHaveTextContent("The guest display is disconnected.");
    expect(region.parentElement).toHaveAttribute("data-status-tone", "alert");
    expect(screen.getByRole("group")).toHaveAttribute("data-assistance", "true");

    const primaries = document.querySelectorAll('[data-fotohavn-action="primary"]');
    expect(primaries).toHaveLength(1);
    const retry = screen.getByRole("button", { name: "RETRY" });
    expect(primaries[0]).toBe(retry);
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["RETRY", "OPEN GALLERY", "END EVENT"]);

    retry.focus();
    expect(retry).toHaveFocus();
    fireEvent.click(retry);
    expect(onRetry).toHaveBeenCalledOnce();

    rerender(<RunningEventPanel {...props({ onRetry })} />);
    expect(screen.queryByRole("button", { name: "RETRY" })).toBeNull();
    expect(region).toBeEmptyDOMElement();
  });

  it("takes a heading level under the screen's heading", () => {
    render(<RunningEventPanel {...props({ headingLevel: 3 })} />);
    expect(screen.getByRole("heading", { level: 3, name: "Santos and Reyes wedding" })).toBeInTheDocument();
  });
});
