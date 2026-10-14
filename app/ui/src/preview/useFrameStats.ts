import { useEffect, useState } from "react";
import type { FrameSource } from "../components/LivePreview/index.ts";
import { FrameStats, noFrameRate, sameFrameRate, type FrameRate } from "./frameStats.ts";

/** How often the screen re-reads the stats: at most four times a second, never once per frame (contract §6). */
export const frameStatsIntervalMs = 250;

/**
 * The frame rate and frame age the page measures on `source`, refreshed on a timer, never per frame. The state only
 * changes when the words it feeds would, so a steady camera re-renders the screen a few times a second at most.
 * While `enabled` is false it listens to nothing and knows nothing, so a camera that isn't delivering shows no figures
 * left over from an earlier picture.
 */
export function useFrameStats(source: FrameSource, enabled = true, now?: () => number): FrameRate {
  const [rate, setRate] = useState<FrameRate>(noFrameRate);

  useEffect(() => {
    if (!enabled) {
      return undefined;
    }
    const stats = new FrameStats(now);
    const unsubscribe = source.subscribe(() => {
      stats.record();
    });
    const timer = setInterval(() => {
      const next = stats.read();
      setRate((current) => (sameFrameRate(current, next) ? current : next));
    }, frameStatsIntervalMs);
    return () => {
      clearInterval(timer);
      unsubscribe();
      setRate(noFrameRate);
    };
  }, [source, enabled, now]);

  return rate;
}
