import { fireEvent, render, screen } from "@testing-library/react";
import { createRef, useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { Switch } from "./Switch.tsx";

describe("Switch", () => {
  it("is a switch button named by the label above it, with its state in text", () => {
    render(<Switch label="Mirror" checked={false} />);
    const control = screen.getByRole("switch", { name: "Mirror" });
    expect(control.tagName).toBe("BUTTON");
    expect(control).toHaveAttribute("type", "button");
    expect(control).toHaveAttribute("aria-checked", "false");
    expect(control).toHaveAttribute("aria-disabled", "false");
    expect(control).not.toHaveAttribute("aria-invalid");
    expect(control).not.toHaveAttribute("aria-describedby");
    expect(control).toHaveTextContent("Off");
    expect(control.querySelector("[data-checked='false']")).toHaveAttribute("aria-hidden", "true");
  });

  it("shows On and the thumb at the end when checked, with custom state words", () => {
    const { rerender } = render(<Switch label="Mirror" checked />);
    const control = screen.getByRole("switch", { name: "Mirror" });
    expect(control).toHaveAttribute("aria-checked", "true");
    expect(control).toHaveTextContent("On");
    expect(control.querySelector("[data-checked='true']")).not.toBeNull();
    rerender(<Switch label="Mirror" checked onLabel="Mirrored" offLabel="As seen" />);
    expect(control).toHaveTextContent("Mirrored");
  });

  it("flips on click, which Space and Enter trigger on a native button", () => {
    function Controlled() {
      const [checked, setChecked] = useState(false);
      return <Switch label="Guest gallery" checked={checked} onCheckedChange={setChecked} />;
    }
    render(<Controlled />);
    const control = screen.getByRole("switch", { name: "Guest gallery" });
    fireEvent.click(control);
    expect(control).toHaveAttribute("aria-checked", "true");
    fireEvent.click(control);
    expect(control).toHaveAttribute("aria-checked", "false");
  });

  it("calls onClick first and lets it cancel the change", () => {
    const onCheckedChange = vi.fn();
    const onClick = vi.fn((event: { preventDefault: () => void }) => {
      event.preventDefault();
    });
    render(<Switch label="Mirror" checked={false} onClick={onClick} onCheckedChange={onCheckedChange} />);
    fireEvent.click(screen.getByRole("switch", { name: "Mirror" }));
    expect(onClick).toHaveBeenCalledOnce();
    expect(onCheckedChange).not.toHaveBeenCalled();
  });

  it("flips without an onCheckedChange handler without throwing", () => {
    render(<Switch label="Mirror" checked={false} />);
    expect(() => fireEvent.click(screen.getByRole("switch", { name: "Mirror" }))).not.toThrow();
  });

  it("stays focusable but ignores activation while disabled", () => {
    const onCheckedChange = vi.fn();
    const onClick = vi.fn();
    render(<Switch label="Mirror" checked disabled onClick={onClick} onCheckedChange={onCheckedChange} />);
    const control = screen.getByRole("switch", { name: "Mirror" });
    expect(control).toHaveAttribute("aria-disabled", "true");
    control.focus();
    expect(control).toHaveFocus();
    fireEvent.click(control);
    expect(onClick).not.toHaveBeenCalled();
    expect(onCheckedChange).not.toHaveBeenCalled();
  });

  it("is described by its error and help, and passes through the ref, id, and class", () => {
    const ref = createRef<HTMLButtonElement>();
    render(
      <>
        <p id="x">More</p>
        <Switch
          ref={ref}
          id="gallery"
          className="extra"
          label="Guest gallery"
          checked
          help="Turn this on only with the Customer's permission."
          error="Confirm the Customer's permission first."
          aria-describedby="x"
        />
      </>,
    );
    const control = screen.getByRole("switch", { name: "Guest gallery" });
    expect(ref.current).toBe(control);
    expect(control).toHaveAttribute("id", "gallery");
    expect(control).toHaveAttribute("aria-invalid", "true");
    expect(control).toHaveAccessibleDescription(
      "Confirm the Customer's permission first. Turn this on only with the Customer's permission. More",
    );
    expect(control.closest(".extra")).not.toBeNull();
  });

  it("can keep its label for assistive technology only, when a visible label beside it already names it", () => {
    render(<Switch label="Template timer" hideLabel checked onCheckedChange={vi.fn()} />);
    const control = screen.getByRole("switch", { name: "Template timer" });
    expect(control).toHaveAttribute("aria-checked", "true");
    expect(screen.getByText("Template timer").parentElement?.className).toContain("visuallyHidden");
  });
});
