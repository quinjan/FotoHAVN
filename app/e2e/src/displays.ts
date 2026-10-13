import type { Page } from "@playwright/test";
import type { DiagnosticsResult, PixelRect, TestDisplay, VirtualDisplaySpec } from "../../ui/src/protocol/generated";
import type { AppSession } from "./launch";

export const virtualOperatorId = "virtual-operator";
export const virtualGuestId = "virtual-guest";

/** The real primary display from diagnostics taken before any virtual displays (none → the test can't map them). */
export function realPrimary(diagnostics: DiagnosticsResult): TestDisplay {
  if (diagnostics.displays.virtual) {
    throw new Error("Diagnostics already list virtual displays; read the real primary before test.useVirtualDisplays.");
  }
  const primary = diagnostics.displays.displays.find((display) => display.isPrimary);
  if (primary === undefined) {
    throw new Error(`No real primary display: ${JSON.stringify(diagnostics.displays)}`);
  }
  return primary;
}

/**
 * Two virtual displays side by side on the real primary: the left half is the virtual primary (the Operator window
 * stays where it is) and the right half is the guest display. Bounds are relative to the real primary's top-left.
 */
export function splitPrimary(primary: TestDisplay): VirtualDisplaySpec[] {
  const half = Math.floor(primary.bounds.width / 2);
  return [
    { id: virtualOperatorId, name: "Virtual operator", bounds: { x: 0, y: 0, width: half, height: primary.bounds.height }, isPrimary: true },
    {
      id: virtualGuestId,
      name: "Virtual guest",
      bounds: { x: half, y: 0, width: primary.bounds.width - half, height: primary.bounds.height },
      isPrimary: false,
    },
  ];
}

export interface GuestScreen {
  readonly page: Page;
  /** The display the Guest window was opened on, as the brain mapped it (physical pixels, virtual-screen coordinates). */
  readonly display: TestDisplay;
}

/** Uses two virtual displays on the real primary and opens the Guest Screen on the second; resolves with its page. */
export async function openGuestOnVirtualDisplays(app: AppSession): Promise<GuestScreen> {
  const primary = realPrimary(await app.hooks.getDiagnostics());
  const displays = await app.hooks.useVirtualDisplays(splitPrimary(primary));
  const display = displays.find((candidate) => candidate.id === virtualGuestId);
  if (display === undefined) {
    throw new Error(`test.useVirtualDisplays didn't return ${virtualGuestId}: ${JSON.stringify(displays)}`);
  }
  await app.hooks.openGuestScreen(display.id);
  return { page: await app.pageFor("guest", { timeoutMs: 20_000 }), display };
}

/** The page's CSS viewport and device pixel ratio. */
export function viewport(page: Page): Promise<{ width: number; height: number; devicePixelRatio: number }> {
  return page.evaluate(() => ({ width: window.innerWidth, height: window.innerHeight, devicePixelRatio: window.devicePixelRatio }));
}

export function size(rect: PixelRect): { width: number; height: number } {
  return { width: rect.width, height: rect.height };
}
