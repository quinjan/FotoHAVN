import type { ComponentFixture } from "../fixture.ts";
import { OverlayFrame } from "../shared/fixtureOverlayFrame.tsx";
import { CountdownNumeral } from "./CountdownNumeral.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "countdown-numeral-5",
    title: "Countdown numeral, 5, over the dark stage",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => (
      <OverlayFrame stage>
        <CountdownNumeral value={5} />
      </OverlayFrame>
    ),
  },
  {
    id: "countdown-numeral-10",
    title: "Countdown numeral, 10, the widest",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => (
      <OverlayFrame stage>
        <CountdownNumeral value={10} />
      </OverlayFrame>
    ),
  },
  {
    id: "countdown-numeral-1",
    title: "Countdown numeral, 1",
    surface: "dark",
    role: "guest",
    render: () => <CountdownNumeral value={1} />,
  },
  {
    id: "countdown-numeral-1-cue",
    title: "Countdown numeral, the last second, with its cue",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => (
      <OverlayFrame stage>
        <CountdownNumeral value={1} cue="Look at the camera." />
      </OverlayFrame>
    ),
  },
];
