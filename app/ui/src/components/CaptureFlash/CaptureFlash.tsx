import { useEffect, useState } from "react";
import { cx } from "../shared/cx.ts";
import { usePrefersReducedMotion } from "../shared/reducedMotion.ts";
import styles from "./CaptureFlash.module.css";

/**
 * How long one flash shows: the top of the brand's 320–380 ms flash range. The fade itself runs for
 * `--motion-duration-flash`, inside this window. Because a new flash can't start while one shows, flashes are always
 * more than a third of a second apart: never more than three a second, never a strobe.
 */
export const flashWindowMs = 380;

export interface CaptureFlashProps {
  /**
   * Changes once per Capture, for example the committed Capture's id; each new value plays one flash. The value
   * present when the flash mounts doesn't play, so returning to the Camera tab never flashes. A value that arrives
   * while a flash still shows doesn't play either.
   */
  readonly flashKey?: string | number | null;
  /** Holds the flash at its brightest, without fading or timing out; for fixtures. */
  readonly peak?: boolean;
  readonly className?: string;
}

/**
 * The simulated exposure flash at each Capture (app/DESIGN.md, Capture flash): one Off-white flash over the stage that
 * fades at `flash` with the editorial ease, at `--app-z-flash`. Under reduced motion it is a near-instant static
 * change instead: a still tint for the same moment, with no fade. Decorative; the capture progress announces the
 * Capture. Place it inside the positioned stage it covers.
 */
export function CaptureFlash({ flashKey = null, peak = false, className }: CaptureFlashProps) {
  const reducedMotion = usePrefersReducedMotion();
  const [flash, setFlash] = useState({ key: flashKey, run: 0, active: false });
  if (flashKey !== flash.key) {
    const play = flashKey !== null && !flash.active;
    setFlash({ key: flashKey, run: play ? flash.run + 1 : flash.run, active: play || flash.active });
  }

  useEffect(() => {
    if (!flash.active) {
      return undefined;
    }
    const timer = window.setTimeout(() => {
      setFlash((current) => ({ ...current, active: false }));
    }, flashWindowMs);
    return () => {
      window.clearTimeout(timer);
    };
  }, [flash.active, flash.run]);

  const state = peak ? "peak" : flash.active ? "flash" : "idle";
  return (
    <div
      key={flash.run}
      className={cx(styles.flash, className)}
      aria-hidden="true"
      data-state={state}
      data-motion={reducedMotion ? "static" : "animated"}
    />
  );
}
