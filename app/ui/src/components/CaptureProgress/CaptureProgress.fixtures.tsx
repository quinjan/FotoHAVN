import type { ComponentFixture } from "../fixture.ts";
import { CaptureProgress } from "./CaptureProgress.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "capture-progress-none",
    title: "Capture progress, none taken, on the dark stage",
    surface: "dark",
    role: "guest",
    render: () => <CaptureProgress taken={0} total={4} />,
  },
  {
    id: "capture-progress-partial",
    title: "Capture progress, 2 of 4",
    surface: "dark",
    role: "guest",
    render: () => <CaptureProgress taken={2} total={4} />,
  },
  {
    id: "capture-progress-complete",
    title: "Capture progress, all 6 windows",
    surface: "dark",
    role: "guest",
    render: () => <CaptureProgress taken={6} total={6} />,
  },
  {
    id: "capture-progress-light",
    title: "Capture progress, 1 of 3, on a light surface",
    surface: "light",
    role: "guest",
    render: () => <CaptureProgress taken={1} total={3} />,
  },
];
