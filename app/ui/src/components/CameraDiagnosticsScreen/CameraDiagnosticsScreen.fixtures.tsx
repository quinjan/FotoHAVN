import type { CameraDiagnosticsView, CameraFailureKind } from "../../protocol/generated.ts";
import type { ComponentFixture } from "../fixture.ts";
import { fixturePreviewUrl } from "../../fixtures/previewImages.ts";
import { CompositionControls } from "../../operator/CompositionControls.tsx";
import { createPatternFrameSource } from "../LivePreview/index.ts";
import { noop } from "../shared/fixtureHelpers.tsx";
import { CameraDiagnosticsScreen, type CameraDiagnosticsControls } from "./CameraDiagnosticsScreen.tsx";

const integrated = { deviceId: "\\\\?\\USB#VID_046D&PID_085E#{e5323777}\\GLOBAL", name: "Integrated Webcam" };
const fake = { deviceId: "fake-camera", name: "Fake camera" };

const ready: CameraDiagnosticsView = {
  cameras: [integrated, fake],
  selectedDeviceId: integrated.deviceId,
  state: "ready",
  reason: null,
  format: { width: 1920, height: 1080, fps: 30 },
  mirrored: false,
  lastCapture: null,
  lastComposition: null,
};

const source = createPatternFrameSource(1280, 720);
const steady = { fps: 30, ageMs: 40 };
const controls: CameraDiagnosticsControls = { onSelect: noop, onMirrorChange: noop, onCapture: noop };
const compositionControls = (
  <CompositionControls
    templateId="signature-strip"
    lookId="naturale-v1"
    onTemplateChange={noop}
    onLookChange={noop}
    onCompose={noop}
    onBlankPreview={noop}
  />
);

function fixture(
  id: string,
  title: string,
  view: CameraDiagnosticsView,
  extra: Partial<Parameters<typeof CameraDiagnosticsScreen>[0]> = {},
): ComponentFixture {
  return {
    id: `camera-diagnostics-screen-${id}`,
    title: `Camera diagnostics, ${title}`,
    surface: "light",
    role: "operator",
    layout: "fill",
    render: () => <CameraDiagnosticsScreen view={view} source={source} measured={steady} {...extra} />,
  };
}

function unavailable(reason: CameraFailureKind): CameraDiagnosticsView {
  return { ...ready, state: "notAvailable", reason, format: null };
}

const kebab = (kind: string): string => kind.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`);

const reasons = ["stale", "failed", "removed", "exclusiveLost", "privacyDenied", "noSupportedFormat", "notFound", "unknown"] as const;

export const fixtures: readonly ComponentFixture[] = [
  fixture("ready", "ready, read-only", ready),
  fixture(
    "ready-controls",
    "ready, mirrored, with controls and a capture",
    {
      ...ready,
      mirrored: true,
      lastCapture: { fileName: "capture-20261014-071910-123.jpg", width: 1920, height: 1080, byteLength: 412_345, mirrored: true },
    },
    { controls, host: { deliveredFps: 29.6, droppedFrames: 2, frameAgeMs: 31 } },
  ),
  fixture(
    "composition-portrait",
    "ready, with the composition controls and a Signature Strip composed",
    {
      ...ready,
      lastCapture: { fileName: "capture-20261014-071910-123.jpg", width: 1920, height: 1080, byteLength: 412_345, mirrored: false },
      lastComposition: { previewKey: "signature-strip.sepia-v1.r2", templateId: "signature-strip", lookId: "sepia-v1", width: 600, height: 1800 },
    },
    { controls: { ...controls, extra: compositionControls }, previewUrl: fixturePreviewUrl },
  ),
  fixture(
    "composition-landscape",
    "ready, Panorama Four composed and turned a quarter counterclockwise, read-only",
    {
      ...ready,
      lastComposition: { previewKey: "panorama-four.classic-v1.r4", templateId: "panorama-four", lookId: "classic-v1", width: 1800, height: 600 },
    },
    { previewUrl: fixturePreviewUrl },
  ),
  fixture("checking", "checking", { ...ready, state: "checking", format: null }, { measured: { fps: null, ageMs: null } }),
  fixture("idle", "no camera chosen", { ...ready, selectedDeviceId: null, state: "idle", format: null }, { measured: { fps: null, ageMs: null } }),
  ...reasons.map((reason) =>
    fixture(`not-available-${kebab(reason)}`, `not available, ${reason}`, unavailable(reason), { measured: { fps: 0, ageMs: 6_400 } }),
  ),
  fixture(
    "in-use",
    "in use by another app",
    { ...ready, state: "inUseByAnotherApp", reason: "inUseByAnotherApp", format: null },
    { measured: { fps: null, ageMs: null } },
  ),
  fixture(
    "no-cameras",
    "no cameras",
    { ...ready, cameras: [], selectedDeviceId: null, state: "idle", format: null },
    { measured: { fps: null, ageMs: null } },
  ),
  fixture(
    "controls-unavailable",
    "with controls, not ready, with a notice",
    unavailable("privacyDenied"),
    { controls: { ...controls, notice: "That didn't work. Try again." }, measured: { fps: null, ageMs: null } },
  ),
];
