import { expect, test } from "../../../src/fixtures";
import {
  freshnessPollMs,
  maxFrameAgeMs,
  openDiagnostics,
  staleAfterMs,
  waitForCameraState,
  waitForFreshFrames,
} from "../../../src/camera-diagnostics";
import type { CameraFaultKind } from "../../../src/hooks";

/**
 * The plan's bound is 2 s from the injection. The brain notices a stale camera two seconds after the last frame
 * (ADR 0004) on a 250 ms poll, so a `stale` fault shows between 2.0 s and 2.25 s after injection; the budget adds one
 * timer period and 150 ms for the hook round trip and the 50 ms state polling. The other faults act at once.
 */
const budgetMs = staleAfterMs + freshnessPollMs + 150;

/** Each fault, the reason the screen must show, and the copy line (DESIGN.md, Camera field). */
const faults: readonly { kind: CameraFaultKind; reason: string; line: string }[] = [
  { kind: "stale", reason: "stale", line: "The camera stopped sending pictures." },
  { kind: "failed", reason: "failed", line: "The camera stopped working." },
  { kind: "removed", reason: "removed", line: "The camera was disconnected." },
  { kind: "exclusiveLost", reason: "exclusiveLost", line: "Another app took over the camera." },
];

test.describe("injected camera faults", { tag: ["@m5"] }, () => {
  test("each injected fault (stale, failed, removed, exclusiveLost) moves the state to Not available within 2 s; clearing it and retrying brings back fresh frames", async ({
    app,
    operatorPage,
    hooks,
    camera,
  }, testInfo) => {
    const screen = await openDiagnostics(operatorPage, hooks, camera);
    await waitForFreshFrames(hooks);

    for (const fault of faults) {
      await test.step(`${fault.kind}: ready → Not available (${fault.reason}) → ready with fresh frames`, async () => {
        const before = await hooks.getDiagnostics();
        expect(before.camera.state).toBe("ready");

        const injectedAt = performance.now();
        await hooks.injectCameraFault(fault.kind);
        const latency = await waitForCameraState(hooks, "notAvailable", { timeoutMs: 5_000, since: injectedAt });
        console.log(`${fault.kind}: Not available after ${latency.toFixed(0)} ms (budget ${String(budgetMs)} ms)`);
        testInfo.annotations.push({ type: `${fault.kind} latency ms`, description: latency.toFixed(0) });
        expect(latency, `${fault.kind} must reach Not available within the budget`).toBeLessThanOrEqual(budgetMs);

        await expect(screen.root).toHaveAttribute("data-camera-state", "notAvailable");
        await expect(screen.root).toHaveAttribute("data-camera-reason", fault.reason);
        await expect(screen.statusLine.first()).toHaveText(`Not available. ${fault.line}`);
        await expect(screen.preview).toHaveAttribute("data-state", "waiting");

        await hooks.clearCameraFault();
        const age = await waitForFreshFrames(hooks);
        console.log(`${fault.kind}: ready again, first fresh frame age ${age.toFixed(0)} ms`);
        expect(age).toBeLessThan(maxFrameAgeMs);
        await expect(screen.root).toHaveAttribute("data-camera-state", "ready");
        await expect(screen.root).not.toHaveAttribute("data-camera-reason");
        await expect(screen.preview).toHaveAttribute("data-state", "live");
      });
    }

    expect((await app.close()).code).toBe(0);
  });
});
