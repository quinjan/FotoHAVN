import { expect, type Page } from "@playwright/test";
import type { Camera } from "./camera";
import type { CameraState, HookClient } from "./hooks";
import { CameraDiagnosticsScreen } from "./screens/screens";

/** The wait for a camera to become `ready` after it is selected or recovers (open, first frame, the first freshness poll). */
export const readyTimeoutMs = 20_000;

/** One timer period of the brain's freshness poll (`Brain.FreshnessPollInterval`). */
export const freshnessPollMs = 250;

/** The brain reports a stale camera two seconds after its last frame (ADR 0004). */
export const staleAfterMs = 2_000;

/** A fresh frame is under this age (plan section 14). */
export const maxFrameAgeMs = 500;

/** Opens the diagnostics surface and selects `camera`; resolves with the screen once the page shows the controls. */
export async function openDiagnostics(page: Page, hooks: HookClient, camera: Camera): Promise<CameraDiagnosticsScreen> {
  await hooks.openCameraDiagnostics();
  const screen = new CameraDiagnosticsScreen(page);
  await expect(screen.root).toBeVisible();
  // The controls arrive with a lazy chunk in E2E builds.
  await expect(screen.capture).toBeVisible();
  await hooks.selectDiagnosticsCamera(camera.id);
  return screen;
}

/** Polls `test.getDiagnostics` until the camera is in `state`; resolves with the milliseconds waited since `since`. */
export async function waitForCameraState(
  hooks: HookClient,
  state: CameraState,
  options: { timeoutMs: number; since?: number; intervalMs?: number },
): Promise<number> {
  const since = options.since ?? performance.now();
  const interval = options.intervalMs ?? 50;
  for (;;) {
    const { camera } = await hooks.getDiagnostics();
    if (camera.state === state) {
      return performance.now() - since;
    }
    if (performance.now() - since > options.timeoutMs) {
      throw new Error(`The camera was "${camera.state}" after ${String(options.timeoutMs)} ms; expected "${state}".`);
    }
    await new Promise((resolve) => setTimeout(resolve, interval));
  }
}

/** Waits until the camera is `ready` and its newest frame is fresh; resolves with the frame age seen. */
export async function waitForFreshFrames(hooks: HookClient, timeoutMs = readyTimeoutMs): Promise<number> {
  const started = performance.now();
  await waitForCameraState(hooks, "ready", { timeoutMs });
  for (;;) {
    const { camera } = await hooks.getDiagnostics();
    if (camera.state === "ready" && camera.frameAgeMs !== null && camera.frameAgeMs < maxFrameAgeMs) {
      return camera.frameAgeMs;
    }
    if (performance.now() - started > timeoutMs) {
      throw new Error(`No fresh frame within ${String(timeoutMs)} ms (state "${camera.state}", age ${String(camera.frameAgeMs)}).`);
    }
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
}
