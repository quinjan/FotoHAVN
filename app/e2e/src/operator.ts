import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import { expect, type Locator, type Page } from "@playwright/test";
import type { Camera } from "./camera";
import { databaseFileName, directoryExists, openDatabase, queryAll, queryOne } from "./data";
import { realPrimary, splitPrimary, virtualGuestId, virtualOperatorId } from "./displays";
import type { HookClient, TestDisplay } from "./hooks";
import type { AppSession } from "./launch";
import { EventListScreen, EventSetupScreen, GuestStartScreen } from "./screens/screens";

/** What every operator helper needs: the app, its Operator page, and the camera chosen by global setup. */
export interface OperatorContext {
  readonly app: AppSession;
  readonly page: Page;
  readonly camera: Camera;
}

/** The wait for the camera field to say Ready after a camera is chosen (open, first frame, the freshness poll). */
export const cameraReadyTimeoutMs = 30_000;

/** The 3-second exit hold plus margin for the page's timer and the bridge. */
export const exitHoldMs = 3_400;

const realPrimaries = new WeakMap<AppSession, TestDisplay>();

export interface VirtualScreens {
  readonly operator: TestDisplay;
  /** Present with two virtual displays: the right half of the real monitor. */
  readonly guest: TestDisplay | undefined;
}

/**
 * Maps virtual displays onto the real monitor: two side by side (the Operator window stays on the left one), or a
 * single one covering the whole monitor. It can be called again to switch between the two.
 */
export async function useVirtualScreens(app: AppSession, count: 1 | 2): Promise<VirtualScreens> {
  let primary = realPrimaries.get(app);
  if (primary === undefined) {
    primary = realPrimary(await app.hooks.getDiagnostics());
    realPrimaries.set(app, primary);
  }
  const specs =
    count === 2
      ? splitPrimary(primary)
      : [{ id: virtualOperatorId, name: "Virtual operator", bounds: { ...primary.bounds }, isPrimary: true }];
  const displays = await app.hooks.useVirtualDisplays(specs);
  const operator = displays.find((display) => display.id === virtualOperatorId);
  if (operator === undefined) {
    throw new Error(`test.useVirtualDisplays didn't return ${virtualOperatorId}: ${JSON.stringify(displays)}`);
  }
  return { operator, guest: displays.find((display) => display.id === virtualGuestId) };
}

/** The Guest window's page once its handshake is done. */
export function guestPage(app: AppSession, timeoutMs = 20_000): Promise<Page> {
  return app.pageFor("guest", { timeoutMs });
}

/** The centre of the control on its page, for pointer input. */
async function centreOf(locator: Locator): Promise<{ x: number; y: number }> {
  const box = await locator.boundingBox();
  if (box === null) {
    throw new Error("The control to hold has no box on the page.");
  }
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

/**
 * A press-and-hold on the control with a touch contact (CDP touch input; the page's own timer counts the hold). The Guest
 * Screen is a touch screen, and a touch keeps its implicit capture for the whole hold. A synthetic mouse press does not:
 * the window takes focus on the press, and Windows then sends the real OS cursor's mouse-move (buttons up) to the window
 * under it, which makes the browser drop the pointer capture, so the hold is cancelled whenever the real cursor rests
 * over the Guest window. Use {@link holdMouseFor} only where the real cursor is elsewhere.
 */
export async function holdFor(locator: Locator, ms: number): Promise<void> {
  const page = locator.page();
  const { x, y } = await centreOf(locator);
  const session = await page.context().newCDPSession(page);
  try {
    await session.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: [{ x, y }] });
    try {
      await page.waitForTimeout(ms);
    } finally {
      await session.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
    }
  } finally {
    await session.detach();
  }
}

/** A mouse press-and-hold at the control's centre: mouse down, wait, mouse up (see {@link holdFor} for its limit). */
export async function holdMouseFor(locator: Locator, ms: number): Promise<void> {
  const page = locator.page();
  const { x, y } = await centreOf(locator);
  await page.mouse.move(x, y);
  await page.mouse.down();
  try {
    await page.waitForTimeout(ms);
  } finally {
    await page.mouse.up();
  }
}

/** The keyboard variant of {@link holdFor}: key down on the focused control, wait, key up. */
export async function holdKeyFor(locator: Locator, key: "Enter" | " ", ms: number): Promise<void> {
  const page = locator.page();
  await locator.focus();
  await page.keyboard.down(key);
  try {
    await page.waitForTimeout(ms);
  } finally {
    await page.keyboard.up(key);
  }
}

/** Polls the brain's recent command results until a command with this name was answered with `status`. */
export async function waitForCommandResult(
  hooks: HookClient,
  name: string,
  status: "accepted" | "rejected" | "failed" = "accepted",
  timeoutMs = 10_000,
): Promise<void> {
  await expect
    .poll(async () => (await hooks.getDiagnostics()).lastCommandResults.some((result) => result.name === name && result.status === status), {
      timeout: timeoutMs,
      message: `waiting for ${name} to be ${status}`,
    })
    .toBe(true);
}

export interface EventDetails {
  readonly name: string;
  /** `yyyy-MM-dd`; the default is today's date, which a new Event already has. */
  readonly date?: string;
  readonly venue?: string;
  readonly customer?: string;
  readonly notes?: string;
  /** The guest display row's name (`Virtual guest`); none chosen when omitted. */
  readonly display?: string;
  /** Four to six digits; none set when omitted. */
  readonly pin?: string;
  /** Chooses the context's camera and waits for Ready (default true). */
  readonly camera?: boolean;
}

/** How `createEvent` ends: the Event list after `SAVE`, the running Event after `SAVE AND START`, or the form as it is. */
export type CreateFinish = "save" | "saveAndStart" | "none";

/** Chooses the context's camera in Event setup and waits for the field to say Ready. */
export async function chooseCamera(context: OperatorContext, setup: EventSetupScreen): Promise<void> {
  await setup.camera.selectOption({ label: context.camera.name });
  await expect(setup.cameraStatus).toHaveText("Ready", { timeout: cameraReadyTimeoutMs });
}

/** Opens `NEW EVENT` from the Event list and fills every field the details name, through the UI. */
export async function openNewEventAndFill(context: OperatorContext, details: EventDetails): Promise<EventSetupScreen> {
  const { page } = context;
  const list = new EventListScreen(page);
  await list.newEvent.click();
  const setup = new EventSetupScreen(page);
  await expect(setup.heading).toBeVisible();
  await setup.name.fill(details.name);
  if (details.date !== undefined) {
    await setup.date.fill(details.date);
  }
  if (details.venue !== undefined) {
    await setup.venue.fill(details.venue);
  }
  if (details.customer !== undefined) {
    await setup.customer.fill(details.customer);
  }
  if (details.notes !== undefined) {
    await setup.notes.fill(details.notes);
  }
  if (details.camera !== false) {
    await chooseCamera(context, setup);
  }
  if (details.display !== undefined) {
    await setup.display(details.display).click();
    await expect(setup.display(details.display)).toBeChecked();
  }
  if (details.pin !== undefined) {
    await setup.pin.fill(details.pin);
    await setup.pinConfirmation.fill(details.pin);
    await setup.pinConfirmation.blur();
  }
  return setup;
}

/**
 * Creates an Event through the UI and returns its id, read from the store. With `saveAndStart` the Event is running
 * when it returns; with `none` the form stays open and the id is empty.
 */
export async function createEvent(context: OperatorContext, details: EventDetails, finish: CreateFinish = "save"): Promise<string> {
  const setup = await openNewEventAndFill(context, details);
  if (finish === "none") {
    return "";
  }
  if (details.pin !== undefined) {
    await waitForCommandResult(context.app.hooks, "SetPin");
  }
  const list = new EventListScreen(context.page);
  if (finish === "save") {
    await setup.save.click();
    await expect(list.row(details.name)).toBeVisible();
  } else {
    await setup.saveAndStart.click();
    await expect(list.runningPanel(details.name)).toBeVisible({ timeout: 30_000 });
  }
  return eventIdByName(context.app.dataDir, details.name);
}

/** The id of the saved Event with this name. */
export function eventIdByName(dataDir: string, name: string): string {
  const row = queryOne<{ id: string }>(path.join(dataDir, databaseFileName), "SELECT id FROM events WHERE name = ? AND deleting_at IS NULL", name);
  if (row === undefined) {
    throw new Error(`No Event named "${name}" in the store.`);
  }
  return row.id;
}

export interface StoredEvent {
  readonly row: Record<string, unknown>;
  readonly templates: string[];
  readonly looks: string[];
  readonly timers: Record<string, number>;
}

/** An Event's row, offered Templates and Looks, and Phase Timers, read-only from the store; undefined when it isn't there. */
export function readStoredEvent(dataDir: string, id: string): StoredEvent | undefined {
  const file = path.join(dataDir, databaseFileName);
  const row = queryOne(file, "SELECT * FROM events WHERE id = ?", id);
  if (row === undefined) {
    return undefined;
  }
  const templates = queryAll<{ template_id: string }>(file, "SELECT template_id FROM event_templates WHERE event_id = ? ORDER BY template_id", id).map(
    (entry) => entry.template_id,
  );
  const looks = queryAll<{ look_id: string }>(file, "SELECT look_id FROM event_looks WHERE event_id = ? ORDER BY look_id", id).map(
    (entry) => entry.look_id,
  );
  const timers = Object.fromEntries(
    queryAll<{ phase: string; seconds: number }>(file, "SELECT phase, seconds FROM event_phase_timers WHERE event_id = ?", id).map((entry) => [
      entry.phase,
      entry.seconds,
    ]),
  );
  return { row, templates, looks, timers };
}

/** Where `digits` appears in the store: `table.column` for each text or blob cell containing them (empty = nowhere). */
export function findDigitsInStore(dataDir: string, digits: string): string[] {
  const db = openDatabase(path.join(dataDir, databaseFileName));
  const needle = Buffer.from(digits, "utf8");
  const found: string[] = [];
  try {
    const tables = db.prepare("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'").all() as { name: string }[];
    for (const { name: table } of tables) {
      const columns = (db.prepare(`PRAGMA table_info("${table}")`).all() as { name: string }[]).map((column) => column.name);
      for (const row of db.prepare(`SELECT * FROM "${table}"`).iterate() as Iterable<Record<string, unknown>>) {
        for (const column of columns) {
          const value = row[column];
          const bytes =
            value instanceof Uint8Array ? Buffer.from(value) : typeof value === "string" ? Buffer.from(value, "utf8") : Buffer.from(String(value ?? ""), "utf8");
          if (bytes.includes(needle) && !found.includes(`${table}.${column}`)) {
            found.push(`${table}.${column}`);
          }
        }
      }
    }
  } finally {
    db.close();
  }
  return found;
}

/** Log and text files in the Data folder (the host's `logs/`, outside the browser profile) that contain `digits`. */
export function findDigitsInLogs(dataDir: string, digits: string): string[] {
  const logs = path.join(dataDir, "logs");
  if (!directoryExists(logs)) {
    return [];
  }
  const hits: string[] = [];
  const walk = (dir: string): void => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        walk(full);
      } else if (existsSync(full) && statSync(full).size < 20_000_000 && readFileSync(full).includes(Buffer.from(digits, "utf8"))) {
        hits.push(full);
      }
    }
  };
  walk(logs);
  return hits;
}

/** The Event's folder under the Data folder. */
export function eventFolder(dataDir: string, eventId: string): string {
  return path.join(dataDir, "Events", eventId);
}

/** The running Event ends: the Guest Screen is closed, the camera is released, and the wake lock is gone. */
export async function expectEventEnded(app: AppSession): Promise<void> {
  await expect.poll(async () => (await app.hooks.getDiagnostics()).guestScreenOpen, { timeout: 15_000 }).toBe(false);
  await expect
    .poll(
      async () => {
        const diagnostics = await app.hooks.getDiagnostics();
        return { camera: diagnostics.camera.state, wakeLock: diagnostics.wakeLock.held, roles: diagnostics.connectedRoles };
      },
      { timeout: 15_000 },
    )
    .toEqual({ camera: "idle", wakeLock: false, roles: ["operator"] });
}

/** Ends the running Event with `END EVENT` and its confirmation, on the Operator Screen. */
export async function endEventFromOperator(app: AppSession, page: Page): Promise<void> {
  const list = new EventListScreen(page);
  await list.endEvent.click();
  const dialog = page.getByRole("alertdialog");
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: "END EVENT", exact: true }).click();
  await expectEventEnded(app);
  await expect(list.runningPanel()).toHaveCount(0);
}

/** The Guest Screen's Start surface once the Guest page has handshaken. */
export async function expectGuestStart(app: AppSession): Promise<GuestStartScreen> {
  const guest = await guestPage(app);
  const start = new GuestStartScreen(guest);
  await expect(start.stepInside).toBeVisible();
  return start;
}
