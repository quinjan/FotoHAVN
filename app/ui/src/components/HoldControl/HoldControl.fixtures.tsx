import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import { HoldControl, HoldControlView } from "./HoldControl.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "hold-control",
    title: "Exit hold, guest, at rest",
    surface: "light",
    role: "guest",
    render: () => <HoldControl onComplete={noop} />,
  },
  {
    id: "hold-control-focus",
    title: "Exit hold, keyboard focus",
    surface: "light",
    role: "guest",
    render: () => (
      <FocusFirst>
        <HoldControl onComplete={noop} />
      </FocusFirst>
    ),
  },
  {
    id: "hold-control-holding",
    title: "Exit hold, held for about a second",
    surface: "light",
    role: "guest",
    render: () => <HoldControlView phase="holding" progress={0.4} secondsLeft={2} />,
  },
  {
    id: "hold-control-dark",
    title: "Exit hold, on the dark Operator Assistance band",
    surface: "dark",
    role: "guest",
    render: () => <HoldControl onComplete={noop} />,
  },
  {
    id: "hold-control-dark-holding",
    title: "Exit hold, held, on dark",
    surface: "dark",
    role: "guest",
    render: () => <HoldControlView phase="holding" progress={0.8} secondsLeft={1} />,
  },
];
