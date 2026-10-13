import { CircleIcon } from "@phosphor-icons/react";
import { useEffect, useRef } from "react";
import { cx } from "../shared/cx.ts";
import styles from "./CaptureProgress.module.css";

export interface CaptureProgressProps {
  /** Captures kept so far in this Photo Strip's windows. */
  readonly taken: number;
  /** The Template's windows, e.g. 4. */
  readonly total: number;
  readonly className?: string;
}

/**
 * How many of the Template's windows hold a Capture (app/DESIGN.md, Photographs: Camera): the count as text ("2 of
 * 4"), with one mark per window, filled once it holds a Capture, so the state is never color alone. It announces
 * each completed Capture politely ("Photograph 2 of 4 taken."), never on first render.
 */
export function CaptureProgress({ taken, total, className }: CaptureProgressProps) {
  const count = Math.min(Math.max(0, taken), total);
  const liveRef = useRef<HTMLSpanElement>(null);
  const previous = useRef(count);

  useEffect(() => {
    const before = previous.current;
    previous.current = count;
    if (liveRef.current !== null && count > before) {
      liveRef.current.textContent = `Photograph ${String(count)} of ${String(total)} taken.`;
    }
  }, [count, total]);

  return (
    <div className={cx(styles.progress, className)}>
      <div
        className={styles.meter}
        role="progressbar"
        aria-label="Photographs"
        aria-valuemin={0}
        aria-valuemax={total}
        aria-valuenow={count}
        aria-valuetext={`${String(count)} of ${String(total)}`}
      >
        <span className={styles.marks} aria-hidden="true">
          {Array.from({ length: total }, (_, index) => (
            <span key={index} className={styles.mark} data-taken={index < count || undefined}>
              <CircleIcon weight={index < count ? "fill" : "light"} size="1em" />
            </span>
          ))}
        </span>
        <span className={styles.count}>
          {count} of {total}
        </span>
      </div>
      <span ref={liveRef} className={styles.visuallyHidden} aria-live="polite" />
    </div>
  );
}
