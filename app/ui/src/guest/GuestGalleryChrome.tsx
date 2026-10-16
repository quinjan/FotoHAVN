import { useState, type ReactNode } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { PhaseTimerWarning } from "../components/PhaseTimerWarning/index.ts";
import { cx } from "../components/shared/cx.ts";
import type { GuestOverlays } from "../protocol/generated.ts";
import styles from "./GuestGalleryChrome.module.css";

/**
 * What both gallery views share (M9): the guest_gallery Phase Timer's warning with `MORE TIME` (`MoreTime`), drawn from
 * `overlays` alone. The gallery has no top band, so the warning sits at the end of the footer line beside `BACK TO START`,
 * over space that line already keeps: nothing moves when it arrives or leaves, and the strip row keeps its height.
 */
export function GuestGalleryChrome({ overlays, children }: { readonly overlays: GuestOverlays; readonly children: ReactNode }) {
  const client = useBrainClient();
  const warning = overlays.phaseTimerWarning?.phase === "guestGallery" ? overlays.phaseTimerWarning : null;
  // A warning already under way when the view mounts (a reconnect, a fixture, a strip opened mid-warning) is shown at rest instead of arriving again.
  const [warnedAtMount, setWarnedAtMount] = useState(warning !== null);
  if (warning === null && warnedAtMount) {
    setWarnedAtMount(false);
  }
  return (
    <div className={styles.frame}>
      {children}
      {warning !== null && (
        <PhaseTimerWarning
          className={cx(styles.warning)}
          phase="guest-gallery"
          secondsRemaining={warning.remainingSeconds}
          arrive={!warnedAtMount}
          onMoreTime={() => {
            void client.sendCommand("MoreTime", {});
          }}
        />
      )}
    </div>
  );
}
