import type { ComponentFixture } from "../fixture.ts";
import type { FrameSource } from "./frameSource.ts";
import boxes from "./LivePreview.fixtures.module.css";
import { LivePreview } from "./LivePreview.tsx";
import { createPatternFrameSource } from "./patternFrameSource.ts";

/** A camera that has delivered nothing yet. */
const silent: FrameSource = { subscribe: () => () => undefined };

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "live-preview-waiting",
    title: "Live preview, no frames yet",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => <LivePreview source={silent} guideRatio={600 / 450} />,
  },
  {
    id: "live-preview-signature-strip",
    title: "Live preview, 16:9 frame, Signature Strip crop guide (4:3)",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => <LivePreview source={createPatternFrameSource(1920, 1080)} guideRatio={600 / 450} />,
  },
  {
    id: "live-preview-panorama-mirrored",
    title: "Live preview, mirrored, Panorama Four crop guide (3:4)",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => <LivePreview source={createPatternFrameSource(1920, 1080)} guideRatio={450 / 600} mirrored />,
  },
  {
    id: "live-preview-four-three-frame",
    title: "Live preview, 4:3 frame, Polka Keepsake crop guide",
    surface: "dark",
    role: "guest",
    layout: "fill",
    render: () => <LivePreview source={createPatternFrameSource(1280, 960)} guideRatio={513 / 390} />,
  },
  {
    id: "live-preview-operator",
    title: "Live preview in Event setup, no crop guide",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={boxes.setup}>
        <LivePreview source={createPatternFrameSource(1280, 720)} />
      </div>
    ),
  },
  {
    id: "live-preview-operator-waiting",
    title: "Live preview in Event setup, no frames yet",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={boxes.setup}>
        <LivePreview source={silent} />
      </div>
    ),
  },
];
