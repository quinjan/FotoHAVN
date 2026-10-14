import { describe, expect, it } from "vitest";
import { FrameStats, frameRateWindowMs, noFrameRate, sameFrameRate } from "./frameStats.ts";

function clock(start = 0) {
  let now = start;
  return {
    now: () => now,
    advance: (ms: number) => {
      now += ms;
    },
  };
}

describe("FrameStats", () => {
  it("knows nothing before the first frame", () => {
    const { now } = clock();
    expect(new FrameStats(now).read()).toEqual(noFrameRate);
  });

  it("has an age but no rate after one frame", () => {
    const time = clock(1_000);
    const stats = new FrameStats(time.now);
    stats.record();
    time.advance(120);
    expect(stats.read()).toEqual({ fps: null, ageMs: 120 });
  });

  it("computes frames per second over the arrivals in the window", () => {
    const time = clock();
    const stats = new FrameStats(time.now);
    for (let n = 0; n < 31; n += 1) {
      stats.record();
      time.advance(50);
    }
    // 31 frames, 50 ms apart: 30 intervals over 1.5 s.
    expect(stats.read().fps).toBeCloseTo(20, 5);
    expect(stats.read().ageMs).toBe(50);
  });

  it("reads zero frames per second and a growing age once the camera stops", () => {
    const time = clock();
    const stats = new FrameStats(time.now);
    stats.record();
    time.advance(100);
    stats.record();
    time.advance(frameRateWindowMs + 500);
    expect(stats.read()).toEqual({ fps: 0, ageMs: frameRateWindowMs + 500 });
  });

  it("drops arrivals older than the window, so a recovered camera shows its new rate", () => {
    const time = clock();
    const stats = new FrameStats(time.now, 1_000);
    for (let n = 0; n < 10; n += 1) {
      stats.record();
      time.advance(100);
    }
    time.advance(5_000);
    stats.record();
    time.advance(250);
    stats.record();
    expect(stats.read().fps).toBeCloseTo(4, 5);
  });

  it("gives no rate for frames that all arrived at the same instant", () => {
    const stats = new FrameStats(() => 500);
    stats.record();
    stats.record();
    expect(stats.read()).toEqual({ fps: null, ageMs: 0 });
  });

  it("never reports a negative age if the clock steps back", () => {
    let now = 1_000;
    const stats = new FrameStats(() => now);
    stats.record();
    now = 900;
    expect(stats.read().ageMs).toBe(0);
  });

  it("uses performance.now by default", () => {
    const stats = new FrameStats();
    stats.record();
    expect(stats.read().ageMs).toBeGreaterThanOrEqual(0);
  });
});

describe("sameFrameRate", () => {
  it("treats readings that would show the same words as equal", () => {
    expect(sameFrameRate({ fps: 29.6, ageMs: 40 }, { fps: 30.4, ageMs: 900 })).toBe(true);
    expect(sameFrameRate(noFrameRate, noFrameRate)).toBe(true);
  });

  it("tells apart a different rounded rate, a different whole second, and known from unknown", () => {
    expect(sameFrameRate({ fps: 29, ageMs: 40 }, { fps: 31, ageMs: 40 })).toBe(false);
    expect(sameFrameRate({ fps: 30, ageMs: 900 }, { fps: 30, ageMs: 1_100 })).toBe(false);
    expect(sameFrameRate({ fps: null, ageMs: 40 }, { fps: 30, ageMs: 40 })).toBe(false);
    expect(sameFrameRate(noFrameRate, { fps: null, ageMs: 40 })).toBe(false);
  });
});
