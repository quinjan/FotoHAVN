import { useState } from "react";
import { ConfirmDialog } from "../ConfirmDialog/index.ts";
import { TextLink } from "../TextLink/index.ts";
import { cx } from "../shared/cx.ts";
import styles from "./StartOverAction.module.css";

/** The Guest Cycle steps that offer Start Over. Keepsake never does: its Guest Cycle is already completed. */
export type StartOverStep = "template" | "photographs" | "filters";

const startOverSteps: ReadonlySet<string> = new Set<StartOverStep>(["template", "photographs", "filters"]);

export interface StartOverActionProps {
  /** The step showing the action. Any other step (Keepsake) renders nothing. */
  readonly step: StartOverStep;
  /**
   * Whether the confirmation is open, from the brain's `startOverConfirmation` overlay. The brain closes it as
   * `KEEP GOING` when the Phase Timer runs out. Omit it to let the component open and close the confirmation itself.
   */
  readonly confirming?: boolean;
  /** The guest tapped `START OVER` (the `StartOver` command). */
  readonly onStartOver?: () => void;
  /** The guest confirmed (`ConfirmStartOver`): the Guest Cycle ends and the booth returns to Start. */
  readonly onConfirm: () => void;
  /** The guest chose `KEEP GOING`, or pressed Escape (`KeepGoing`). */
  readonly onKeepGoing: () => void;
  readonly className?: string;
}

/**
 * The quiet `START OVER` text action in the guest top band (app/DESIGN.md, Start Over), on the Template, Photographs,
 * and Filters steps only. It opens the confirmation "Start over?" with `START OVER` (the one primary action) and
 * `KEEP GOING`; focus starts on `KEEP GOING` and returns to the action when the confirmation closes.
 */
export function StartOverAction({ step, confirming, onStartOver, onConfirm, onKeepGoing, className }: StartOverActionProps) {
  const [ownConfirming, setOwnConfirming] = useState(false);
  if (!startOverSteps.has(step)) {
    return null;
  }
  const open = confirming ?? ownConfirming;
  return (
    <>
      <TextLink
        className={cx(styles.action, className)}
        aria-haspopup="dialog"
        onClick={() => {
          setOwnConfirming(true);
          onStartOver?.();
        }}
      >
        START OVER
      </TextLink>
      {open && (
        <ConfirmDialog
          title="Start over?"
          body="Your photographs stay with the Event, but no strip will be made."
          confirmLabel="START OVER"
          cancelLabel="KEEP GOING"
          onConfirm={() => {
            setOwnConfirming(false);
            onConfirm();
          }}
          onCancel={() => {
            setOwnConfirming(false);
            onKeepGoing();
          }}
        />
      )}
    </>
  );
}
