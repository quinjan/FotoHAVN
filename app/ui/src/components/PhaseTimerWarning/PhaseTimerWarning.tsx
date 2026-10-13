import { useEffect, useId, useRef, useState } from "react";
import { SecondaryAction } from "../SecondaryAction/index.ts";
import { cx } from "../shared/cx.ts";
import styles from "./PhaseTimerWarning.module.css";
import { usePrefersReducedMotion } from "./usePrefersReducedMotion.ts";

/**
 * The phases whose Phase Timer warns (implementation plan section 7, D18). Keepsake is deliberately absent: its Photo
 * Strip is already saved, so it has no warning and no `MORE TIME`.
 */
export type PhaseTimerWarningPhase = "template" | "photographs-capture" | "photographs-review" | "filters" | "guest-gallery";

const warnedPhases: ReadonlySet<string> = new Set<PhaseTimerWarningPhase>([
  "template",
  "photographs-capture",
  "photographs-review",
  "filters",
  "guest-gallery",
]);

/** The whole seconds shown for a remaining time: never negative, rounded up so the last second reads 1. */
export function wholeSeconds(seconds: number): number {
  return Math.max(0, Math.ceil(seconds));
}

/** The one polite announcement when the warning appears. */
export function warningAnnouncement(seconds: number): string {
  return `${String(seconds)} ${seconds === 1 ? "second" : "seconds"} left. Choose MORE TIME to stay a little longer.`;
}

export interface PhaseTimerWarningProps {
  /** The phase that is running out. A phase outside the warned set (Keepsake) renders nothing. */
  readonly phase: PhaseTimerWarningPhase;
  /** The remaining time from the brain's Phase Timer, in seconds; the brain shows the warning in the last 20. */
  readonly secondsRemaining: number;
  /** Asks the brain to restart the phase's full time. */
  readonly onMoreTime: () => void;
  /**
   * Whether it arrives with its short rise and fade. Default true. Pass false where the warning is already under way
   * as the view mounts (a view shown again mid-warning), so it doesn't arrive twice; fixtures pass false for stable
   * screenshots.
   */
  readonly arrive?: boolean;
  readonly className?: string;
}

/**
 * The Phase Timer warning (DESIGN.md, Deliberate departures): the remaining seconds of a guest phase in its last 20 s,
 * with `MORE TIME`, so a guest who is present can always extend. Never use it on Keepsake; a Keepsake phase renders
 * nothing. `MORE TIME` is a secondary action: the step's own action stays the view's one primary action.
 *
 * Mount it when the brain starts the warning and unmount it when the phase restarts or ends. Each mount announces once
 * through a polite live region, with the seconds at that moment; the visible count updates every second without
 * being announced again. It arrives with a short rise and fade; under reduced motion it simply appears.
 */
export function PhaseTimerWarning({ phase, secondsRemaining, onMoreTime, arrive = true, className }: PhaseTimerWarningProps) {
  const reducedMotion = usePrefersReducedMotion();
  const [announcement] = useState(() => warningAnnouncement(wholeSeconds(secondsRemaining)));
  const liveRegion = useRef<HTMLParagraphElement>(null);
  const countId = useId();

  // The live region is empty on the first commit and gets its text once afterwards, so it is announced exactly once.
  useEffect(() => {
    const region = liveRegion.current;
    if (region !== null && region.textContent !== announcement) {
      region.textContent = announcement;
    }
  }, [announcement]);

  if (!warnedPhases.has(phase)) {
    return null;
  }
  const seconds = wholeSeconds(secondsRemaining);
  return (
    <div
      className={cx(styles.warning, className)}
      data-tone="light"
      data-phase={phase}
      data-motion={!arrive ? "static" : reducedMotion ? "reduced" : "full"}
    >
      <p id={countId} className={styles.count}>
        <span className={styles.numeral}>{seconds}</span>{" "}
        <span className={styles.unit}>{seconds === 1 ? "SECOND LEFT" : "SECONDS LEFT"}</span>
      </p>
      <SecondaryAction onClick={onMoreTime} aria-describedby={countId}>
        MORE TIME
      </SecondaryAction>
      <p ref={liveRegion} className={styles.visuallyHidden} role="status" />
    </div>
  );
}
