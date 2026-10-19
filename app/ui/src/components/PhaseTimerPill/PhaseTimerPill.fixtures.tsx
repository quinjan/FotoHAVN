import type { ComponentFixture } from "../fixture.ts";
import { PhaseTimerPill } from "./PhaseTimerPill.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "phase-timer-pill-running",
    title: "Phase Timer, running",
    surface: "light",
    role: "guest",
    render: () => <PhaseTimerPill secondsRemaining={45} />,
  },
  {
    id: "phase-timer-pill-low",
    title: "Phase Timer, the last 10 seconds",
    surface: "light",
    role: "guest",
    render: () => <PhaseTimerPill secondsRemaining={8} />,
  },
  {
    id: "phase-timer-pill-paused",
    title: "Phase Timer, paused by the brain",
    surface: "light",
    role: "guest",
    render: () => <PhaseTimerPill secondsRemaining={32} paused />,
  },
  {
    id: "phase-timer-pill-minutes",
    title: "Phase Timer, over a minute left",
    surface: "light",
    role: "guest",
    render: () => <PhaseTimerPill secondsRemaining={125} />,
  },
];
