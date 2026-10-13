import { randomUUID } from "node:crypto";
import type { Page } from "@playwright/test";
import type { Camera } from "./camera";

/**
 * The E2E-only test hooks and their payloads (M1 interim channel).
 * Messages go out as `{ type, id, args? }` through `window.chrome.webview.postMessage`;
 * the host replies with `{ type: "test.result", id, ok, result | error }`.
 */
export interface HookMap {
  "test.listCameras": { args: undefined; result: { cameras: Camera[] } };
  "test.exit": { args: undefined; result: unknown };
}

export type HookName = keyof HookMap;

export type HookResultMessage =
  | { type: "test.result"; id: string; ok: true; result?: unknown }
  | { type: "test.result"; id: string; ok: false; error: string };

export class HookError extends Error {
  constructor(
    readonly hook: string,
    readonly code: string,
  ) {
    super(`Test hook ${hook} failed: ${code}`);
    this.name = "HookError";
  }
}

export const defaultHookTimeoutMs = 10_000;

interface HookRequest {
  type: string;
  id: string;
  args: unknown;
  timeoutMs: number;
}

export class HookClient {
  constructor(private readonly page: Page) {}

  async call<K extends HookName>(
    type: K,
    args?: HookMap[K]["args"],
    timeoutMs: number = defaultHookTimeoutMs,
  ): Promise<HookMap[K]["result"]> {
    const reply = await this.post({ type, id: randomUUID(), args, timeoutMs });
    if (!reply.ok) {
      throw new HookError(type, reply.error);
    }
    return reply.result as HookMap[K]["result"];
  }

  async listCameras(): Promise<Camera[]> {
    const { cameras } = await this.call("test.listCameras");
    return cameras;
  }

  /**
   * Asks the app to shut down. The app may close the page before or while the reply arrives,
   * so a lost connection after sending counts as delivered; the caller checks the exit code.
   */
  async exit(): Promise<void> {
    try {
      await this.call("test.exit");
    } catch (error) {
      if (error instanceof HookError) {
        throw error;
      }
      if (!isConnectionLost(error)) {
        throw error;
      }
    }
  }

  private post(request: HookRequest): Promise<HookResultMessage> {
    return this.page.evaluate(async ({ type, id, args, timeoutMs }) => {
      interface WebViewMessageEvent {
        data: unknown;
      }
      interface WebView {
        postMessage(message: unknown): void;
        addEventListener(type: "message", listener: (event: WebViewMessageEvent) => void): void;
        removeEventListener(type: "message", listener: (event: WebViewMessageEvent) => void): void;
      }
      const webview = (window as unknown as { chrome?: { webview?: WebView } }).chrome?.webview;
      if (webview === undefined) {
        return { type: "test.result", id, ok: false, error: "webviewUnavailable" } as const;
      }
      return await new Promise<
        | { type: "test.result"; id: string; ok: true; result?: unknown }
        | { type: "test.result"; id: string; ok: false; error: string }
      >((resolve) => {
        const onMessage = (event: WebViewMessageEvent): void => {
          const data = event.data as { type?: unknown; id?: unknown } | null;
          if (data !== null && typeof data === "object" && data.type === "test.result" && data.id === id) {
            clearTimeout(timer);
            webview.removeEventListener("message", onMessage);
            resolve(data as never);
          }
        };
        const timer = setTimeout(() => {
          webview.removeEventListener("message", onMessage);
          resolve({ type: "test.result", id, ok: false, error: "timeout" });
        }, timeoutMs);
        webview.addEventListener("message", onMessage);
        webview.postMessage(args === undefined ? { type, id } : { type, id, args });
      });
    }, request);
  }
}

function isConnectionLost(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error);
  return /closed|disconnected|crashed|destroyed|detached/i.test(message);
}
