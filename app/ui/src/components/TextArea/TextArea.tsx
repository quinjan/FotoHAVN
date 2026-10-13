import { useState, type ChangeEvent, type ComponentPropsWithRef } from "react";
import { cx } from "../shared/cx.ts";
import { describedBy, FieldMessages, fieldStyles, present, RequiredMark, useFieldIds, type FieldText } from "../shared/field.tsx";
import styles from "./TextArea.module.css";

export interface TextAreaProps
  extends FieldText,
    Omit<ComponentPropsWithRef<"textarea">, "style" | "children" | "value" | "defaultValue" | "aria-invalid" | keyof FieldText> {
  readonly value?: string | undefined;
  readonly defaultValue?: string | undefined;
  /** Shows the character counter under the field when `maxLength` applies. On by default. */
  readonly showCount?: boolean | undefined;
}

/**
 * A multi-line operator text input, such as the Event's notes (up to 1,000 characters): the label above, a 4 radius
 * Hairline box that the operator may resize vertically, help and the character counter under it, and the error in
 * text with `aria-invalid`.
 */
export function TextArea({
  label,
  help,
  error,
  id,
  value,
  defaultValue,
  maxLength,
  rows = 4,
  showCount = true,
  required = false,
  className,
  onChange,
  "aria-describedby": external,
  ...rest
}: TextAreaProps) {
  const ids = useFieldIds(id);
  const [typedLength, setTypedLength] = useState(() => (defaultValue ?? "").length);
  const counter =
    showCount && maxLength !== undefined ? { length: value === undefined ? typedLength : value.length, max: maxLength } : undefined;

  function handleChange(event: ChangeEvent<HTMLTextAreaElement>) {
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
      <textarea
        {...rest}
        id={ids.control}
        rows={rows}
        value={value}
        defaultValue={defaultValue}
        maxLength={maxLength}
        required={required}
        className={cx(fieldStyles.control, styles.textarea)}
        aria-invalid={present(error) || undefined}
        aria-describedby={describedBy(ids, { help, error, counter: counter !== undefined }, external)}
        onChange={handleChange}
      />
      <FieldMessages ids={ids} help={help} error={error} counter={counter} />
    </div>
  );
}
