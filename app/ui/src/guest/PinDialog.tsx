import { useState } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { Dialog } from "../components/Dialog/index.ts";
import { PinPad } from "../components/PinPad/index.ts";
import type { PinDialogOverlay } from "../protocol/generated.ts";
import { maxWrongAttempts, pinDialogDescription, pinDialogTitle, pinSubmitFailed } from "./guestCopy.ts";
import styles from "./Guest.module.css";

/** Wrong attempts since the dialog opened, from the brain's presentation (it sends the attempts left, not the count). */
function wrongAttemptsOf(pin: PinDialogOverlay): number {
  const counted = pin.attemptsRemaining === null ? 0 : maxWrongAttempts - pin.attemptsRemaining;
  return pin.error === "wrongPin" ? Math.max(1, counted) : counted;
}

/**
 * The Operator PIN dialog: on-screen digits, masked. `SubmitPin` carries the digits and nothing here keeps them. A wrong
 * PIN, the tries left, and the lockout with its countdown all come from the brain's presentation. `KEEP RUNNING` and
 * Escape send `CancelExit`; a lockout that is running keeps running.
 */
export function PinDialog({ pin }: { readonly pin: PinDialogOverlay }) {
  const client = useBrainClient();
  const [failed, setFailed] = useState(false);
  const locked = pin.lockedRemainingSeconds !== null && pin.lockedRemainingSeconds > 0;
  const wrongAttempts = wrongAttemptsOf(pin);

  function cancel(): void {
    void client.sendCommand("CancelExit", {});
  }

  async function submit(digits: string): Promise<void> {
    setFailed(false);
    const result = await client.sendCommand("SubmitPin", { digits });
    // A wrong PIN is an accepted command whose outcome is in the presentation; the pad being locked says itself.
    setFailed(result.status === "failed" || (result.status === "rejected" && result.reason !== "pinLocked"));
  }

  return (
    <Dialog size="wide" title={pinDialogTitle} description={pinDialogDescription} onCancel={cancel}>
      <PinPad
        onSubmit={(digits) => {
          void submit(digits);
        }}
        onCancel={cancel}
        wrongAttempts={wrongAttempts}
        {...(pin.attemptsRemaining === null ? {} : { attemptsRemaining: pin.attemptsRemaining })}
        lockedSeconds={locked ? (pin.lockedRemainingSeconds ?? 0) : 0}
      />
      {failed && !locked && (
        <p className={styles.dialogNotice} role="alert">
          {pinSubmitFailed}
        </p>
      )}
    </Dialog>
  );
}
