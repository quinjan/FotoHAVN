import type { ReactNode } from "react";
import type { ComponentFixture } from "../fixture.ts";
import { LivePreview } from "../LivePreview/LivePreview.tsx";
import { createPatternFrameSource } from "../LivePreview/patternFrameSource.ts";
import { OverlayFrame } from "../shared/fixtureOverlayFrame.tsx";
import { CaptureFlash } from "./CaptureFlash.tsx";

const testPattern = createPatternFrameSource(1920, 1080);

/** What the flash covers: a test-pattern preview, so a frame with no flash and a frame under one differ in the picture. */
function PreviewStage({ children }: { readonly children: ReactNode }) {
  return (
    <OverlayFrame stage>
      <LivePreview source={testPattern} guideRatio={600 / 450} />
      {children}
    </OverlayFrame>
  );
}

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "capture-flash-idle",
    title: "Capture flash, at rest over the live preview",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => (
      <PreviewStage>
        <CaptureFlash flashKey="capture-1" />
      </PreviewStage>
    ),
  },
  {
    id: "capture-flash-peak",
    title: "Capture flash, at its brightest, over the live preview",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => (
      <PreviewStage>
        <CaptureFlash peak />
      </PreviewStage>
    ),
  },
];
