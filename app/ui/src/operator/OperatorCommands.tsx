import { createContext, use, useCallback, useRef, useState, type ReactNode } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import type { CommandResult } from "../brain/types.ts";
import { Notice } from "../components/Notice/index.ts";
import type { CommandPayloadMap, RejectReason } from "../protocol/generated.ts";
import { commandNotice } from "./format.ts";

const ReportContext = createContext<(message: string | null) => void>(() => undefined);
const MessageContext = createContext<string | null>(null);

/**
 * Holds the surface's local notice: the polite line for a command the brain didn't accept ("That isn't ready yet.").
 * Surfaces send commands through `useSendCommand`, which reports into it; `LocalNotice` shows it.
 */
export function OperatorNoticeProvider({ children }: { readonly children: ReactNode }) {
  const [message, setMessage] = useState<string | null>(null);
  return (
    <ReportContext value={setMessage}>
      <MessageContext value={message}>{children}</MessageContext>
    </ReportContext>
  );
}

/**
 * The local notice, in a live region that stays mounted so its text is announced when it appears. It stays until the
 * operator closes it or a command is accepted; it is never a toast.
 */
export function LocalNotice() {
  const message = use(MessageContext);
  const report = use(ReportContext);
  return (
    <Notice
      onDismiss={() => {
        report(null);
      }}
    >
      {message ?? ""}
    </Notice>
  );
}

export interface SendOptions {
  /** One command per key at a time: a second request with the same key is ignored until the first is answered. */
  readonly key?: string;
  /** Rejections the caller handles itself, such as `validationFailed` on save: they raise no notice. */
  readonly expected?: readonly RejectReason[];
}

export type SendCommand = <K extends keyof CommandPayloadMap>(
  name: K,
  payload: CommandPayloadMap[K],
  options?: SendOptions,
) => Promise<CommandResult | null>;

/**
 * Sends a command to the brain and puts a polite notice up for any outcome other than accepted; an accepted command
 * clears it. Resolves with the result, or null when a request with the same `key` was still in flight.
 */
export function useSendCommand(): SendCommand {
  const client = useBrainClient();
  const report = use(ReportContext);
  const inFlight = useRef(new Set<string>());
  return useCallback(
    async (name, payload, options) => {
      const key = options?.key;
      if (key !== undefined) {
        if (inFlight.current.has(key)) {
          return null;
        }
        inFlight.current.add(key);
      }
      const result = await client.sendCommand(name, payload);
      if (key !== undefined) {
        inFlight.current.delete(key);
      }
      const handled = result.status === "rejected" && result.reason !== null && options?.expected?.includes(result.reason) === true;
      if (!handled) {
        report(commandNotice(result));
      }
      return result;
    },
    [client, report],
  );
}
