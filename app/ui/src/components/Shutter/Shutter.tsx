import { CameraIcon } from "@phosphor-icons/react";
import type { ComponentPropsWithRef } from "react";
import { cx } from "../shared/cx.ts";
import { guardDisabled } from "../shared/guardDisabled.ts";
import styles from "./Shutter.module.css";

export type ShutterState = "idle" | "recording";

export interface ShutterProps
  extends Omit<ComponentPropsWithRef<"button">, "children" | "disabled" | "type" | "style" | "aria-label"> {
  /** `recording` while the capture sequence runs: the icon takes its Fill weight, and the control stops the sequence. */
  readonly state?: ShutterState;
  /** Unavailable (no fresh frame yet): stays focusable with `aria-disabled="true"`; give the reason in nearby text. */
  readonly disabled?: boolean;
  /** Shows the uppercase label under the circle. Default true; the accessible name is the same words either way. */
  readonly showLabel?: boolean;
}

const copy = {
  idle: { name: "Take photographs", label: "TAKE PHOTOGRAPHS" },
  recording: { name: "Stop taking photographs", label: "STOP" },
} as const;

/**
 * The circular shutter on the Photographs step (app/DESIGN.md: circular shutter, full radius): at least 112 × 112,
 * with the Phosphor Light camera icon, which takes its Fill weight only while the capture sequence runs. Idle, it
 * starts the sequence (`TakePhotographs`); recording, it stops it (`StopCapture`). The label under it says which, so
 * the state is never the icon weight alone. It inverts on the dark stage through the tone.
 */
export function Shutter({ state = "idle", disabled = false, showLabel = true, className, onClick, ...rest }: ShutterProps) {
  const { name, label } = copy[state];
  return (
    <span className={cx(styles.shutter, className)} data-state={state}>
      <button
        {...rest}
        type="button"
        className={styles.button}
        aria-label={name}
        aria-disabled={disabled}
        onClick={guardDisabled(disabled, onClick)}
      >
        <span className={styles.icon} aria-hidden="true">
          <CameraIcon weight={state === "recording" ? "fill" : "light"} size="1em" />
        </span>
      </button>
      {showLabel && (
        <span className={styles.label} aria-hidden="true">
          {label}
        </span>
      )}
    </span>
  );
}
