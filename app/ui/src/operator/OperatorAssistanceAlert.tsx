import { InlineStatus } from "../components/InlineStatus/index.ts";
import { TextLink } from "../components/TextLink/index.ts";
import type { AssistanceReason } from "../protocol/generated.ts";
import { assistanceCopy, returnToRunningEventLabel } from "./copy.ts";

export interface OperatorAssistanceAlertProps {
  /** The reason the running Event needs help; null when it doesn't, and the alert's live region stays empty. */
  readonly reason: AssistanceReason | null;
  /** Goes back to the running-Event panel, which is on the Event list. */
  readonly onReturn: () => void;
}

/**
 * The Operator Assistance alert on every Operator Screen view but the Event list (DESIGN.md): the reason as an inline
 * status line in text, with a text link back to the running-Event panel, where `RETRY` is. It doesn't change the view's
 * primary action, and its live region stays mounted so the reason is announced when it appears.
 */
export function OperatorAssistanceAlert({ reason, onReturn }: OperatorAssistanceAlertProps) {
  return (
    <InlineStatus
      tone="alert"
      {...(reason === null
        ? {}
        : {
            action: (
              <TextLink size="meta" arrow="right" onClick={onReturn}>
                {returnToRunningEventLabel}
              </TextLink>
            ),
          })}
    >
      {reason === null ? "" : assistanceCopy[reason]}
    </InlineStatus>
  );
}
