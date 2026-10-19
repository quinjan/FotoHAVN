import type { Ref } from "react";
import { InlineStatus } from "../../components/InlineStatus/index.ts";
import type { EventSetupFieldError, PhaseTimerOption } from "../../protocol/generated.ts";
import { blocksOnlyStart, errorSummaryHeading, errorSummaryItem } from "./setupCopy.ts";
import styles from "./EventSetup.module.css";

const headingId = "setup-error-summary-heading";

export interface ErrorSummaryProps {
  /** The brain's marked fields in the order the steps show them; empty before a refused save. */
  readonly errors: readonly EventSetupFieldError[];
  readonly timers: readonly PhaseTimerOption[];
  /** The summary itself, which takes focus after a refused save. */
  readonly summaryRef: Ref<HTMLDivElement>;
  /** The operator chose a problem: open its step and focus its field. */
  readonly onSelect: (error: EventSetupFieldError) => void;
}

/**
 * The error summary (DESIGN.md, Event setup): above the step after a refused save, a heading that says what is blocked
 * (saving, or only starting when just the Operator PIN is missing) and one link per problem that opens its step and
 * focuses its field. The heading is a polite live region that stays mounted, so the announcement is made once.
 */
export function ErrorSummary({ errors, timers, summaryRef, onSelect }: ErrorSummaryProps) {
  const open = errors.length > 0;
  const otherBlockers = errors.some((error) => !blocksOnlyStart(error));
  return (
    <div
      ref={summaryRef}
      tabIndex={-1}
      className={styles.errorSummary}
      data-open={open || undefined}
      role={open ? "group" : undefined}
      aria-labelledby={open ? headingId : undefined}
    >
      <InlineStatus id={headingId} tone="alert">
        {errorSummaryHeading(errors)}
      </InlineStatus>
      {open && (
        <ul className={styles.errorList}>
          {errors.map((error) => (
            <li key={`${error.field}:${error.code}`}>
              <button
                type="button"
                className={styles.errorLink}
                onClick={() => {
                  onSelect(error);
                }}
              >
                {errorSummaryItem(error.field, error.code, timers, otherBlockers)}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
