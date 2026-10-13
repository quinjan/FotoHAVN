import { CaretDownIcon } from "@phosphor-icons/react";
import type { ComponentPropsWithRef } from "react";
import { cx } from "../shared/cx.ts";
import { describedBy, FieldMessages, fieldStyles, present, RequiredMark, useFieldIds, type FieldText } from "../shared/field.tsx";
import styles from "./Select.module.css";

export interface SelectOption {
  readonly value: string;
  readonly label: string;
  readonly disabled?: boolean | undefined;
}

export interface SelectProps
  extends FieldText,
    Omit<
      ComponentPropsWithRef<"select">,
      "style" | "children" | "multiple" | "size" | "value" | "defaultValue" | "aria-invalid" | keyof FieldText
    > {
  readonly options: readonly SelectOption[];
  readonly value?: string | undefined;
  readonly defaultValue?: string | undefined;
  /**
   * A first, unselectable line shown while nothing is chosen, e.g. "Choose a camera". Its value is the empty string,
   * so a `required` select with nothing chosen is invalid.
   */
  readonly placeholder?: string | undefined;
}

/**
 * An operator choice from a list, as a native `<select>` (keyboard, type-ahead, and the Windows list come free): the
 * label above, a 4 radius Soft Brown-bordered box with a Phosphor Light chevron, help under it, and the error in text with
 * `aria-invalid`. Use `OptionList` instead when the choices need thumbnails or descriptions.
 */
export function Select({
  label,
  help,
  error,
  id,
  options,
  placeholder,
  required = false,
  className,
  "aria-describedby": external,
  ...rest
}: SelectProps) {
  const ids = useFieldIds(id);
  return (
    <div className={cx(fieldStyles.field, className)}>
      <div className={fieldStyles.labelRow}>
        <label id={ids.label} htmlFor={ids.control} className={fieldStyles.label}>
          {label}
        </label>
        <RequiredMark required={required} />
      </div>
      <div className={styles.box}>
        <select
          {...rest}
          id={ids.control}
          required={required}
          className={cx(fieldStyles.control, styles.select)}
          aria-invalid={present(error) || undefined}
          aria-describedby={describedBy(ids, { help, error }, external)}
        >
          {placeholder !== undefined && (
            <option value="" disabled data-placeholder="">
              {placeholder}
            </option>
          )}
          {options.map((option) => (
            <option key={option.value} value={option.value} disabled={option.disabled}>
              {option.label}
            </option>
          ))}
        </select>
        <span className={styles.chevron} aria-hidden="true">
          <CaretDownIcon weight="light" size="1em" />
        </span>
      </div>
      <FieldMessages ids={ids} help={help} error={error} />
    </div>
  );
}
