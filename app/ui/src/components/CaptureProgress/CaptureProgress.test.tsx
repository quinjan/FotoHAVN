import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { CaptureProgress } from "./CaptureProgress.tsx";

function live(container: HTMLElement): HTMLElement {
  const region = container.querySelector<HTMLElement>("[aria-live='polite']");
  if (region === null) {
    throw new Error("no live region");
  }
  return region;
}

describe("CaptureProgress", () => {
  it("is a named progressbar with the count as text and one mark per window", () => {
    const { container } = render(<CaptureProgress taken={2} total={4} className="extra" />);
    const meter = screen.getByRole("progressbar", { name: "Photographs" });
    expect(meter).toHaveAttribute("aria-valuemin", "0");
    expect(meter).toHaveAttribute("aria-valuemax", "4");
    expect(meter).toHaveAttribute("aria-valuenow", "2");
    expect(meter).toHaveAttribute("aria-valuetext", "2 of 4");
    expect(meter).toHaveTextContent("2 of 4");
    expect(container.querySelectorAll("[data-taken]")).toHaveLength(2);
    expect(meter.querySelector("[aria-hidden='true']")?.children).toHaveLength(4);
    expect(container.firstElementChild?.className).toContain("extra");
  });

  it("doesn't announce on first render", () => {
    const { container } = render(<CaptureProgress taken={1} total={3} />);
    expect(live(container)).toHaveTextContent("");
  });

  it("announces each completed Capture once, politely", () => {
    const { container, rerender } = render(<CaptureProgress taken={0} total={4} />);
    rerender(<CaptureProgress taken={1} total={4} />);
    expect(live(container)).toHaveTextContent("Photograph 1 of 4 taken.");
    rerender(<CaptureProgress taken={2} total={4} />);
    expect(live(container)).toHaveTextContent("Photograph 2 of 4 taken.");

    // Fewer windows filled (a Retake, a new Template) changes the count without an announcement.
    rerender(<CaptureProgress taken={1} total={4} />);
    expect(screen.getByRole("progressbar")).toHaveAttribute("aria-valuenow", "1");
    expect(live(container)).toHaveTextContent("Photograph 2 of 4 taken.");
  });

  it("keeps the count within the windows", () => {
    const { container, rerender } = render(<CaptureProgress taken={7} total={4} />);
    expect(screen.getByRole("progressbar")).toHaveAttribute("aria-valuenow", "4");
    expect(container.querySelectorAll("[data-taken]")).toHaveLength(4);
    rerender(<CaptureProgress taken={-1} total={4} />);
    expect(screen.getByRole("progressbar")).toHaveAttribute("aria-valuetext", "0 of 4");
  });
});
