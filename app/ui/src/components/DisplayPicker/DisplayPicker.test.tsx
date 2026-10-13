import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { COVERS_OPERATOR_NOTE, DisplayPicker, NOT_CONNECTED_NOTE, type DisplayPickerDisplay } from "./DisplayPicker.tsx";

const operator: DisplayPickerDisplay = {
  id: "op",
  name: "Lenovo ThinkPad display",
  width: 1920,
  height: 1080,
  position: "Primary",
  isOperatorScreen: true,
};
const guest: DisplayPickerDisplay = { id: "guest", name: "DELL P2422H", width: 2560, height: 1440, position: "Right of the Operator Screen" };

function Picker({ initial = null }: { readonly initial?: string | null }) {
  const [value, setValue] = useState<string | null>(initial);
  return <DisplayPicker displays={[operator, guest]} value={value} onChange={setValue} onIdentify={() => undefined} />;
}

describe("DisplayPicker", () => {
  it("lists the displays as radio rows named Guest display, with resolution, position, and the Operator Screen marker", () => {
    render(<Picker />);
    expect(screen.getByRole("group", { name: "Guest display" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Lenovo ThinkPad display" })).toHaveAccessibleDescription(
      "Operator Screen 1920 × 1080 · Primary",
    );
    expect(screen.getByRole("radio", { name: "DELL P2422H" })).toHaveAccessibleDescription("2560 × 1440 · Right of the Operator Screen");
    expect(screen.getAllByText("Operator Screen")).toHaveLength(1);
  });

  it("chooses a display by click or arrow key", () => {
    render(<Picker />);
    fireEvent.click(screen.getByText("DELL P2422H"));
    const dell = screen.getByRole("radio", { name: "DELL P2422H" });
    expect(dell).toBeChecked();
    fireEvent.keyDown(dell, { key: "ArrowUp" });
    expect(screen.getByRole("radio", { name: "Lenovo ThinkPad display" })).toBeChecked();
  });

  it("shows the D38 note, politely, only while the Operator Screen's display is chosen", () => {
    render(<Picker initial="guest" />);
    const status = screen.getByRole("status");
    expect(status).toBeEmptyDOMElement();
    fireEvent.click(screen.getByText("Lenovo ThinkPad display"));
    expect(status).toHaveTextContent(COVERS_OPERATOR_NOTE);
    expect(COVERS_OPERATOR_NOTE).toBe("The Guest Screen will cover this screen. Use the hold and PIN to return.");
    fireEvent.click(screen.getByText("DELL P2422H"));
    expect(status).toBeEmptyDOMElement();
  });

  it("keeps a bound display that isn't connected as a chosen, unavailable row with the note", () => {
    const onChange = vi.fn();
    render(<DisplayPicker displays={[operator]} value="gone" boundDisplayName="DELL P2422H" onChange={onChange} />);
    const missing = screen.getByRole("radio", { name: "DELL P2422H" });
    expect(missing).toBeChecked();
    expect(missing).toBeDisabled();
    expect(missing).toHaveAccessibleDescription("Not connected");
    expect(screen.getByRole("status")).toHaveTextContent(NOT_CONNECTED_NOTE);
    expect(NOT_CONNECTED_NOTE).toBe("The chosen guest display isn't connected.");
    expect(screen.getByRole("radio", { name: "Lenovo ThinkPad display" }).tabIndex).toBe(0);
    fireEvent.click(screen.getByText("Lenovo ThinkPad display"));
    expect(onChange).toHaveBeenCalledWith("op");
  });

  it("names a missing display without a stored name", () => {
    render(<DisplayPicker displays={[guest]} value="gone" />);
    expect(screen.getByRole("radio", { name: "Chosen display" })).toBeChecked();
  });

  it("offers IDENTIFY DISPLAYS as a text action, unavailable on request", () => {
    const onIdentify = vi.fn();
    const { rerender } = render(<DisplayPicker displays={[operator, guest]} value={null} onIdentify={onIdentify} />);
    const identify = screen.getByRole("button", { name: "IDENTIFY DISPLAYS" });
    expect(identify).toHaveAttribute("data-fotohavn-action", "text");
    fireEvent.click(identify);
    expect(onIdentify).toHaveBeenCalledOnce();
    rerender(<DisplayPicker displays={[operator, guest]} value={null} onIdentify={onIdentify} identifyDisabled />);
    expect(identify).toHaveAttribute("aria-disabled", "true");
    fireEvent.click(identify);
    expect(onIdentify).toHaveBeenCalledOnce();
  });

  it("takes a custom label, help, error, id, and class", () => {
    render(
      <DisplayPicker
        displays={[guest]}
        value={null}
        label="Second screen"
        help="Choose where guests look."
        error="Choose the display guests will use."
        id="display"
        className="extra"
      />,
    );
    const group = screen.getByRole("group", { name: "Second screen" });
    expect(group).toHaveAttribute("id", "display");
    expect(group).toHaveAccessibleDescription("Choose the display guests will use. Choose where guests look.");
    expect(group.closest(".extra")).not.toBeNull();
  });
});
