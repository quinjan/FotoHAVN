import type { AppEnv } from "../env.ts";
import { ProtocolBrainClient, type ProtocolClientOptions } from "./protocolClient.ts";
import type { BrainClient, Transport } from "./types.ts";
import { bridgeUrl, createWebSocketTransport, type SocketLike } from "./webSocketTransport.ts";
import { createWebView2Transport } from "./webview2Transport.ts";

export interface StartupParams {
  /** `?token=`: the headless host's per-role token. */
  readonly token: string | null;
  /** `?brain=`: a bridge URL on another origin, for `npm run dev` against a headless host. */
  readonly brain: string | null;
  /** `?fixture=`: an inline fixture name (development and E2E builds only). */
  readonly fixture: string | null;
  /** `?e2eProtocolVersion=`: the `hello` version override (E2E builds only), for the protocol-mismatch scenario. */
  readonly protocolVersion: number | null;
}

function nonEmpty(value: string | null): string | null {
  return value === null || value === "" ? null : value;
}

export function readStartupParams(search: string, env: AppEnv): StartupParams {
  const params = new URLSearchParams(search);
  const version = params.get("e2eProtocolVersion");
  return {
    token: nonEmpty(params.get("token")),
    brain: nonEmpty(params.get("brain")),
    fixture: env.dev || env.e2e ? nonEmpty(params.get("fixture")) : null,
    protocolVersion: env.e2e && version !== null && /^-?\d+$/.test(version) ? Number(version) : null,
  };
}

export interface BrainClientStartup {
  readonly target: Pick<Window, "chrome" | "location">;
  readonly env: AppEnv;
  readonly createSocket?: (url: string) => SocketLike;
  readonly clientOptions?: Omit<ProtocolClientOptions, "protocolVersion">;
  /**
   * The fixture lane's transport for a `?fixture=` name (null when unknown). Only development and E2E builds pass it
   * (`main.tsx` loads the lane with a guarded dynamic import), so field builds contain no fixture code.
   */
  readonly fixtures?: (name: string) => Transport | null;
}

/**
 * Picks the transport once at startup: the WebView2 bridge inside the WPF host; else the headless host's WebSocket
 * when the page has a `?token=`; else, in development and E2E builds, a `?fixture=`; else none, and the page shows
 * that it isn't connected.
 */
export function selectTransport(startup: BrainClientStartup, params: StartupParams): Transport | null {
  const webview = startup.target.chrome?.webview;
  if (webview !== undefined) {
    return createWebView2Transport(webview);
  }
  if (params.token !== null) {
    let url: string;
    try {
      url = bridgeUrl(startup.target.location, params.token, params.brain);
    } catch {
      console.warn("FOTOHAVN: ?brain= isn't a valid URL.");
      return null;
    }
    return createWebSocketTransport(startup.createSocket === undefined ? { url } : { url, createSocket: startup.createSocket });
  }
  if (params.fixture !== null) {
    return startup.fixtures?.(params.fixture) ?? null;
  }
  return null;
}

export function createBrainClient(startup: BrainClientStartup): BrainClient {
  const params = readStartupParams(startup.target.location.search, startup.env);
  const transport = selectTransport(startup, params);
  return new ProtocolBrainClient(transport, {
    ...startup.clientOptions,
    ...(params.protocolVersion === null ? {} : { protocolVersion: params.protocolVersion }),
  });
}

/** What E2E builds expose as `window.fotohavnE2E`, so Playwright can send hooks through the page's own connection. */
export type E2EHandle = Pick<BrainClient, "kind" | "getState" | "sendCommand" | "sendTest">;

export function exposeE2EHandle(target: object, client: BrainClient, env: AppEnv): boolean {
  if (!env.e2e) {
    return false;
  }
  const handle: E2EHandle = {
    kind: client.kind,
    getState: () => client.getState(),
    sendCommand: (name, payload) => client.sendCommand(name, payload),
    sendTest: (name, args) => client.sendTest(name, args),
  };
  Object.defineProperty(target, "fotohavnE2E", { value: handle, configurable: true });
  return true;
}
