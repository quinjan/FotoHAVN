import { fireEvent, render, screen } from "@testing-library/react";
import { createRef, useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { TextField } from "./TextField.tsx";

describe("TextField", () => {
  it("labels the input from above and passes native attributes", () => {
    const ref = createRef<HTMLInputElement>();
    render(<TextField label="Venue" ref={ref} name="venue" placeholder="Where" className="extra" />);
    const input = screen.getByRole("textbox", { name: "Venue" });
    expect(input).toHaveAttribute("type", "text");
    expect(input).toHaveAttribute("name", "venue");
    expect(input).toHaveAttribute("placeholder", "Where");
    expect(input).not.toHaveAttribute("aria-invalid");
    expect(input).not.toHaveAttribute("aria-describedby");
    expect(input).not.toBeRequired();
    expect(ref.current).toBe(input);
    expect(input.closest(".extra")).not.toBeNull();
    const label = screen.getByText("Venue");
    expect(label.tagName).toBe("LABEL");
    expect(label.compareDocumentPosition(input) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("uses the caller's id and type", () => {
    render(<TextField label="Email" id="email" type="email" />);
    const input = screen.getByRole("textbox", { name: "Email" });
    expect(input).toHaveAttribute("id", "email");
    expect(input).toHaveAttribute("type", "email");
  });

  it("marks a required field in text and with the native state", () => {
    render(<TextField label="Name" required />);
    const input = screen.getByRole("textbox", { name: "Name" });
    expect(input).toBeRequired();
    expect(screen.getByText("Required")).toHaveAttribute("aria-hidden", "true");
  });

  it("describes the field with its help", () => {
    render(<TextField label="Name" help="Shown in the Event list." />);
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveAccessibleDescription("Shown in the Event list.");
  });

  it("shows an error in text, sets aria-invalid, and puts the error first in the description", () => {
    render(<TextField label="Name" help="Shown in the Event list." error="Enter a name for the Event." />);
    const input = screen.getByRole("textbox", { name: "Name" });
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription("Enter a name for the Event. Shown in the Event list.");
    const error = screen.getByText("Enter a name for the Event.").closest("p");
    expect(error?.querySelector("[aria-hidden='true'] svg")).not.toBeNull();
  });

  it("ignores empty help and error", () => {
    render(<TextField label="Name" help="" error={false} />);
    const input = screen.getByRole("textbox", { name: "Name" });
    expect(input).not.toHaveAttribute("aria-invalid");
    expect(input).not.toHaveAttribute("aria-describedby");
  });

  it("limits entry with maxLength and counts a controlled value", () => {
    render(<TextField label="Name" maxLength={80} value="Santos wedding" onChange={() => undefined} />);
    const input = screen.getByRole("textbox", { name: "Name" });
    expect(input).toHaveAttribute("maxlength", "80");
    expect(screen.getByText("14 / 80")).toHaveAttribute("aria-hidden", "true");
    expect(input).toHaveAccessibleDescription("14 of 80 characters");
  });

  it("counts as the operator types into an uncontrolled field and still calls onChange", () => {
    const onChange = vi.fn();
    render(<TextField label="Venue" maxLength={120} defaultValue="Manila" onChange={onChange} />);
    expect(screen.getByText("6 / 120")).toBeInTheDocument();
    fireEvent.change(screen.getByRole("textbox", { name: "Venue" }), { target: { value: "Manila Hotel" } });
    expect(screen.getByText("12 / 120")).toBeInTheDocument();
    expect(onChange).toHaveBeenCalledOnce();
  });

  it("counts from zero without a default and follows a controlled value", () => {
    function Controlled() {
      const [value, setValue] = useState("");
      return (
        <TextField
          label="Venue"
          maxLength={120}
          value={value}
          onChange={(event) => {
            setValue(event.target.value);
          }}
        />
      );
    }
    render(<Controlled />);
    expect(screen.getByText("0 / 120")).toBeInTheDocument();
    fireEvent.change(screen.getByRole("textbox", { name: "Venue" }), { target: { value: "Hall" } });
    expect(screen.getByText("4 / 120")).toBeInTheDocument();
  });

  it("counts an uncontrolled field with no default from zero", () => {
    render(<TextField label="Venue" maxLength={120} />);
    expect(screen.getByText("0 / 120")).toBeInTheDocument();
  });

  it("marks a value over the limit and hides the counter on request", () => {
    const { rerender } = render(<TextField label="Name" maxLength={4} value="Santos" onChange={() => undefined} />);
    expect(screen.getByText("6 / 4").closest("p")).toHaveAttribute("data-over", "true");
    rerender(<TextField label="Name" maxLength={4} value="Sa" showCount={false} onChange={() => undefined} />);
    expect(screen.queryByText("2 / 4")).toBeNull();
  });

  it("joins the caller's aria-describedby after its own", () => {
    render(
      <>
        <p id="extra">Extra</p>
        <TextField label="Name" help="Help" aria-describedby="extra" />
      </>,
    );
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveAccessibleDescription("Help Extra");
  });

  it("shows a read-only fixed value and a disabled field", () => {
    render(
      <>
        <TextField label="Printer" readOnly value="No printer" />
        <TextField label="Venue" disabled value="Hall" help="End the running Event to change this." />
      </>,
    );
    expect(screen.getByRole("textbox", { name: "Printer" })).toHaveAttribute("readonly");
    expect(screen.getByRole("textbox", { name: "Printer" })).toHaveValue("No printer");
    expect(screen.getByRole("textbox", { name: "Venue" })).toBeDisabled();
  });

  it("writes a unit after the box without making it part of the name, and can keep the label for assistive technology only", () => {
    render(<TextField label="Template seconds" hideLabel suffix="s" invalid value="45" onChange={vi.fn()} />);
    const input = screen.getByRole("textbox", { name: "Template seconds" });
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByText("s")).toHaveAttribute("aria-hidden", "true");
    expect(input.parentElement).toContainElement(screen.getByText("s"));
    expect(screen.getByText("Template seconds").closest("div")?.className).toContain("visuallyHidden");
  });
});
