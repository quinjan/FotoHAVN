import type { ClientMessage } from "../protocol/generated.ts";
import { WebSocketFrameSource } from "../preview/webSocketFrameSource.ts";
import { headlessPreviewUrl } from "./previewUrl.ts";
import type { Transport, TransportListener } from "./types.ts";

/** The part of the browser `WebSocket` the transport uses, so tests can substitute a fake. */
export interface SocketLike {
  readonly readyState: number;
  /** Set to `arraybuffer` on every new socket, so binary preview frames arrive as `ArrayBuffer`s. */
  binaryType?: "arraybuffer" | "blob";
  onopen: ((event: Event) => void) | null;
  onmessage: ((event: MessageEvent) => void) | null;
  onclose: ((event: CloseEvent) => void) | null;
  onerror: ((event: Event) => void) | null;
  send(data: string): void;
  close(): void;
}

export interface WebSocketTransportOptions {
  /** `ws://127.0.0.1:<port>/bridge?token=<token>`; the token decides the role (contract §4). */
  readonly url: string;
  readonly createSocket?: (url: string) => SocketLike;
  /** Delays before each reconnect attempt; the last one repeats. A successful open starts again from the first. */
  readonly backoffMs?: readonly number[];
  /** Where the host serves previews (`headlessPreviewBase`); the page's own `/preview/` by default. */
  readonly previewBase?: string;
}

export const defaultBackoffMs: readonly number[] = [250, 500, 1_000, 2_000, 4_000, 5_000];

const socketOpen = 1;

/**
 * The headless host's bridge: one JSON text frame per message. When the socket closes, it reconnects with backoff
 * and reports `onOpen` again, so the client re-handshakes on every new socket.
 *
 * Binary messages on the same socket are live preview frames (24-byte header, then RGBA; contract §6): `frames` draws
 * them and sends each `frameAck` back as a JSON text message.
 */
export function createWebSocketTransport(options: WebSocketTransportOptions): Transport {
  const createSocket = options.createSocket ?? ((url: string) => new WebSocket(url));
  const backoff = options.backoffMs ?? defaultBackoffMs;
  let listener: TransportListener | null = null;
  let socket: SocketLike | null = null;
  let timer: ReturnType<typeof setTimeout> | null = null;
  let attempt = 0;
  let stopped = false;

  function send(message: ClientMessage): boolean {
    if (socket?.readyState !== socketOpen) {
      return false;
    }
    socket.send(JSON.stringify(message));
    return true;
  }

  const frames = new WebSocketFrameSource((seq) => {
    send({ type: "frameAck", seq });
  });

  function open(): void {
    timer = null;
    const current = createSocket(options.url);
    current.binaryType = "arraybuffer";
    socket = current;
    current.onopen = () => {
      attempt = 0;
      listener?.onOpen();
    };
    current.onmessage = (event) => {
      if (event.data instanceof ArrayBuffer) {
        frames.receive(event.data);
        return;
      }
      if (typeof event.data !== "string") {
        return;
      }
      let message: unknown;
      try {
        message = JSON.parse(event.data);
      } catch {
        console.warn("FOTOHAVN: ignored a bridge message that isn't JSON.");
        return;
      }
      listener?.onMessage(message);
    };
    current.onerror = () => {
      // A close event always follows; reconnecting happens there.
    };
    current.onclose = () => {
      // A socket closed by stop() no longer reports.
      if (socket !== current) {
        return;
      }
      socket = null;
      listener?.onClose();
      scheduleReconnect();
    };
  }

  function scheduleReconnect(): void {
    const index = Math.min(attempt, backoff.length - 1);
    const delay = backoff[index] ?? 0;
    attempt += 1;
    timer = setTimeout(open, delay);
  }

  return {
    kind: "websocket",
    frames,
    previewUrl: (key: string) => headlessPreviewUrl(key, options.previewBase),
    start(next: TransportListener) {
      if (listener !== null || stopped) {
        return;
      }
      listener = next;
      open();
    },
    send,
    stop() {
      stopped = true;
      if (timer !== null) {
        clearTimeout(timer);
        timer = null;
      }
      const current = socket;
      socket = null;
      current?.close();
    },
  };
}

/**
 * The bridge URL for a page served by the headless host: `?brain=` (for `npm run dev` against a host on another port)
 * or `/bridge` on the page's own host, with the page's `?token=` appended.
 */
export function bridgeUrl(location: Pick<Location, "protocol" | "host">, token: string, brain: string | null): string {
  const base = brain ?? `${location.protocol === "https:" ? "wss:" : "ws:"}//${location.host}/bridge`;
  const url = new URL(base);
  url.searchParams.set("token", token);
  return url.toString();
}
