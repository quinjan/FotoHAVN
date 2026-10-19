import type { ComponentPropsWithRef } from "react";
import { cx } from "../shared/cx.ts";
import { describedBy, FieldMessages, fieldStyles, joinIds, present, RequiredMark, useFieldIds, type FieldText } from "../shared/field.tsx";
import styles from "./DateField.module.css";

export interface DateFieldProps
  extends FieldText,
    Omit<ComponentPropsWithRef<"input">, "type" | "style" | "children" | "size" | "value" | "defaultValue" | "aria-invalid" | keyof FieldText> {
  /** A local date as `YYYY-MM-DD`, the native date input's value. */
  readonly value?: string | undefined;
  readonly defaultValue?: string | undefined;
  /** The earliest date, `YYYY-MM-DD`. */
  readonly min?: string | undefined;
  /** The latest date, `YYYY-MM-DD`. */
  readonly max?: string | undefined;
  /**
   * Writes the chosen date in words beside the input ("Tue 13 Oct 2026"), because the native input shows Windows' own
   * short format. Receives the input's value; the words also join the input's description. Needs a controlled `value`.
   */
  readonly formatValue?: ((value: string) => string) | undefined;
}

/**
 * An operator date input, such as the Event's date (required, a local date, today by default; the screen supplies
 * today). A native date input styled like the other fields, so Windows supplies the picker and the keyboard entry, with
 * the label above, the date in words beside it when `formatValue` is given, help under it, and the error in text with
 * `aria-invalid`.
 */
export function DateField({
  label,
  help,
  error,
  id,
  required = false,
  className,
  formatValue,
  value,
  "aria-describedby": external,
  ...rest
}: DateFieldProps) {
  const ids = useFieldIds(id);
  const words = value === undefined || value === "" ? "" : (formatValue?.(value) ?? "");
  const wordsId = `${ids.control}-words`;
  const hasWords = words !== "" && words !== value;
  return (
    <div className={cx(fieldStyles.field, className)}>
      <div className={fieldStyles.labelRow}>
        <label id={ids.label} htmlFor={ids.control} className={fieldStyles.label}>
          {label}
        </label>
        <RequiredMark required={required} />
      </div>
      <div className={styles.row}>
        <input
          {...rest}
          id={ids.control}
          type="date"
          value={value}
          required={required}
          className={cx(fieldStyles.control, styles.date)}
          aria-invalid={present(error) || undefined}
          aria-describedby={describedBy(ids, { help, error }, joinIds(hasWords ? wordsId : undefined, external))}
        />
        {hasWords && (
          <span id={wordsId} className={styles.words}>
            {words}
          </span>
        )}
      </div>
      <FieldMessages ids={ids} help={help} error={error} />
    </div>
  );
}
