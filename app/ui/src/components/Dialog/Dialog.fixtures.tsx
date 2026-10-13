import type { ComponentFixture } from "../fixture.ts";
import { PrimaryAction } from "../PrimaryAction/index.ts";
import { SecondaryAction } from "../SecondaryAction/index.ts";
import { TextLink } from "../TextLink/index.ts";
import { noop } from "../shared/fixtureHelpers.tsx";
import { OverlayFrame } from "../shared/fixtureOverlayFrame.tsx";
import { Dialog } from "./Dialog.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "dialog-operator",
    title: "Dialog, operator, leaving Event setup with changes",
    surface: "light",
    role: "operator",
    layout: "fill",
    render: () => (
      <OverlayFrame>
        <Dialog
          title="Save your changes?"
          description="This Event has changes that aren't saved yet."
          onCancel={noop}
          actions={
            <>
              <PrimaryAction onClick={noop}>SAVE</PrimaryAction>
              <SecondaryAction onClick={noop}>DISCARD CHANGES</SecondaryAction>
              <TextLink onClick={noop}>KEEP EDITING</TextLink>
            </>
          }
        />
      </OverlayFrame>
    ),
  },
  {
    id: "dialog-guest",
    title: "Dialog, guest, over the Photographs stage",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => (
      <OverlayFrame stage>
        <Dialog
          title="Start over?"
          description="Your photographs stay with the Event, but no strip will be made."
          onCancel={noop}
          actions={
            <>
              <PrimaryAction onClick={noop}>START OVER</PrimaryAction>
              <SecondaryAction onClick={noop}>KEEP GOING</SecondaryAction>
            </>
          }
        />
      </OverlayFrame>
    ),
  },
  {
    id: "dialog-operator-long",
    title: "Dialog, operator, longest copy over a dark surface",
    surface: "dark",
    role: "operator",
    layout: "fill",
    render: () => (
      <OverlayFrame>
        <Dialog
          title="Delete this Guest Cycle and every photograph in it?"
          description="Its Captures and Photo Strip leave both views of the Event Gallery at once, and the files leave this computer. This can't be undone."
          onCancel={noop}
          actions={
            <>
              <PrimaryAction onClick={noop}>DELETE</PrimaryAction>
              <SecondaryAction onClick={noop}>KEEP IT</SecondaryAction>
            </>
          }
        />
      </OverlayFrame>
    ),
  },
];
