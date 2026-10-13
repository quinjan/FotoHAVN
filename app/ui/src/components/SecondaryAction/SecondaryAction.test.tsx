import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { SecondaryAction } from "./SecondaryAction.tsx";

describe("SecondaryAction", () => {
  it("is a button named by its label and marked as a secondary action", () => {
    render(<SecondaryAction>KEEP GOING</SecondaryAction>);
    const button = screen.getByRole("button", { name: "KEEP GOING" });
    expect(button).toHaveAttribute("type", "button");
    expect(button).toHaveAttribute("data-fotohavn-action", "secondary");
    expect(button).toHaveAttribute("aria-disabled", "false");
    expect(button.querySelector("[data-direction]")).toBeNull();
  });

  it("puts a decorative arrow after the label", () => {
    render(<SecondaryAction arrow="left">BACK</SecondaryAction>);
    const arrow = screen.getByRole("button", { name: "BACK" }).querySelector("[data-direction='left']");
    expect(arrow).toHaveAttribute("aria-hidden", "true");
  });

  it("calls onClick when available and ignores it while disabled", () => {
    const onClick = vi.fn();
    const { rerender } = render(<SecondaryAction onClick={onClick}>SAVE AND START</SecondaryAction>);
    fireEvent.click(screen.getByRole("button", { name: "SAVE AND START" }));
    expect(onClick).toHaveBeenCalledOnce();

    rerender(
      <SecondaryAction onClick={onClick} disabled type="submit">
        SAVE AND START
      </SecondaryAction>,
    );
    const button = screen.getByRole("button", { name: "SAVE AND START" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(button).toHaveAttribute("type", "submit");
    fireEvent.click(button);
    expect(onClick).toHaveBeenCalledOnce();
  });
});
