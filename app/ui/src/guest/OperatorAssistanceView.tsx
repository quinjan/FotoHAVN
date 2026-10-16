import { useState } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { InlineStatus } from "../components/InlineStatus/index.ts";
import { PrimaryAction } from "../components/PrimaryAction/index.ts";
import { cx } from "../components/shared/cx.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { GuestCycleView, OperatorAssistanceView as OperatorAssistance } from "../protocol/generated.ts";
import { assistanceCopy, retryingLine, retryLabel, retryNotSentLine } from "./finishCopy.ts";
import styles from "./OperatorAssistanceView.module.css";

/**
 * Operator Assistance on the Guest Screen (plan section 7, Failures; app/DESIGN.md, Guest Screen): one Dark Walnut band with
 * Off-white type across the Off-white canvas, "A little *interruption.*", "Please ask the FOTOHAVN team for help.", and
 * `RETRY` as the one primary action, inverted on the dark band. Every reason reads the same to a guest; the operator's panel
 * says which. `RETRY` is harmless, so anyone may press it. It sends `RetryAssistance` once: while the brain is checking
 * (`retrying`, or a request still in flight) it is unavailable and a polite line says "Trying again…". The exit hold stays
 * reachable on the canvas above the band. `cycle` is the Guest Cycle the screen returns to, or null with none.
 */
export function OperatorAssistanceView({ cycle, view }: { readonly cycle: GuestCycleView | null; readonly view: OperatorAssistance }) {
  const client = useBrainClient();
  const [sending, setSending] = useState(false);
  const [notSent, setNotSent] = useState(false);
  const retrying = view.retrying || sending;
  const line = retrying ? retryingLine : notSent ? retryNotSentLine : "";

  async function retry(): Promise<void> {
    setNotSent(false);
    setSending(true);
    const result = await client.sendCommand("RetryAssistance", {});
    setSending(false);
    setNotSent(result.status !== "accepted");
  }

  return (
    <div className={styles.view} data-assistance-reason={view.reason} data-assistance-step={cycle?.step}>
      <div className={styles.band} data-tone="dark" data-retrying={retrying || undefined}>
        <StatementHeading
          level={1}
          size="heading-lg"
          align="center"
          {...assistanceCopy}
          action={
            <PrimaryAction
              disabled={retrying}
              aria-busy={retrying}
              onClick={() => {
                void retry();
              }}
            >
              {retryLabel}
            </PrimaryAction>
          }
        />
        <InlineStatus className={cx(styles.status)}>{line}</InlineStatus>
      </div>
    </div>
  );
}
