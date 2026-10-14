import { createPatternFrameSource } from "../components/LivePreview/index.ts";
import {
  PROTOCOL_VERSION,
  type ClientMessage,
  type Command,
  type CommandResultMessage,
  type GuestWelcomeMessage,
  type OperatorWelcomeMessage,
  type ProtocolMismatchMessage,
  type ServerMessage,
} from "../protocol/generated.ts";
import type { Transport, TransportListener } from "./types.ts";
import { webViewPreviewUrl } from "./previewUrl.ts";

export type WelcomeMessage = OperatorWelcomeMessage | GuestWelcomeMessage;

/** What a fixture brain answers `hello` with: a welcome, or a fixed `protocolMismatch` (the mismatch fixture). */
export type FixtureSnapshot = WelcomeMessage | ProtocolMismatchMessage;

export interface FixtureTransport extends Transport {
  /** Delivers a brain message as if the brain had sent it (tests and the fixture gallery). */
  push(message: ServerMessage): void;
  /** Every message the page sent, oldest first. */
  readonly sent: readonly ClientMessage[];
}

export interface FixtureTransportOptions {
  /** Decides each command's answer; by default every command is `rejected notAvailable`, as the M2 brain answers. */
  readonly answerCommand?: (command: Command) => Pick<CommandResultMessage, "status" | "reason">;
  /** The image for a preview key; the fixture lane passes data URLs, since no host serves `preview.fotohavn` there. */
  readonly previewUrl?: (key: string) => string;
}

/**
 * A stand-in brain with one static snapshot: answers `hello` with the welcome (or `protocolMismatch` for another
 * version, and always when the snapshot itself is a `protocolMismatch`), each command with a `commandResult`, and
 * each test hook with `hooksUnavailable`, as a field build would.
 * Messages arrive asynchronously, like a real bridge.
 */
export function createFixtureTransport(snapshot: FixtureSnapshot, options: FixtureTransportOptions = {}): FixtureTransport {
  const answerCommand = options.answerCommand ?? (() => ({ status: "rejected", reason: "notAvailable" }) as const);
  const sent: ClientMessage[] = [];
  // The fixture brain has no camera: the diagnostics preview shows the deterministic pattern instead.
  const frames = createPatternFrameSource(1280, 720);
  let listener: TransportListener | null = null;

  function deliver(message: ServerMessage): void {
    queueMicrotask(() => {
      listener?.onMessage(message);
    });
  }

  function answer(message: ClientMessage): ServerMessage | null {
    switch (message.type) {
      case "hello":
        if (snapshot.type === "protocolMismatch") {
          return snapshot;
        }
        return message.protocolVersion === PROTOCOL_VERSION
          ? snapshot
          : { type: "protocolMismatch", brainVersion: PROTOCOL_VERSION, uiVersion: message.protocolVersion };
      case "command":
        return { type: "commandResult", id: message.id, ...answerCommand(message) };
      case "test":
        return { type: "testResult", id: message.id, ok: false, result: null, error: "hooksUnavailable" };
      case "frameAck":
        // The host consumes frame acks; nothing answers them.
        return null;
    }
  }

  return {
    kind: "fixture",
    frames,
    previewUrl: options.previewUrl ?? webViewPreviewUrl,
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
      const reply = answer(message);
      if (reply !== null) {
        deliver(reply);
      }
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
