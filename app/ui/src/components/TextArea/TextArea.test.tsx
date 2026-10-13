import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { TextArea } from "./TextArea.tsx";

describe("TextArea", () => {
  it("is a labelled multi-line field with four rows by default", () => {
    render(<TextArea label="Notes" name="notes" />);
    const area = screen.getByRole("textbox", { name: "Notes" });
    expect(area.tagName).toBe("TEXTAREA");
    expect(area).toHaveAttribute("rows", "4");
    expect(area).toHaveAttribute("name", "notes");
    expect(area).not.toHaveAttribute("aria-describedby");
  });

  it("counts toward its 1,000-character limit as the operator types", () => {
    const onChange = vi.fn();
    render(<TextArea label="Notes" maxLength={1000} rows={6} onChange={onChange} />);
    const area = screen.getByRole("textbox", { name: "Notes" });
    expect(area).toHaveAttribute("rows", "6");
    expect(area).toHaveAttribute("maxlength", "1000");
    expect(screen.getByText("0 / 1000")).toBeInTheDocument();
    fireEvent.change(area, { target: { value: "Left of the stage." } });
    expect(screen.getByText("18 / 1000")).toBeInTheDocument();
    expect(onChange).toHaveBeenCalledOnce();
  });

  it("counts a controlled value and a default value", () => {
    const { rerender } = render(<TextArea label="Notes" maxLength={1000} value="Hall" onChange={() => undefined} />);
    expect(screen.getByText("4 / 1000")).toBeInTheDocument();
    rerender(<TextArea key="b" label="Notes" maxLength={1000} defaultValue="Stage left" />);
    expect(screen.getByText("10 / 1000")).toBeInTheDocument();
  });

  it("hides the counter on request", () => {
    render(<TextArea label="Notes" maxLength={1000} showCount={false} />);
    expect(screen.queryByText("0 / 1000")).toBeNull();
  });

  it("shows help, a required mark, and an error in text with aria-invalid", () => {
    render(
      <>
        <p id="more">More</p>
        <TextArea label="Notes" required help="For the team only." error="Too long." aria-describedby="more" />
      </>,
    );
    const area = screen.getByRole("textbox", { name: "Notes" });
    expect(area).toBeRequired();
    expect(area).toHaveAttribute("aria-invalid", "true");
    expect(area).toHaveAccessibleDescription("Too long. For the team only. More");
    expect(screen.getByText("Required")).toBeInTheDocument();
  });

  it("is disabled or read-only on request", () => {
    render(
      <>
        <TextArea label="A" disabled />
        <TextArea label="B" readOnly value="Fixed" />
      </>,
    );
    expect(screen.getByRole("textbox", { name: "A" })).toBeDisabled();
    expect(screen.getByRole("textbox", { name: "B" })).toHaveAttribute("readonly");
  });
});
