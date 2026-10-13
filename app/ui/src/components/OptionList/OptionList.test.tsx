import { fireEvent, render, screen, within } from "@testing-library/react";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { OptionList, type OptionListItem } from "./OptionList.tsx";

const templates: readonly OptionListItem[] = [
  { value: "signature", title: "Signature Strip", meta: "4 photographs", thumbnail: <img src="data:," alt="" /> },
  { value: "panorama", title: "Panorama Four", description: "Four side by side.", badge: "New" },
  { value: "butter", title: "Butter Gingham", disabled: true, description: "Not offered at this Event." },
  { value: "polka", title: "Polka Keepsake" },
];

function RadioList({ initial = "signature", items = templates }: { readonly initial?: string | null; readonly items?: readonly OptionListItem[] }) {
  const [value, setValue] = useState<string | null>(initial);
  return <OptionList variant="radio" label="Template" items={items} value={value} onChange={setValue} />;
}

function CheckboxList({
  initial = ["signature", "panorama"],
  initialDefault = "signature",
  withDefault = true,
}: {
  readonly initial?: readonly string[];
  readonly initialDefault?: string | null;
  readonly withDefault?: boolean;
}) {
  const [values, setValues] = useState<readonly string[]>(initial);
  const [defaultValue, setDefaultValue] = useState<string | null>(initialDefault);
  return (
    <OptionList
      variant="checkbox"
      label="Offered Templates"
      items={templates}
      values={values}
      onChange={setValues}
      defaultChoice={withDefault ? { value: defaultValue, onChange: setDefaultValue } : undefined}
    />
  );
}

function row(name: string): HTMLElement {
  const item = screen.getByRole("radio", { name }).closest("li");
  if (item === null) {
    throw new Error(`No row for ${name}`);
  }
  return item;
}

describe("OptionList, radio", () => {
  it("is a group named by its legend, with rows in a list", () => {
    render(<RadioList />);
    const group = screen.getByRole("group", { name: "Template" });
    expect(group.tagName).toBe("FIELDSET");
    expect(within(group).getAllByRole("listitem")).toHaveLength(4);
    expect(within(group).getAllByRole("radio")).toHaveLength(4);
  });

  it("names each radio by its title and describes it by its badge, description, and meta", () => {
    render(<RadioList />);
    expect(screen.getByRole("radio", { name: "Signature Strip" })).toHaveAccessibleDescription("4 photographs");
    expect(screen.getByRole("radio", { name: "Panorama Four" })).toHaveAccessibleDescription("New Four side by side.");
    expect(screen.getByRole("radio", { name: "Polka Keepsake" })).not.toHaveAttribute("aria-describedby");
  });

  it("marks the selected row and checks its radio, never by color alone", () => {
    render(<RadioList />);
    expect(screen.getByRole("radio", { name: "Signature Strip" })).toBeChecked();
    expect(row("Signature Strip")).toHaveAttribute("data-selected", "true");
    expect(row("Panorama Four")).not.toHaveAttribute("data-selected");
    const mark = screen.getByRole("radio", { name: "Signature Strip" }).parentElement?.querySelector("[data-kind='radio']");
    expect(mark).toHaveAttribute("aria-hidden", "true");
  });

  it("renders the thumbnail as decoration before the text", () => {
    render(<RadioList />);
    const thumbnail = row("Signature Strip").querySelector("img")?.parentElement;
    expect(thumbnail).toHaveAttribute("aria-hidden", "true");
  });

  it("has one tab stop on the selected row, or the first enabled row when none is selected", () => {
    const { unmount } = render(<RadioList initial="panorama" />);
    expect(screen.getAllByRole("radio").map((radio) => radio.tabIndex)).toEqual([-1, 0, -1, -1]);
    unmount();
    render(<RadioList initial={null} />);
    expect(screen.getAllByRole("radio").map((radio) => radio.tabIndex)).toEqual([0, -1, -1, -1]);
  });

  it("moves between rows with the arrow keys, selecting, skipping disabled rows, and wrapping", () => {
    render(<RadioList initial="panorama" />);
    const panorama = screen.getByRole("radio", { name: "Panorama Four" });
    panorama.focus();
    fireEvent.keyDown(panorama, { key: "ArrowDown" });
    const polka = screen.getByRole("radio", { name: "Polka Keepsake" });
    expect(polka).toHaveFocus();
    expect(polka).toBeChecked();
    expect(row("Polka Keepsake")).toHaveAttribute("data-selected", "true");
    fireEvent.keyDown(polka, { key: "ArrowRight" });
    const signature = screen.getByRole("radio", { name: "Signature Strip" });
    expect(signature).toHaveFocus();
    fireEvent.keyDown(signature, { key: "ArrowUp" });
    expect(polka).toHaveFocus();
    fireEvent.keyDown(polka, { key: "ArrowLeft" });
    expect(panorama).toHaveFocus();
    expect(panorama).toBeChecked();
  });

  it("selects on click and shows a disabled row as unavailable", () => {
    const onChange = vi.fn();
    render(<OptionList variant="radio" label="Template" items={templates} value={null} onChange={onChange} />);
    fireEvent.click(screen.getByText("Polka Keepsake"));
    expect(onChange).toHaveBeenCalledWith("polka");
    expect(screen.getByRole("radio", { name: "Butter Gingham" })).toBeDisabled();
    expect(row("Butter Gingham")).toHaveAttribute("data-disabled", "true");
  });

  it("works without an onChange handler", () => {
    render(<OptionList variant="radio" label="Template" items={templates} value="signature" />);
    const signature = screen.getByRole("radio", { name: "Signature Strip" });
    expect(() => fireEvent.keyDown(signature, { key: "ArrowDown" })).not.toThrow();
    expect(() => fireEvent.click(screen.getByText("Polka Keepsake"))).not.toThrow();
  });

  it("can hide its legend visually and keep it as the group's name", () => {
    render(<OptionList variant="radio" label="Look" hideLabel items={templates} value={null} id="looks" className="extra" />);
    const group = screen.getByRole("group", { name: "Look" });
    expect(group).toHaveAttribute("id", "looks");
    expect(group).toHaveClass("extra");
    expect(screen.getByText("Look").tagName).toBe("LEGEND");
  });

  it("describes the group with its error and help, without aria-invalid on the radios", () => {
    render(<OptionList variant="radio" label="Template" items={templates} value={null} help="Pick one." error="Choose a Template." />);
    expect(screen.getByRole("group", { name: "Template" })).toHaveAccessibleDescription("Choose a Template. Pick one.");
    expect(screen.getByRole("radio", { name: "Signature Strip" })).not.toHaveAttribute("aria-invalid");
  });
});

describe("OptionList, checkbox", () => {
  it("makes each row a checkbox, checked for the given values, with each one tabbable", () => {
    render(<CheckboxList withDefault={false} />);
    const boxes = screen.getAllByRole("checkbox");
    expect(boxes).toHaveLength(4);
    expect(boxes.map((box) => (box as HTMLInputElement).checked)).toEqual([true, true, false, false]);
    expect(boxes.every((box) => !box.hasAttribute("tabindex"))).toBe(true);
    expect(screen.queryAllByRole("radio")).toHaveLength(0);
    expect(row2("Signature Strip")).toHaveAttribute("data-selected", "true");
  });

  it("toggles rows and reports the checked values in the items' order", () => {
    const onChange = vi.fn();
    render(<OptionList variant="checkbox" label="Offered" items={templates} values={["polka", "signature"]} onChange={onChange} />);
    fireEvent.click(screen.getByRole("checkbox", { name: "Panorama Four" }));
    expect(onChange).toHaveBeenLastCalledWith(["signature", "panorama", "polka"]);
    fireEvent.click(screen.getByRole("checkbox", { name: "Signature Strip" }));
    expect(onChange).toHaveBeenLastCalledWith(["polka"]);
  });

  it("toggles without an onChange handler", () => {
    render(<OptionList variant="checkbox" label="Offered" items={templates} values={[]} />);
    expect(() => fireEvent.click(screen.getByRole("checkbox", { name: "Panorama Four" }))).not.toThrow();
  });

  it("moves focus between rows with the up and down arrows and Home and End, without toggling", () => {
    const onChange = vi.fn();
    render(<OptionList variant="checkbox" label="Offered" items={templates} values={[]} onChange={onChange} />);
    const [signature, panorama, , polka] = screen.getAllByRole("checkbox") as [HTMLElement, HTMLElement, HTMLElement, HTMLElement];
    signature.focus();
    fireEvent.keyDown(signature, { key: "ArrowDown" });
    expect(panorama).toHaveFocus();
    fireEvent.keyDown(panorama, { key: "ArrowDown" });
    expect(polka).toHaveFocus();
    fireEvent.keyDown(polka, { key: "ArrowDown" });
    expect(polka).toHaveFocus();
    fireEvent.keyDown(polka, { key: "Home" });
    expect(signature).toHaveFocus();
    fireEvent.keyDown(signature, { key: "ArrowUp" });
    expect(signature).toHaveFocus();
    fireEvent.keyDown(signature, { key: "End" });
    expect(polka).toHaveFocus();
    fireEvent.keyDown(polka, { key: "ArrowUp" });
    expect(panorama).toHaveFocus();
    const right = fireEvent.keyDown(panorama, { key: "ArrowRight" });
    expect(right).toBe(true);
    expect(panorama).toHaveFocus();
    expect(onChange).not.toHaveBeenCalled();
  });

  it("puts aria-invalid on the checkboxes and describes the group when there's an error", () => {
    render(<OptionList variant="checkbox" label="Offered" items={templates} values={[]} error="Offer at least one Template." />);
    expect(screen.getByRole("group", { name: "Offered" })).toHaveAccessibleDescription("Offer at least one Template.");
    for (const box of screen.getAllByRole("checkbox")) {
      expect(box).toHaveAttribute("aria-invalid", "true");
    }
  });
});

function row2(name: string): HTMLElement {
  const item = screen.getByRole("checkbox", { name }).closest("li");
  if (item === null) {
    throw new Error(`No row for ${name}`);
  }
  return item;
}

describe("OptionList, checkbox with a default radio", () => {
  it("adds a Default radio to each row, named with the row's title, enabled only on checked rows", () => {
    render(<CheckboxList />);
    const defaults = screen.getAllByRole("radio");
    expect(defaults).toHaveLength(4);
    expect(screen.getByRole("radio", { name: "Default Signature Strip" })).toBeChecked();
    expect(screen.getByRole("radio", { name: "Default Panorama Four" })).toBeEnabled();
    expect(screen.getByRole("radio", { name: "Default Butter Gingham" })).toBeDisabled();
    expect(screen.getByRole("radio", { name: "Default Polka Keepsake" })).toBeDisabled();
    expect(defaults.map((radio) => radio.tabIndex)).toEqual([0, -1, -1, -1]);
    expect(new Set(defaults.map((radio) => radio.getAttribute("name"))).size).toBe(1);
  });

  it("chooses the default by click, and enables a row's default once it's offered", () => {
    render(<CheckboxList />);
    fireEvent.click(screen.getByRole("radio", { name: "Default Panorama Four" }));
    expect(screen.getByRole("radio", { name: "Default Panorama Four" })).toBeChecked();
    fireEvent.click(screen.getByRole("checkbox", { name: "Polka Keepsake" }));
    expect(screen.getByRole("radio", { name: "Default Polka Keepsake" })).toBeEnabled();
  });

  it("moves the default with the arrow keys among offered rows only", () => {
    render(<CheckboxList initial={["signature", "panorama", "polka"]} />);
    const signature = screen.getByRole("radio", { name: "Default Signature Strip" });
    signature.focus();
    fireEvent.keyDown(signature, { key: "ArrowDown" });
    const panorama = screen.getByRole("radio", { name: "Default Panorama Four" });
    expect(panorama).toHaveFocus();
    expect(panorama).toBeChecked();
    fireEvent.keyDown(panorama, { key: "ArrowDown" });
    const polka = screen.getByRole("radio", { name: "Default Polka Keepsake" });
    expect(polka).toHaveFocus();
    expect(polka).toBeChecked();
    fireEvent.keyDown(polka, { key: "ArrowDown" });
    expect(signature).toHaveFocus();
  });

  it("puts the default tab stop on the first offered row when no default is chosen, and uses a custom label", () => {
    render(
      <OptionList
        variant="checkbox"
        label="Offered"
        items={templates}
        values={["panorama"]}
        defaultChoice={{ value: null, label: "First" }}
      />,
    );
    expect(screen.getAllByRole("radio").map((radio) => radio.tabIndex)).toEqual([-1, 0, -1, -1]);
    const panorama = screen.getByRole("radio", { name: "First Panorama Four" });
    expect(() => fireEvent.click(panorama)).not.toThrow();
    expect(() => fireEvent.keyDown(panorama, { key: "ArrowDown" })).not.toThrow();
  });
});
