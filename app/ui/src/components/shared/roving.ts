import type { KeyboardEvent } from "react";

/**
 * Arrow-key helpers for groups of native radios (`SegmentedChoice`, `OptionList`) that keep the ARIA radio group
 * pattern explicit, so it is the same in every engine and testable: one tab stop (the checked radio, or the first
 * enabled one), and the arrow keys move to the next or previous enabled radio, wrapping, and select it.
 */

export interface RovingItem {
  readonly value: string;
  readonly disabled?: boolean | undefined;
}

/** The value that holds the group's tab stop: the checked enabled item, or else the first enabled item. */
export function tabStopValue(items: readonly RovingItem[], checked: string | null | undefined): string | undefined {
  const enabled = items.filter((item) => item.disabled !== true);
  return enabled.find((item) => item.value === checked)?.value ?? enabled[0]?.value;
}

/** +1 for ArrowDown and ArrowRight, −1 for ArrowUp and ArrowLeft, and `undefined` for any other key. */
export function arrowStep(key: string, keys: "all" | "vertical" = "all"): 1 | -1 | undefined {
  if (key === "ArrowDown" || (keys === "all" && key === "ArrowRight")) {
    return 1;
  }
  if (key === "ArrowUp" || (keys === "all" && key === "ArrowLeft")) {
    return -1;
  }
  return undefined;
}

/** The index of the next enabled item from `from` in the direction of `step`, wrapping or stopping at the ends. */
export function nextEnabledIndex(
  items: readonly RovingItem[],
  from: number,
  step: 1 | -1,
  wrap: boolean,
): number | undefined {
  for (let offset = 1; offset <= items.length; offset += 1) {
    let index = from + step * offset;
    if (wrap) {
      index = (index + items.length) % items.length;
    } else if (index < 0 || index >= items.length) {
      return undefined;
    }
    if (items[index]?.disabled !== true) {
      return index;
    }
  }
  return undefined;
}

/**
 * Handles an arrow key on the radio at `index`: focuses the next enabled radio (wrapping) and selects it through
 * `select`. Other keys pass through, so Tab and Space keep their native behavior.
 */
export function handleRadioArrow(
  event: KeyboardEvent<HTMLInputElement>,
  items: readonly RovingItem[],
  index: number,
  inputs: readonly (HTMLInputElement | null)[],
  select: (value: string) => void,
): void {
  const step = arrowStep(event.key);
  if (step === undefined) {
    return;
  }
  event.preventDefault();
  const next = nextEnabledIndex(items, index, step, true);
  const item = next === undefined ? undefined : items[next];
  if (next === undefined || item === undefined || next === index) {
    return;
  }
  inputs[next]?.focus();
  select(item.value);
}
