import type { CSSProperties } from "react";
import { PrintObject } from "../components/PrintObject/index.ts";
import { cx } from "../components/shared/cx.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { GuestCycleView, KeepsakeView } from "../protocol/generated.ts";
import { keepsakeCopy, keepsakeStripAlt } from "./finishCopy.ts";
import styles from "./KeepsakeStep.module.css";

/**
 * The Keepsake step (plan section 7, Steps; app/DESIGN.md, Guest Screen): a centered single-subject moment. The finished Photo
 * Strip, lifted with the `print` shadow, is revealed like paper developing (a fade and a short settle over `reveal`, 600 ms;
 * reduced motion shows it at once), beside the statement. The Guest Cycle is already completed, so there is no warning and
 * no START OVER here; the cycle chrome sends neither, and this step has no action: it ends on its own.
 * A landscape strip (Panorama Four) is turned a quarter turn, as everywhere on the Guest Screen; the file keeps its orientation.
 */
export function KeepsakeStep({ view }: { readonly cycle: GuestCycleView; readonly view: KeepsakeView }) {
  const ratio = view.landscape ? view.height / view.width : view.width / view.height;
  return (
    <div className={styles.step} data-strip-landscape={view.landscape}>
      <div className={styles.strip} style={{ "--strip-ratio": String(ratio) } as CSSProperties}>
        <div className={styles.reveal}>
          <PrintObject src={view.stripUrl} alt={keepsakeStripAlt} width={view.width} height={view.height} shadow="lifted" rotated={view.landscape} />
        </div>
      </div>
      <StatementHeading level={1} size="heading-lg" align="center" className={cx(styles.statement)} {...keepsakeCopy} />
    </div>
  );
}
