import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DateField } from "./DateField.tsx";

describe("DateField", () => {
  it("is a native date input labelled from above", () => {
    const onChange = vi.fn();
    render(<DateField label="Date" value="2026-10-17" min="2026-01-01" max="2027-12-31" onChange={onChange} />);
    const input = screen.getByLabelText("Date");
    expect(input).toHaveAttribute("type", "date");
    expect(input).toHaveValue("2026-10-17");
    expect(input).toHaveAttribute("min", "2026-01-01");
    expect(input).toHaveAttribute("max", "2027-12-31");
    expect(input).not.toHaveAttribute("aria-invalid");
    expect(input).not.toHaveAttribute("aria-describedby");
    fireEvent.change(input, { target: { value: "2026-10-18" } });
    expect(onChange).toHaveBeenCalledOnce();
  });

  it("marks required, and shows help and an error in text with aria-invalid", () => {
    render(<DateField label="Date" id="when" required help="The day the booth runs." error="Choose the Event's date." />);
    const input = screen.getByLabelText("Date");
    expect(input).toHaveAttribute("id", "when");
    expect(input).toBeRequired();
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription("Choose the Event's date. The day the booth runs.");
    expect(screen.getByText("Required")).toBeInTheDocument();
  });

  it("is disabled on request and joins the caller's description", () => {
    render(
      <>
        <p id="why">Why</p>
        <DateField label="Date" disabled className="extra" aria-describedby="why" />
      </>,
    );
    const input = screen.getByLabelText("Date");
    expect(input).toBeDisabled();
    expect(input).toHaveAccessibleDescription("Why");
    expect(input.closest(".extra")).not.toBeNull();
  });
});
