import { CheckIcon } from "@phosphor-icons/react";
import { useId, useRef, type ReactNode } from "react";
import { choiceStyles } from "../shared/ChoiceMark.tsx";
import { cx } from "../shared/cx.ts";
import { describedBy, FieldMessages, fieldStyles, useFieldIds, type FieldText } from "../shared/field.tsx";
import { handleRadioArrow, tabStopValue } from "../shared/roving.ts";
import styles from "./SegmentedChoice.module.css";

export interface SegmentedChoiceOption {
  readonly value: string;
  /** The visible label, e.g. `5 s`. */
  readonly label: ReactNode;
  /** A fuller name for assistive technology when the label is abbreviated, e.g. "5 seconds". */
  readonly accessibleLabel?: string | undefined;
  readonly disabled?: boolean | undefined;
}

export interface SegmentedChoiceProps extends FieldText {
  readonly options: readonly SegmentedChoiceOption[];
  /** The chosen value; `null` when nothing is chosen yet. */
  readonly value: string | null;
  readonly onChange?: ((value: string) => void) | undefined;
  /** Disables the whole group (a native disabled fieldset); give the reason as `help`. */
  readonly disabled?: boolean | undefined;
  /** The radios' shared `name`; generated when omitted. */
  readonly name?: string | undefined;
  readonly id?: string | undefined;
  readonly className?: string | undefined;
}

/**
 * A short row of mutually exclusive values, such as Countdown 3, 5, or 10 seconds: a radio group (native radios in a
 * fieldset whose legend is the label) drawn as separate 4 radius segments. One tab stop; the arrow keys move to the
 * next or previous value and select it. The chosen segment fills with the ink and shows a check, never color alone.
 */
export function SegmentedChoice({
  label,
  help,
  error,
  options,
  value,
  onChange,
  disabled = false,
  name,
  id,
  className,
}: SegmentedChoiceProps) {
  const ids = useFieldIds(id);
  const generatedName = useId();
  const groupName = name ?? generatedName;
  const inputs = useRef<(HTMLInputElement | null)[]>([]);
  const tabStop = tabStopValue(options, value);
  const select = (next: string) => onChange?.(next);

  return (
    <fieldset
      id={ids.control}
      className={cx(fieldStyles.field, className)}
      disabled={disabled}
      aria-describedby={describedBy(ids, { help, error })}
    >
      <legend id={ids.label} className={cx(fieldStyles.label, fieldStyles.legend)}>
        {label}
      </legend>
      <div className={styles.segments}>
        {options.map((option, index) => (
          <label key={option.value} className={styles.segment}>
            <input
              ref={(element) => {
                inputs.current[index] = element;
              }}
              type="radio"
              className={choiceStyles.input}
              name={groupName}
              value={option.value}
              checked={option.value === value}
              disabled={option.disabled}
              tabIndex={option.value === tabStop ? 0 : -1}
              aria-label={option.accessibleLabel}
              onChange={() => {
                select(option.value);
              }}
              onKeyDown={(event) => {
                handleRadioArrow(event, options, index, inputs.current, select);
              }}
            />
            <span className={styles.check} aria-hidden="true">
              <CheckIcon weight="fill" size="1em" />
            </span>
            <span className={styles.label}>{option.label}</span>
          </label>
        ))}
      </div>
      <FieldMessages ids={ids} help={help} error={error} />
    </fieldset>
  );
}
