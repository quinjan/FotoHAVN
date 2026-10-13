import { useEffect, useState } from "react";
import { InlineStatus } from "../InlineStatus/index.ts";
import { StatementHeading } from "../StatementHeading/index.ts";
import { cx } from "../shared/cx.ts";
import styles from "./StartingScreen.module.css";

/** What the operator reads while the booth opens the Data folder (DESIGN.md, Operator Screen copy). */
export const startingCopy = "Opening the Data folder…";

export interface StartingScreenProps {
  readonly className?: string;
}

/**
 * The operator's starting view (DESIGN.md, Operator Screen): Off-white canvas and the left-aligned Events heading the
 * list will keep, with one neutral status line, so the list appears in place. It has no action and no motion. The
 * status mounts empty and gets its text just after the screen mounts, so the polite live region announces it once.
 */
export function StartingScreen({ className }: StartingScreenProps) {
  const [announced, setAnnounced] = useState(false);
  useEffect(() => {
    // The live region mounts empty and gets its text a moment later, which is what makes a screen reader announce it.
    const timer = setTimeout(() => {
      setAnnounced(true);
    }, 0);
    return () => {
      clearTimeout(timer);
    };
  }, []);

  return (
    <div className={cx(styles.screen, className)} aria-busy="true">
      <StatementHeading level={1} lead="Events" />
      <InlineStatus>{announced ? startingCopy : ""}</InlineStatus>
    </div>
  );
}
