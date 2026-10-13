import { useEffect, useRef, useState } from "react";
import { cx } from "../shared/cx.ts";
import { secondsText } from "../shared/secondsText.ts";
import styles from "./CountdownNumeral.module.css";

export interface CountdownNumeralProps {
  /** Whole seconds left before the next Capture, pushed by the brain every second. 0 or less shows nothing. */
  readonly value: number;
  readonly className?: string;
}

/**
 * The countdown before each Capture (app/DESIGN.md, Countdown): one large Cormorant Garamond numeral per second in
 * the Display role, over the live preview (`--app-z-stage-overlay`). Each numeral fades in and rises at most 16 px
 * at `standard`; under reduced motion it appears with no movement. It is announced once, politely, when a countdown
 * starts ("Next photograph in 5 seconds."), never every second.
 */
export function CountdownNumeral({ value, className }: CountdownNumeralProps) {
  const liveRef = useRef<HTMLSpanElement>(null);
  const previous = useRef<number | null>(null);
  // The first numeral rendered appears as it is (so fixtures and a screen that mounts mid-countdown are still); every
  // later one animates in.
  const [shown, setShown] = useState({ value, changed: false });
  if (shown.value !== value) {
    setShown({ value, changed: true });
  }

  useEffect(() => {
    const live = liveRef.current;
    const before = previous.current;
    previous.current = value;
    if (live === null) {
      return;
    }
    if (value <= 0) {
      live.textContent = "";
    } else if (before === null || before <= 0 || value > before) {
      live.textContent = `Next photograph in ${secondsText(value)}.`;
    }
  }, [value]);

  return (
    <div className={cx(styles.countdown, className)}>
      {value > 0 && (
        <span key={value} className={styles.numeral} data-enter={shown.changed || undefined}>
          {value}
        </span>
      )}
      <span ref={liveRef} className={styles.visuallyHidden} aria-live="polite" />
    </div>
  );
}
