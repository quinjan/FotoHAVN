import {
  PROTOCOL_VERSION,
  type ClientMessage,
  type Command,
  type CommandResultMessage,
  type GuestWelcomeMessage,
  type OperatorWelcomeMessage,
  type ServerMessage,
} from "../protocol/generated.ts";
import type { Transport, TransportListener } from "./types.ts";

export type WelcomeMessage = OperatorWelcomeMessage | GuestWelcomeMessage;

export interface FixtureTransport extends Transport {
  /** Delivers a brain message as if the brain had sent it (tests and the fixture gallery). */
  push(message: ServerMessage): void;
  /** Every message the page sent, oldest first. */
  readonly sent: readonly ClientMessage[];
}

export interface FixtureTransportOptions {
  /** Decides each command's answer; by default every command is `rejected notAvailable`, as the M2 brain answers. */
  readonly answerCommand?: (command: Command) => Pick<CommandResultMessage, "status" | "reason">;
}

/**
 * A stand-in brain with one static snapshot: answers `hello` with the welcome (or `protocolMismatch` for another
 * version), each command with a `commandResult`, and each test hook with `hooksUnavailable`, as a field build would.
 * Messages arrive asynchronously, like a real bridge.
 */
export function createFixtureTransport(welcome: WelcomeMessage, options: FixtureTransportOptions = {}): FixtureTransport {
  const answerCommand = options.answerCommand ?? (() => ({ status: "rejected", reason: "notAvailable" }) as const);
  const sent: ClientMessage[] = [];
  let listener: TransportListener | null = null;

  function deliver(message: ServerMessage): void {
    queueMicrotask(() => {
      listener?.onMessage(message);
    });
  }

  function answer(message: ClientMessage): ServerMessage {
    switch (message.type) {
      case "hello":
        return message.protocolVersion === PROTOCOL_VERSION
          ? welcome
          : { type: "protocolMismatch", brainVersion: PROTOCOL_VERSION, uiVersion: message.protocolVersion };
      case "command":
        return { type: "commandResult", id: message.id, ...answerCommand(message) };
      case "test":
        return { type: "testResult", id: message.id, ok: false, result: null, error: "hooksUnavailable" };
    }
  }

  return {
    kind: "fixture",
    sent,
    start(next: TransportListener) {
      if (listener !== null) {
        return;
      }
      listener = next;
      queueMicrotask(() => {
        next.onOpen();
      });
    },
    send(message: ClientMessage) {
      if (listener === null) {
        return false;
      }
      sent.push(message);
      deliver(answer(message));
      return true;
    },
    push(message: ServerMessage) {
      deliver(message);
    },
    stop() {
      listener = null;
    },
  };
}
