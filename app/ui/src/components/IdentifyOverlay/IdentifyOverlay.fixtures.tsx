import type { ComponentFixture } from "../fixture.ts";
import { IdentifyOverlay } from "./IdentifyOverlay.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "identify-overlay",
    title: "Identify overlay, on the Operator Screen",
    surface: "light",
    role: "operator",
    layout: "fill",
    render: () => <IdentifyOverlay number={1} name="Built-in display" />,
  },
  {
    id: "identify-overlay-over-dark",
    title: "Identify overlay over a dark desktop",
    surface: "dark",
    role: "operator",
    layout: "fill",
    render: () => <IdentifyOverlay number={2} name="DELL U2720Q" />,
  },
  {
    id: "identify-overlay-long-name",
    title: "Identify overlay, longest realistic display name",
    surface: "light",
    role: "operator",
    layout: "fill",
    render: () => <IdentifyOverlay number={12} name="Generic PnP Monitor on DisplayPort 3 (ASUS ProArt PA32UCG-K)" />,
  },
];
