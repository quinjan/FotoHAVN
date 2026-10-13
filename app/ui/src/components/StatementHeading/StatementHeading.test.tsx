import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { StatementHeading } from "./StatementHeading.tsx";

describe("StatementHeading", () => {
  it("ends the heading with one italic turn in <em>", () => {
    render(<StatementHeading lead="Small room." turn="Whole other world." />);
    const heading = screen.getByRole("heading", { level: 2, name: "Small room. Whole other world." });
    const turns = heading.querySelectorAll("em");
    expect(turns).toHaveLength(1);
    expect(turns[0]).toHaveTextContent("Whole other world.");
    expect(heading.lastElementChild).toBe(turns[0]);
    expect(turns[0]).not.toHaveAttribute("data-own-line");
    expect(heading).toHaveAttribute("data-size", "heading");
    expect(heading.parentElement).toHaveAttribute("data-align", "start");
  });

  it("renders a plain heading without a turn", () => {
    render(<StatementHeading level={1} lead="Events" />);
    const heading = screen.getByRole("heading", { level: 1, name: "Events" });
    expect(heading.querySelector("em")).toBeNull();
  });

  it("composes eyebrow, heading, supporting lines, and action in reading order", () => {
    render(
      <StatementHeading
        level={3}
        size="display"
        align="center"
        eyebrow="ON THE OTHER SIDE"
        lead="A little moment."
        turn="Yours."
        turnOnOwnLine
        supporting={["Keep the moment close.", "Your strip is saved."]}
        action={<button type="button">CLOSE</button>}
        id="keepsake-heading"
        className="extra"
      />,
    );
    const heading = screen.getByRole("heading", { level: 3, name: "A little moment. Yours." });
    expect(heading).toHaveAttribute("id", "keepsake-heading");
    expect(heading).toHaveAttribute("data-size", "display");
    expect(heading.querySelector("em")).toHaveAttribute("data-own-line", "true");

    const statement = heading.parentElement;
    expect(statement).toHaveAttribute("data-align", "center");
    expect(statement?.className).toContain("extra");
    const children = [...(statement?.children ?? [])];
    expect(children.map((child) => child.textContent)).toEqual([
      "ON THE OTHER SIDE",
      "A little moment. Yours.",
      "Keep the moment close.",
      "Your strip is saved.",
      "CLOSE",
    ]);
    expect(children[0]?.tagName).toBe("P");
    expect(screen.getByRole("button", { name: "CLOSE" })).toBeVisible();
  });

  it("accepts a single supporting line as a string", () => {
    render(<StatementHeading lead="Find your" turn="light." supporting="Let this moment become a memory." size="heading-sm" />);
    expect(screen.getByText("Let this moment become a memory.").tagName).toBe("P");
    expect(screen.getByRole("heading", { level: 2 })).toHaveAttribute("data-size", "heading-sm");
  });
});
