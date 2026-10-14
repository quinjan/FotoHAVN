import { useEffect, useRef, useState } from "react";
import { InlineStatus } from "../../components/InlineStatus/index.ts";
import { TextLink } from "../../components/TextLink/index.ts";
import type { EventSetupPinView } from "../../protocol/generated.ts";
import { draftIdleMs } from "./useDraftText.ts";
import { PinInput } from "./PinInput.tsx";
import { copy, fieldIds, limits } from "./setupCopy.ts";
import styles from "./EventSetup.module.css";

export interface PinSectionProps {
  readonly pin: EventSetupPinView;
  readonly pinError: string | undefined;
  readonly confirmationError: string | undefined;
  /** Sends the pair to the brain; both empty clears a pending change. */
  readonly onChange: (pin: string, confirmation: string) => void;
}

/**
 * The Operator PIN: "PIN set" with `CHANGE PIN` when the Event has one, or the two masked inputs. The digits live here
 * only while they are typed; the brain holds the pending pair and never echoes it, so nothing here can show them again.
 */
export function PinSection({ pin, pinError, confirmationError, onChange }: PinSectionProps) {
  const [entering, setEntering] = useState(!pin.isSet || pin.changing);
  const [digits, setDigits] = useState({ pin: "", confirmation: "" });
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(
    () => () => {
      if (timer.current !== null) {
        clearTimeout(timer.current);
      }
    },
    [],
  );

  function cancelTimer(): void {
    if (timer.current !== null) {
      clearTimeout(timer.current);
      timer.current = null;
    }
  }

  function update(next: { pin: string; confirmation: string }): void {
    setDigits(next);
    cancelTimer();
    timer.current = setTimeout(() => {
      timer.current = null;
      onChange(next.pin, next.confirmation);
    }, draftIdleMs);
  }

  function flush(): void {
    if (timer.current !== null) {
      cancelTimer();
      onChange(digits.pin, digits.confirmation);
    }
  }

  function keepCurrent(): void {
    cancelTimer();
    setDigits({ pin: "", confirmation: "" });
    setEntering(false);
    onChange("", "");
  }

  if (!entering) {
    return (
      <div className={styles.pinSet}>
        <InlineStatus>{copy.pin.set}</InlineStatus>
        <TextLink
          onClick={() => {
            setEntering(true);
          }}
        >
          {copy.pin.change}
        </TextLink>
      </div>
    );
  }

  const changing = pin.isSet;
  return (
    <div className={styles.pinFields}>
      <PinInput
        id={fieldIds.pin}
        label={changing ? copy.pin.newLabel : copy.pin.label}
        help={copy.pin.help}
        error={pinError}
        value={digits.pin}
        maxLength={limits.pinDigits}
        onValueChange={(next) => {
          update({ pin: next, confirmation: digits.confirmation });
        }}
        onBlur={flush}
      />
      <PinInput
        id={fieldIds.pinConfirmation}
        label={changing ? copy.pin.newConfirmation : copy.pin.confirmation}
        error={confirmationError}
        value={digits.confirmation}
        maxLength={limits.pinDigits}
        onValueChange={(next) => {
          update({ pin: digits.pin, confirmation: next });
        }}
        onBlur={flush}
      />
      {changing && <TextLink onClick={keepCurrent}>{copy.pin.keep}</TextLink>}
    </div>
  );
}
