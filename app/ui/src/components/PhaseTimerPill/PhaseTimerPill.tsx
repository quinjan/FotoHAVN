import { PauseIcon, TimerIcon } from "@phosphor-icons/react";
import { useEffect, useRef } from "react";
import { cx } from "../shared/cx.ts";
import { secondsText } from "../shared/secondsText.ts";
import styles from "./PhaseTimerPill.module.css";

/** The last seconds of a phase, when the pill fills and is announced once. */
export const lowSeconds = 10;

/** The whole seconds shown for a remaining time: never negative, rounded up so the last second reads 0:01. */
export function wholeSeconds(seconds: number): number {
  return Math.max(0, Math.ceil(seconds));
}

/** The visible time, `m:ss`: 45 s reads "0:45" and 125 s reads "2:05". */
export function clockText(seconds: number): string {
  const whole = wholeSeconds(seconds);
  return `${String(Math.floor(whole / 60))}:${String(whole % 60).padStart(2, "0")}`;
}

/** The one polite announcement, when the phase reaches its last seconds. */
export function lowAnnouncement(seconds: number): string {
  return `${secondsText(wholeSeconds(seconds))} left.`;
}

export interface PhaseTimerPillProps {
  /** The remaining time from the brain's Phase Timer, in seconds. */
  readonly secondsRemaining: number;
  /** The brain holds the timer: the time stands still and the pill says so. Default false. */
  readonly paused?: boolean;
  readonly className?: string;
}

/**
 * The Phase Timer (app/DESIGN.md, Deliberate departures): the time left in a timed guest phase, always visible while the
 * phase runs, as a quiet pill in the guest top band: a clock icon and `m:ss` in Cormorant lining figures inside a Hairline
 * outline. In the last 10 seconds it fills with the primary ink, so the change reads by shape as well as by the numbers.
 * There is no way to extend it. While the brain pauses the timer, the time stands still and the clock becomes a pause mark.
 *
 * The pill is a `timer`, which is never announced as it ticks; a separate polite status says the remaining seconds once,
 * when the phase reaches its last 10 seconds (or mounts inside them), and again only after the time has gone back above them.
 * Mount one per phase (key it by phase), so a new phase starts quiet.
 */
export function PhaseTimerPill({ secondsRemaining, paused = false, className }: PhaseTimerPillProps) {
  const seconds = wholeSeconds(secondsRemaining);
  const low = seconds <= lowSeconds;
  const liveRef = useRef<HTMLParagraphElement>(null);
  const announced = useRef(false);

  useEffect(() => {
    const region = liveRef.current;
    if (region === null) {
      return;
    }
    if (!low) {
      announced.current = false;
      region.textContent = "";
    } else if (!announced.current) {
      announced.current = true;
      region.textContent = lowAnnouncement(seconds);
    }
  }, [low, seconds]);

  const Icon = paused ? PauseIcon : TimerIcon;
  return (
    <div className={cx(styles.phaseTimer, className)}>
      <p
        className={styles.pill}
        role="timer"
        aria-label={`Time left: ${secondsText(seconds)}${paused ? ", paused" : ""}`}
        data-low={low || undefined}
        data-paused={paused || undefined}
      >
        <Icon className={styles.icon} weight="light" size="1em" aria-hidden="true" />
        <span className={styles.time} aria-hidden="true">
          {clockText(seconds)}
        </span>
      </p>
      <p ref={liveRef} className={styles.visuallyHidden} role="status" />
    </div>
  );
}
