import { fireEvent, render, screen } from "@testing-library/react";
import { createRef } from "react";
import { describe, expect, it, vi } from "vitest";
import { PrimaryAction } from "./PrimaryAction.tsx";

describe("PrimaryAction", () => {
  it("is a button named by its label and marked as the view's primary action", () => {
    render(<PrimaryAction>SAVE</PrimaryAction>);
    const button = screen.getByRole("button", { name: "SAVE" });
    expect(button).toHaveAttribute("type", "button");
    expect(button).toHaveAttribute("data-fotohavn-action", "primary");
    expect(button).toHaveAttribute("aria-disabled", "false");
    expect(button.querySelector("[data-direction]")).toBeNull();
  });

  it("puts a decorative Phosphor arrow after the label", () => {
    render(<PrimaryAction arrow="right">NEW EVENT</PrimaryAction>);
    const button = screen.getByRole("button", { name: "NEW EVENT" });
    const arrow = button.querySelector("[data-direction='right']");
    expect(arrow).toHaveAttribute("aria-hidden", "true");
    expect(arrow?.querySelector("svg")).not.toBeNull();
    expect(button.lastElementChild).toBe(arrow);
    expect(button).toHaveTextContent(/^NEW EVENT$/);
  });

  it("calls onClick when available", () => {
    const onClick = vi.fn();
    render(<PrimaryAction onClick={onClick}>RETRY</PrimaryAction>);
    fireEvent.click(screen.getByRole("button", { name: "RETRY" }));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it("stays focusable but ignores activation while disabled", () => {
    const onClick = vi.fn();
    render(
      <PrimaryAction disabled onClick={onClick}>
        RETRY
      </PrimaryAction>,
    );
    const button = screen.getByRole("button", { name: "RETRY" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(button).toBeEnabled();
    button.focus();
    expect(button).toHaveFocus();
    fireEvent.click(button);
    expect(onClick).not.toHaveBeenCalled();
  });

  it("passes through the type, ref, class, and other button attributes", () => {
    const ref = createRef<HTMLButtonElement>();
    render(
      <PrimaryAction type="submit" ref={ref} className="extra" aria-describedby="why" id="save">
        SAVE
      </PrimaryAction>,
    );
    const button = screen.getByRole("button", { name: "SAVE" });
    expect(button).toHaveAttribute("type", "submit");
    expect(button).toHaveAttribute("aria-describedby", "why");
    expect(button).toHaveAttribute("id", "save");
    expect(button.className).toContain("extra");
    expect(ref.current).toBe(button);
  });
});
