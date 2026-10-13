import { expect, test } from "../../../src/fixtures";
import {
  cameraEnvVar,
  describeCamera,
  noCameraMessage,
  selectCamera,
  unmatchedCameraMessage,
} from "../../../src/camera";

test.describe("camera precondition", { tag: ["@m1"] }, () => {
  test("test.listCameras reports the camera global setup chose", async ({ hooks, camera }) => {
    const cameras = await hooks.listCameras();
    expect(cameras.length).toBeGreaterThan(0);

    const selected = selectCamera(cameras, process.env[cameraEnvVar]);
    console.log(describeCamera(selected));
    expect(selected).toEqual(camera);
  });

  test(`${cameraEnvVar}=missing-device fails with the exact required-camera message`, async ({ hooks }) => {
    const cameras = await hooks.listCameras();
    const expected = `End-to-end tests require at least one connected camera. ${cameraEnvVar}="missing-device" matched none of: ${cameras
      .map((camera) => camera.name)
      .join(", ")}.`;

    expect(unmatchedCameraMessage("missing-device", cameras)).toBe(expected);
    expect(() => selectCamera(cameras, "missing-device")).toThrow(
      new RegExp(`^${expected.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}$`),
    );
  });

  test("no cameras fails with the exact required-camera message", () => {
    expect(noCameraMessage).toBe("End-to-end tests require at least one connected camera.");
    expect(() => selectCamera([], undefined)).toThrow(/^End-to-end tests require at least one connected camera\.$/);
    expect(() => selectCamera([], "anything")).toThrow(/^End-to-end tests require at least one connected camera\.$/);
  });
});
