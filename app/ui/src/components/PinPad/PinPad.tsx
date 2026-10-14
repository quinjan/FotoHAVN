import { BackspaceIcon, CircleIcon } from "@phosphor-icons/react";
import { useEffect, useId, useRef, useState } from "react";
import { PrimaryAction } from "../PrimaryAction/index.ts";
import { SecondaryAction } from "../SecondaryAction/index.ts";
import { cx } from "../shared/cx.ts";
import { secondsText } from "../shared/secondsText.ts";
import styles from "./PinPad.module.css";

const digitKeys = ["1", "2", "3", "4", "5", "6", "7", "8", "9"] as const;

function digitsText(count: number): string {
  return count === 1 ? "1 digit" : `${String(count)} digits`;
}

function triesLeftText(count: number): string {
  return count === 1 ? "1 try left." : `${String(count)} tries left.`;
}

export interface PinPadProps {
  /** A complete entry (`SubmitPin`). The pad clears itself after submitting. */
  readonly onSubmit: (pin: string) => void;
  /** Renders the cancel action (`CancelExit`). */
  readonly onCancel?: () => void;
  /**
   * Wrong attempts since the dialog opened, from the brain. Above zero, the pad says the PIN wasn't right; each new
   * wrong attempt announces again.
   */
  readonly wrongAttempts?: number;
  /** Wrong attempts left before the lockout, from the brain. Said after a wrong PIN ("2 tries left."), never while locked. */
  readonly attemptsRemaining?: number;
  /** Whole seconds left in the lockout, from the brain (five wrong attempts lock the pad for 30 seconds). Above zero, the pad is locked. */
  readonly lockedSeconds?: number;
  /** The shortest and longest Operator PIN: 4 and 6 digits. */
  readonly minLength?: number;
  readonly maxLength?: number;
  /** Digits already entered when the pad appears; for fixtures. */
  readonly defaultValue?: string;
  /** The submit label: the one primary action. Default `END EVENT`, what a correct PIN does. */
  readonly submitLabel?: string;
  /** The cancel label. Default `KEEP RUNNING`. */
  readonly cancelLabel?: string;
  readonly className?: string;
}

/**
 * The Operator PIN pad (app/DESIGN.md, PinPad): on-screen digits, so no system keyboard opens; masked entry shown as
 * dots; a backspace; and the submit action, available at 4 to 6 digits. A wrong PIN is said in text, and the lockout
 * shows its countdown in text, never color alone. Keys are at least the role's target (64 on the Guest Screen). With
 * focus inside the pad, typed digits and Backspace work too. Compose it in a Dialog.
 */
export function PinPad({
  onSubmit,
  onCancel,
  wrongAttempts = 0,
  attemptsRemaining,
  lockedSeconds = 0,
  minLength = 4,
  maxLength = 6,
  defaultValue = "",
  submitLabel = "END EVENT",
  cancelLabel = "KEEP RUNNING",
  className,
}: PinPadProps) {
  const [digits, setDigits] = useState(() => defaultValue.replace(/\D/g, "").slice(0, maxLength));
  const rootRef = useRef<HTMLDivElement>(null);
  const hintId = useId();
  const locked = lockedSeconds > 0;
  const entry = locked ? "" : digits;
  const full = entry.length >= maxLength;
  const ready = !locked && entry.length >= minLength;

  function append(digit: string): void {
    if (!locked) {
      setDigits((current) => (current.length >= maxLength ? current : current + digit));
    }
  }

  function removeLast(): void {
    if (!locked) {
      setDigits((current) => current.slice(0, -1));
    }
  }

  function submit(): void {
    if (ready) {
      onSubmit(entry);
      setDigits("");
    }
  }

  const keyboard = useRef({ append, removeLast });
  useEffect(() => {
    keyboard.current = { append, removeLast };
  });

  useEffect(() => {
    const root = rootRef.current;
    function onKeyDown(event: KeyboardEvent): void {
      if (/^\d$/.test(event.key)) {
        event.preventDefault();
        keyboard.current.append(event.key);
      } else if (event.key === "Backspace") {
        event.preventDefault();
        keyboard.current.removeLast();
      }
    }
    root?.addEventListener("keydown", onKeyDown);
    return () => {
      root?.removeEventListener("keydown", onKeyDown);
    };
  }, []);

  function digitKey(digit: string) {
    const unavailable = locked || full;
    return (
      <button
        key={digit}
        type="button"
        className={styles.key}
        aria-disabled={unavailable}
        onClick={() => {
          if (!unavailable) {
            append(digit);
          }
        }}
      >
        {digit}
      </button>
    );
  }

  return (
    <div ref={rootRef} className={cx(styles.pad, className)} data-locked={locked || undefined}>
      <div className={styles.status}>
        <p className={styles.dots} aria-hidden="true">
          {Array.from({ length: maxLength }, (_, index) => (
            <span key={index} className={styles.dot} data-filled={index < entry.length || undefined}>
              <CircleIcon weight={index < entry.length ? "fill" : "light"} size="1em" />
            </span>
          ))}
        </p>
        <p className={styles.visuallyHidden} aria-live="polite">
          {entry.length === 0 ? "No digits entered." : `${digitsText(entry.length)} entered.`}
        </p>
        <div className={styles.messages}>
          {locked ? (
            <>
              <p className={styles.alert} role="alert">
                Too many tries. The pad is locked for now.
              </p>
              <p className={styles.countdown}>Try again in {secondsText(lockedSeconds)}.</p>
            </>
          ) : wrongAttempts > 0 ? (
            <>
              <p key={wrongAttempts} className={styles.alert} role="alert">
                That PIN isn&apos;t right. Try again.
              </p>
              {attemptsRemaining !== undefined && <p className={styles.tries}>{triesLeftText(attemptsRemaining)}</p>}
            </>
          ) : null}
          <p id={hintId} className={styles.hint}>
            {`${String(minLength)} to ${String(maxLength)} digits.`}
          </p>
        </div>
      </div>
      <div className={styles.keys} role="group" aria-label="PIN digits">
        {digitKeys.map(digitKey)}
        <span className={styles.spacer} aria-hidden="true" />
        {digitKey("0")}
        <button
          type="button"
          className={styles.key}
          aria-label="Delete last digit"
          aria-disabled={locked || entry.length === 0}
          onClick={removeLast}
        >
          <span className={styles.icon} aria-hidden="true">
            <BackspaceIcon weight="light" size="1em" />
          </span>
        </button>
      </div>
      <div className={styles.actions}>
        <PrimaryAction disabled={!ready} aria-describedby={hintId} onClick={submit}>
          {submitLabel}
        </PrimaryAction>
        {onCancel !== undefined && <SecondaryAction onClick={onCancel}>{cancelLabel}</SecondaryAction>}
      </div>
    </div>
  );
}
