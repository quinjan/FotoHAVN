import type { ComponentFixture } from "../fixture.ts";
import { noop } from "../shared/fixtureHelpers.tsx";
import { OverlayFrame } from "../shared/fixtureOverlayFrame.tsx";
import { ConfirmDialog } from "./ConfirmDialog.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "confirm-dialog-start-over",
    title: "Confirm dialog, guest, Start Over",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => (
      <OverlayFrame>
        <ConfirmDialog
          title="Start over?"
          body="Your photographs stay with the Event, but no strip will be made."
          confirmLabel="START OVER"
          cancelLabel="KEEP GOING"
          onConfirm={noop}
          onCancel={noop}
        />
      </OverlayFrame>
    ),
  },
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
    id: "confirm-dialog-guest-stage",
    title: "Confirm dialog, guest, over the dark Photographs stage",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => (
      <OverlayFrame stage>
        <ConfirmDialog
          title="Start over?"
          body="Your photographs stay with the Event, but no strip will be made."
          confirmLabel="START OVER"
          cancelLabel="KEEP GOING"
          onConfirm={noop}
          onCancel={noop}
        />
      </OverlayFrame>
    ),
  },
];
