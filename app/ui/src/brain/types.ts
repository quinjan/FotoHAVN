import type { FrameSource } from "../components/LivePreview/index.ts";
import type {
  ClientMessage,
  CommandPayloadMap,
  EnvelopeError,
  GuestPresentation,
  OperatorPresentation,
  RejectReason,
  TestHookError,
  TestHookMap,
} from "../protocol/generated.ts";

/**
 * - `notConnected`: no host bridge exists (a field build opened outside the host); nothing will ever connect.
 * - `connecting`: the transport is opening or `hello` is on its way; no `welcome` yet.
 * - `connected`: a `welcome` arrived on the current connection.
 * - `reconnecting`: the connection dropped after a `welcome`; the last snapshot stays on screen until the next one.
 * - `protocolMismatch`: the brain answered `hello` with `protocolMismatch`.
 */
export type ConnectionStatus = "notConnected" | "connecting" | "connected" | "reconnecting" | "protocolMismatch";

/** The role and latest accepted snapshot. The role always comes from the brain's `welcome`, never from the page. */
export type Session =
  | { readonly role: "operator"; readonly revision: number; readonly presentation: OperatorPresentation }
  | { readonly role: "guest"; readonly revision: number; readonly presentation: GuestPresentation };

export interface ProtocolMismatch {
  readonly brainVersion: number;
  readonly uiVersion: number;
}

export interface BrainClientState {
  readonly status: ConnectionStatus;
  readonly session: Session | null;
  readonly mismatch: ProtocolMismatch | null;
}

/** Failures decided in the UI, without an answer from the brain. */
export type LocalFailure = "timeout" | "notConnected" | "disconnected" | "disposed";

export type CommandResult =
  | { readonly status: "accepted" }
  | { readonly status: "rejected"; readonly reason: RejectReason | null }
  | { readonly status: "failed"; readonly error: LocalFailure | EnvelopeError };

export type TestHookName = keyof TestHookMap;

export type TestResult<K extends TestHookName> =
  | { readonly ok: true; readonly result: TestHookMap[K]["result"] }
  | { readonly ok: false; readonly error: TestHookError | EnvelopeError | LocalFailure };

export type TransportKind = "webview2" | "websocket" | "fixture";

/** The connection from a page to the brain. Every implementation speaks the same envelope (contract §2). */
export interface BrainClient {
  /** The transport this client uses, or `none` when no bridge exists. */
  readonly kind: TransportKind | "none";
  /** The live preview frames this connection delivers (contract §6); absent when the transport carries none. */
  readonly frames?: FrameSource | undefined;
  /** The URL a composed preview is fetched from, by its key; absent when the transport has no host to serve it. */
  readonly previewUrl?: ((key: string) => string) | undefined;
  /** Starts the transport; `hello` goes out whenever it opens. Calling it again does nothing. */
  connect(): void;
  /** The current state. The same object is returned until it changes. Bound, so `useSyncExternalStore` can take it. */
  readonly getState: () => BrainClientState;
  /** Calls `listener` after each state change; returns the unsubscribe function. Bound, like `getState`. */
  readonly subscribe: (listener: () => void) => () => void;
  /** Sends a command and resolves with its correlated `commandResult`, or a local failure. Never rejects. */
  sendCommand<K extends keyof CommandPayloadMap>(name: K, payload: CommandPayloadMap[K]): Promise<CommandResult>;
  /** Sends a test hook message (E2E). Hooks without arguments take `null`. Never rejects. */
  sendTest<K extends TestHookName>(name: K, args: TestHookMap[K]["args"]): Promise<TestResult<K>>;
  /** Stops the transport and fails every pending request with `disposed`. */
  dispose(): void;
}

export interface TransportListener {
  /** The transport is open; for WebSocket, called again after every reconnect. */
  onOpen(): void;
  /** One brain message, already decoded from JSON. */
  onMessage(message: unknown): void;
  /** The transport closed; a WebSocket transport reconnects on its own and calls `onOpen` again. */
  onClose(): void;
}

/** Moves envelopes between the page and the brain; holds no protocol state. */
export interface Transport {
  readonly kind: TransportKind;
  /** The live preview frames this bridge carries next to the envelopes; absent when it carries none. */
  readonly frames?: FrameSource | undefined;
  /** The URL this bridge's host serves a composed preview at, by its key (`preview.fotohavn`, or `/preview/` headless). */
  readonly previewUrl?: ((key: string) => string) | undefined;
  start(listener: TransportListener): void;
  /** Returns false when the message couldn't be sent (not open). */
  send(message: ClientMessage): boolean;
  stop(): void;
}
