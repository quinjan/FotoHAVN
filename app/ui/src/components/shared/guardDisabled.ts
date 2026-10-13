import type { MouseEvent, MouseEventHandler } from "react";

/**
 * Actions stay focusable while unavailable (`aria-disabled`, so the reason can be announced and focus never jumps),
 * and swallow activation instead of calling `onClick`.
 */
export function guardDisabled<E extends HTMLElement>(
  disabled: boolean,
  onClick: MouseEventHandler<E> | undefined,
): MouseEventHandler<E> {
  return (event: MouseEvent<E>) => {
    if (disabled) {
      event.preventDefault();
      return;
    }
    onClick?.(event);
  };
}
