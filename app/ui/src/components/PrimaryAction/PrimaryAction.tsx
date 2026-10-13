import type { ComponentPropsWithRef, ReactNode } from "react";
import { ActionArrow, type ActionArrowDirection } from "../shared/ActionArrow.tsx";
import { cx } from "../shared/cx.ts";
import { guardDisabled } from "../shared/guardDisabled.ts";
import styles from "./PrimaryAction.module.css";

export interface PrimaryActionProps extends Omit<ComponentPropsWithRef<"button">, "children" | "disabled" | "type" | "style"> {
  /** The uppercase action label, as written in DESIGN.md's copy tables, e.g. `NEW EVENT`. */
  readonly children: ReactNode;
  /** The arrow after the label, when the action leads somewhere (`STEP INSIDE →`). None by default. */
  readonly arrow?: ActionArrowDirection;
  /** Unavailable: stays focusable with `aria-disabled="true"` and ignores activation. */
  readonly disabled?: boolean;
  readonly type?: "button" | "submit";
}

/**
 * The view's one primary action (brand components.md): Ebony fill, Off-white label, 4 radius, at least the role's
 * target size. It inverts to an Off-white fill with an Ebony label on a dark tone, with no prop. Use one per view.
 */
export function PrimaryAction({
  children,
  arrow,
  disabled = false,
  type = "button",
  className,
  onClick,
  ...rest
}: PrimaryActionProps) {
  return (
    <button
      {...rest}
      type={type}
      className={cx(styles.action, className)}
      data-fotohavn-action="primary"
      aria-disabled={disabled}
      onClick={guardDisabled(disabled, onClick)}
    >
      <span>{children}</span>
      {arrow !== undefined && <ActionArrow direction={arrow} />}
    </button>
  );
}
