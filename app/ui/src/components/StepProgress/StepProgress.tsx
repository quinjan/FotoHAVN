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
   * The steps the Event shows, in order, renumbered consecutively from `01`: a one-Template Event leaves Template out and a
   * one-Look Event leaves Filters out (M8, Q26). Default: all four.
   */
  readonly steps?: readonly GuestStepId[];
  /** The list's accessible name. */
  readonly label?: string;
  readonly className?: string;
}

/**
 * The guest's step progress (brand components.md): a numbered row, `01 Template` to `04 Keepsake`, in the guest top
 * band. Numbers use the Meta role. The current step is the ink with a 2 px ink underline and `aria-current="step"`;
 * the others take the secondary ink, and every completed step also says "completed" to assistive technology.
 * It only shows where the guest is: no step is a control, so a guest never goes back by it (app/DESIGN.md, Deliberate departures).
 */
export function StepProgress({ current, steps, label = "Steps", className }: StepProgressProps) {
  const shown = steps === undefined ? guestSteps : guestSteps.filter((step) => steps.includes(step.id));
  const currentIndex = shown.findIndex((step) => step.id === current);
  return (
    <ol className={cx(styles.progress, className)} aria-label={label}>
      {shown.map((step, index) => {
        const state = index < currentIndex ? "completed" : index === currentIndex ? "current" : "upcoming";
        return (
          <li key={step.id} className={styles.item}>
            <span className={styles.step} data-state={state} aria-current={state === "current" ? "step" : undefined}>
              <span className={styles.number}>{String(index + 1).padStart(2, "0")}</span>{" "}
              <span className={styles.label}>{step.label}</span>
              {state === "completed" && <span className={styles.visuallyHidden}>, completed</span>}
            </span>
          </li>
        );
      })}
    </ol>
  );
}