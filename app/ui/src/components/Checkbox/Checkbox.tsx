import type { ComponentPropsWithRef, ReactNode } from "react";
import { ChoiceMark, choiceStyles } from "../shared/ChoiceMark.tsx";
import { cx } from "../shared/cx.ts";
import { describedBy, FieldMessages, fieldStyles, joinIds, present, useFieldIds } from "../shared/field.tsx";
import styles from "./Checkbox.module.css";

export interface CheckboxProps
  extends Omit<ComponentPropsWithRef<"input">, "type" | "style" | "children" | "size" | "aria-invalid" | "aria-labelledby"> {
  /** The choice, beside the box in the Body role. */
  readonly label: ReactNode;
  /** A Supporting line under the label, inside the clickable area. */
  readonly description?: ReactNode;
  /** Instructions under the control, outside the clickable area. */
  readonly help?: ReactNode;
  /** What's wrong, in words. Sets `aria-invalid`. */
  readonly error?: ReactNode;
}

/**
 * An operator checkbox: a native checkbox stretched invisibly over the whole row (at least the role's target size)
 * with a 4 radius box beside the label that fills with the ink and shows a check when checked. Space toggles it; the
 * error is text with `aria-invalid`. For a list of choices with thumbnails, use `OptionList`'s checkbox variant.
 */
export function Checkbox({
  label,
  description,
  help,
  error,
  id,
  className,
  "aria-describedby": external,
  ...rest
}: CheckboxProps) {
  const ids = useFieldIds(id);
  const descriptionId = `${ids.control}-description`;
  return (
    <div className={cx(fieldStyles.field, styles.root, className)}>
      <label className={choiceStyles.option}>
        <input
          {...rest}
          id={ids.control}
          type="checkbox"
          className={choiceStyles.input}
          aria-labelledby={ids.label}
          aria-invalid={present(error) || undefined}
          aria-describedby={describedBy(ids, { help, error }, joinIds(present(description) ? descriptionId : undefined, external))}
        />
        <ChoiceMark kind="checkbox" />
        <span className={choiceStyles.optionText}>
          <span id={ids.label} className={choiceStyles.optionLabel}>
            {label}
          </span>
          {present(description) && (
            <span id={descriptionId} className={choiceStyles.optionDescription}>
              {description}
            </span>
          )}
        </span>
      </label>
      <FieldMessages ids={ids} help={help} error={error} />
    </div>
  );
}
