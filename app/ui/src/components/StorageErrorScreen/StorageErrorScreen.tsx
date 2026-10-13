import { useEffect, useState } from "react";
import type { StorageErrorReason } from "../../protocol/generated.ts";
import { InlineStatus } from "../InlineStatus/index.ts";
import { PrimaryAction } from "../PrimaryAction/index.ts";
import { StatementHeading } from "../StatementHeading/index.ts";
import { cx } from "../shared/cx.ts";
import styles from "./StorageErrorScreen.module.css";

/** What the Data folder's problem means for the operator, and what to do about it, per kind (DESIGN.md, Copy). */
export const storageErrorCopy: Readonly<Record<StorageErrorReason, string>> = {
  diskFull: "The disk is full. Free some space, then try again.",
  accessDenied: "FOTOHAVN can't write to the Data folder. Check its permissions, then try again.",
  locked: "Another program is using a FOTOHAVN file. Close it, then try again.",
  notLocalDisk: "The Data folder is on a network drive. Move FOTOHAVN to this computer's own disk.",
  cloudSynced:
    "The Data folder is inside a cloud-synced folder, which can change files while the booth runs. Move FOTOHAVN outside it.",
  notWritable: "The Data folder can't be written to. Choose a folder FOTOHAVN can write to.",
  integrityFailed:
    "The Event database didn't pass its check. Copy the Data folder somewhere safe and contact the FOTOHAVN team.",
  corrupt: "The Event database didn't pass its check. Copy the Data folder somewhere safe and contact the FOTOHAVN team.",
  unknown: "Something went wrong with the Data folder. Try again.",
};

export interface StorageErrorScreenProps {
  /** Why the Data folder can't be used; chooses the explanation. */
  readonly kind: StorageErrorReason;
  /** The Data folder's full path, shown as selectable text so the operator can copy it. */
  readonly dataPath: string;
  /** `TRY AGAIN` is in flight: the action is unavailable and the screen says so. */
  readonly retrying?: boolean;
  /** The outcome line when the last try didn't work, e.g. "That isn't ready yet."; null shows nothing. */
  readonly notice?: string | null;
  readonly onRetry: () => void;
  readonly className?: string;
}

/**
 * The operator's storage error view (DESIGN.md, Operator Screen): Off-white canvas, a left-aligned statement heading,
 * the problem in one line, the Data folder's path in the Meta role, and `TRY AGAIN` as the one primary action. The
 * problem sits in an alert status that fills in just after the screen mounts, so the polite live region announces it
 * once; trying again and its outcome use a second status beside the action, so the problem is never announced twice.
 */
export function StorageErrorScreen({ kind, dataPath, retrying = false, notice = null, onRetry, className }: StorageErrorScreenProps) {
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
    <div className={cx(styles.screen, className)} data-storage-error={kind}>
      <StatementHeading level={1} size="heading-lg" lead="The Data folder needs" turn="attention." />
      <InlineStatus tone="alert">{announced ? storageErrorCopy[kind] : ""}</InlineStatus>
      <dl className={styles.path}>
        <dt className={styles.label}>Data folder</dt>
        <dd className={styles.value}>{dataPath}</dd>
      </dl>
      <div className={styles.actions}>
        <PrimaryAction disabled={retrying} onClick={onRetry}>
          TRY AGAIN
        </PrimaryAction>
        <InlineStatus tone={retrying ? "neutral" : "alert"}>{retrying ? "Trying again…" : notice}</InlineStatus>
      </div>
    </div>
  );
}
