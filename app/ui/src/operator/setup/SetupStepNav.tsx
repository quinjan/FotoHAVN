import { WarningCircleIcon } from "@phosphor-icons/react";
import { copy } from "./setupCopy.ts";
import { setupSteps, stepNumber, type SetupStepId } from "./setupSteps.ts";
import styles from "./SetupWizard.module.css";

export interface SetupStepNavProps {
  readonly current: SetupStepId;
  /** Steps holding a field the brain marked after a save attempt; each says so in words and with an icon. */
  readonly attention: ReadonlySet<SetupStepId>;
  readonly onSelect: (step: SetupStepId) => void;
}

/**
 * Event setup's step row (`01 Event` to `05 Guests`), styled after the guest step progress: Meta numbers, the current
 * step in the ink with a 2 px underline and `aria-current="step"`. Every step can be opened in any order, since the
 * brain validates only at save.
 */
export function SetupStepNav({ current, attention, onSelect }: SetupStepNavProps) {
  return (
    <nav className={styles.nav} aria-label={copy.stepsLabel}>
      <ol className={styles.steps}>
        {setupSteps.map((step) => {
          const isCurrent = step.id === current;
          const needsAttention = attention.has(step.id);
          return (
            <li key={step.id} className={styles.stepItem}>
              <button
                type="button"
                className={styles.step}
                data-state={isCurrent ? "current" : "other"}
                data-step={step.id}
                aria-current={isCurrent ? "step" : undefined}
                onClick={() => {
                  onSelect(step.id);
                }}
              >
                {needsAttention && (
                  <span className={styles.stepIcon} aria-hidden="true">
                    <WarningCircleIcon weight="light" size="1em" />
                  </span>
                )}
                <span className={styles.stepNumber}>{stepNumber(step.id)}</span>{" "}
                <span className={styles.stepLabel}>{step.label}</span>
                {needsAttention && <span className={styles.visuallyHidden}>{copy.stepNeedsAttention}</span>}
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
