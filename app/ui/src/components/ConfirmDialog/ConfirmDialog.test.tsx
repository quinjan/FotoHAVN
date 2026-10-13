import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ConfirmDialog } from "./ConfirmDialog.tsx";

function renderEndEvent() {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  render(
    <ConfirmDialog
      title="End this Event?"
      body="Anyone using the booth will be stopped. Their photographs are kept."
      confirmLabel="END EVENT"
      cancelLabel="KEEP RUNNING"
      onConfirm={onConfirm}
      onCancel={onCancel}
      className="extra"
    />,
  );
  return { onConfirm, onCancel, dialog: screen.getByRole("alertdialog", { name: "End this Event?" }) };
}

describe("ConfirmDialog", () => {
  it("is a modal alertdialog with its title, body, and one primary action", () => {
    const { dialog } = renderEndEvent();
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(dialog).toHaveAccessibleDescription("Anyone using the booth will be stopped. Their photographs are kept.");
    expect(dialog.className).toContain("extra");
    const confirm = within(dialog).getByRole("button", { name: "END EVENT" });
    const cancel = within(dialog).getByRole("button", { name: "KEEP RUNNING" });
    expect(confirm).toHaveAttribute("data-fotohavn-action", "primary");
    expect(cancel).toHaveAttribute("data-fotohavn-action", "secondary");
    expect(dialog.querySelectorAll('[data-fotohavn-action="primary"]')).toHaveLength(1);
  });

  it("opens with focus on the cancel action", () => {
    renderEndEvent();
    expect(screen.getByRole("button", { name: "KEEP RUNNING" })).toHaveFocus();
  });

  it("confirms and cancels through its actions, and cancels on Escape", () => {
    const { onConfirm, onCancel, dialog } = renderEndEvent();
    fireEvent.click(within(dialog).getByRole("button", { name: "END EVENT" }));
    expect(onConfirm).toHaveBeenCalledOnce();
    fireEvent.click(within(dialog).getByRole("button", { name: "KEEP RUNNING" }));
    expect(onCancel).toHaveBeenCalledOnce();
    fireEvent.keyDown(within(dialog).getByRole("button", { name: "KEEP RUNNING" }), { key: "Escape" });
    expect(onCancel).toHaveBeenCalledTimes(2);
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it("keeps Tab between its two actions", () => {
    const { dialog } = renderEndEvent();
    const confirm = within(dialog).getByRole("button", { name: "END EVENT" });
    const cancel = within(dialog).getByRole("button", { name: "KEEP RUNNING" });
    fireEvent.keyDown(cancel, { key: "Tab" });
    expect(confirm).toHaveFocus();
    fireEvent.keyDown(confirm, { key: "Tab", shiftKey: true });
    expect(cancel).toHaveFocus();
  });
});
