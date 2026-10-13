import type { ComponentPropsWithRef, ReactNode } from "react";
import { ActionArrow, type ActionArrowDirection } from "../shared/ActionArrow.tsx";
import { cx } from "../shared/cx.ts";
import { guardDisabled } from "../shared/guardDisabled.ts";
import styles from "./TextLink.module.css";

interface TextLinkOwnProps {
  /** The uppercase label, e.g. `SEE THE GOOD COMPANY`. */
  readonly children: ReactNode;
  /** The arrow after the label, when the action leads somewhere (`BACK TO START ←` is `left`). None by default. */
  readonly arrow?: ActionArrowDirection;
  /** Unavailable: stays focusable with `aria-disabled="true"` and ignores activation. */
  readonly disabled?: boolean;
  /** The label role: `action` (default) or the smaller `meta`, for row actions and dense metadata. */
  readonly size?: "action" | "meta";
}

type ButtonProps = TextLinkOwnProps &
  Omit<ComponentPropsWithRef<"button">, keyof TextLinkOwnProps | "type" | "style"> & {
    readonly href?: undefined;
    readonly type?: "button" | "submit";
  };

type AnchorProps = TextLinkOwnProps &
  Omit<ComponentPropsWithRef<"a">, keyof TextLinkOwnProps | "style"> & {
    /** Renders a link instead of a button. The booth navigates by command, so this is rare (development pages). */
    readonly href: string;
  };

export type TextLinkProps = ButtonProps | AnchorProps;

/**
 * The text action (brand components.md): an uppercase Action or Meta label with a 1 px underline offset 7, Hairline
 * at rest and the text color on hover, followed by an optional arrow. Its target is at least the role's target size.
 * A `<button>` by default; with `href`, an `<a>`.
 */
export function TextLink(props: TextLinkProps) {
  if (props.href !== undefined) {
    const { children, arrow, disabled = false, size = "action", className, onClick, href, ...rest } = props;
    return (
      <a
        {...rest}
        href={href}
        className={cx(styles.link, className)}
        data-size={size}
        data-fotohavn-action="text"
        aria-disabled={disabled || undefined}
        onClick={guardDisabled(disabled, onClick)}
      >
        <span className={styles.label}>{children}</span>
        {arrow !== undefined && <ActionArrow direction={arrow} />}
      </a>
    );
  }
  const { children, arrow, disabled = false, size = "action", type = "button", className, onClick, ...rest } = props;
  return (
    <button
      {...rest}
      type={type}
      className={cx(styles.link, className)}
      data-size={size}
      data-fotohavn-action="text"
      aria-disabled={disabled}
      onClick={guardDisabled(disabled, onClick)}
    >
      <span className={styles.label}>{children}</span>
      {arrow !== undefined && <ActionArrow direction={arrow} />}
    </button>
  );
}
