import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import { OverlayFrame } from "../shared/fixtureOverlayFrame.tsx";
import { StartOverAction } from "./StartOverAction.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "start-over-action",
    title: "Start over action, guest top band",
    surface: "light",
    role: "guest",
    render: () => <StartOverAction step="template" onConfirm={noop} onKeepGoing={noop} />,
  },
  {
    id: "start-over-action-focus",
    title: "Start over action, keyboard focus",
    surface: "light",
    role: "guest",
    render: () => (
      <FocusFirst>
        <StartOverAction step="filters" onConfirm={noop} onKeepGoing={noop} />
      </FocusFirst>
    ),
  },
  {
    id: "start-over-action-confirming",
    title: "Start over action, confirmation open",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => (
      <OverlayFrame>
        <StartOverAction step="photographs" confirming onConfirm={noop} onKeepGoing={noop} />
      </OverlayFrame>
    ),
  },
];
