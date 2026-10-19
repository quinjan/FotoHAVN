import { afterEach, describe, expect, it, vi } from "vitest";
import { settleFixtureDocument } from "./settle.ts";

function animation(playState: "running" | "finished", iterations: number, finished: Promise<unknown>): Animation {
  return { playState, effect: { getComputedTiming: () => ({ iterations }) }, finished } as unknown as Animation;
}

describe("settleFixtureDocument", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    Reflect.deleteProperty(document, "getAnimations");
  });

  it("resolves when nothing is animating", async () => {
    await expect(settleFixtureDocument()).resolves.toBeUndefined();
  });

  it("ignores a delayed reveal, which only waits", async () => {
    const hint = document.createElement("p");
    hint.setAttribute("data-delayed-reveal", "");
    const waiting = {
      playState: "running",
      effect: { getComputedTiming: () => ({ iterations: 1 }), target: hint },
      finished: new Promise(() => undefined),
    } as unknown as Animation;
    Object.defineProperty(document, "getAnimations", { configurable: true, value: () => [waiting] });
    await expect(settleFixtureDocument()).resolves.toBeUndefined();
  });

  it("waits for finite animations to finish and ignores looping ones", async () => {
    let finish: () => void = () => undefined;
    const running = animation("running", 1, new Promise<void>((resolve) => (finish = resolve)));
    const looping = animation("running", Infinity, new Promise(() => undefined));
    let finishedRunning = false;
    Object.defineProperty(document, "getAnimations", {
      configurable: true,
      value: () => [looping, finishedRunning ? animation("finished", 1, Promise.resolve()) : running],
    });

    let settled = false;
    const done = settleFixtureDocument().then(() => {
      settled = true;
    });
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(settled).toBe(false);

    finishedRunning = true;
    finish();
    await done;
    expect(settled).toBe(true);
  });
});
