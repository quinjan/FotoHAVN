import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { SegmentedChoice, type SegmentedChoiceOption } from "./SegmentedChoice.tsx";

const countdown: readonly SegmentedChoiceOption[] = [
  { value: "3", label: "3 s", accessibleLabel: "3 seconds" },
  { value: "5", label: "5 s", accessibleLabel: "5 seconds" },
  { value: "10", label: "10 s", accessibleLabel: "10 seconds" },
];

function Countdown({ initial = "5", options = countdown }: { readonly initial?: string | null; readonly options?: readonly SegmentedChoiceOption[] }) {
  const [value, setValue] = useState<string | null>(initial);
  return <SegmentedChoice label="Countdown" options={options} value={value} onChange={setValue} />;
}

describe("SegmentedChoice", () => {
  it("is a radio group named by its legend, with the chosen value checked", () => {
    render(<Countdown />);
    const group = screen.getByRole("group", { name: "Countdown" });
    expect(group.tagName).toBe("FIELDSET");
    expect(group).not.toHaveAttribute("aria-describedby");
    const radios = screen.getAllByRole("radio");
    expect(radios.map((radio) => radio.getAttribute("aria-label"))).toEqual(["3 seconds", "5 seconds", "10 seconds"]);
    expect(screen.getByRole("radio", { name: "5 seconds" })).toBeChecked();
    expect(new Set(radios.map((radio) => radio.getAttribute("name"))).size).toBe(1);
    expect(screen.getByText("5 s")).toBeInTheDocument();
  });

  it("has one tab stop: the chosen value", () => {
    render(<Countdown />);
    expect(screen.getAllByRole("radio").map((radio) => radio.tabIndex)).toEqual([-1, 0, -1]);
  });

  it("puts the tab stop on the first value while nothing is chosen", () => {
    render(<Countdown initial={null} />);
    expect(screen.getAllByRole("radio").map((radio) => radio.tabIndex)).toEqual([0, -1, -1]);
    expect(screen.getAllByRole("radio").every((radio) => !(radio as HTMLInputElement).checked)).toBe(true);
  });

  it("moves to and selects the next and previous value with the arrow keys, wrapping", () => {
    render(<Countdown />);
    const five = screen.getByRole("radio", { name: "5 seconds" });
    five.focus();
    fireEvent.keyDown(five, { key: "ArrowRight" });
    const ten = screen.getByRole("radio", { name: "10 seconds" });
    expect(ten).toHaveFocus();
    expect(ten).toBeChecked();
    expect(ten.tabIndex).toBe(0);
    fireEvent.keyDown(ten, { key: "ArrowDown" });
    const three = screen.getByRole("radio", { name: "3 seconds" });
    expect(three).toHaveFocus();
    expect(three).toBeChecked();
    fireEvent.keyDown(three, { key: "ArrowLeft" });
    expect(ten).toHaveFocus();
    fireEvent.keyDown(ten, { key: "ArrowUp" });
    expect(five).toHaveFocus();
    expect(five).toBeChecked();
  });

  it("skips a disabled value and leaves other keys alone", () => {
    render(<Countdown options={countdown.map((option) => (option.value === "10" ? { ...option, disabled: true } : option))} />);
    const five = screen.getByRole("radio", { name: "5 seconds" });
    expect(screen.getByRole("radio", { name: "10 seconds" })).toBeDisabled();
    five.focus();
    fireEvent.keyDown(five, { key: "ArrowRight" });
    expect(screen.getByRole("radio", { name: "3 seconds" })).toHaveFocus();
    const three = screen.getByRole("radio", { name: "3 seconds" });
    const tab = fireEvent.keyDown(three, { key: "Tab" });
    expect(tab).toBe(true);
    expect(three).toHaveFocus();
  });

  it("stays put when the only enabled value is the current one", () => {
    const onChange = vi.fn();
    render(
      <SegmentedChoice
        label="Countdown"
        options={[{ value: "3", label: "3 s", accessibleLabel: "3 seconds" }, { value: "5", label: "5 s", disabled: true }]}
        value="3"
        onChange={onChange}
      />,
    );
    const three = screen.getByRole("radio", { name: "3 seconds" });
    fireEvent.keyDown(three, { key: "ArrowRight" });
    expect(onChange).not.toHaveBeenCalled();
  });

  it("selects on click", () => {
    const onChange = vi.fn();
    render(<SegmentedChoice label="Countdown" options={countdown} value="5" onChange={onChange} />);
    fireEvent.click(screen.getByText("10 s"));
    expect(onChange).toHaveBeenCalledWith("10");
  });

  it("works without an onChange handler", () => {
    render(<SegmentedChoice label="Countdown" options={countdown} value="5" name="countdown" />);
    const five = screen.getByRole("radio", { name: "5 seconds" });
    expect(five).toHaveAttribute("name", "countdown");
    expect(() => fireEvent.keyDown(five, { key: "ArrowRight" })).not.toThrow();
    expect(() => fireEvent.click(screen.getByText("3 s"))).not.toThrow();
  });

  it("describes the group with its error and help, and disables the whole group", () => {
    render(
      <SegmentedChoice
        label="Countdown"
        id="countdown"
        className="extra"
        options={countdown}
        value={null}
        disabled
        help="Before each Capture."
        error="Choose a countdown."
      />,
    );
    const group = screen.getByRole("group", { name: "Countdown" });
    expect(group).toHaveAttribute("id", "countdown");
    expect(group).toHaveClass("extra");
    expect(group).toBeDisabled();
    expect(group).toHaveAccessibleDescription("Choose a countdown. Before each Capture.");
    for (const radio of screen.getAllByRole("radio")) {
      expect(radio).toBeDisabled();
    }
  });

  it("shows a check on every segment, visible only on the chosen one through CSS", () => {
    render(<Countdown />);
    const labels = screen.getAllByRole("radio").map((radio) => radio.closest("label"));
    for (const label of labels) {
      expect(label?.querySelector("[aria-hidden='true'] svg")).not.toBeNull();
    }
  });
});
