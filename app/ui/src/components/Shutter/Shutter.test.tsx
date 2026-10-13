import { fireEvent, render, screen } from "@testing-library/react";
import { createRef } from "react";
import { describe, expect, it, vi } from "vitest";
import { Shutter } from "./Shutter.tsx";

describe("Shutter", () => {
  it("is a button named Take photographs, with the light camera icon and its label", () => {
    const { container } = render(<Shutter className="extra" />);
    const button = screen.getByRole("button", { name: "Take photographs" });
    expect(button).toHaveAttribute("type", "button");
    expect(button).toHaveAttribute("aria-disabled", "false");
    expect(button.querySelector("svg")?.parentElement).toHaveAttribute("aria-hidden", "true");
    const root = container.firstElementChild;
    expect(root).toHaveAttribute("data-state", "idle");
    expect(root?.className).toContain("extra");
    expect(root).toHaveTextContent("TAKE PHOTOGRAPHS");
  });

  it("names and labels the recording state, with the Fill icon", () => {
    const { container, rerender } = render(<Shutter />);
    const lightIcon = screen.getByRole("button").innerHTML;
    rerender(<Shutter state="recording" />);
    const button = screen.getByRole("button", { name: "Stop taking photographs" });
    expect(container.firstElementChild).toHaveAttribute("data-state", "recording");
    expect(container.firstElementChild).toHaveTextContent("STOP");
    expect(button.innerHTML).not.toBe(lightIcon);
  });

  it("calls onClick, and ignores it while disabled", () => {
    const onClick = vi.fn();
    const { rerender } = render(<Shutter onClick={onClick} />);
    fireEvent.click(screen.getByRole("button", { name: "Take photographs" }));
    expect(onClick).toHaveBeenCalledOnce();

    rerender(<Shutter onClick={onClick} disabled />);
    const button = screen.getByRole("button", { name: "Take photographs" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    button.focus();
    expect(button).toHaveFocus();
    fireEvent.click(button);
    expect(onClick).toHaveBeenCalledOnce();
  });

  it("can hide the visible label, and passes through the ref and attributes", () => {
    const ref = createRef<HTMLButtonElement>();
    const { container } = render(<Shutter ref={ref} showLabel={false} aria-describedby="why" />);
    const button = screen.getByRole("button", { name: "Take photographs" });
    expect(ref.current).toBe(button);
    expect(button).toHaveAttribute("aria-describedby", "why");
    expect(container.firstElementChild).not.toHaveTextContent("TAKE PHOTOGRAPHS");
  });
});
