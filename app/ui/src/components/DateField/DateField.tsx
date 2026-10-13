import type { ComponentPropsWithRef } from "react";
import { cx } from "../shared/cx.ts";
import { describedBy, FieldMessages, fieldStyles, present, RequiredMark, useFieldIds, type FieldText } from "../shared/field.tsx";
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
}

/**
 * An operator date input, such as the Event's date (required, a local date, today by default; the screen supplies
 * today). A native date input styled like the other fields, so Windows supplies the localized format and the picker,
 * with the label above, help under it, and the error in text with `aria-invalid`.
 */
export function DateField({
  label,
  help,
  error,
  id,
  required = false,
  className,
  "aria-describedby": external,
  ...rest
}: DateFieldProps) {
  const ids = useFieldIds(id);
  return (
    <div className={cx(fieldStyles.field, className)}>
      <div className={fieldStyles.labelRow}>
        <label id={ids.label} htmlFor={ids.control} className={fieldStyles.label}>
          {label}
        </label>
        <RequiredMark required={required} />
      </div>
      <input
        {...rest}
        id={ids.control}
        type="date"
        required={required}
        className={cx(fieldStyles.control, styles.date)}
        aria-invalid={present(error) || undefined}
        aria-describedby={describedBy(ids, { help, error }, external)}
      />
      <FieldMessages ids={ids} help={help} error={error} />
    </div>
  );
}
