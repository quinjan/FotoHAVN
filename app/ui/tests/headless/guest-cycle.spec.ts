import { readFileSync } from "node:fs";
import { join } from "node:path";
import { DatabaseSync } from "node:sqlite";
import type { Locator, Page } from "@playwright/test";
import type { HeadlessHost } from "./headlessHost.ts";
import { expect, openRolePage, test } from "./hostFixture.ts";

// Integration lane (plan §14, M8): every Guest Cycle scenario that needs no test hook, run on the real headless host
// (real brain, store, compositor, encoder; fake camera and displays) with an operator page and a guest page. The
// hook-dependent scenarios (clock scale, camera, storage and display faults, the exit hold with its PIN) are covered
// by the brain tests named in the M8 verification report (`GuestCycles/Orchestration`), which drive the same brain.

const pin = "739104";

type TemplateName = "Signature Strip" | "Panorama Four" | "Butter Gingham" | "Polka Keepsake";
type LookName = "Monochrome" | "Naturale" | "Soft Monochrome" | "Soft Color" | "Sepia" | "Warm Monochrome" | "Classic";

const allTemplates: readonly TemplateName[] = ["Signature Strip", "Panorama Four", "Butter Gingham", "Polka Keepsake"];
const allLooks: readonly LookName[] = ["Monochrome", "Naturale", "Soft Monochrome", "Soft Color", "Sepia", "Warm Monochrome", "Classic"];

interface EventOptions {
  readonly name: string;
  readonly templates?: readonly TemplateName[];
  readonly looks?: readonly LookName[];
}

interface Running {
  readonly operator: Page;
  readonly guest: Page;
  readonly name: string;
}

/** Creates the Event through Event setup (fake camera, the fake guest display, a PIN, a 3 s countdown) and starts it. */
async function startEvent(operator: Page, options: EventOptions): Promise<void> {
  await operator.getByRole("button", { name: "NEW EVENT" }).click();
  const main = operator.locator('main[data-fotohavn-surface="eventSetup"]');
  await main.getByLabel("Name", { exact: true }).fill(options.name);
  await main.getByLabel("Camera", { exact: true }).selectOption({ label: "Fake camera" });
  await expect(main.locator('section[aria-labelledby="setup-section-camera"]').getByRole("status").first()).toHaveText("Ready");
  await main.getByRole("radio", { name: "Fake guest", exact: true }).click();
  await main.getByRole("radio", { name: "3 seconds", exact: true }).click();
  await main.getByLabel(/^(New )?PIN$/).fill(pin);
  await main.getByLabel(/^Confirm (new )?PIN$/).fill(pin);
  await main.getByLabel(/^Confirm (new )?PIN$/).blur();
  if (options.templates !== undefined) {
    const [first] = options.templates;
    await main.getByRole("radio", { name: `Default ${first ?? ""}`, exact: true }).click();
    for (const template of allTemplates.filter((candidate) => !options.templates?.includes(candidate))) {
      await main.getByRole("checkbox", { name: template, exact: true }).click();
      await expect(main.getByRole("checkbox", { name: template, exact: true })).not.toBeChecked();
    }
  }
  if (options.looks !== undefined) {
    const [first] = options.looks;
    await main.getByRole("radio", { name: `Default ${first ?? ""}`, exact: true }).click();
    for (const look of allLooks.filter((candidate) => !options.looks?.includes(candidate))) {
      await main.getByRole("checkbox", { name: look, exact: true }).click();
      await expect(main.getByRole("checkbox", { name: look, exact: true })).not.toBeChecked();
    }
  }
  await main.getByRole("button", { name: "SAVE AND START", exact: true }).click();
  await expect(operator.getByRole("button", { name: "END EVENT", exact: true })).toBeVisible({ timeout: 30_000 });
}

async function endEvent(operator: Page): Promise<void> {
  await operator.getByRole("button", { name: "END EVENT", exact: true }).click();
  await operator.getByRole("alertdialog").getByRole("button", { name: "END EVENT", exact: true }).click();
  await expect(operator.getByRole("button", { name: "END EVENT", exact: true })).toHaveCount(0);
}

/** An operator page and a guest page on a started Event; the Event ends and both pages close afterwards. */
async function withRunningEvent(
  browser: Parameters<typeof openRolePage>[0],
  host: HeadlessHost,
  options: EventOptions,
  body: (running: Running) => Promise<void>,
): Promise<void> {
  const operator = await openRolePage(browser, host.listening.operatorUrl);
  const guest = await openRolePage(browser, host.listening.guestUrl);
  try {
    for (const rolePage of [operator, guest]) {
      await expect(rolePage.page.locator("html")).toHaveAttribute("data-fotohavn-status", "connected");
    }
    await expect(operator.page.getByRole("button", { name: "NEW EVENT" })).toBeVisible();
    await startEvent(operator.page, options);
    await body({ operator: operator.page, guest: guest.page, name: options.name });
    await endEvent(operator.page);
  } finally {
    await operator.page.context().close();
    await guest.page.context().close();
  }
}

// --- What the guest sees and does. ---

function surface(page: Page, name: string): Locator {
  return page.locator(`main[data-fotohavn-surface="${name}"]`);
}

const button = (page: Page, name: string | RegExp, exact = true): Locator => page.getByRole("button", { name, exact: typeof name === "string" ? exact : false });

function windowButton(page: Page, position: number): Locator {
  return page.getByRole("button", { name: new RegExp(`^Photograph ${String(position)} of \\d+$`) });
}

/** The capture numbers in the strip's windows, in order; null for an empty window. */
async function arrangement(page: Page): Promise<(number | null)[]> {
  return page.getByRole("group", { name: "Your Photo Strip" }).evaluate((group) =>
    Array.from(group.children).map((cell) => {
      const match = /capture-(\d+)\.jpg/.exec(cell.querySelector("img")?.getAttribute("src") ?? "");
      return match === null ? null : Number(match[1]);
    }),
  );
}

async function stepInside(guest: Page): Promise<void> {
  await expect(surface(guest, "start")).toBeVisible();
  const action = button(guest, "STEP INSIDE", false);
  await expect(action).not.toHaveAttribute("aria-disabled", "true", { timeout: 30_000 });
  await action.click();
}

/** The Camera tab's shutter takes photographs until `windows` windows are filled; Arrange opens once all are. */
async function takePhotographs(guest: Page): Promise<void> {
  await button(guest, "Take photographs").click();
}

async function waitForArrange(guest: Page): Promise<void> {
  await expect(guest.locator('[data-photographs-tab="arrange"]')).toBeVisible({ timeout: 90_000 });
}

async function dragWindow(guest: Page, from: number, to: number): Promise<void> {
  const start = await windowButton(guest, from).boundingBox();
  const end = await windowButton(guest, to).boundingBox();
  if (start === null || end === null) {
    throw new Error("A photograph window has no box.");
  }
  await guest.mouse.move(start.x + start.width / 2, start.y + start.height / 2);
  await guest.mouse.down();
  await guest.mouse.move(end.x + end.width / 2, end.y + end.height / 2, { steps: 12 });
  await guest.mouse.up();
}

/** Presses Tab until the control holds focus. */
async function tabTo(page: Page, control: Locator, max = 80): Promise<void> {
  await expect(control).toBeVisible();
  for (let press = 0; press <= max; press += 1) {
    if (await control.evaluate((element) => element === document.activeElement)) {
      return;
    }
    await page.keyboard.press("Tab");
  }
  throw new Error(`Tab didn't reach the control within ${String(max)} presses.`);
}

async function keyboardActivate(page: Page, control: Locator): Promise<void> {
  await tabTo(page, control);
  await expect(control).not.toHaveAttribute("aria-disabled", "true");
  await page.keyboard.press("Enter");
}

// --- What the store holds. ---

interface CycleRow {
  readonly id: string;
  readonly end_reason: string | null;
  readonly ended_at: string | null;
}

interface CaptureRow {
  readonly id: string;
  readonly sequence: number;
  readonly file_name: string;
  readonly state: string;
}

interface StripRow {
  readonly template_id: string;
  readonly look_id: string;
  readonly arrangement: string;
  readonly file_name: string;
}

function query<T>(host: HeadlessHost, sql: string, ...params: string[]): T[] {
  const db = new DatabaseSync(join(host.listening.dataFolder, "fotohavn.db"), { readOnly: true });
  try {
    return db.prepare(sql).all(...params) as T[];
  } finally {
    db.close();
  }
}

function eventId(host: HeadlessHost, name: string): string {
  const [row] = query<{ id: string }>(host, "SELECT id FROM events WHERE name = ?", name);
  if (row === undefined) {
    throw new Error(`No Event named ${name}.`);
  }
  return row.id;
}

function cycles(host: HeadlessHost, name: string): CycleRow[] {
  return query<CycleRow>(host, "SELECT id, end_reason, ended_at FROM guest_cycles WHERE event_id = ? ORDER BY started_at, id", eventId(host, name));
}

async function endedCycles(host: HeadlessHost, name: string, count: number): Promise<CycleRow[]> {
  await expect.poll(() => cycles(host, name).filter((cycle) => cycle.end_reason !== null).length, { timeout: 30_000 }).toBe(count);
  return cycles(host, name);
}

function captures(host: HeadlessHost, cycleId: string): CaptureRow[] {
  return query<CaptureRow>(host, "SELECT id, sequence, file_name, state FROM captures WHERE guest_cycle_id = ? ORDER BY sequence", cycleId);
}

function kept(host: HeadlessHost, cycleId: string): CaptureRow[] {
  return captures(host, cycleId).filter((capture) => capture.state === "kept");
}

function strip(host: HeadlessHost, cycleId: string): StripRow | undefined {
  return query<StripRow>(host, "SELECT template_id, look_id, arrangement, file_name FROM photo_strips WHERE guest_cycle_id = ?", cycleId)[0];
}

function cycleFolder(host: HeadlessHost, name: string, cycleId: string): string {
  return join(host.listening.dataFolder, "Events", eventId(host, name), "GuestCycles", cycleId);
}

/** The PNG's size from its IHDR chunk. */
function pngSize(file: string): { width: number; height: number } {
  const bytes = readFileSync(file);
  expect(bytes.subarray(1, 4).toString("latin1")).toBe("PNG");
  return { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) };
}

function expectStrip(host: HeadlessHost, name: string, cycle: CycleRow, size: { width: number; height: number }): StripRow {
  const row = strip(host, cycle.id);
  expect(row, "the Photo Strip row").toBeDefined();
  if (row === undefined) {
    throw new Error("No Photo Strip.");
  }
  expect(pngSize(join(cycleFolder(host, name, cycle.id), row.file_name))).toEqual(size);
  return row;
}

test.describe.configure({ mode: "serial" });

test("the touch and pointer cycle: Template, four photographs, a drag swap, a Retake accepted, a Look, and the Keepsake", async ({ browser, host }) => {
  test.setTimeout(180_000);
  const name = "Pointer cycle";
  await withRunningEvent(browser, host, { name }, async ({ guest }) => {
    await stepInside(guest);
    await expect(surface(guest, "template")).toBeVisible();
    await button(guest, "CONTINUE TO PHOTOGRAPHS", false).click();
    await expect(surface(guest, "photographs")).toBeVisible();
    await takePhotographs(guest);
    await waitForArrange(guest);
    expect(await arrangement(guest)).toEqual([1, 2, 3, 4]);

    await dragWindow(guest, 1, 3);
    await expect.poll(() => arrangement(guest)).toEqual([3, 2, 1, 4]);

    await windowButton(guest, 2).click();
    await expect(guest.getByText("Photograph 2 is selected.")).toBeVisible();
    await button(guest, "RETAKE").click();
    await button(guest, "ACCEPT").click({ timeout: 30_000 });
    await expect.poll(() => arrangement(guest)).toEqual([3, 5, 1, 4]);

    await button(guest, "CONTINUE TO FILTERS", false).click();
    await expect(surface(guest, "filters")).toBeVisible();
    await guest.getByRole("radio", { name: /^Sepia\b/ }).click();
    await expect(guest.locator("[data-selected-look]")).toHaveAttribute("data-selected-look", "sepia-v1");
    await button(guest, "FINISH MY STRIP", false).click();
    await expect(surface(guest, "keepsake")).toBeVisible({ timeout: 30_000 });
  });

  const [cycle] = await endedCycles(host, name, 1);
  expect(cycle?.end_reason).toBe("completed");
  if (cycle === undefined) {
    return;
  }
  const rows = captures(host, cycle.id);
  expect(rows.map((capture) => `${String(capture.sequence)}:${capture.state}`)).toEqual(["1:kept", "2:discarded", "3:kept", "4:kept", "5:kept"]);
  const folder = cycleFolder(host, name, cycle.id);
  for (const capture of rows) {
    const present = (() => {
      try {
        readFileSync(join(folder, capture.file_name));
        return true;
      } catch {
        return false;
      }
    })();
    expect(present, capture.file_name).toBe(capture.state === "kept");
  }
  const row = expectStrip(host, name, cycle, { width: 600, height: 1800 });
  expect(row.look_id).toBe("sepia-v1");
  const idOf = (sequence: number): string => rows.find((capture) => capture.sequence === sequence)?.id ?? "";
  expect(JSON.parse(row.arrangement)).toEqual([idOf(3), idOf(5), idOf(1), idOf(4)]);
});

test("the keyboard-only cycle moves a photograph with Move instead of drag", async ({ browser, host }) => {
  test.setTimeout(180_000);
  const name = "Keyboard cycle";
  await withRunningEvent(browser, host, { name }, async ({ guest }) => {
    await expect(surface(guest, "start")).toBeVisible();
    await keyboardActivate(guest, button(guest, "STEP INSIDE", false));
    await keyboardActivate(guest, button(guest, "CONTINUE TO PHOTOGRAPHS", false));
    await keyboardActivate(guest, button(guest, "Take photographs"));
    await waitForArrange(guest);
    expect(await arrangement(guest)).toEqual([1, 2, 3, 4]);

    await keyboardActivate(guest, windowButton(guest, 1));
    await expect(guest.getByText("Photograph 1 is selected.")).toBeVisible();
    await keyboardActivate(guest, button(guest, "MOVE"));
    await expect(guest.getByText("Choose where photograph 1 goes.")).toBeVisible();
    await keyboardActivate(guest, button(guest, "Swap with position 3"));
    await expect.poll(() => arrangement(guest)).toEqual([3, 2, 1, 4]);

    await keyboardActivate(guest, button(guest, "CONTINUE TO FILTERS", false));
    await expect(surface(guest, "filters")).toBeVisible();
    await keyboardActivate(guest, button(guest, "FINISH MY STRIP", false));
    await expect(surface(guest, "keepsake")).toBeVisible({ timeout: 30_000 });
  });

  const [cycle] = await endedCycles(host, name, 1);
  expect(cycle?.end_reason).toBe("completed");
  if (cycle !== undefined) {
    const row = expectStrip(host, name, cycle, { width: 600, height: 1800 });
    const rows = kept(host, cycle.id);
    expect(rows).toHaveLength(4);
    const idOf = (sequence: number): string => rows.find((capture) => capture.sequence === sequence)?.id ?? "";
    expect(JSON.parse(row.arrangement)).toEqual([idOf(3), idOf(2), idOf(1), idOf(4)]);
  }
});

test("Start Over from Template, Photographs, and Filters ends each cycle as started_over, keeps the saved Captures, and makes no strip", async ({ browser, host }) => {
  test.setTimeout(180_000);
  const name = "Start over";
  await withRunningEvent(browser, host, { name }, async ({ guest }) => {
    const startOver = async (): Promise<void> => {
      await button(guest, "START OVER").click();
      await expect(guest.getByRole("alertdialog")).toContainText("Your photographs stay with the Event, but no strip will be made.");
      await guest.getByRole("alertdialog").getByRole("button", { name: "START OVER", exact: true }).click();
      await expect(surface(guest, "start")).toBeVisible();
    };

    await stepInside(guest);
    await expect(surface(guest, "template")).toBeVisible();
    await startOver();

    await stepInside(guest);
    await button(guest, "CONTINUE TO PHOTOGRAPHS", false).click();
    await takePhotographs(guest);
    await expect(guest.getByRole("progressbar", { name: "Photographs" })).toHaveAttribute("aria-valuenow", "1", { timeout: 30_000 });
    await button(guest, "Stop taking photographs").click();
    await expect(button(guest, "Take photographs")).toBeVisible();
    await startOver();

    await stepInside(guest);
    await guest.getByRole("radio", { name: /^Butter Gingham\b/ }).click();
    await expect(guest.locator("[data-selected-template]")).toHaveAttribute("data-selected-template", "butter-gingham");
    await button(guest, "CONTINUE TO PHOTOGRAPHS", false).click();
    await takePhotographs(guest);
    await waitForArrange(guest);
    await button(guest, "CONTINUE TO FILTERS", false).click();
    await expect(surface(guest, "filters")).toBeVisible();
    await startOver();
  });

  const all = await endedCycles(host, name, 3);
  expect(all.map((cycle) => cycle.end_reason)).toEqual(["started_over", "started_over", "started_over"]);
  expect(all.map((cycle) => kept(host, cycle.id).length)).toEqual([0, 1, 3]);
  for (const cycle of all) {
    expect(strip(host, cycle.id)).toBeUndefined();
  }
});

test("changing the Template after Captures keeps them, and an empty window blocks continuing", async ({ browser, host }) => {
  test.setTimeout(180_000);
  const name = "Template change";
  await withRunningEvent(browser, host, { name }, async ({ guest }) => {
    const changeTemplate = async (template: TemplateName, id: string): Promise<void> => {
      await guest.getByRole("list", { name: "Steps" }).getByRole("button", { name: /Template/ }).click();
      await expect(surface(guest, "template")).toBeVisible();
      await guest.getByRole("radio", { name: new RegExp(`^${template}\\b`) }).click();
      await expect(guest.locator("[data-selected-template]")).toHaveAttribute("data-selected-template", id);
      await button(guest, "CONTINUE TO PHOTOGRAPHS", false).click();
      await expect(surface(guest, "photographs")).toBeVisible();
    };

    await stepInside(guest);
    await button(guest, "CONTINUE TO PHOTOGRAPHS", false).click();
    await takePhotographs(guest);
    await expect(guest.getByRole("progressbar", { name: "Photographs" })).toHaveAttribute("aria-valuenow", "2", { timeout: 30_000 });
    await button(guest, "Stop taking photographs").click();
    await expect(button(guest, "Take photographs")).toBeVisible();

    await changeTemplate("Butter Gingham", "butter-gingham");
    await button(guest, "SEE MY PHOTOGRAPHS", false).click();
    await expect(guest.locator('[data-photographs-tab="arrange"]')).toBeVisible();
    expect(await arrangement(guest)).toEqual([1, 2, null]);
    await expect(button(guest, /^CONTINUE TO FILTERS/, false)).toHaveAttribute("aria-disabled", "true");

    await changeTemplate("Signature Strip", "signature-strip");
    await button(guest, "SEE MY PHOTOGRAPHS", false).click();
    expect(await arrangement(guest)).toEqual([1, 2, null, null]);
    await expect(button(guest, /^CONTINUE TO FILTERS/, false)).toHaveAttribute("aria-disabled", "true");

    await button(guest, "TAKE MORE PHOTOGRAPHS", false).click();
    await takePhotographs(guest);
    await waitForArrange(guest);
    expect(await arrangement(guest)).toEqual([1, 2, 3, 4]);
    await expect(button(guest, /^CONTINUE TO FILTERS/, false)).not.toHaveAttribute("aria-disabled", "true");
  });

  const all = await endedCycles(host, name, 1);
  const [cycle] = all;
  expect(cycle?.end_reason).toBe("operator_exit");
  if (cycle !== undefined) {
    expect(kept(host, cycle.id).map((capture) => capture.sequence)).toEqual([1, 2, 3, 4]);
  }
});

test("an Event with one Template and one Look skips both steps and FINISH MY STRIP composes with the only Look", async ({ browser, host }) => {
  test.setTimeout(180_000);
  const name = "Skipped steps";
  await withRunningEvent(browser, host, { name, templates: ["Panorama Four"], looks: ["Sepia"] }, async ({ guest }) => {
    await stepInside(guest);
    await expect(surface(guest, "photographs")).toBeVisible();
    await expect(guest.getByRole("list", { name: "Steps" }).getByRole("listitem")).toHaveText([/01\s*Photographs/, /02\s*Keepsake/]);
    await takePhotographs(guest);
    await waitForArrange(guest);
    await button(guest, "FINISH MY STRIP", false).click();
    await expect(surface(guest, "keepsake")).toBeVisible({ timeout: 30_000 });
  });

  const [cycle] = await endedCycles(host, name, 1);
  expect(cycle?.end_reason).toBe("completed");
  if (cycle !== undefined) {
    const row = expectStrip(host, name, cycle, { width: 1800, height: 600 });
    expect(row.template_id).toBe("panorama-four");
    expect(row.look_id).toBe("sepia-v1");
  }
});
