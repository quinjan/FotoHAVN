import type { CameraDiagnosticsView, CameraFailureKind, CameraState } from "../../protocol/generated.ts";

/**
 * Why a camera isn't available, in plain words for the operator (DESIGN.md, Copy: Camera field). The privacy line is
 * the approved copy (Q16), exactly.
 */
export const cameraReasonCopy: Readonly<Record<CameraFailureKind, string>> = {
  stale: "The camera stopped sending pictures.",
  failed: "The camera stopped working.",
  removed: "The camera was disconnected.",
  exclusiveLost: "Another app took over the camera.",
  inUseByAnotherApp: "Another app is using this camera.",
  privacyDenied: "Camera access is off in Windows privacy settings.",
  noSupportedFormat: "This camera has no picture size FOTOHAVN can use.",
  notFound: "FOTOHAVN can't find this camera.",
  unknown: "Something went wrong with the camera.",
};

/** The state words (DESIGN.md: Checking · Ready · Not available · In use by another app). */
export const cameraStateCopy: Readonly<Record<CameraState, string>> = {
  idle: "Choose a camera.",
  checking: "Checking",
  ready: "Ready",
  notAvailable: "Not available",
  inUseByAnotherApp: "In use by another app",
};

export const noCamerasCopy = "No camera found.";
export const noCamerasHelp = "Connect a camera and it will appear here.";
export const noPictureCopy = "No picture right now.";
export const chooseCameraCopy = "Choose a camera to see it here.";

/** The one status line for the camera: the state, then, when it isn't available, the reason. */
export function cameraStatusText(view: Pick<CameraDiagnosticsView, "state" | "reason">): string {
  const state = cameraStateCopy[view.state];
  return view.state === "notAvailable" && view.reason !== null ? `${state}. ${cameraReasonCopy[view.reason]}` : state;
}

/** A problem the operator should notice: the line takes the alert tone. */
export function cameraStatusIsAlert(state: CameraState): boolean {
  return state === "notAvailable" || state === "inUseByAnotherApp";
}

const dash = "—";

/** A frame rate: "30 fps", or up to two decimals for a fraction, "29.97 fps". */
export function formatFps(fps: number): string {
  return `${String(Math.round(fps * 100) / 100)} fps`;
}

/** "1920 × 1080 · 30 fps"; a dash when the camera's format isn't known. */
export function formatCameraFormat(format: CameraDiagnosticsView["format"]): string {
  return format === null ? dash : `${String(format.width)} × ${String(format.height)} · ${formatFps(format.fps)}`;
}

/** The page's measured frame rate, to whole frames; a dash until it has one. */
export function formatMeasuredRate(fps: number | null): string {
  return fps === null ? dash : `${String(Math.round(fps))} fps`;
}

/** How long ago the newest picture arrived: "Just now" under a second, then whole seconds. A dash before any. */
export function formatPictureAge(ageMs: number | null): string {
  if (ageMs === null) {
    return dash;
  }
  const seconds = Math.floor(ageMs / 1_000);
  return seconds < 1 ? "Just now" : `${String(seconds)} s ago`;
}

/** A file size in kilobytes (1 KB = 1024 bytes), at least 1 KB, e.g. "403 KB". */
export function formatByteLength(bytes: number): string {
  return `${String(Math.max(1, Math.round(bytes / 1_024)))} KB`;
}

/** The last capture's facts on one line under its file name, e.g. "1920 × 1080 · 403 KB · Mirrored". */
export function formatCaptureFacts(capture: NonNullable<CameraDiagnosticsView["lastCapture"]>): string {
  return [
    `${String(capture.width)} × ${String(capture.height)}`,
    formatByteLength(capture.byteLength),
    capture.mirrored ? "Mirrored" : "Not mirrored",
  ].join(" · ");
}
