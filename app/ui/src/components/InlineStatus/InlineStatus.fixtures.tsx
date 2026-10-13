import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import layout from "../shared/fixtureLayout.module.css";
import { TextLink } from "../TextLink/index.ts";
import { InlineStatus } from "./InlineStatus.tsx";

// The Operator Assistance link label is written in M6 (DESIGN.md, Open items); the fixtures use a stand-in.
const backToPanel = (
  <TextLink size="meta" arrow="right" onClick={noop}>
    SEE THE RUNNING EVENT
  </TextLink>
);

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "inline-status-neutral",
    title: "Inline status, neutral, operator",
    surface: "light",
    role: "operator",
    render: () => <InlineStatus>Checking the camera.</InlineStatus>,
  },
  {
    id: "inline-status-alert",
    title: "Inline status, alert, operator",
    surface: "light",
    role: "operator",
    render: () => <InlineStatus tone="alert">Camera access is off in Windows privacy settings.</InlineStatus>,
  },
  {
    id: "inline-status-assistance",
    title: "Inline status, Operator Assistance alert with the link back to the running-Event panel",
    surface: "light",
    role: "operator",
    render: () => (
      <InlineStatus tone="alert" action={backToPanel}>
        Operator Assistance: the guest display is disconnected.
      </InlineStatus>
    ),
  },
  {
    id: "inline-status-assistance-focus",
    title: "Inline status, Operator Assistance alert, keyboard focus on the link",
    surface: "light",
    role: "operator",
    render: () => (
      <FocusFirst>
        <InlineStatus tone="alert" action={backToPanel}>
          Operator Assistance: the camera stopped sending pictures.
        </InlineStatus>
      </FocusFirst>
    ),
  },
  {
    id: "inline-status-long",
    title: "Inline status, alert, longest realistic reason",
    surface: "light",
    role: "operator",
    render: () => (
      <InlineStatus tone="alert" action={backToPanel}>
        Operator Assistance: the Event folder can&apos;t be written. Check that the drive is connected and has space,
        then retry from the running Event.
      </InlineStatus>
    ),
  },
  {
    id: "inline-status-guest",
    title: "Inline status, alert, guest",
    surface: "light",
    role: "guest",
    render: () => <InlineStatus tone="alert">Your strip couldn&apos;t be prepared. Try again.</InlineStatus>,
  },
  {
    id: "inline-status-guest-neutral",
    title: "Inline status, neutral, guest",
    surface: "light",
    role: "guest",
    render: () => <InlineStatus>Saving your photographs.</InlineStatus>,
  },
  {
    id: "inline-status-dark",
    title: "Inline status on dark, neutral and alert",
    surface: "dark",
    role: "guest",
    render: () => (
      <div className={layout.stack}>
        <InlineStatus>Saving your photographs.</InlineStatus>
        <InlineStatus tone="alert">Your strip couldn&apos;t be prepared. Try again.</InlineStatus>
      </div>
    ),
  },
];
