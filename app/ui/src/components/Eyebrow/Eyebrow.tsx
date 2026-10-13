import type { ReactNode } from "react";
import { cx } from "../shared/cx.ts";
import styles from "./Eyebrow.module.css";

export interface EyebrowProps {
  /** The label as written, uppercase, e.g. `THE FOTOHAVN EXPERIENCE`. */
  readonly children: ReactNode;
  readonly id?: string;
  readonly className?: string;
}

/**
 * The uppercase sans label that introduces a heading (brand components.md): Soft Brown on light, the derived
 * secondary ink on Cream Paper, Antique Metal on dark. A paragraph, never a heading.
 */
export function Eyebrow({ children, id, className }: EyebrowProps) {
  return (
    <p id={id} className={cx(styles.eyebrow, className)}>
      {children}
    </p>
  );
}
