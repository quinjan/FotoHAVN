import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { IdentifyOverlay } from "./IdentifyOverlay.tsx";

describe("IdentifyOverlay", () => {
  it("shows the display number and name on an Off-white panel", () => {
    const { container } = render(<IdentifyOverlay number={2} name="DELL U2720Q" className="extra" />);
    const number = screen.getByText("2");
    const name = screen.getByText("DELL U2720Q");
    expect(number.tagName).toBe("P");
    expect(name.tagName).toBe("P");
    expect(number.parentElement).toHaveAttribute("data-tone", "light");
    expect(number.parentElement).toBe(name.parentElement);
    expect(container.firstElementChild?.className).toContain("extra");
  });

  it("takes no focus and holds nothing focusable", () => {
    const before = document.createElement("button");
    document.body.append(before);
    before.focus();
    const { container } = render(<IdentifyOverlay number={1} name="Built-in display" />);
    expect(before).toHaveFocus();
    expect(container.querySelector("button, a, input, select, textarea, [tabindex]")).toBeNull();
    before.remove();
  });

  it("renders nothing while hidden", () => {
    const { container, rerender } = render(<IdentifyOverlay number={1} name="Built-in display" visible={false} />);
    expect(container).toBeEmptyDOMElement();
    rerender(<IdentifyOverlay number={1} name="Built-in display" visible />);
    expect(screen.getByText("Built-in display")).toBeInTheDocument();
  });
});
