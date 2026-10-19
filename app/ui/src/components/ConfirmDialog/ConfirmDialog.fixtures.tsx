import type { ComponentFixture } from "../fixture.ts";
import { noop } from "../shared/fixtureHelpers.tsx";
import { OverlayFrame } from "../shared/fixtureOverlayFrame.tsx";
import { ConfirmDialog } from "./ConfirmDialog.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "confirm-dialog-end-event",
    title: "Confirm dialog, operator, End Event",
    surface: "light",
    role: "operator",
    layout: "fill",
    render: () => (
      <OverlayFrame>
        <ConfirmDialog
          title="End this Event?"
          body="Anyone using the booth will be stopped. Their photographs are kept."
          confirmLabel="END EVENT"
          cancelLabel="KEEP RUNNING"
          onConfirm={noop}
          onCancel={noop}
        />
      </OverlayFrame>
    ),
  },
  {
    id: "confirm-dialog-delete-event",
    title: "Confirm dialog, operator, Delete Event",
    surface: "light",
    role: "operator",
    layout: "fill",
    render: () => (
      <OverlayFrame>
        <ConfirmDialog
          title="Delete this Event?"
          body="“Harbour gala” will be removed with 12 Guest Cycles, 10 Photo Strips, and 46 photographs. This can't be undone."
          confirmLabel="DELETE EVENT"
          cancelLabel="KEEP EVENT"
          onConfirm={noop}
          onCancel={noop}
        />
      </OverlayFrame>
    ),
  },
];