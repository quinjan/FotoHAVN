import type { Page } from "@playwright/test";
import type {
  CameraCaptureView,
  CameraDiagnostics,
  CameraFaultKind,
  CameraState,
  CommandPayloadMap,
  DiagnosticsResult,
  Role,
  StorageFaultKind,
  StorageFaultScope,
  TestCamera,
  TestDisplay,
  TestHookMap,
  TestWindow,
  VirtualDisplaySpec,
} from "../../ui/src/protocol/generated";

/**
 * The E2E-only test hooks over the M2 protocol envelope. A page of the E2E UI build exposes `window.fotohavnE2E`
 * (its own brain connection): hooks go out as `{type:"test", id, name, args?}` and come back as the correlated
 * `{type:"testResult", id, ok, result, error}` (contract §8, §10). Types come from the generated protocol.
 */
export type HookName = keyof TestHookMap;
export type HookArgs<K extends HookName> = TestHookMap[K]["args"];
export type HookResult<K extends HookName> = TestHookMap[K]["result"];

export type {
  CameraCaptureView,
  CameraDiagnostics,
  CameraFaultKind,
  CameraState,
  CommandPayloadMap,
  DiagnosticsResult,
  Role,
  StorageFaultKind,
  StorageFaultScope,
  TestCamera,
  TestDisplay,
  TestWindow,
  VirtualDisplaySpec,
};

/** What the UI's `window.fotohavnE2E.sendTest` resolves with (it never rejects). */
export type HookReply<K extends HookName> = { ok: true; result: HookResult<K> } | { ok: false; error: string };

/** What the UI's `window.fotohavnE2E.sendCommand` resolves with (it never rejects). */
export type CommandReply =
  | { status: "accepted" }
  | { status: "rejected"; reason: string | null }
  | { status: "failed"; error: string };

export class HookError extends Error {
  constructor(
    readonly hook: string,
    readonly code: string,
  ) {
    super(`Test hook ${hook} failed: ${code}`);
    this.name = "HookError";
  }
}

interface E2EHandle {
  sendTest(name: string, args: unknown): Promise<HookReply<HookName>>;
  sendCommand(name: string, payload: unknown): Promise<CommandReply>;
}

/** Sends one hook through `page`'s brain connection and resolves with the brain's reply. */
export function sendHook<K extends HookName>(page: Page, name: K, args: HookArgs<K>): Promise<HookReply<K>> {
  return page.evaluate(
    async ({ name, args }) => {
      const handle = (window as unknown as { fotohavnE2E?: E2EHandle }).fotohavnE2E;
      if (handle === undefined) {
        return { ok: false, error: "fotohavnE2EUnavailable" } as const;
      }
      return await handle.sendTest(name, args);
    },
    { name: name as string, args: args as unknown },
  ) as Promise<HookReply<K>>;
}

/** Sends one command through `page`'s brain connection, as the page's own UI would. */
export function sendCommand<K extends keyof CommandPayloadMap>(
  page: Page,
  name: K,
  payload: CommandPayloadMap[K],
): Promise<CommandReply> {
  return page.evaluate(
    async ({ name, payload }) => {
      const handle = (window as unknown as { fotohavnE2E?: E2EHandle }).fotohavnE2E;
      if (handle === undefined) {
        return { status: "failed", error: "fotohavnE2EUnavailable" } as const;
      }
      return await handle.sendCommand(name, payload);
    },
    { name: name as string, payload: payload as unknown },
  );
}

/** A typed hook client. Each call goes through the page `pageProvider` returns (the Operator page by default). */
export class HookClient {
  constructor(private readonly pageProvider: () => Promise<Page>) {}

  async call<K extends HookName>(name: K, args: HookArgs<K>): Promise<HookResult<K>> {
    const reply = await sendHook(await this.pageProvider(), name, args);
    if (!reply.ok) {
      throw new HookError(name, reply.error);
    }
    return reply.result;
  }

  async listCameras(): Promise<TestCamera[]> {
    const { cameras } = await this.call("test.listCameras", null);
    return cameras;
  }

  getDiagnostics(): Promise<DiagnosticsResult> {
    return this.call("test.getDiagnostics", null);
  }

  async useVirtualDisplays(displays: VirtualDisplaySpec[]): Promise<TestDisplay[]> {
    return (await this.call("test.useVirtualDisplays", { displays })).displays;
  }

  async openGuestScreen(displayId: string): Promise<void> {
    await this.call("test.openGuestScreen", { displayId });
  }

  async closeGuestScreen(): Promise<void> {
    await this.call("test.closeGuestScreen", null);
  }

  setWindowBounds(role: Role, width: number, height: number): Promise<TestWindow> {
    return this.call("test.setWindowBounds", { role, width, height });
  }

  async crashUi(role: Role): Promise<void> {
    await this.call("test.crashUi", { role });
  }

  async injectStorageFault(kind: StorageFaultKind, scope: StorageFaultScope): Promise<void> {
    await this.call("test.injectStorageFault", { kind, scope });
  }

  async clearStorageFault(): Promise<void> {
    await this.call("test.clearStorageFault", null);
  }

  async openCameraDiagnostics(): Promise<void> {
    await this.call("test.openCameraDiagnostics", null);
  }

  async selectDiagnosticsCamera(deviceId: string): Promise<void> {
    await this.call("test.selectDiagnosticsCamera", { deviceId });
  }

  async setDiagnosticsMirror(mirrored: boolean): Promise<void> {
    await this.call("test.setDiagnosticsMirror", { mirrored });
  }

  captureDiagnostics(): Promise<CameraCaptureView> {
    return this.call("test.captureDiagnostics", null);
  }

  async injectCameraFault(kind: CameraFaultKind): Promise<void> {
    await this.call("test.injectCameraFault", { kind });
  }

  async clearCameraFault(): Promise<void> {
    await this.call("test.clearCameraFault", null);
  }

  /**
   * Asks the app to shut down. The app may close the page before or while the reply arrives, so a lost connection
   * after sending counts as delivered; the caller checks the exit code.
   */
  async exit(): Promise<void> {
    try {
      await this.call("test.exit", null);
    } catch (error) {
      if (error instanceof HookError && error.code !== "disconnected") {
        throw error;
      }
      if (!(error instanceof HookError) && !isConnectionLost(error)) {
        throw error;
      }
    }
  }
}

export function isConnectionLost(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error);
  return /closed|disconnected|crashed|destroyed|detached|navigat/i.test(message);
}
