import type { ReactNode } from "react";
import { cx } from "../shared/cx.ts";
import { TextLink } from "../TextLink/index.ts";
import styles from "./Notice.module.css";

export interface NoticeProps {
  /**
   * The notice in sentence case, usually from the brain ("Move cancelled."). Empty renders only the live region, so a
   * screen can keep the Notice mounted where notices appear and change its message, which is what gets announced.
   */
  readonly children?: ReactNode;
  /** An optional action the notice offers, such as a `TextLink` to `TRY AGAIN`. */
  readonly action?: ReactNode;
  /** When given, a `CLOSE` text action dismisses the notice; the screen then clears it. */
  readonly onDismiss?: () => void;
  readonly id?: string;
  readonly className?: string;
}

function isEmpty(children: ReactNode): boolean {
  return children === undefined || children === null || children === false || children === "";
}

/**
 * A short notice in a polite live region (`role="status"`), bordered with a Hairline on whatever surface it sits on;
 * its inks follow the surface's tone, light or dark. It never dismisses itself: no toast timers. It stays until the
 * screen clears it or the reader chooses `CLOSE`. Its actions sit outside the live region.
 */
export function Notice({ children, action, onDismiss, id, className }: NoticeProps) {
  const empty = isEmpty(children);
  const hasActions = !empty && (action !== undefined || onDismiss !== undefined);
  return (
    <div id={id} className={cx(styles.notice, className)} data-empty={empty || undefined}>
      <div className={styles.message} role="status">
        {children}
      </div>
      {hasActions && (
        <div className={styles.actions}>
          {action}
          {onDismiss !== undefined && (
            <TextLink size="meta" onClick={onDismiss}>
              CLOSE
            </TextLink>
          )}
        </div>
      )}
    </div>
  );
}
