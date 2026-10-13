import { act, render, screen } from "@testing-library/react";
import { renderToString } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { StartingScreen, startingCopy } from "./StartingScreen.tsx";

async function settle(): Promise<void> {
  await act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
  });
}

describe("StartingScreen", () => {
  it("uses the agreed line", () => {
    expect(startingCopy).toBe("Opening the Data folder…");
  });

  it("shows the Events heading, one neutral status line, and no action", async () => {
    const { container } = render(<StartingScreen />);
    await settle();
    expect(screen.getByRole("heading", { level: 1, name: "Events" })).toBeVisible();
    const status = screen.getByRole("status");
    expect(status).toHaveTextContent(startingCopy);
    expect(status.closest("[data-status-tone]")).toHaveAttribute("data-status-tone", "neutral");
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(container.querySelector("[data-fotohavn-action]")).toBeNull();
  });

  it("mounts the status empty, then fills it once, so the live region announces it", async () => {
    const html = renderToString(<StartingScreen />);
    expect(html).not.toContain(startingCopy);
    expect(html).toContain('role="status"');

    render(<StartingScreen />);
    const status = screen.getByRole("status");
    expect(status).toBeEmptyDOMElement();
    await settle();
    expect(status).toHaveTextContent(startingCopy);
  });

  it("marks the view busy and takes a layout class from its parent", () => {
    const { container } = render(<StartingScreen className="from-parent" />);
    expect(container.firstElementChild).toHaveAttribute("aria-busy", "true");
    expect(container.firstElementChild).toHaveClass("from-parent");
  });

  it("cancels the announcement timer when it unmounts first", () => {
    const { unmount } = render(<StartingScreen />);
    expect(() => {
      unmount();
    }).not.toThrow();
  });
});
