import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { TextLink } from "../TextLink/index.ts";
import { InlineStatus } from "./InlineStatus.tsx";

describe("InlineStatus", () => {
  it("is a polite live region with a decorative icon beside the text, neutral by default", () => {
    const { container } = render(<InlineStatus id="camera">Checking the camera.</InlineStatus>);
    const region = screen.getByRole("status");
    expect(region).toHaveTextContent("Checking the camera.");
    const wrapper = container.firstElementChild;
    expect(wrapper).toHaveAttribute("id", "camera");
    expect(wrapper).toHaveAttribute("data-status-tone", "neutral");
    expect(wrapper).not.toHaveAttribute("data-empty");
    const icon = region.querySelector("[aria-hidden='true']");
    expect(icon?.querySelector("svg")).not.toBeNull();
  });

  it("marks an alert with its own icon and tone, not color alone", () => {
    const { container, rerender } = render(<InlineStatus>Ready</InlineStatus>);
    const neutralIcon = container.querySelector("svg")?.innerHTML;
    rerender(<InlineStatus tone="alert">The guest display is disconnected.</InlineStatus>);
    expect(container.firstElementChild).toHaveAttribute("data-status-tone", "alert");
    expect(screen.getByRole("status")).toHaveTextContent("The guest display is disconnected.");
    expect(container.querySelector("svg")?.innerHTML).not.toBe(neutralIcon);
  });

  it("keeps the live region mounted while empty, and announces the text when it arrives", () => {
    const { container, rerender } = render(<InlineStatus tone="alert" action={<button type="button">LINK</button>} />);
    const region = screen.getByRole("status");
    expect(region).toBeEmptyDOMElement();
    expect(container.firstElementChild).toHaveAttribute("data-empty", "true");
    expect(screen.queryByRole("button")).toBeNull();

    rerender(<InlineStatus tone="alert">Camera not available.</InlineStatus>);
    expect(screen.getByRole("status")).toBe(region);
    expect(region).toHaveTextContent("Camera not available.");

    for (const empty of [null, false, ""]) {
      rerender(<InlineStatus>{empty}</InlineStatus>);
      expect(screen.getByRole("status")).toBeEmptyDOMElement();
    }
  });

  it("puts its action outside the live region, so the link isn't announced with each change", () => {
    const onClick = vi.fn();
    render(
      <InlineStatus
        tone="alert"
        className="extra"
        action={
          <TextLink size="meta" onClick={onClick}>
            BACK
          </TextLink>
        }
      >
        Operator Assistance: the camera stopped.
      </InlineStatus>,
    );
    const region = screen.getByRole("status");
    const link = screen.getByRole("button", { name: "BACK" });
    expect(region).not.toContainElement(link);
    expect(region.parentElement?.className).toContain("extra");
    fireEvent.click(link);
    expect(onClick).toHaveBeenCalledOnce();
  });
});
