import { WarningCircleIcon } from "@phosphor-icons/react";
import { useId, type ReactNode } from "react";
import styles from "./field.module.css";

/**
 * Internals shared by the operator inputs (`TextField`, `TextArea`, `DateField`, `Select`, `Checkbox`, `Radio`,
 * `Switch`, `SegmentedChoice`) and `OptionList`: element ids, the label above a field, and the messages under it
 * (error, help, character counter), wired to the control with `aria-describedby`.
 */

/** The text around a field. Errors are text with an icon, never color alone; the control also gets `aria-invalid`. */
export interface FieldText {
  /** Shown above the field in the Meta role. */
  readonly label: ReactNode;
  /** Instructions or context under the field, in the Supporting role. */
  readonly help?: ReactNode;
  /** What's wrong, in words, e.g. "Enter a name for the Event." Sets `aria-invalid` on the control. */
  readonly error?: ReactNode;
}

export interface FieldIds {
  readonly control: string;
  readonly label: string;
  readonly help: string;
  readonly error: string;
  readonly counter: string;
}

export interface FieldCounter {
  readonly length: number;
  readonly max: number;
}

/** True when a message slot holds something to render. */
export function present(node: ReactNode): boolean {
  return node !== undefined && node !== null && node !== false && node !== "";
}

/** The control's id (the caller's, or a generated one) and the ids of its label and messages. */
export function useFieldIds(id: string | undefined): FieldIds {
  const generated = useId();
  const control = id ?? generated;
  return {
    control,
    label: `${control}-label`,
    help: `${control}-help`,
    error: `${control}-error`,
    counter: `${control}-counter`,
  };
}

/** Joins element ids for an ARIA id-list attribute, skipping empty ones; `undefined` when none remain. */
export function joinIds(...ids: readonly (string | undefined)[]): string | undefined {
  const joined = ids.filter((part): part is string => part !== undefined && part !== "").join(" ");
  return joined === "" ? undefined : joined;
}

/** The `aria-describedby` value: the error first, then help, then the counter, then any ids the caller passed. */
export function describedBy(
  ids: FieldIds,
  { help, error, counter = false }: { readonly help?: ReactNode; readonly error?: ReactNode; readonly counter?: boolean },
  external?: string,
): string | undefined {
  return joinIds(
    present(error) ? ids.error : undefined,
    present(help) ? ids.help : undefined,
    counter ? ids.counter : undefined,
    external,
  );
}

/** "Required" beside a label: visible, and hidden from assistive technology, which hears `required` on the control. */
export function RequiredMark({ required }: { readonly required: boolean }) {
  return required ? (
    <span className={styles.requirement} aria-hidden="true">
      Required
    </span>
  ) : null;
}

/** The error line: a Phosphor Light warning icon and the message in the primary ink. */
export function FieldError({ id, children }: { readonly id: string; readonly children: ReactNode }) {
  return (
    <p id={id} className={styles.error}>
      <span className={styles.errorIcon} aria-hidden="true">
        <WarningCircleIcon weight="light" size="1em" />
      </span>
      <span>{children}</span>
    </p>
  );
}

/** The messages under a field: the error, then help on the left and the character counter on the right. */
export function FieldMessages({
  ids,
  help,
  error,
  counter,
}: {
  readonly ids: FieldIds;
  readonly help?: ReactNode;
  readonly error?: ReactNode;
  readonly counter?: FieldCounter | undefined;
}) {
  const hasHelp = present(help);
  if (!present(error) && !hasHelp && counter === undefined) {
    return null;
  }
  return (
    <div className={styles.messages}>
      {present(error) && <FieldError id={ids.error}>{error}</FieldError>}
      {(hasHelp || counter !== undefined) && (
        <div className={styles.footer}>
          {hasHelp && (
            <p id={ids.help} className={styles.help}>
              {help}
            </p>
          )}
          {counter !== undefined && (
            <p id={ids.counter} className={styles.counter} data-over={counter.length > counter.max || undefined}>
              <span aria-hidden="true">
                {counter.length} / {counter.max}
              </span>
              <span className={styles.visuallyHidden}>
                {counter.length} of {counter.max} characters
              </span>
            </p>
          )}
        </div>
      )}
    </div>
  );
}

export { styles as fieldStyles };
