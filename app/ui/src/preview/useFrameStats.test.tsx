import { act, render } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { FrameSource, PreviewFrame } from "../components/LivePreview/index.ts";
import type { FrameRate } from "./frameStats.ts";
import { frameStatsIntervalMs, useFrameStats } from "./useFrameStats.ts";

function manualSource() {
  const listeners = new Set<(frame: PreviewFrame) => void>();
  const source: FrameSource = {
    subscribe(onFrame) {
      listeners.add(onFrame);
      return () => {
        listeners.delete(onFrame);
      };
    },
  };
  return {
    source,
    listeners,
    emit: () => {
      for (const listener of listeners) {
        listener({ width: 1, height: 1, draw: vi.fn() });
      }
    },
  };
}
function Probe({
  source,
  now,
  onRender,
  enabled = true,
}: {
  source: FrameSource;
  now: () => number;
  onRender: (rate: FrameRate) => void;
  enabled?: boolean;
}) {
  onRender(useFrameStats(source, enabled, now));
  return null;
}

describe("useFrameStats", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("refreshes on a timer, at most four times a second, however many frames arrive", () => {
    const { source, emit } = manualSource();
    const onRender = vi.fn();
    render(<Probe source={source} now={() => performance.now()} onRender={onRender} />);
    const initial = onRender.mock.calls.length;

    // 100 frames inside one second: the screen re-renders only when the readings change, a few times at most.
    for (let n = 0; n < 100; n += 1) {
      act(() => {
        emit();
        vi.advanceTimersByTime(10);
      });
    }
    expect(onRender.mock.calls.length - initial).toBeLessThanOrEqual(1_000 / frameStatsIntervalMs);
    expect(frameStatsIntervalMs).toBeGreaterThanOrEqual(250);
  });

  it("reports the rate and the age as they change, and never between ticks", () => {
    const { source, emit } = manualSource();
    const readings: FrameRate[] = [];
    let now = 0;
    render(
      <Probe
        source={source}
        now={() => now}
        onRender={(rate) => {
          readings.push(rate);
        }}
      />,
    );

    for (let n = 0; n < 20; n += 1) {
      now += 50;
      emit();
    }
    // Nothing has ticked yet, so nothing re-rendered.
    expect(readings.at(-1)).toEqual({ fps: null, ageMs: null });

    act(() => {
      vi.advanceTimersByTime(frameStatsIntervalMs);
    });
    expect(readings.at(-1)?.fps).toBeCloseTo(20, 0);
    expect(readings.at(-1)?.ageMs).toBe(0);

    now += 3_000;
    act(() => {
      vi.advanceTimersByTime(frameStatsIntervalMs);
    });
    expect(readings.at(-1)).toMatchObject({ fps: 0 });
    expect(readings.at(-1)?.ageMs).toBeGreaterThanOrEqual(3_000);
  });

  it("subscribes to the source and stops on unmount", () => {
    const { source, listeners } = manualSource();
    const { unmount } = render(<Probe source={source} now={() => 0} onRender={vi.fn()} />);
    expect(listeners.size).toBe(1);
    unmount();
    expect(listeners.size).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("listens to nothing while disabled, and forgets what it measured when it is switched off", () => {
    const { source, listeners, emit } = manualSource();
    const readings: FrameRate[] = [];
    const record = (rate: FrameRate) => {
      readings.push(rate);
    };
    const { rerender } = render(<Probe source={source} now={() => 0} onRender={record} enabled={false} />);
    expect(listeners.size).toBe(0);
    expect(vi.getTimerCount()).toBe(0);

    rerender(<Probe source={source} now={() => 0} onRender={record} />);
    expect(listeners.size).toBe(1);
    act(() => {
      emit();
      vi.advanceTimersByTime(frameStatsIntervalMs);
    });
    expect(readings.at(-1)?.ageMs).toBe(0);

    rerender(<Probe source={source} now={() => 0} onRender={record} enabled={false} />);
    expect(listeners.size).toBe(0);
    expect(readings.at(-1)).toEqual({ fps: null, ageMs: null });
  });

  it("starts over when the source changes", () => {
    const first = manualSource();
    const second = manualSource();
    const readings: FrameRate[] = [];
    const record = (rate: FrameRate) => {
      readings.push(rate);
    };
    const { rerender } = render(<Probe source={first.source} now={() => 0} onRender={record} />);
    act(() => {
      first.emit();
      vi.advanceTimersByTime(frameStatsIntervalMs);
    });
    expect(readings.at(-1)?.ageMs).toBe(0);

    rerender(<Probe source={second.source} now={() => 0} onRender={record} />);
    expect(first.listeners.size).toBe(0);
    expect(second.listeners.size).toBe(1);
    expect(readings.at(-1)).toEqual({ fps: null, ageMs: null });
  });
});
