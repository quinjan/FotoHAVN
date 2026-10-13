import type { ComponentFixture } from "../fixture.ts";
import { OverlayFrame } from "../shared/fixtureOverlayFrame.tsx";
import { CaptureFlash } from "./CaptureFlash.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "capture-flash-idle",
    title: "Capture flash, at rest over the dark stage",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => (
      <OverlayFrame stage>
        <CaptureFlash flashKey="capture-1" />
      </OverlayFrame>
    ),
  },
  {
    id: "capture-flash-peak",
    title: "Capture flash, at its brightest",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => (
      <OverlayFrame stage>
        <CaptureFlash peak />
      </OverlayFrame>
    ),
  },
];
