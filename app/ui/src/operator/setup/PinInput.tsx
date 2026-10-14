import type { ChangeEvent, ComponentPropsWithRef } from "react";
import { cx } from "../../components/shared/cx.ts";
import {
  describedBy,
  FieldMessages,
  fieldStyles,
  present,
  RequiredMark,
  useFieldIds,
  type FieldText,
} from "../../components/shared/field.tsx";

export interface PinInputProps
  extends FieldText,
    Omit<
      ComponentPropsWithRef<"input">,
      "type" | "style" | "children" | "size" | "value" | "defaultValue" | "aria-invalid" | "onChange" | keyof FieldText
    > {
  readonly value: string;
  readonly onValueChange: (value: string) => void;
}

/**
 * A masked numeric input for the Operator PIN: the operator's own field, typed on the keyboard (the on-screen `PinPad`
 * is the Guest Screen's). It looks and behaves like the other operator inputs (label above, error in text under it) and
 * takes digits only, so a stray letter never reaches the brain.
 */
export function PinInput({
  label,
  help,
  error,
  id,
  value,
  onValueChange,
  required = false,
  className,
  "aria-describedby": external,
  ...rest
}: PinInputProps) {
  const ids = useFieldIds(id);
  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    onValueChange(event.target.value.replace(/\D/g, ""));
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
        type="password"
        inputMode="numeric"
        autoComplete="off"
        value={value}
        required={required}
        className={fieldStyles.control}
        aria-invalid={present(error) || undefined}
        aria-describedby={describedBy(ids, { help, error }, external)}
        onChange={handleChange}
      />
      <FieldMessages ids={ids} help={help} error={error} />
    </div>
  );
}
