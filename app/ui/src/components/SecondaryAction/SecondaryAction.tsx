import type { ComponentPropsWithRef, ReactNode } from "react";
import { ActionArrow, type ActionArrowDirection } from "../shared/ActionArrow.tsx";
import { cx } from "../shared/cx.ts";
import { guardDisabled } from "../shared/guardDisabled.ts";
import styles from "./SecondaryAction.module.css";

export interface SecondaryActionProps extends Omit<ComponentPropsWithRef<"button">, "children" | "disabled" | "type" | "style"> {
  /** The uppercase action label, e.g. `TRY AGAIN`. */
  readonly children: ReactNode;
  /** The arrow after the label, when the action leads somewhere. None by default. */
  readonly arrow?: ActionArrowDirection;
  /** Unavailable: stays focusable with `aria-disabled="true"` and ignores activation. */
  readonly disabled?: boolean;
  readonly type?: "button" | "submit";
}

/**
 * The bordered secondary action (brand components.md): transparent fill and a 1 px border in the current ink. Rare;
 * for paired choices beside a primary action, such as `END EVENT` / `KEEP RUNNING`, and for `SAVE AND START`.
 */
export function SecondaryAction({
  children,
  arrow,
  disabled = false,
  type = "button",
  className,
  onClick,
  ...rest
}: SecondaryActionProps) {
  return (
    <button
      {...rest}
      type={type}
      className={cx(styles.action, className)}
      data-fotohavn-action="secondary"
      aria-disabled={disabled}
      onClick={guardDisabled(disabled, onClick)}
    >
      <span>{children}</span>
      {arrow !== undefined && <ActionArrow direction={arrow} />}
    </button>
  );
}
