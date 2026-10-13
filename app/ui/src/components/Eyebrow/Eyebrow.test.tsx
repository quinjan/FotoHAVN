import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Eyebrow } from "./Eyebrow.tsx";

describe("Eyebrow", () => {
  it("is a paragraph, never a heading", () => {
    render(
      <Eyebrow id="intro" className="extra">
        THE FOTOHAVN EXPERIENCE
      </Eyebrow>,
    );
    const eyebrow = screen.getByText("THE FOTOHAVN EXPERIENCE");
    expect(eyebrow.tagName).toBe("P");
    expect(eyebrow).toHaveAttribute("id", "intro");
    expect(eyebrow.className).toContain("extra");
    expect(screen.queryByRole("heading")).not.toBeInTheDocument();
  });
});
