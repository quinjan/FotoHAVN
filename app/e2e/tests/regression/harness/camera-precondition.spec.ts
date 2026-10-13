import { spawnSync } from "node:child_process";
import { expect, test } from "../../../src/fixtures";
import {
  cameraEnvVar,
  describeCamera,
  noCameraMessage,
  selectCamera,
  unmatchedCameraMessage,
} from "../../../src/camera";
import { chosenCameraEnvVar } from "../../../src/chosen-camera";
import { outputEnvVar, paths } from "../../../src/paths";

interface ChildRun {
  status: number | null;
  output: string;
}

/**
 * Runs `npx playwright test --project=smoke` in a child process, so its global setup applies the camera precondition
 * for real. The child runs only the smoke project (never this harness), reuses the existing E2E build, and writes every
 * result to `outputDir`, so it can't recurse or touch this run's camera.json, runs/, or reports.
 */
function runSmokeChild(outputDir: string, camera: string | undefined): ChildRun {
  const env: NodeJS.ProcessEnv = {};
  for (const [key, value] of Object.entries(process.env)) {
    // Drop this worker's Playwright state so the child starts as a fresh run.
    if (!/^(PW_|TEST_WORKER_INDEX$|TEST_PARALLEL_INDEX$)/.test(key)) {
      env[key] = value;
    }
  }
  delete env[chosenCameraEnvVar];
  delete env[cameraEnvVar];
  if (camera !== undefined) {
    env[cameraEnvVar] = camera;
  }
  env[outputEnvVar] = outputDir;
  env.FOTOHAVN_E2E_SKIP_BUILD = "1";
  env.FORCE_COLOR = "0";

  // A shell resolves npx.cmd on Windows; the command is a fixed string.
  const result = spawnSync("npx playwright test --project=smoke", {
    cwd: paths.e2eDir,
    env,
    shell: true,
    encoding: "utf8",
    maxBuffer: 64 * 1024 * 1024,
    timeout: 4 * 60_000,
  });
  expect(result.error, "npx must be on PATH").toBeUndefined();
  const output = `${result.stdout}\n${result.stderr}`.replace(/\x1b\[[0-9;]*m/g, "");
  return { status: result.status, output };
}

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

test.describe("camera precondition in global setup", { tag: ["@m1"] }, () => {
  test.setTimeout(6 * 60_000);

  test(`a run with ${cameraEnvVar}=missing-device fails global setup with the exact required-camera message`, async ({
    hooks,
    appManager,
  }, testInfo) => {
    const cameras = await hooks.listCameras();
    // The camera is exclusive: no app of this run stays open while the child run launches its own.
    await appManager.closeCurrent();

    const child = runSmokeChild(testInfo.outputPath("child-run"), "missing-device");

    expect(child.status, child.output).not.toBe(0);
    const prefix = `${noCameraMessage} ${cameraEnvVar}="missing-device" matched none of: `;
    expect(child.output).toContain(prefix);
    const line = child.output.split(/\r?\n/).find((candidate) => candidate.includes(prefix)) ?? "";
    expect(line.slice(line.indexOf(prefix))).toBe(unmatchedCameraMessage("missing-device", cameras));
  });

  test("a run with the configured camera passes global setup and prints the chosen camera", async ({
    appManager,
    camera,
  }, testInfo) => {
    await appManager.closeCurrent();

    const child = runSmokeChild(testInfo.outputPath("child-run"), process.env[cameraEnvVar]);

    expect(child.status, child.output).toBe(0);
    expect(child.output).toMatch(/\b\d+ passed\b/);
    expect(child.output.split(/\r?\n/).map((line) => line.trim())).toContain(describeCamera(camera));
  });
});
