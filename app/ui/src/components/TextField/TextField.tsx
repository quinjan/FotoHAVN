import { useState, type ChangeEvent, type ComponentPropsWithRef } from "react";
import { cx } from "../shared/cx.ts";
import { describedBy, FieldMessages, fieldStyles, present, RequiredMark, useFieldIds, type FieldText } from "../shared/field.tsx";
import styles from "./TextField.module.css";

export interface TextFieldProps
  extends FieldText,
    Omit<
      ComponentPropsWithRef<"input">,
      "type" | "style" | "children" | "size" | "value" | "defaultValue" | "aria-invalid" | keyof FieldText
    > {
  readonly type?: "text" | "email" | "tel" | "url" | "search" | undefined;
  readonly value?: string | undefined;
  readonly defaultValue?: string | undefined;
  /** Shows the character counter under the field when `maxLength` applies. On by default. */
  readonly showCount?: boolean | undefined;
}

/**
 * A single-line operator text input (DESIGN.md, Event setup): the label above the field in the Meta role, a 4 radius
 * box with a Hairline border, help and a character counter under it, and the error in text with `aria-invalid`.
 * `maxLength` limits entry and shows the counter (Event name 80, venue and Customer or host 120). `readOnly` shows a
 * fixed value such as "No printer"; `disabled` takes its reason as `help`.
 */
export function TextField({
  label,
  help,
  error,
  id,
  type = "text",
  value,
  defaultValue,
  maxLength,
  showCount = true,
  required = false,
  className,
  onChange,
  "aria-describedby": external,
  ...rest
}: TextFieldProps) {
  const ids = useFieldIds(id);
  const [typedLength, setTypedLength] = useState(() => (defaultValue ?? "").length);
  const counter =
    showCount && maxLength !== undefined ? { length: value === undefined ? typedLength : value.length, max: maxLength } : undefined;

  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    setTypedLength(event.target.value.length);
    onChange?.(event);
  }

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
        type={type}
        value={value}
        defaultValue={defaultValue}
        maxLength={maxLength}
        required={required}
        className={cx(fieldStyles.control, styles.input)}
        aria-invalid={present(error) || undefined}
        aria-describedby={describedBy(ids, { help, error, counter: counter !== undefined }, external)}
        onChange={handleChange}
      />
      <FieldMessages ids={ids} help={help} error={error} counter={counter} />
    </div>
  );
}
