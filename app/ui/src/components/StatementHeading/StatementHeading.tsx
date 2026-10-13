import type { ReactNode } from "react";
import { Eyebrow } from "../Eyebrow/index.ts";
import { cx } from "../shared/cx.ts";
import styles from "./StatementHeading.module.css";

/** The serif type role the heading takes; sizes resolve per window role (kiosk scale on the Guest Screen). */
export type StatementSize = "display" | "heading-lg" | "heading" | "heading-sm" | "subheading";

export interface StatementHeadingProps {
  /** The roman part of the sentence-case heading, e.g. "Small room." Never empty: a heading is never all italic. */
  readonly lead: string;
  /**
   * The italic turn: the closing phrase, rendered in `<em>` after the lead, e.g. "Whole other world." One per
   * heading, always at the end, so it is a prop and not free markup. Omit it for a plain heading such as "Events".
   */
  readonly turn?: string;
  /** Sets the italic turn on its own line at 0.86em (brand typography.md). */
  readonly turnOnOwnLine?: boolean;
  /** The heading element level. Default 2. */
  readonly level?: 1 | 2 | 3 | 4;
  /** Default `heading`. */
  readonly size?: StatementSize;
  /** `start` (default) everywhere except single-subject moments such as Start, Keepsake, and the guest gallery. */
  readonly align?: "start" | "center";
  /** An uppercase eyebrow above the heading. */
  readonly eyebrow?: string;
  /** One or two short Supporting lines under the heading. */
  readonly supporting?: string | readonly [string] | readonly [string, string];
  /** An optional action after the supporting lines, usually a PrimaryAction or TextLink. */
  readonly action?: ReactNode;
  /** The heading element's id, for `aria-labelledby`. */
  readonly id?: string;
  readonly className?: string;
}

/**
 * The signature statement (brand components.md): eyebrow, a sentence-case Cormorant Garamond heading that ends in an
 * italic turn, one or two Supporting lines, then an optional action.
 */
export function StatementHeading({
  lead,
  turn,
  turnOnOwnLine = false,
  level = 2,
  size = "heading",
  align = "start",
  eyebrow,
  supporting,
  action,
  id,
  className,
}: StatementHeadingProps) {
  const Heading = `h${String(level)}` as "h1" | "h2" | "h3" | "h4";
  const lines = supporting === undefined ? [] : typeof supporting === "string" ? [supporting] : supporting;
  return (
    <div className={cx(styles.statement, className)} data-align={align}>
      {eyebrow !== undefined && <Eyebrow>{eyebrow}</Eyebrow>}
      <Heading id={id} className={styles.heading} data-size={size}>
        {lead}
        {turn !== undefined && (
          <>
            {" "}
            <em className={styles.turn} data-own-line={turnOnOwnLine || undefined}>
              {turn}
            </em>
          </>
        )}
      </Heading>
      {lines.map((line) => (
        <p key={line} className={styles.supporting}>
          {line}
        </p>
      ))}
      {action !== undefined && <div className={styles.action}>{action}</div>}
    </div>
  );
}
