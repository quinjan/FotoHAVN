import {
  PROTOCOL_VERSION,
  type Command,
  type CommandPayloadMap,
  type EnvelopeError,
  type ServerMessage,
  type TestHookMap,
  type TestMessage,
} from "../protocol/generated.ts";
import type {
  BrainClient,
  BrainClientState,
  CommandResult,
  LocalFailure,
  TestHookName,
  TestResult,
  Transport,
  TransportListener,
} from "./types.ts";

export interface ProtocolClientOptions {
  /** The version sent in `hello`; E2E builds may override it (contract §9, UI). */
  readonly protocolVersion?: number;
  readonly commandTimeoutMs?: number;
  readonly testTimeoutMs?: number;
  readonly createId?: () => string;
}

export const defaultCommandTimeoutMs = 10_000;
export const defaultTestTimeoutMs = 30_000;

type Pending =
  | { readonly kind: "command"; readonly resolve: (result: CommandResult) => void; readonly timer: ReturnType<typeof setTimeout> }
  | { readonly kind: "test"; readonly resolve: (result: TestResult<TestHookName>) => void; readonly timer: ReturnType<typeof setTimeout> };

function isServerMessage(value: unknown): value is ServerMessage {
  return typeof value === "object" && value !== null && typeof (value as { type?: unknown }).type === "string";
}

/**
 * The protocol logic shared by every transport: handshake, revision ordering, and request correlation.
 * With no transport, the client stays `notConnected` and every request fails locally.
 */
export class ProtocolBrainClient implements BrainClient {
  readonly kind: BrainClient["kind"];

  private readonly transport: Transport | null;
  private readonly protocolVersion: number;
  private readonly commandTimeoutMs: number;
  private readonly testTimeoutMs: number;
  private readonly createId: () => string;
  private readonly listeners = new Set<() => void>();
  private readonly pending = new Map<string, Pending>();
  private state: BrainClientState;
  private started = false;
  private disposed = false;
  /** True once a `welcome` arrived on the current transport connection; reset on every open and close. */
  private handshaken = false;

  constructor(transport: Transport | null, options: ProtocolClientOptions = {}) {
    this.transport = transport;
    this.kind = transport?.kind ?? "none";
    this.protocolVersion = options.protocolVersion ?? PROTOCOL_VERSION;
    this.commandTimeoutMs = options.commandTimeoutMs ?? defaultCommandTimeoutMs;
    this.testTimeoutMs = options.testTimeoutMs ?? defaultTestTimeoutMs;
    this.createId = options.createId ?? (() => crypto.randomUUID());
    this.state = { status: transport === null ? "notConnected" : "connecting", session: null, mismatch: null };
  }

  connect(): void {
    if (this.started || this.disposed || this.transport === null) {
      return;
    }
    this.started = true;
    const listener: TransportListener = {
      onOpen: () => {
        this.handleOpen();
      },
      onMessage: (message) => {
        this.handleMessage(message);
      },
      onClose: () => {
        this.handleClose();
      },
    };
    this.transport.start(listener);
  }

  getState = (): BrainClientState => this.state;

  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };

  sendCommand<K extends keyof CommandPayloadMap>(name: K, payload: CommandPayloadMap[K]): Promise<CommandResult> {
    const id = this.createId();
    const message = { type: "command", id, name, payload } as Command;
    return new Promise<CommandResult>((resolve) => {
      const failure = this.trySend(message);
      if (failure !== null) {
        resolve({ status: "failed", error: failure });
        return;
      }
      const timer = setTimeout(() => {
        this.settle(id, { status: "failed", error: "timeout" }, { ok: false, error: "timeout" });
      }, this.commandTimeoutMs);
      this.pending.set(id, { kind: "command", resolve, timer });
    });
  }

  sendTest<K extends TestHookName>(name: K, args: TestHookMap[K]["args"]): Promise<TestResult<K>> {
    const id = this.createId();
    const message: TestMessage = args === null ? { type: "test", id, name } : { type: "test", id, name, args };
    return new Promise<TestResult<K>>((resolve) => {
      const failure = this.trySend(message);
      if (failure !== null) {
        resolve({ ok: false, error: failure });
        return;
      }
      const timer = setTimeout(() => {
        this.settle(id, { status: "failed", error: "timeout" }, { ok: false, error: "timeout" });
      }, this.testTimeoutMs);
      this.pending.set(id, { kind: "test", resolve, timer });
    });
  }

  dispose(): void {
    if (this.disposed) {
      return;
    }
    this.disposed = true;
    this.transport?.stop();
    this.failPending("disposed");
    this.listeners.clear();
  }

  private trySend(message: Command | TestMessage): LocalFailure | null {
    if (this.disposed) {
      return "disposed";
    }
    if (this.transport?.send(message) !== true) {
      return "notConnected";
    }
    return null;
  }

  private handleOpen(): void {
    this.handshaken = false;
    this.transport?.send({ type: "hello", protocolVersion: this.protocolVersion });
  }

  private handleClose(): void {
    this.handshaken = false;
    this.failPending("disconnected");
    if (this.state.session !== null) {
      this.setState({ ...this.state, status: "reconnecting" });
    }
  }

  private handleMessage(message: unknown): void {
    if (this.disposed || !isServerMessage(message)) {
      return;
    }
    switch (message.type) {
      case "welcome":
        // A welcome always resets the baseline, whatever its revision (contract §4).
        this.handshaken = true;
        this.setState({
          status: "connected",
          session:
            message.role === "operator"
              ? { role: "operator", revision: message.revision, presentation: message.presentation }
              : { role: "guest", revision: message.revision, presentation: message.presentation },
          mismatch: null,
        });
        return;
      case "presentation": {
        const session = this.state.session;
        if (!this.handshaken || session?.role !== message.role || message.revision <= session.revision) {
          return;
        }
        this.setState({
          ...this.state,
          session:
            message.role === "operator"
              ? { role: "operator", revision: message.revision, presentation: message.presentation }
              : { role: "guest", revision: message.revision, presentation: message.presentation },
        });
        return;
      }
      case "protocolMismatch":
        this.handshaken = false;
        this.setState({
          status: "protocolMismatch",
          session: null,
          mismatch: { brainVersion: message.brainVersion, uiVersion: message.uiVersion },
        });
        return;
      case "commandResult":
        this.settle(
          message.id,
          message.status === "accepted" ? { status: "accepted" } : { status: "rejected", reason: message.reason },
          null,
        );
        return;
      case "testResult":
        this.settle(
          message.id,
          null,
          message.ok ? { ok: true, result: message.result as never } : { ok: false, error: message.error ?? "hookFailed" },
        );
        return;
      case "error":
        this.handleError(message.id, message.reason);
        return;
      default:
        return;
    }
  }

  private handleError(id: string | null, reason: EnvelopeError): void {
    if (id !== null && this.pending.has(id)) {
      this.settle(id, { status: "failed", error: reason }, { ok: false, error: reason });
      return;
    }
    console.warn(`FOTOHAVN: the brain reported a protocol error (${reason}).`);
  }

  /** Resolves the pending request with this id using the result for its kind; ignores unknown or settled ids. */
  private settle(id: string, commandResult: CommandResult | null, testResult: TestResult<TestHookName> | null): void {
    const pending = this.pending.get(id);
    if (pending === undefined) {
      return;
    }
    if (pending.kind === "command" && commandResult !== null) {
      this.pending.delete(id);
      clearTimeout(pending.timer);
      pending.resolve(commandResult);
    } else if (pending.kind === "test" && testResult !== null) {
      this.pending.delete(id);
      clearTimeout(pending.timer);
      pending.resolve(testResult);
    }
  }

  private failPending(error: LocalFailure): void {
    const entries = [...this.pending.values()];
    this.pending.clear();
    for (const pending of entries) {
      clearTimeout(pending.timer);
      if (pending.kind === "command") {
        pending.resolve({ status: "failed", error });
      } else {
        pending.resolve({ ok: false, error });
      }
    }
  }

  private setState(next: BrainClientState): void {
    this.state = next;
    for (const listener of [...this.listeners]) {
      listener();
    }
  }
}
