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
 * Per-kind budgets from the injection to Not available. `failed`, `removed`, and `exclusiveLost` act at once (measured 14–22 ms), so
 * 500 ms leaves room for the hook round trip and the 50 ms state polling and nothing else. `stale` is defined as two seconds
 * without frames (ADR 0004), so "within 2 s" can't literally hold: the budget is StaleAfter plus one freshness tick
 * (250 ms: the brain polls on that period) plus 150 ms slack for the hook round trip and the state polling.
 */
const immediateBudgetMs = 500;
const staleBudgetMs = staleAfterMs + freshnessPollMs + 150;

/** Each fault, the reason the screen must show, the copy line (DESIGN.md, Camera field), and its latency budget. */
const faults: readonly { kind: CameraFaultKind; reason: string; line: string; budgetMs: number }[] = [
  { kind: "stale", reason: "stale", line: "The camera stopped sending pictures.", budgetMs: staleBudgetMs },
  { kind: "failed", reason: "failed", line: "The camera stopped working.", budgetMs: immediateBudgetMs },
  { kind: "removed", reason: "removed", line: "The camera was disconnected.", budgetMs: immediateBudgetMs },
  { kind: "exclusiveLost", reason: "exclusiveLost", line: "Another app took over the camera.", budgetMs: immediateBudgetMs },
];

test.describe("injected camera faults", { tag: ["@m5", "@camera"] }, () => {
  test("each injected fault (stale, failed, removed, exclusiveLost) moves the state to Not available within its budget; clearing it and retrying brings back fresh frames", async ({
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
        console.log(`${fault.kind}: Not available after ${latency.toFixed(0)} ms (budget ${String(fault.budgetMs)} ms)`);
        testInfo.annotations.push({ type: `${fault.kind} latency ms`, description: latency.toFixed(0) });
        expect(latency, `${fault.kind} must reach Not available within ${String(fault.budgetMs)} ms`).toBeLessThanOrEqual(fault.budgetMs);

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
