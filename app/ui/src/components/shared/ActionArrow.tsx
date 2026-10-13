import { ArrowDownIcon, ArrowDownRightIcon, ArrowLeftIcon, ArrowRightIcon, ArrowUpIcon, ArrowUpRightIcon } from "@phosphor-icons/react";
import styles from "./ActionArrow.module.css";

/**
 * The direction an action leads (brand iconography.md): → next, ← back, ↓ or ↘ in place, ↗ elsewhere, ↑ to the top.
 */
export type ActionArrowDirection = "right" | "left" | "up" | "down" | "up-right" | "down-right";

const icons = {
  right: ArrowRightIcon,
  left: ArrowLeftIcon,
  up: ArrowUpIcon,
  down: ArrowDownIcon,
  "up-right": ArrowUpRightIcon,
  "down-right": ArrowDownRightIcon,
} as const;

/**
 * The Phosphor Light arrow after an action label. Decorative: the label carries the accessible name. It nudges
 * 2 px in its direction while its action is hovered, unless the user prefers reduced motion.
 */
export function ActionArrow({ direction }: { readonly direction: ActionArrowDirection }) {
  const Icon = icons[direction];
  return (
    <span className={styles.arrow} data-direction={direction} aria-hidden="true">
      <Icon weight="light" size="1em" />
    </span>
  );
}
