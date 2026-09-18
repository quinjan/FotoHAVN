import { analyticsEvents, type AnalyticsEventName, type MarketingSectionName } from "./contracts";

const REACH_MS = 1_000;
const ENGAGEMENT_MS = 5_000;

type SectionTiming = {
  aboveThreshold: boolean;
  pageVisible: boolean;
  lastSampleAt: number | null;
  continuousMs: number;
  cumulativeMs: number;
  reached: boolean;
  engaged: boolean;
};

function initialTiming(): SectionTiming {
  return {
    aboveThreshold: false,
    pageVisible: true,
    lastSampleAt: null,
    continuousMs: 0,
    cumulativeMs: 0,
    reached: false,
    engaged: false,
  };
}

export class SectionTimingTracker {
  private readonly sections = new Map<MarketingSectionName, SectionTiming>();

  update(section: MarketingSectionName, aboveThreshold: boolean, pageVisible: boolean, now: number) {
    const timing = this.sections.get(section) ?? initialTiming();
    const wasActive = timing.aboveThreshold && timing.pageVisible;
    const active = aboveThreshold && pageVisible;
    const events: AnalyticsEventName[] = [];

    if (wasActive && timing.lastSampleAt !== null) {
      const elapsed = Math.max(0, now - timing.lastSampleAt);
      timing.continuousMs += elapsed;
      timing.cumulativeMs += elapsed;
    }

    if (!active) {
      timing.continuousMs = 0;
      timing.lastSampleAt = null;
    } else {
      timing.lastSampleAt = now;
    }

    timing.aboveThreshold = aboveThreshold;
    timing.pageVisible = pageVisible;

    if (!timing.reached && timing.continuousMs >= REACH_MS) {
      timing.reached = true;
      events.push(analyticsEvents.sectionReach(section));
    }
    if (!timing.engaged && timing.cumulativeMs >= ENGAGEMENT_MS) {
      timing.engaged = true;
      events.push(analyticsEvents.sectionEngagement(section));
    }

    this.sections.set(section, timing);
    return events;
  }
}
