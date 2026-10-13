import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Radio } from "./Radio.tsx";

describe("Radio", () => {
  it("is a native radio in a named group, named by its label, with a round mark", () => {
    render(
      <fieldset>
        <legend>Sort Events by</legend>
        <Radio name="sort" value="date" label="Date, newest first" defaultChecked />
        <Radio name="sort" value="used" label="Last used" />
      </fieldset>,
    );
    const group = screen.getByRole("group", { name: "Sort Events by" });
    expect(group).toBeInTheDocument();
    const date = screen.getByRole("radio", { name: "Date, newest first" });
    expect(date).toBeChecked();
    expect(date).toHaveAttribute("name", "sort");
    expect(date).toHaveAttribute("value", "date");
    expect(date).not.toHaveAttribute("aria-describedby");
    expect(date.nextElementSibling).toHaveAttribute("data-kind", "radio");
    expect(date.nextElementSibling).toHaveAttribute("aria-hidden", "true");
  });

  it("selects with a click and reports the change", () => {
    const onChange = vi.fn();
    render(<Radio name="sort" value="used" label="Last used" onChange={onChange} />);
    fireEvent.click(screen.getByText("Last used"));
    expect(onChange).toHaveBeenCalledOnce();
    expect(screen.getByRole("radio", { name: "Last used" })).toBeChecked();
  });

  it("is described by its description and the caller's ids, and passes through id and class", () => {
    render(
      <>
        <p id="x">More</p>
        <Radio
          name="sort"
          value="used"
          id="used"
          className="extra"
          label="Last used"
          description="The most recently run Events come first."
          aria-describedby="x"
        />
      </>,
    );
    const radio = screen.getByRole("radio", { name: "Last used" });
    expect(radio).toHaveAttribute("id", "used");
    expect(radio).toHaveAccessibleDescription("The most recently run Events come first. More");
    expect(radio.closest(".extra")).not.toBeNull();
  });

  it("is disabled on request", () => {
    render(<Radio name="sort" value="used" label="Last used" disabled />);
    expect(screen.getByRole("radio", { name: "Last used" })).toBeDisabled();
  });
});
