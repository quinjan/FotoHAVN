import { InfoIcon, WarningCircleIcon } from "@phosphor-icons/react";
import type { ReactNode } from "react";
import { cx } from "../shared/cx.ts";
import styles from "./InlineStatus.module.css";

/** `neutral` for ordinary state ("Checking the camera."); `alert` for a problem, such as an Operator Assistance reason. */
export type InlineStatusTone = "neutral" | "alert";

export interface InlineStatusProps {
  /**
   * The short status line in sentence case, from the brain or DESIGN.md's copy. Empty renders only the live region,
   * so a screen can keep the status mounted and change its text, which is what assistive technology announces.
   */
  readonly children?: ReactNode;
  /** Default `neutral`. An alert takes the ink, a semibold weight, and a warning icon, never color alone. */
  readonly tone?: InlineStatusTone;
  /**
   * An optional action after the line, outside the live region so it isn't announced with every change, e.g. the
   * Operator Assistance alert's text link back to the running-Event panel (`TextLink size="meta"`).
   */
  readonly action?: ReactNode;
  readonly id?: string;
  readonly className?: string;
}

const icons = { neutral: InfoIcon, alert: WarningCircleIcon } as const;

function isEmpty(children: ReactNode): boolean {
  return children === undefined || children === null || children === false || children === "";
}

/**
 * A one-line status in a polite live region (`role="status"`), such as the camera state, "Saving.", or, as the
 * Operator Assistance alert on every Operator Screen view other than the Event list, the reason with a text link back
 * to the running-Event panel. A Phosphor Light icon and the text carry the tone; color never does alone. It never
 * disappears on its own; the screen clears it.
 */
export function InlineStatus({ children, tone = "neutral", action, id, className }: InlineStatusProps) {
  const empty = isEmpty(children);
  const Icon = icons[tone];
  return (
    <div id={id} className={cx(styles.status, className)} data-status-tone={tone} data-empty={empty || undefined}>
      <p className={styles.line} role="status">
        {!empty && (
          <>
            <span className={styles.icon} aria-hidden="true">
              <Icon weight="light" size="1em" />
            </span>
            <span className={styles.text}>{children}</span>
          </>
        )}
      </p>
      {!empty && action !== undefined && <div className={styles.action}>{action}</div>}
    </div>
  );
}
