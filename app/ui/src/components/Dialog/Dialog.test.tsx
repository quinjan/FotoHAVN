import { fireEvent, render, screen } from "@testing-library/react";
import { createRef, useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { SegmentedChoice } from "../SegmentedChoice/SegmentedChoice.tsx";
import { Dialog } from "./Dialog.tsx";

function Opener({ onCancel = vi.fn() }: { readonly onCancel?: () => void }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        onClick={() => {
          setOpen(true);
        }}
      >
        OPEN
      </button>
      {open && (
        <Dialog
          title="Save your changes?"
          onCancel={() => {
            onCancel();
            setOpen(false);
          }}
          actions={
            <button
              type="button"
              onClick={() => {
                setOpen(false);
              }}
            >
              CLOSE
            </button>
          }
        />
      )}
    </>
  );
}

describe("Dialog", () => {
  it("is a modal dialog labelled by its title and described by its description", () => {
    render(<Dialog title="Save your changes?" description="This Event has changes that aren't saved." onCancel={vi.fn()} />);
    const dialog = screen.getByRole("dialog", { name: "Save your changes?" });
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(dialog).toHaveAccessibleDescription("This Event has changes that aren't saved.");
    expect(dialog).toHaveAttribute("data-tone", "light");
    expect(dialog).toHaveAttribute("data-size", "narrow");
    expect(screen.getByRole("heading", { level: 2, name: "Save your changes?" })).toBeInTheDocument();
  });

  it("has no description reference without a description, and takes the alertdialog role and wide size", () => {
    render(
      <Dialog title="Operator PIN" role="alertdialog" size="wide" onCancel={vi.fn()}>
        <p>Body</p>
      </Dialog>,
    );
    const dialog = screen.getByRole("alertdialog", { name: "Operator PIN" });
    expect(dialog).not.toHaveAttribute("aria-describedby");
    expect(dialog).toHaveAttribute("data-size", "wide");
    expect(screen.getByText("Body")).toBeInTheDocument();
  });

  it("focuses the first control on open, or the initialFocus element", () => {
    const { unmount } = render(
      <Dialog
        title="Title"
        onCancel={vi.fn()}
        actions={
          <>
            <button type="button">FIRST</button>
            <button type="button">SECOND</button>
          </>
        }
      />,
    );
    expect(screen.getByRole("button", { name: "FIRST" })).toHaveFocus();
    unmount();

    const second = createRef<HTMLButtonElement>();
    render(
      <Dialog
        title="Title"
        onCancel={vi.fn()}
        initialFocus={second}
        actions={
          <>
            <button type="button">FIRST</button>
            <button type="button" ref={second}>
              SECOND
            </button>
          </>
        }
      />,
    );
    expect(screen.getByRole("button", { name: "SECOND" })).toHaveFocus();
  });

  it("focuses the panel when it holds no control, and keeps Tab there", () => {
    render(<Dialog title="Title" description="Nothing to press." onCancel={vi.fn()} />);
    const dialog = screen.getByRole("dialog");
    expect(dialog).toHaveFocus();
    expect(fireEvent.keyDown(dialog, { key: "Tab" })).toBe(false);
    expect(dialog).toHaveFocus();
  });

  it("traps Tab and Shift+Tab inside", () => {
    render(
      <Dialog
        title="Title"
        onCancel={vi.fn()}
        actions={
          <>
            <button type="button">FIRST</button>
            <button type="button">MIDDLE</button>
            <button type="button">LAST</button>
          </>
        }
      />,
    );
    const first = screen.getByRole("button", { name: "FIRST" });
    const middle = screen.getByRole("button", { name: "MIDDLE" });
    const last = screen.getByRole("button", { name: "LAST" });

    expect(fireEvent.keyDown(first, { key: "Tab", shiftKey: true })).toBe(false);
    expect(last).toHaveFocus();
    expect(fireEvent.keyDown(last, { key: "Tab" })).toBe(false);
    expect(first).toHaveFocus();

    // Between the ends, the browser moves focus as usual.
    middle.focus();
    expect(fireEvent.keyDown(middle, { key: "Tab" })).toBe(true);
    expect(fireEvent.keyDown(middle, { key: "Tab", shiftKey: true })).toBe(true);
    expect(middle).toHaveFocus();

    // From the panel itself, Shift+Tab goes to the last control.
    const dialog = screen.getByRole("dialog");
    dialog.focus();
    expect(fireEvent.keyDown(dialog, { key: "Tab", shiftKey: true })).toBe(false);
    expect(last).toHaveFocus();
  });

  it("cancels on Escape and ignores other keys", () => {
    const onCancel = vi.fn();
    render(<Dialog title="Title" onCancel={onCancel} actions={<button type="button">OK</button>} />);
    const button = screen.getByRole("button", { name: "OK" });
    fireEvent.keyDown(button, { key: "Enter" });
    expect(onCancel).not.toHaveBeenCalled();
    expect(fireEvent.keyDown(button, { key: "Escape" })).toBe(false);
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it("returns focus to the opener when it closes, by action or Escape", () => {
    const onCancel = vi.fn();
    render(<Opener onCancel={onCancel} />);
    const opener = screen.getByRole("button", { name: "OPEN" });
    opener.focus();
    fireEvent.click(opener);
    const close = screen.getByRole("button", { name: "CLOSE" });
    expect(close).toHaveFocus();
    fireEvent.click(close);
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(opener).toHaveFocus();

    fireEvent.click(opener);
    fireEvent.keyDown(screen.getByRole("button", { name: "CLOSE" }), { key: "Escape" });
    expect(onCancel).toHaveBeenCalledOnce();
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(opener).toHaveFocus();
  });

  it("doesn't try to refocus an opener that has left the document", () => {
    const outside = document.createElement("button");
    document.body.append(outside);
    outside.focus();
    const { unmount } = render(<Dialog title="Title" onCancel={vi.fn()} />);
    outside.remove();
    unmount();
    expect(document.activeElement).toBe(document.body);
  });
});

describe("Dialog focus trap", () => {
  const options = [
    { value: "3", label: "3 s" },
    { value: "5", label: "5 s" },
    { value: "10", label: "10 s" },
  ];

  function choice(value: string | null) {
    return <SegmentedChoice label="Countdown" options={options} value={value} />;
  }

  it("keeps focus on the lone tab stop when the only content is a SegmentedChoice", () => {
    render(
      <Dialog title="Title" onCancel={vi.fn()}>
        {choice("5")}
      </Dialog>,
    );
    // The chosen radio is the group's only tab stop; the other two sit at tabindex -1.
    const radios = screen.getAllByRole("radio");
    const stop = screen.getByRole("radio", { name: "5 s" });
    expect(radios.map((radio) => radio.tabIndex)).toEqual([-1, 0, -1]);
    expect(stop).toHaveFocus();

    // The dialog's only tab stop is both its first and its last stop, so both directions stay on it.
    expect(fireEvent.keyDown(stop, { key: "Tab" })).toBe(false);
    expect(stop).toHaveFocus();
    expect(fireEvent.keyDown(stop, { key: "Tab", shiftKey: true })).toBe(false);
    expect(stop).toHaveFocus();
  });

  it("wraps over a leading SegmentedChoice and trailing actions, never landing on a tabindex -1 radio", () => {
    render(
      <Dialog
        title="Title"
        onCancel={vi.fn()}
        actions={
          <>
            <button type="button">CANCEL</button>
            <button type="button">SAVE</button>
          </>
        }
      >
        {choice("5")}
      </Dialog>,
    );
    const stop = screen.getByRole("radio", { name: "5 s" });
    const save = screen.getByRole("button", { name: "SAVE" });
    expect(stop).toHaveFocus();

    expect(fireEvent.keyDown(stop, { key: "Tab", shiftKey: true })).toBe(false);
    expect(save).toHaveFocus();
    expect(fireEvent.keyDown(save, { key: "Tab" })).toBe(false);
    expect(stop).toHaveFocus();
  });

  it("wraps with a SegmentedChoice as the last content, from the first control to the real last stop and back", () => {
    render(
      <Dialog title="Title" onCancel={vi.fn()}>
        <button type="button">FIRST</button>
        {choice(null)}
      </Dialog>,
    );
    const first = screen.getByRole("button", { name: "FIRST" });
    const firstRadio = screen.getByRole("radio", { name: "3 s" });
    expect(screen.getAllByRole("radio").map((radio) => radio.tabIndex)).toEqual([0, -1, -1]);

    expect(first).toHaveFocus();

    expect(fireEvent.keyDown(first, { key: "Tab", shiftKey: true })).toBe(false);
    expect(firstRadio).toHaveFocus();
    expect(fireEvent.keyDown(firstRadio, { key: "Tab" })).toBe(false);
    expect(first).toHaveFocus();
    expect(screen.getByRole("dialog")).toContainElement(document.activeElement as HTMLElement);
  });

  it("never lets focus leave the dialog while Tab keeps pressing through every stop", () => {
    render(
      <>
        <button type="button">OUTSIDE</button>
        <Dialog title="Title" onCancel={vi.fn()} actions={<button type="button">OK</button>}>
          <button type="button">FIRST</button>
          {choice("10")}
        </Dialog>
      </>,
    );
    const dialog = screen.getByRole("dialog");
    const order = ["FIRST", "10 s", "OK"];
    const stops = order.map((name) => screen.getByRole(name === "10 s" ? "radio" : "button", { name }));
    for (let step = 0; step < 6; step += 1) {
      const active = document.activeElement as HTMLElement;
      const index = stops.indexOf(active);
      expect(index).toBeGreaterThanOrEqual(0);
      const proceed = fireEvent.keyDown(active, { key: "Tab" });
      if (!proceed) {
        // The trap wrapped; otherwise the browser would move to the next stop, which the test does by hand.
        expect(index).toBe(stops.length - 1);
      } else {
        stops[index + 1]?.focus();
      }
      expect(dialog).toContainElement(document.activeElement as HTMLElement);
    }
  });

  it("skips disabled, hidden, inert, display:none, and fieldset-disabled controls, and tabindex -1 and hidden inputs", () => {
    render(
      <Dialog
        title="Title"
        onCancel={vi.fn()}
        actions={
          <>
            <button type="button" disabled>
              DISABLED FIRST
            </button>
            <button type="button">REAL FIRST</button>
            <button type="button" tabIndex={-1}>
              PROGRAMMATIC
            </button>
            <button type="button" hidden>
              HIDDEN
            </button>
            <input type="hidden" name="secret" />
            <div inert>
              <button type="button">INERT</button>
            </div>
            <div style={{ display: "none" }}>
              <button type="button">NOT RENDERED</button>
            </div>
            <div hidden>
              <a href="#x">HIDDEN LINK</a>
            </div>
            <fieldset disabled>
              <input aria-label="fieldset disabled" />
            </fieldset>
            <button type="button" style={{ visibility: "hidden" }}>
              INVISIBLE
            </button>
            <button type="button">REAL LAST</button>
            <button type="button" disabled>
              DISABLED LAST
            </button>
          </>
        }
      />,
    );
    const first = screen.getByRole("button", { name: "REAL FIRST" });
    const last = screen.getByRole("button", { name: "REAL LAST" });
    expect(first).toHaveFocus();

    expect(fireEvent.keyDown(first, { key: "Tab", shiftKey: true })).toBe(false);
    expect(last).toHaveFocus();
    expect(fireEvent.keyDown(last, { key: "Tab" })).toBe(false);
    expect(first).toHaveFocus();
  });

  it("follows the browser for native radio groups: only the checked radio, or the first when none is checked, is a tab stop", () => {
    render(
      <Dialog title="Title" onCancel={vi.fn()}>
        <input type="radio" name="g" aria-label="one" defaultChecked />
        <input type="radio" name="g" aria-label="two" />
        <input type="radio" name="loose" aria-label="loose one" />
        <input type="radio" name="loose" aria-label="loose two" />
        <input type="radio" name="off" aria-label="off one" disabled />
        <input type="radio" name="off" aria-label="off two" />
        <input type="radio" aria-label="unnamed" />
      </Dialog>,
    );
    // Stops: one (checked), loose one (first of an unchecked group), off two (first enabled), unnamed.
    // The first and last of them are one and unnamed.
    const one = screen.getByRole("radio", { name: "one" });
    const unnamed = screen.getByRole("radio", { name: "unnamed" });
    expect(one).toHaveFocus();
    expect(fireEvent.keyDown(one, { key: "Tab", shiftKey: true })).toBe(false);
    expect(unnamed).toHaveFocus();
    expect(fireEvent.keyDown(unnamed, { key: "Tab" })).toBe(false);
    expect(one).toHaveFocus();
    fireEvent.keyDown(one, { key: "Tab" });
    // Not the second radio of the checked group.
    expect(screen.getByRole("radio", { name: "two" })).not.toHaveFocus();
  });
});