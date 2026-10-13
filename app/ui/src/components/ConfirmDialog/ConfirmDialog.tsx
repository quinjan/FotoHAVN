import { useRef } from "react";
import { PrimaryAction } from "../PrimaryAction/index.ts";
import { SecondaryAction } from "../SecondaryAction/index.ts";
import { cx } from "../shared/cx.ts";
import { Dialog } from "../Dialog/index.ts";
import styles from "./ConfirmDialog.module.css";

export interface ConfirmDialogProps {
  /** The question, sentence case, e.g. "Start over?" or "End this Event?". */
  readonly title: string;
  /** What confirming means, in one or two short sentences. */
  readonly body: string;
  /** The uppercase confirm label, e.g. `START OVER` or `END EVENT`: the dialog's one primary action. */
  readonly confirmLabel: string;
  /** The uppercase cancel label, e.g. `KEEP GOING` or `KEEP RUNNING`: a secondary action. */
  readonly cancelLabel: string;
  readonly onConfirm: () => void;
  /** The cancel action and Escape. */
  readonly onCancel: () => void;
  readonly className?: string;
}

/**
 * A confirmation (app/DESIGN.md, Dialogs): an `alertdialog` with a title, a body line, the confirm action as the one
 * primary action, and the cancel action as a secondary action. It opens with focus on the cancel action, the choice
 * that loses nothing; Escape cancels, and focus returns to the opener on close. Use it for Start Over, End Event,
 * deletions, and leaving with changes.
 */
export function ConfirmDialog({ title, body, confirmLabel, cancelLabel, onConfirm, onCancel, className }: ConfirmDialogProps) {
  const cancelRef = useRef<HTMLButtonElement>(null);
  return (
    <Dialog
      role="alertdialog"
      title={title}
      description={body}
      onCancel={onCancel}
      initialFocus={cancelRef}
      className={cx(styles.confirm, className)}
      actions={
        <>
          <PrimaryAction onClick={onConfirm}>{confirmLabel}</PrimaryAction>
          <SecondaryAction ref={cancelRef} onClick={onCancel}>
            {cancelLabel}
          </SecondaryAction>
        </>
      }
    />
  );
}
