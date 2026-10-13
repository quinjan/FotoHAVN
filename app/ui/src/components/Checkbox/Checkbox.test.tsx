import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Checkbox } from "./Checkbox.tsx";

describe("Checkbox", () => {
  it("is a native checkbox named by its label, with a decorative mark", () => {
    render(<Checkbox label="Offer Naturale" />);
    const box = screen.getByRole("checkbox", { name: "Offer Naturale" });
    expect(box).not.toBeChecked();
    expect(box).not.toHaveAttribute("aria-invalid");
    expect(box).not.toHaveAttribute("aria-describedby");
    const mark = box.nextElementSibling;
    expect(mark).toHaveAttribute("aria-hidden", "true");
    expect(mark).toHaveAttribute("data-kind", "checkbox");
    expect(mark?.querySelector("svg")).not.toBeNull();
  });

  it("toggles with a click on the label and reports the change", () => {
    const onChange = vi.fn();
    render(<Checkbox label="Offer Sepia" onChange={onChange} />);
    fireEvent.click(screen.getByText("Offer Sepia"));
    expect(onChange).toHaveBeenCalledOnce();
    expect(screen.getByRole("checkbox", { name: "Offer Sepia" })).toBeChecked();
  });

  it("reflects a controlled checked state", () => {
    render(<Checkbox label="Offer Classic" checked onChange={() => undefined} />);
    expect(screen.getByRole("checkbox", { name: "Offer Classic" })).toBeChecked();
  });

  it("describes the choice with its description, error, help, and the caller's ids", () => {
    render(
      <>
        <p id="x">More</p>
        <Checkbox
          label="Offer Naturale"
          id="naturale"
          className="extra"
          description="Original colors. No filter."
          help="Guests see offered Looks."
          error="Offer at least one Look."
          aria-describedby="x"
        />
      </>,
    );
    const box = screen.getByRole("checkbox", { name: "Offer Naturale" });
    expect(box).toHaveAttribute("id", "naturale");
    expect(box).toHaveAttribute("aria-invalid", "true");
    expect(box).toHaveAccessibleDescription("Offer at least one Look. Guests see offered Looks. Original colors. No filter. More");
    expect(box.closest(".extra")).not.toBeNull();
  });

  it("is natively disabled on request", () => {
    render(<Checkbox label="Offer Classic" disabled checked onChange={() => undefined} />);
    const box = screen.getByRole("checkbox", { name: "Offer Classic" });
    expect(box).toBeDisabled();
    expect(box).toBeChecked();
  });
});
