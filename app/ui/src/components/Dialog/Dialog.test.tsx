import { fireEvent, render, screen } from "@testing-library/react";
import { createRef, useState } from "react";
import { describe, expect, it, vi } from "vitest";
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
