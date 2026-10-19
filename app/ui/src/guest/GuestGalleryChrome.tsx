import type { ReactNode } from "react";
import { PhaseTimerPill } from "../components/PhaseTimerPill/index.ts";
import { cx } from "../components/shared/cx.ts";
import type { GuestOverlays } from "../protocol/generated.ts";
import styles from "./GuestGalleryChrome.module.css";

/**
 * What both gallery views share (M9): the guest gallery's Phase Timer, drawn from `overlays` alone. The gallery has no
 * top band, so the same pill as the Guest Cycle's sits in the top-right corner, just left of the exit hold, over space
 * the gallery already keeps clear: nothing moves when it arrives or leaves. There is no way to extend it.
 */
export function GuestGalleryChrome({ overlays, children }: { readonly overlays: GuestOverlays; readonly children: ReactNode }) {
  const timer = overlays.phaseTimer;
  return (
    <div className={styles.frame}>
      {children}
      {timer !== null && (
        <div className={styles.timer}>
          <PhaseTimerPill key={timer.phase} className={cx(styles.pill)} secondsRemaining={timer.remainingSeconds} paused={timer.paused} />
        </div>
      )}
    </div>
  );
}