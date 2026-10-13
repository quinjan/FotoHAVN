import type { ReactNode } from "react";
import { Dialog } from "../Dialog/index.ts";
import type { ComponentFixture } from "../fixture.ts";
import { noop } from "../shared/fixtureHelpers.tsx";
import { OverlayFrame } from "../shared/fixtureOverlayFrame.tsx";
import { PinPad } from "./PinPad.tsx";

function PinDialog({ children }: { readonly children: ReactNode }) {
  return (
    <OverlayFrame>
      <Dialog size="wide" title="Operator PIN" description="Enter the Event's Operator PIN to end the Event." onCancel={noop}>
        {children}
      </Dialog>
    </OverlayFrame>
  );
}

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "pin-pad-empty",
    title: "PIN pad in its dialog, nothing entered, focus on the first key",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => (
      <PinDialog>
        <PinPad onSubmit={noop} onCancel={noop} />
      </PinDialog>
    ),
  },
  {
    id: "pin-pad-entering",
    title: "PIN pad, four digits entered and masked",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => (
      <PinDialog>
        <PinPad onSubmit={noop} onCancel={noop} defaultValue="2580" />
      </PinDialog>
    ),
  },
  {
    id: "pin-pad-full",
    title: "PIN pad, six digits entered, digit keys unavailable",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => (
      <PinDialog>
        <PinPad onSubmit={noop} onCancel={noop} defaultValue="258014" />
      </PinDialog>
    ),
  },
  {
    id: "pin-pad-wrong",
    title: "PIN pad, wrong PIN",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => (
      <PinDialog>
        <PinPad onSubmit={noop} onCancel={noop} wrongAttempts={2} />
      </PinDialog>
    ),
  },
  {
    id: "pin-pad-locked",
    title: "PIN pad, locked with its countdown",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => (
      <PinDialog>
        <PinPad onSubmit={noop} onCancel={noop} wrongAttempts={5} lockedSeconds={28} />
      </PinDialog>
    ),
  },
  {
    id: "pin-pad-over-dark",
    title: "PIN pad dialog over the dark Operator Assistance band",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => (
      <PinDialog>
        <PinPad onSubmit={noop} onCancel={noop} defaultValue="25" />
      </PinDialog>
    ),
  },
];
