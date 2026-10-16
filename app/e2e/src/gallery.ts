import { existsSync, readdirSync, readFileSync } from "node:fs";
import path from "node:path";
import { expect, type CDPSession, type Locator, type Page } from "@playwright/test";
import { databaseFileName, queryAll } from "./data";
import { tap, type GuestRun, type StoredCycle } from "./guest-cycle";
import type { DiagnosticsResult, HookClient } from "./hooks";
import { appOrigin } from "./launch";
import { paths } from "./paths";

/** The fixture gallery, served by the E2E UI build the host loads from https://app.fotohavn/. */
export const galleryUrl = `${appOrigin}index.html`;

/** One entry of `?gallery=manifest` (app/ui/src/gallery/discover.ts). */
export interface ManifestFixture {
  readonly id: string;
  readonly title: string;
  readonly surface: "light" | "dark";
  readonly role: "guest" | "operator";
  readonly layout: "centered" | "fill";
  readonly source: string;
}

export const resolutions = [
  { width: 1280, height: 720 },
  { width: 1366, height: 768 },
  { width: 1920, height: 1080 },
  { width: 2560, height: 1440 },
] as const;

export function resolutionName({ width, height }: { width: number; height: number }): string {
  return `${String(width)}x${String(height)}`;
}

/** The Chromium fixture lane's screenshot for one fixture and resolution (app/ui `npm run test:fixtures`). */
export function chromiumBaselinePath(fixtureId: string, resolution: { width: number; height: number }): string {
  return path.join(paths.appDir, "output", "fixtures", "components", fixtureId, `${resolutionName(resolution)}.png`);
}

/**
 * Navigates the page back to the app itself (not a gallery URL) and waits for its brain handshake, so the test hooks
 * (`window.fotohavnE2E`) work again. The gallery mounts without a brain connection, so a test that visited it calls this
 * before it ends and before it needs a hook.
 */
export async function returnToApp(page: Page): Promise<void> {
  await page.goto(galleryUrl);
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-status", "connected");
}

export async function readManifest(page: Page): Promise<readonly ManifestFixture[]> {
  await page.goto(`${galleryUrl}?gallery=manifest`);
  await expect(page.locator('[data-fotohavn-gallery-error="true"]')).toHaveCount(0);
  const text = await page.locator("#fotohavn-gallery-manifest").textContent();
  return (JSON.parse(text ?? "{}") as { fixtures: ManifestFixture[] }).fixtures;
}

/** Opens one fixture alone and waits as the Chromium fixture lane does: gallery ready (fonts loaded, painted, finite animations finished), role set, fixture visible. */
export async function openFixture(page: Page, fixtureId: string, role: ManifestFixture["role"]): Promise<void> {
  await page.goto(`${galleryUrl}?gallery=${encodeURIComponent(fixtureId)}`);
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-gallery-ready", "true");
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", role);
  await expect(page.locator(`main[data-fotohavn-fixture="${fixtureId}"]`)).toBeVisible();
}

interface TokenFile {
  color: Record<string, { $value: string }>;
}

/** The brand color tokens from brand/tokens/tokens.json, keyed by token name (`off-white`), as `rgb(r, g, b)` strings. */
export function brandColors(): Record<string, string> {
  const file = path.resolve(paths.appDir, "..", "brand", "tokens", "tokens.json");
  const tokens = JSON.parse(readFileSync(file, "utf8")) as TokenFile;
  return Object.fromEntries(
    Object.entries(tokens.color).map(([name, token]) => {
      const hex = /^#([0-9a-f]{6})$/i.exec(token.$value);
      if (hex?.[1] === undefined) {
        throw new Error(`brand/tokens/tokens.json color ${name} is not a #RRGGBB value: ${token.$value}`);
      }
      const value = Number.parseInt(hex[1], 16);
      return [name, `rgb(${String((value >> 16) & 255)}, ${String((value >> 8) & 255)}, ${String(value & 255)})`];
    }),
  );
}

// --- The Event Gallery (M9): the operator's list and the guest's row, their diagnostics, and the cycles that fill them. ---

export type GalleryDiagnostics = NonNullable<DiagnosticsResult["gallery"]>;

/** The brain's view of the open galleries (null while neither is open). */
export async function galleryDiagnostics(hooks: HookClient): Promise<GalleryDiagnostics | null> {
  return (await hooks.getDiagnostics()).gallery;
}

/** The operator's gallery diagnostics, or null while it is closed. */
export async function operatorGallery(hooks: HookClient): Promise<NonNullable<GalleryDiagnostics["operator"]> | null> {
  return (await galleryDiagnostics(hooks))?.operator ?? null;
}

/** The guest's gallery diagnostics, or null while it is closed. */
export async function guestGallery(hooks: HookClient): Promise<NonNullable<GalleryDiagnostics["guest"]> | null> {
  return (await galleryDiagnostics(hooks))?.guest ?? null;
}

/** The Operator Screen's Event Gallery and Guest Cycle detail, found by role and name. */
export class OperatorGalleryScreen {
  readonly main: Locator;
  readonly detail: Locator;
  readonly heading: Locator;
  readonly backToEvents: Locator;
  readonly list: Locator;
  readonly items: Locator;
  readonly empty: Locator;
  readonly confirmDialog: Locator;

  constructor(readonly page: Page) {
    this.main = page.locator('main[data-fotohavn-surface="eventGallery"]');
    this.detail = page.locator('main[data-fotohavn-surface="guestCycleDetail"]');
    this.heading = this.main.getByRole("heading", { level: 1, name: /Event\s+Gallery\./ });
    this.backToEvents = this.main.getByRole("button", { name: "BACK TO EVENTS" });
    this.list = this.main.getByRole("list", { name: "Guest Cycles" });
    this.items = this.list.getByRole("listitem");
    this.empty = this.main.getByText("No photographs yet.");
    this.confirmDialog = page.getByRole("alertdialog", { name: "Delete this Guest Cycle?" });
  }

  /** The nth item (0 = newest). */
  item(index: number): Locator {
    return this.items.nth(index);
  }

  /** What an item's caption says: time, outcome, Template and Look. */
  caption(index: number): Locator {
    return this.item(index).locator("p");
  }

  /** The text of every item's caption, newest first. */
  async captions(): Promise<string[]> {
    return (await this.items.locator("p").allTextContents()).map((text) => text.replace(/\s+/g, " ").trim());
  }

  /** `DELETE` on the nth item, then the confirmation's `DELETE GUEST CYCLE`. */
  async deleteItem(index: number): Promise<void> {
    await this.item(index).getByRole("button", { name: "DELETE", exact: true }).click();
    await expect(this.confirmDialog).toBeVisible();
    await this.confirmDialog.getByRole("button", { name: "DELETE GUEST CYCLE", exact: true }).click();
  }
}

/** The Guest Screen's gallery row, strip detail, and Start link. */
export class GuestGalleryScreen {
  readonly list: Locator;
  readonly detail: Locator;
  readonly strips: Locator;
  readonly link: Locator;
  readonly backToStart: Locator;
  readonly close: Locator;
  readonly warning: Locator;
  readonly row: Locator;

  constructor(readonly page: Page) {
    this.list = page.locator('main[data-fotohavn-surface="guestGallery"]');
    this.detail = page.locator('main[data-fotohavn-surface="guestStripDetail"]');
    this.strips = this.list.locator("ul li button");
    this.link = page.locator('main[data-fotohavn-surface="start"]').getByRole("button", { name: "SEE THE GOOD COMPANY" });
    this.backToStart = page.getByRole("button", { name: "BACK TO START" });
    this.close = this.detail.getByRole("button", { name: "CLOSE", exact: true });
    this.warning = page.locator('[data-phase="guest-gallery"]');
    this.row = this.list.locator("[data-fotohavn-scroll-region]");
  }

  /** `SEE THE GOOD COMPANY →` by touch: the guest gallery opens. */
  async open(): Promise<void> {
    await tap(this.link);
    await expect(this.list).toBeVisible();
  }
}

/** The Operator Screen's gallery of the running Event, opened from its panel (`OPEN GALLERY`). */
export async function openRunningGallery(run: GuestRun): Promise<OperatorGalleryScreen> {
  await run.list.openGallery.click();
  const screen = new OperatorGalleryScreen(run.operatorPage);
  await expect(screen.main).toBeVisible();
  await expect.poll(async () => (await operatorGallery(run.hooks))?.eventId, { message: "the operator gallery is open on the running Event" }).toBe(run.eventId);
  return screen;
}

async function waitForEndedCycle(run: GuestRun, endReason: string, before: number): Promise<StoredCycle> {
  await expect
    .poll(async () => (await run.cycles()).filter((cycle) => cycle.end_reason !== null).length, {
      timeout: 60_000,
      message: `the Event has ${String(before + 1)} ended Guest Cycles`,
    })
    .toBe(before + 1);
  const ended = (await run.cycles()).filter((cycle) => cycle.end_reason !== null);
  const cycle = ended[ended.length - 1];
  if (cycle?.end_reason !== endReason) {
    throw new Error(`The newest Guest Cycle ended as ${cycle?.end_reason ?? "nothing"}, not ${endReason}.`);
  }
  return cycle;
}

/**
 * One guest finishes a Photo Strip by touch on the fast path: Template, four photographs, Filters, FINISH MY STRIP, and the Keepsake
 * running out (set the Event's Keepsake timer short). With `retake`, the second photograph is retaken and accepted first, so
 * the cycle also holds a discarded Capture. Returns the stored cycle once it has ended as completed.
 */
export async function completeCycle(run: GuestRun, options: { readonly retake?: boolean } = {}): Promise<StoredCycle> {
  const before = (await run.cycles()).filter((cycle) => cycle.end_reason !== null).length;
  const guest = await run.stepInside();
  await expect(guest.surface("template")).toBeVisible();
  await tap(guest.templateContinue);
  await run.waitForStep("photographs", "camera");
  await run.takePhotographsUntil(guest, 4);
  await run.waitForStep("photographs", "arrange");
  if (options.retake === true) {
    await tap(guest.windowButton(2));
    await expect(guest.page.getByText("Photograph 2 is selected.")).toBeVisible();
    await tap(guest.retake);
    await expect(guest.accept).toBeVisible({ timeout: 30_000 });
    await tap(guest.accept);
    await expect(guest.accept).toHaveCount(0);
  }
  await tap(guest.continueToFilters);
  await run.waitForStep("filters");
  await tap(guest.finishStrip);
  await expect(guest.surface("keepsake")).toBeVisible({ timeout: 30_000 });
  await expect(guest.surface("start")).toBeVisible({ timeout: 60_000 });
  return await waitForEndedCycle(run, "completed", before);
}

/** One guest takes a photograph and starts over: the cycle ends as started_over, keeping the Capture, with no Photo Strip. */
export async function startOverCycle(run: GuestRun): Promise<StoredCycle> {
  const before = (await run.cycles()).filter((cycle) => cycle.end_reason !== null).length;
  const guest = await run.stepInside();
  await expect(guest.surface("template")).toBeVisible();
  await tap(guest.templateContinue);
  await run.waitForStep("photographs", "camera");
  await run.takePhotographsUntil(guest, 1);
  await run.stopCapture(guest);
  await tap(guest.startOver);
  await tap(guest.confirmStartOver());
  await expect(guest.surface("start")).toBeVisible();
  return await waitForEndedCycle(run, "started_over", before);
}

/** The thumbnails of a Guest Cycle's strip (named by the cycle's id) and of its Captures (named by their ids) in the Data folder's cache. */
export function thumbnailFiles(dataDir: string, sourceIds: readonly string[]): string[] {
  const folder = path.join(dataDir, "Cache", "thumbnails");
  if (!existsSync(folder)) {
    return [];
  }
  return readdirSync(folder).filter((name) => sourceIds.some((id) => name.startsWith(`${id}-`)));
}

/** Every Capture id of a Guest Cycle, kept or discarded. */
export function captureIds(dataDir: string, cycleId: string): string[] {
  return queryAll<{ id: string }>(path.join(dataDir, databaseFileName), "SELECT id FROM captures WHERE guest_cycle_id = ?", cycleId).map((row) => row.id);
}

const touchSessions = new WeakMap<Page, Promise<CDPSession>>();

/** One finger from `from` to `to` in small steps, as raw touch events (CDP's synthesized scroll gesture doesn't scroll WebView2). */
export async function touchSwipe(page: Page, from: { x: number; y: number }, to: { x: number; y: number }, steps = 10): Promise<void> {
  let session = touchSessions.get(page);
  if (session === undefined) {
    session = page.context().newCDPSession(page);
    touchSessions.set(page, session);
  }
  const cdp = await session;
  await cdp.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: [{ ...from, id: 0 }] });
  try {
    for (let step = 1; step <= steps; step += 1) {
      const progress = step / steps;
      await cdp.send("Input.dispatchTouchEvent", {
        type: "touchMove",
        touchPoints: [{ x: from.x + (to.x - from.x) * progress, y: from.y + (to.y - from.y) * progress, id: 0 }],
      });
    }
  } finally {
    await cdp.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
  }
}
