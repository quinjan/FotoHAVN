import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Select } from "./Select.tsx";

const cameras = [
  { value: "a", label: "Logitech BRIO" },
  { value: "b", label: "Integrated Camera" },
  { value: "c", label: "Elgato Facecam", disabled: true },
];

describe("Select", () => {
  it("is a native select labelled from above with its options", () => {
    const onChange = vi.fn();
    render(<Select label="Camera" options={cameras} value="a" onChange={onChange} />);
    const select = screen.getByRole("combobox", { name: "Camera" });
    const options = within(select).getAllByRole("option");
    expect(options.map((option) => option.textContent)).toEqual(["Logitech BRIO", "Integrated Camera", "Elgato Facecam"]);
    expect(options[2]).toBeDisabled();
    expect(select).toHaveValue("a");
    expect(select).not.toHaveAttribute("aria-invalid");
    fireEvent.change(select, { target: { value: "b" } });
    expect(onChange).toHaveBeenCalledOnce();
  });

  it("draws a decorative chevron in place of the native arrow", () => {
    render(<Select label="Camera" options={cameras} defaultValue="a" />);
    const chevron = screen.getByRole("combobox", { name: "Camera" }).nextElementSibling;
    expect(chevron).toHaveAttribute("aria-hidden", "true");
    expect(chevron?.querySelector("svg")).not.toBeNull();
  });

  it("leads with an unselectable placeholder while nothing is chosen", () => {
    render(<Select label="Camera" options={cameras} placeholder="Choose a camera" value="" onChange={() => undefined} required />);
    const select = screen.getByRole("combobox", { name: "Camera" });
    const placeholder = within(select).getByRole("option", { name: "Choose a camera" });
    expect(placeholder).toHaveValue("");
    expect(placeholder).toBeDisabled();
    expect(placeholder).toHaveAttribute("data-placeholder");
    expect(within(select).getByRole("option", { name: "Logitech BRIO" })).not.toHaveAttribute("data-placeholder");
    expect(select).toBeRequired();
    expect(screen.getByText("Required")).toBeInTheDocument();
  });

  it("shows help and an error in text with aria-invalid, and passes through the id and class", () => {
    render(
      <>
        <p id="x">More</p>
        <Select
          label="Camera"
          id="camera"
          className="extra"
          options={cameras}
          help="Ready"
          error="Choose a camera for the booth."
          aria-describedby="x"
        />
      </>,
    );
    const select = screen.getByRole("combobox", { name: "Camera" });
    expect(select).toHaveAttribute("id", "camera");
    expect(select).toHaveAttribute("aria-invalid", "true");
    expect(select).toHaveAccessibleDescription("Choose a camera for the booth. Ready More");
    expect(select.closest(".extra")).not.toBeNull();
  });

  it("is disabled on request", () => {
    render(<Select label="Camera" options={cameras} disabled />);
    expect(screen.getByRole("combobox", { name: "Camera" })).toBeDisabled();
  });
});
