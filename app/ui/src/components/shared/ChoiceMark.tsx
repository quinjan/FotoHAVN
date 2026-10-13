import { CheckIcon, CircleIcon } from "@phosphor-icons/react";
import { cx } from "./cx.ts";
import styles from "./choice.module.css";

/**
 * The visible mark of a checkbox or radio, drawn beside a native input that is invisible and stretched over the whole
 * clickable area (`choiceStyles.input`), so the input itself meets the role's target size and keeps native behavior.
 * The mark must be a later sibling of that input: its checked, disabled, and invalid looks follow the input through
 * the `~` combinator, so they hold for controlled and uncontrolled inputs alike.
 *
 * Checkbox: a 4 radius box that fills with the ink and shows a check. Radio: a circle with a Muted Brass ring and a
 * filled brass dot (brand components.md, option list). The check and the dot are shapes, so the state never rests on
 * color alone, and they stay visible in forced colors.
 */
export function ChoiceMark({ kind, className }: { readonly kind: "checkbox" | "radio"; readonly className?: string | undefined }) {
  return (
    <span className={cx(styles.mark, className)} data-kind={kind} aria-hidden="true">
      <span className={styles.glyph}>
        {kind === "checkbox" ? <CheckIcon weight="fill" size="1em" /> : <CircleIcon weight="fill" size="1em" />}
      </span>
    </span>
  );
}

export { styles as choiceStyles };
