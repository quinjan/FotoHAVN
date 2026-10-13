import { cx } from "../shared/cx.ts";
import styles from "./StepProgress.module.css";

/** The four Guest Cycle steps, in order. */
export type GuestStepId = "template" | "photographs" | "filters" | "keepsake";

/** The steps with their numbers and labels, exactly as DESIGN.md's copy table writes them (`01 Template`). */
export const guestSteps: readonly { readonly id: GuestStepId; readonly number: string; readonly label: string }[] = [
  { id: "template", number: "01", label: "Template" },
  { id: "photographs", number: "02", label: "Photographs" },
  { id: "filters", number: "03", label: "Filters" },
  { id: "keepsake", number: "04", label: "Keepsake" },
];

export interface StepProgressProps {
  /** The step the guest is on; it carries `aria-current="step"`. */
  readonly current: GuestStepId;
  /**
   * Going back, as on the website: when given, each completed step is a button that calls this with its id. Omit it
   * where going back isn't possible (Keepsake, where the Guest Cycle is already completed).
   */
  readonly onSelectStep?: (step: GuestStepId) => void;
  /** Completed steps the Event skipped (one Template or one Look); they show as completed but aren't offered. */
  readonly skipped?: readonly GuestStepId[];
  /** The list's accessible name. */
  readonly label?: string;
  readonly className?: string;
}

/**
 * The guest's step progress (brand components.md): a numbered row, `01 Template` to `04 Keepsake`, in the guest top
 * band. Numbers use the Meta role. The current step is the ink with a 2 px ink underline and `aria-current="step"`;
 * the others take the secondary ink. Completed steps the guest may return to are buttons with a Hairline underline,
 * so going back never depends on color; every completed step also says "completed" to assistive technology.
 */
export function StepProgress({ current, onSelectStep, skipped = [], label = "Steps", className }: StepProgressProps) {
  const currentIndex = guestSteps.findIndex((step) => step.id === current);
  return (
    <ol className={cx(styles.progress, className)} aria-label={label}>
      {guestSteps.map((step, index) => {
        const content = (
          <>
            <span className={styles.number}>{step.number}</span>{" "}
            <span className={styles.label}>{step.label}</span>
          </>
        );
        if (index >= currentIndex) {
          const isCurrent = index === currentIndex;
          return (
            <li key={step.id} className={styles.item}>
              <span
                className={styles.step}
                data-state={isCurrent ? "current" : "upcoming"}
                aria-current={isCurrent ? "step" : undefined}
              >
                {content}
              </span>
            </li>
          );
        }
        const completed = (
          <>
            {content}
            <span className={styles.visuallyHidden}>, completed</span>
          </>
        );
        return (
          <li key={step.id} className={styles.item}>
            {onSelectStep !== undefined && !skipped.includes(step.id) ? (
              <button
                type="button"
                className={styles.step}
                data-state="completed"
                data-revisit="true"
                onClick={() => {
                  onSelectStep(step.id);
                }}
              >
                {completed}
              </button>
            ) : (
              <span className={styles.step} data-state="completed">
                {completed}
              </span>
            )}
          </li>
        );
      })}
    </ol>
  );
}
