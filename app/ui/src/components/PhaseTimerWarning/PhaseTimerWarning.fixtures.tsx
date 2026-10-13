import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import { PhaseTimerWarning } from "./PhaseTimerWarning.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "phase-timer-warning-start",
    title: "Phase Timer warning, as it appears with 20 seconds left",
    surface: "light",
    role: "guest",
    render: () => <PhaseTimerWarning phase="template" secondsRemaining={20} onMoreTime={noop} arrive={false} />,
  },
  {
    id: "phase-timer-warning-last-second",
    title: "Phase Timer warning, last second",
    surface: "light",
    role: "guest",
    render: () => <PhaseTimerWarning phase="filters" secondsRemaining={1} onMoreTime={noop} arrive={false} />,
  },
  {
    id: "phase-timer-warning-focus",
    title: "Phase Timer warning, keyboard focus on MORE TIME",
    surface: "light",
    role: "guest",
    render: () => (
      <FocusFirst>
        <PhaseTimerWarning phase="guest-gallery" secondsRemaining={12} onMoreTime={noop} arrive={false} />
      </FocusFirst>
    ),
  },
  {
    id: "phase-timer-warning-over-dark",
    title: "Phase Timer warning over the dark Photographs stage",
    surface: "dark",
    role: "guest",
    render: () => <PhaseTimerWarning phase="photographs-capture" secondsRemaining={8} onMoreTime={noop} arrive={false} />,
  },
];
