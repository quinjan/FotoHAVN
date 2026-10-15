import type { Ref } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { HoldControl } from "../components/HoldControl/index.ts";
import styles from "./Guest.module.css";

/**
 * The discreet exit hold in the top-right corner of every Guest Screen view. The page draws the 3-second hold itself
 * and sends the three moments to the brain, which keeps the time and opens the PIN dialog: a hold that begins is
 * `BeginExitHold`, one that reaches 3 seconds is `CompleteExitHold`, and one released early is `CancelExit`. Pointer,
 * touch, and Enter or Space held on the focused control all do the same.
 */
export function ExitHold({ ref }: { readonly ref?: Ref<HTMLDivElement> }) {
  const client = useBrainClient();
  return (
    <div ref={ref} className={styles.exitHold}>
      <HoldControl
        onHoldStart={() => {
          void client.sendCommand("BeginExitHold", {});
        }}
        onComplete={() => {
          void client.sendCommand("CompleteExitHold", {});
        }}
        onHoldCancel={() => {
          void client.sendCommand("CancelExit", {});
        }}
      />
    </div>
  );
}
