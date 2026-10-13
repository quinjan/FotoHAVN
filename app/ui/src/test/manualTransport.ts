import type { Transport, TransportListener } from "../brain/types.ts";
import type { ClientMessage } from "../protocol/generated.ts";

/** A transport driven by the test: open, deliver, and close by hand; inspect what the client sent. */
export class ManualTransport implements Transport {
  readonly kind = "fixture" as const;
  readonly sent: ClientMessage[] = [];
  listener: TransportListener | null = null;
  isOpen = false;
  stopped = false;

  start(listener: TransportListener): void {
    this.listener = listener;
  }

  send(message: ClientMessage): boolean {
    if (!this.isOpen) {
      return false;
    }
    this.sent.push(message);
    return true;
  }

  stop(): void {
    this.stopped = true;
    this.isOpen = false;
  }

  open(): void {
    this.isOpen = true;
    this.listener?.onOpen();
  }

  receive(message: unknown): void {
    this.listener?.onMessage(message);
  }

  close(): void {
    this.isOpen = false;
    this.listener?.onClose();
  }

  /** The last message sent; fails the test if nothing was sent. */
  last(): ClientMessage {
    const message = this.sent.at(-1);
    if (message === undefined) {
      throw new Error("Nothing was sent.");
    }
    return message;
  }

  /** The id of the last command or test message sent. */
  lastId(): string {
    const message = this.last();
    if (message.type === "hello") {
      throw new Error("The last message was hello.");
    }
    return message.id;
  }
}

/** Ids in the UUID shape, predictable per test. */
export function sequentialIds(): () => string {
  let next = 0;
  return () => {
    next += 1;
    return `00000000-0000-4000-8000-${next.toString(16).padStart(12, "0")}`;
  };
}
