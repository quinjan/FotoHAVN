import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { TextLink } from "./TextLink.tsx";

describe("TextLink", () => {
  it("is a text-action button in the Action role by default", () => {
    render(<TextLink>CLOSE</TextLink>);
    const button = screen.getByRole("button", { name: "CLOSE" });
    expect(button).toHaveAttribute("type", "button");
    expect(button).toHaveAttribute("data-fotohavn-action", "text");
    expect(button).toHaveAttribute("data-size", "action");
    expect(button).toHaveAttribute("aria-disabled", "false");
    expect(button.querySelector("[data-direction]")).toBeNull();
  });

  it("puts a decorative arrow after the underlined label", () => {
    render(
      <TextLink arrow="left" size="meta">
        BACK TO START
      </TextLink>,
    );
    const button = screen.getByRole("button", { name: "BACK TO START" });
    expect(button).toHaveAttribute("data-size", "meta");
    expect(button.firstElementChild).toHaveTextContent("BACK TO START");
    expect(button.lastElementChild).toHaveAttribute("data-direction", "left");
    expect(button.lastElementChild).toHaveAttribute("aria-hidden", "true");
  });

  it("calls onClick when available and ignores it while disabled", () => {
    const onClick = vi.fn();
    const { rerender } = render(<TextLink onClick={onClick}>START</TextLink>);
    fireEvent.click(screen.getByRole("button", { name: "START" }));
    expect(onClick).toHaveBeenCalledOnce();

    rerender(
      <TextLink onClick={onClick} disabled type="submit">
        START
      </TextLink>,
    );
    const button = screen.getByRole("button", { name: "START" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(button).toHaveAttribute("type", "submit");
    fireEvent.click(button);
    expect(onClick).toHaveBeenCalledOnce();
  });

  it("renders a link with href, and blocks it while disabled", () => {
    const onClick = vi.fn();
    const { rerender } = render(
      <TextLink href="#all" arrow="up-right" onClick={onClick} className="extra">
        ALL FIXTURES
      </TextLink>,
    );
    const link = screen.getByRole("link", { name: "ALL FIXTURES" });
    expect(link).toHaveAttribute("href", "#all");
    expect(link).toHaveAttribute("data-fotohavn-action", "text");
    expect(link).not.toHaveAttribute("aria-disabled");
    expect(link.className).toContain("extra");
    expect(link.querySelector("[data-direction='up-right']")).not.toBeNull();
    fireEvent.click(link);
    expect(onClick).toHaveBeenCalledOnce();

    rerender(
      <TextLink href="#all" size="meta" disabled onClick={onClick}>
        ALL FIXTURES
      </TextLink>,
    );
    expect(link).toHaveAttribute("aria-disabled", "true");
    expect(link).toHaveAttribute("data-size", "meta");
    expect(fireEvent.click(link)).toBe(false);
    expect(onClick).toHaveBeenCalledOnce();
  });
});
