import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { CountdownNumeral } from "./CountdownNumeral.tsx";

// The component's stylesheet as written, read from disk relative to app/ui, where Vitest runs (it turns CSS imports into modules, even with ?raw).
const fsModule = "node:fs";
const { readFileSync } = (await import(/* @vite-ignore */ fsModule)) as {
  readFileSync: (path: string, encoding: "utf8") => string;
};
const css = readFileSync("src/components/CountdownNumeral/CountdownNumeral.module.css", "utf8");

function live(container: HTMLElement): HTMLElement {
  const region = container.querySelector<HTMLElement>("[aria-live='polite']");
  if (region === null) {
    throw new Error("no live region");
  }
  return region;
}

describe("CountdownNumeral", () => {
  it("shows the number, and announces the start once, politely", () => {
    const { container, rerender } = render(<CountdownNumeral value={5} className="extra" />);
    expect(screen.getByText("5")).toBeInTheDocument();
    expect(container.firstElementChild?.className).toContain("extra");
    expect(live(container)).toHaveTextContent("Next photograph in 5 seconds.");

    rerender(<CountdownNumeral value={4} />);
    expect(screen.getByText("4")).toBeInTheDocument();
    expect(screen.queryByText("5")).toBeNull();
    rerender(<CountdownNumeral value={3} />);
    rerender(<CountdownNumeral value={1} />);
    expect(live(container)).toHaveTextContent("Next photograph in 5 seconds.");
  });

  it("shows nothing at zero, and announces the next countdown", () => {
    const { container, rerender } = render(<CountdownNumeral value={1} />);
    expect(live(container)).toHaveTextContent("Next photograph in 1 second.");
    rerender(<CountdownNumeral value={0} />);
    expect(container.querySelector("[data-enter], span:not([aria-live])")).toBeNull();
    expect(live(container)).toHaveTextContent("");

    rerender(<CountdownNumeral value={3} />);
    expect(live(container)).toHaveTextContent("Next photograph in 3 seconds.");
    // A countdown that restarts higher without reaching zero is a new countdown too.
    rerender(<CountdownNumeral value={2} />);
    rerender(<CountdownNumeral value={10} />);
    expect(live(container)).toHaveTextContent("Next photograph in 10 seconds.");
  });

  it("says nothing when it mounts at zero", () => {
    const { container } = render(<CountdownNumeral value={0} />);
    expect(live(container)).toHaveTextContent("");
  });

  it("keeps the first numeral still and animates every later one", () => {
    const { rerender } = render(<CountdownNumeral value={5} />);
    expect(screen.getByText("5")).not.toHaveAttribute("data-enter");
    rerender(<CountdownNumeral value={4} />);
    expect(screen.getByText("4")).toHaveAttribute("data-enter", "true");
  });

  it("shows the cue only during the last second, and never announces it", () => {
    const { container, rerender } = render(<CountdownNumeral value={3} cue="Look at the camera." />);
    expect(screen.queryByText("Look at the camera.")).toBeNull();
    rerender(<CountdownNumeral value={2} cue="Look at the camera." />);
    expect(screen.queryByText("Look at the camera.")).toBeNull();
    rerender(<CountdownNumeral value={1} cue="Look at the camera." />);
    expect(screen.getByText("Look at the camera.")).toBeInTheDocument();
    expect(screen.getByText("Look at the camera.").closest("[aria-live]")).toBeNull();
    expect(live(container)).toHaveTextContent("Next photograph in 3 seconds.");
    rerender(<CountdownNumeral value={0} cue="Look at the camera." />);
    expect(screen.queryByText("Look at the camera.")).toBeNull();
  });

  it("shows no cue unless one is given", () => {
    render(<CountdownNumeral value={1} />);
    expect(screen.queryByText("Look at the camera.")).toBeNull();
  });

  it("sets the numeral on a soft disc at weight 500, top-centre, with no transform outside the motion query", () => {
    expect(css).toMatch(/\.disc \{[^}]*background-color: var\(--app-countdown-disc\)/);
    expect(css).toMatch(/\.numeral \{[^}]*font-weight: var\(--font-weight-medium\)/);
    expect(css).toMatch(/\.countdown \{[^}]*place-self: start center/);
  });

  it("moves numerals only when the user doesn't prefer reduced motion", () => {
    const outsideNoPreference = css.replace(/@media \(prefers-reduced-motion: no-preference\) \{[\s\S]*?\n\}/g, "");
    expect(outsideNoPreference).not.toMatch(/transform|translate|scale/);
    expect(css).toMatch(/@media \(prefers-reduced-motion: no-preference\)[\s\S]*translateY\(var\(--motion-distance-reveal-max\)\)/);
  });
});
