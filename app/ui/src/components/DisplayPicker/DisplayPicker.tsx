import { InfoIcon, MonitorIcon, WarningCircleIcon } from "@phosphor-icons/react";
import type { ReactNode } from "react";
import { OptionList, type OptionListItem } from "../OptionList/index.ts";
import { cx } from "../shared/cx.ts";
import { TextLink } from "../TextLink/index.ts";
import styles from "./DisplayPicker.module.css";

/** An Available Display as the host reports it. */
export interface DisplayPickerDisplay {
  /** The display's stable identity (the monitor device path), stored by the Guest Display Binding. */
  readonly id: string;
  /** The friendly name, e.g. "DELL P2422H". */
  readonly name: string;
  readonly width: number;
  readonly height: number;
  /** Where it sits, phrased by the screen, e.g. "Right of the Operator Screen". */
  readonly position: string;
  /** True for the display showing the Operator Screen. */
  readonly isOperatorScreen?: boolean | undefined;
}

export interface DisplayPickerProps {
  readonly displays: readonly DisplayPickerDisplay[];
  /** The bound display's id; `null` when none is chosen. A bound id missing from `displays` shows as not connected. */
  readonly value: string | null;
  /** The bound display's stored friendly name, shown when it isn't connected. */
  readonly boundDisplayName?: string | undefined;
  readonly onChange?: ((id: string) => void) | undefined;
  /** `IDENTIFY DISPLAYS`: shows each display's number and name on that display for 3 seconds. */
  readonly onIdentify?: (() => void) | undefined;
  /** Disables `IDENTIFY DISPLAYS`, e.g. while the overlay already shows. */
  readonly identifyDisabled?: boolean | undefined;
  /** "Guest display" when omitted. */
  readonly label?: ReactNode;
  readonly help?: ReactNode;
  readonly error?: ReactNode;
  readonly id?: string | undefined;
  readonly className?: string | undefined;
}

export const OPERATOR_SCREEN_MARKER = "Operator Screen";
export const NOT_CONNECTED_MARKER = "Not connected";
export const COVERS_OPERATOR_NOTE = "The Guest Screen will cover this screen. Use the hold and PIN to return.";
export const NOT_CONNECTED_NOTE = "The chosen guest display isn't connected.";

/**
 * The Guest display field of Event setup (DESIGN.md; ADR 0008): the Available Displays as option-list rows with name,
 * resolution, position, and the "Operator Screen" marker, and `IDENTIFY DISPLAYS` to match the list to the hardware.
 * A bound display that isn't connected stays chosen as a "Not connected" row, keeping the binding until the operator
 * picks another. Choosing the Operator Screen's display shows the D38 note. Notes are announced politely.
 */
export function DisplayPicker({
  displays,
  value,
  boundDisplayName,
  onChange,
  onIdentify,
  identifyDisabled = false,
  label = "Guest display",
  help,
  error,
  id,
  className,
}: DisplayPickerProps) {
  const chosen = displays.find((display) => display.id === value);
  const missing = value !== null && chosen === undefined;

  const items: OptionListItem[] = displays.map((display) => ({
    value: display.id,
    title: display.name,
    meta: `${String(display.width)} × ${String(display.height)} · ${display.position}`,
    badge: display.isOperatorScreen === true ? OPERATOR_SCREEN_MARKER : undefined,
    thumbnail: <MonitorIcon weight="light" size="1em" />,
  }));
  if (missing) {
    items.push({
      value,
      title: boundDisplayName ?? "Chosen display",
      badge: NOT_CONNECTED_MARKER,
      thumbnail: <MonitorIcon weight="light" size="1em" />,
      disabled: true,
    });
  }

  return (
    <div className={cx(styles.root, className)}>
      <OptionList variant="radio" id={id} label={label} help={help} error={error} items={items} value={value} onChange={onChange} />
      <div role="status" className={styles.notes}>
        {missing && (
          <p className={styles.note}>
            <span className={styles.icon} aria-hidden="true">
              <WarningCircleIcon weight="light" size="1em" />
            </span>
            <span>{NOT_CONNECTED_NOTE}</span>
          </p>
        )}
        {chosen?.isOperatorScreen === true && (
          <p className={styles.note}>
            <span className={styles.icon} aria-hidden="true">
              <InfoIcon weight="light" size="1em" />
            </span>
            <span>{COVERS_OPERATOR_NOTE}</span>
          </p>
        )}
      </div>
      <TextLink disabled={identifyDisabled} onClick={onIdentify}>
        IDENTIFY DISPLAYS
      </TextLink>
    </div>
  );
}
