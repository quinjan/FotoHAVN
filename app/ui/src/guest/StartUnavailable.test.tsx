import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { StartUnavailableSurface } from "./StartSurface.tsx";

// The stylesheets as written, read from disk relative to app/ui, where Vitest runs (it turns CSS imports into modules).
const fsModule = "node:fs";
const { readFileSync } = (await import(/* @vite-ignore */ fsModule)) as {
  readFileSync: (path: string, encoding: "utf8") => string;
};
const css = readFileSync("src/guest/StartUnavailable.module.css", "utf8");
const globalCss = readFileSync("src/styles/global.css", "utf8");
const tokensCss = readFileSync("src/styles/app-tokens.css", "utf8");

const actions = { onStepInside: vi.fn(), onOpenGallery: vi.fn() };

describe("Start unavailable wait hint", () => {
  it.each(["camera", "storage"] as const)("is in the page for the %s reason as plain text, not a live region", (reason) => {
    const { container } = render(
      <StartUnavailableSurface unavailable={{ reason, guestGalleryLinkVisible: false }} actions={actions} />,
    );
    const hint = screen.getByText("If this takes a while, please ask the FOTOHAVN team.");
    expect(hint).toHaveAttribute("data-delayed-reveal");
    expect(hint.closest("[aria-live], [role='status'], [role='alert']")).toBeNull();
    expect(container.querySelector("button")).toBeNull();
  });

  it("sits beside the gallery link when the brain shows it", () => {
    render(<StartUnavailableSurface unavailable={{ reason: "camera", guestGalleryLinkVisible: true }} actions={actions} />);
    expect(screen.getByRole("button", { name: "SEE THE GOOD COMPANY" })).toBeInTheDocument();
    expect(screen.getByText("If this takes a while, please ask the FOTOHAVN team.")).toBeInTheDocument();
  });

  it("stays hidden for 25 seconds with a CSS delay and no movement, so reduced motion changes nothing", () => {
    expect(css).toMatch(/\.waitHint \{[^}]*visibility: hidden;/);
    expect(css).toMatch(/animation: reveal var\(--app-duration-instant\) linear var\(--app-wait-hint-delay\) forwards;/);
    expect(tokensCss).toMatch(/--app-wait-hint-delay: 25s;/);
    expect(css).not.toMatch(/transform|translate|opacity/);
    expect(globalCss).toMatch(/prefers-reduced-motion: reduce\) \{\s*\*:not\(\[data-delayed-reveal\]\)/);
  });
});
