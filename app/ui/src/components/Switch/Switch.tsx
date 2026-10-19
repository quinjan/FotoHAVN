import { CircleIcon } from "@phosphor-icons/react";
import type { ComponentPropsWithRef, MouseEvent } from "react";
import { cx } from "../shared/cx.ts";
import { describedBy, FieldMessages, fieldStyles, present, useFieldIds, type FieldText } from "../shared/field.tsx";
import styles from "./Switch.module.css";

export interface SwitchProps
  extends FieldText,
    Omit<
      ComponentPropsWithRef<"button">,
      "type" | "style" | "children" | "role" | "disabled" | "onChange" | "aria-checked" | "aria-invalid" | "aria-labelledby" | keyof FieldText
    > {
  readonly checked: boolean;
  /** Called with the new state when the operator flips the switch. */
  readonly onCheckedChange?: ((checked: boolean) => void) | undefined;
  /** Unavailable: stays focusable with `aria-disabled="true"` and ignores activation; give the reason as `help`. */
  readonly disabled?: boolean | undefined;
  /** The state as text beside the track, so it never rests on the track's position or fill. */
  readonly onLabel?: string | undefined;
  readonly offLabel?: string | undefined;
  /** Keeps the label for assistive technology only, when a visible label beside the switch already names it (a timer's name). */
  readonly hideLabel?: boolean | undefined;
}

/**
 * An operator on/off setting, such as Mirror or Guest gallery: `role="switch"` on a native button with
 * `aria-checked`, the label above in the Meta role, a track with a thumb that moves to the end when on, and the state
 * written beside it ("On" or "Off"). Space and Enter flip it. The thumb jumps rather than slides, so reduced motion
 * changes nothing. Help sits under it, e.g. "Turn this on only with the Customer's permission."
 */
export function Switch({
  label,
  help,
  error,
  id,
  checked,
  onCheckedChange,
  disabled = false,
  onLabel = "On",
  offLabel = "Off",
  hideLabel = false,
  className,
  onClick,
  "aria-describedby": external,
  ...rest
}: SwitchProps) {
  const ids = useFieldIds(id);

  function handleClick(event: MouseEvent<HTMLButtonElement>) {
    if (disabled) {
      event.preventDefault();
      return;
    }
    onClick?.(event);
    if (!event.defaultPrevented) {
      onCheckedChange?.(!checked);
    }
  }

  return (
    <div className={cx(fieldStyles.field, className)}>
      <div className={cx(fieldStyles.labelRow, hideLabel && fieldStyles.visuallyHidden)}>
        <span id={ids.label} className={fieldStyles.label}>
          {label}
        </span>
      </div>
      <button
        {...rest}
        id={ids.control}
        type="button"
        role="switch"
        className={styles.switch}
        aria-checked={checked}
        aria-disabled={disabled}
        aria-invalid={present(error) || undefined}
        aria-labelledby={ids.label}
        aria-describedby={describedBy(ids, { help, error }, external)}
        onClick={handleClick}
      >
        <span className={styles.track} data-checked={checked} aria-hidden="true">
          <span className={styles.thumb}>
            <CircleIcon weight="fill" size="1em" />
          </span>
        </span>
        <span className={styles.state} aria-hidden="true">
          {checked ? onLabel : offLabel}
        </span>
      </button>
      <FieldMessages ids={ids} help={help} error={error} />
    </div>
  );
}
