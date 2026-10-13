import type { ComponentPropsWithRef, ReactNode } from "react";
import { ChoiceMark, choiceStyles } from "../shared/ChoiceMark.tsx";
import { cx } from "../shared/cx.ts";
import { joinIds, present, useFieldIds } from "../shared/field.tsx";
import styles from "./Radio.module.css";

export interface RadioProps
  extends Omit<ComponentPropsWithRef<"input">, "type" | "style" | "children" | "size" | "aria-labelledby"> {
  /** The choice, beside the circle in the Body role. */
  readonly label: ReactNode;
  /** A Supporting line under the label, inside the clickable area. */
  readonly description?: ReactNode;
  /** Groups radios: one choice per `name`. */
  readonly name: string;
  readonly value: string;
}

/**
 * One operator radio: a native radio stretched invisibly over the whole row (at least the role's target size) with a
 * circle beside the label that takes a Muted Brass ring and a filled brass dot when checked. Put radios that share a
 * `name` in a `<fieldset>` with a `<legend>`; the browser gives them one tab stop and arrow keys. For choices with
 * thumbnails use `OptionList`; for a short row of values, such as Countdown, use `SegmentedChoice`.
 */
export function Radio({ label, description, id, className, "aria-describedby": external, ...rest }: RadioProps) {
  const ids = useFieldIds(id);
  const descriptionId = `${ids.control}-description`;
  return (
    <label className={cx(choiceStyles.option, styles.radio, className)}>
      <input
        {...rest}
        id={ids.control}
        type="radio"
        className={choiceStyles.input}
        aria-labelledby={ids.label}
        aria-describedby={joinIds(present(description) ? descriptionId : undefined, external)}
      />
      <ChoiceMark kind="radio" />
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
  );
}
