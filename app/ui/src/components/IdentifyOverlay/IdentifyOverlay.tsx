import { cx } from "../shared/cx.ts";
import styles from "./IdentifyOverlay.module.css";

export interface IdentifyOverlayProps {
  /** The display's number, as the display picker lists it. */
  readonly number: number;
  /** The display's name, e.g. "DELL U2720Q"; set in the Meta role. */
  readonly name: string;
  /** Whether the overlay shows. The brain owns the 3-second timer and sets this; hidden renders nothing. */
  readonly visible?: boolean;
  readonly className?: string;
}

/**
 * The `IDENTIFY DISPLAYS` overlay for one display (DESIGN.md, Operator Screen): an Off-white panel centered in the
 * space it fills, with the display number in the Display role and its name in the Meta role. It holds nothing
 * focusable and never takes focus, so the operator's place in Event setup is kept while it shows for 3 seconds.
 */
export function IdentifyOverlay({ number, name, visible = true, className }: IdentifyOverlayProps) {
  if (!visible) {
    return null;
  }
  return (
    <div className={cx(styles.overlay, className)}>
      <div className={styles.panel} data-tone="light">
        <p className={styles.number}>{number}</p>
        <p className={styles.name}>{name}</p>
      </div>
    </div>
  );
}
