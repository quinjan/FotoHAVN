import { describe, expect, it } from "vitest";
import type { CameraFailureKind, CameraState } from "../../protocol/generated.ts";
import {
  cameraReasonCopy,
  cameraStateCopy,
  cameraStatusIsAlert,
  cameraStatusText,
  formatByteLength,
  formatCameraFormat,
  formatCaptureFacts,
  formatFps,
  formatMeasuredRate,
  formatPictureAge,
} from "./cameraText.ts";

const kinds = Object.keys(cameraReasonCopy) as CameraFailureKind[];
const states = Object.keys(cameraStateCopy) as CameraState[];

describe("camera copy", () => {
  it("has a reason for every failure kind the protocol has, the privacy line exactly as approved", () => {
    expect(kinds).toHaveLength(9);
    expect(cameraReasonCopy.privacyDenied).toBe("Camera access is off in Windows privacy settings.");
    expect(cameraReasonCopy.stale).toBe("The camera stopped sending pictures.");
    expect(cameraReasonCopy.inUseByAnotherApp).toBe("Another app is using this camera.");
    expect(cameraReasonCopy.removed).toBe("The camera was disconnected.");
  });

  it.each(kinds)("writes the %s reason as one plain sentence", (kind) => {
    expect(cameraReasonCopy[kind]).toMatch(/^[A-Z][^!]*\.$/);
    expect(cameraReasonCopy[kind]).not.toMatch(/\s{2}|[—–]/);
  });

  it("names the states as DESIGN.md does", () => {
    expect(cameraStateCopy.checking).toBe("Checking");
    expect(cameraStateCopy.ready).toBe("Ready");
    expect(cameraStateCopy.notAvailable).toBe("Not available");
    expect(cameraStateCopy.inUseByAnotherApp).toBe("In use by another app");
  });

  it("follows Not available with its reason, and says only the state otherwise", () => {
    expect(cameraStatusText({ state: "notAvailable", reason: "privacyDenied" })).toBe(
      "Not available. Camera access is off in Windows privacy settings.",
    );
    expect(cameraStatusText({ state: "notAvailable", reason: null })).toBe("Not available");
    expect(cameraStatusText({ state: "inUseByAnotherApp", reason: "inUseByAnotherApp" })).toBe("In use by another app");
    expect(cameraStatusText({ state: "ready", reason: null })).toBe("Ready");
    expect(cameraStatusText({ state: "checking", reason: null })).toBe("Checking");
    expect(cameraStatusText({ state: "idle", reason: null })).toBe("Choose a camera.");
  });

  it("takes the alert tone only for a camera with a problem", () => {
    expect(states.filter(cameraStatusIsAlert)).toEqual(["notAvailable", "inUseByAnotherApp"]);
  });
});

describe("camera figures", () => {
  it("formats the format, with a dash for none", () => {
    expect(formatCameraFormat({ width: 1920, height: 1080, fps: 30 })).toBe("1920 × 1080 · 30 fps");
    expect(formatCameraFormat({ width: 1280, height: 720, fps: 29.97 })).toBe("1280 × 720 · 29.97 fps");
    expect(formatCameraFormat(null)).toBe("—");
  });

  it("rounds frame rates", () => {
    expect(formatFps(15)).toBe("15 fps");
    expect(formatFps(14.9999)).toBe("15 fps");
    expect(formatMeasuredRate(29.6)).toBe("30 fps");
    expect(formatMeasuredRate(0)).toBe("0 fps");
    expect(formatMeasuredRate(null)).toBe("—");
  });

  it("says how long ago the newest picture arrived", () => {
    expect(formatPictureAge(null)).toBe("—");
    expect(formatPictureAge(0)).toBe("Just now");
    expect(formatPictureAge(999)).toBe("Just now");
    expect(formatPictureAge(1_000)).toBe("1 s ago");
    expect(formatPictureAge(6_400)).toBe("6 s ago");
  });

  it("formats file sizes in kilobytes, at least one", () => {
    expect(formatByteLength(412_345)).toBe("403 KB");
    expect(formatByteLength(100)).toBe("1 KB");
    expect(formatByteLength(0)).toBe("1 KB");
  });

  it("puts a capture's size, weight, and mirroring on one line", () => {
    expect(formatCaptureFacts({ fileName: "a.jpg", width: 1920, height: 1080, byteLength: 412_345, mirrored: true })).toBe(
      "1920 × 1080 · 403 KB · Mirrored",
    );
    expect(formatCaptureFacts({ fileName: "a.jpg", width: 640, height: 480, byteLength: 2_048, mirrored: false })).toBe(
      "640 × 480 · 2 KB · Not mirrored",
    );
  });
});
