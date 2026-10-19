import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import { expect, type CDPSession, type Locator, type Page } from "@playwright/test";
import { PNG } from "pngjs";
import { databaseFileName, fingerprint, listFileNames, queryAll, queryOne } from "./data";
import type { DiagnosticsResult, HookClient } from "./hooks";
import type { AppSession } from "./launch";
import {
  eventFolder,
  eventIdByName,
  openNewEventAndFill,
  useVirtualScreens,
  waitForCommandResult,
  type OperatorContext,
} from "./operator";
import { EventListScreen, GuestStartScreen } from "./screens/screens";

/** The Template names as Event setup and the Guest Screen show them, in the catalog's order. */
export const templateNames = ["Signature Strip", "Panorama Four", "Butter Gingham", "Polka Keepsake"] as const;
export type TemplateName = (typeof templateNames)[number];

/** The Look names, in the catalog's display order. */
export const lookNames = ["Monochrome", "Naturale", "Soft Monochrome", "Soft Color", "Sepia", "Warm Monochrome", "Classic"] as const;
export type LookName = (typeof lookNames)[number];

/** The Template's native size, which the Photo Strip PNG must have. */
export const templateSizes: Readonly<Record<TemplateName, { width: number; height: number; windows: number }>> = {
  "Signature Strip": { width: 600, height: 1800, windows: 4 },
  "Panorama Four": { width: 1800, height: 600, windows: 4 },
  "Butter Gingham": { width: 600, height: 1800, windows: 3 },
  "Polka Keepsake": { width: 600, height: 1800, windows: 4 },
};

/** The Phase Timer inputs of Event setup, by their labels. */
export type TimerLabel = "Template" | "Photographs: capture" | "Photographs: review" | "Filters" | "Keepsake" | "Guest gallery view";

export interface GuestEventOptions {
  readonly name: string;
  /** The offered Templates; the first is the default. All four when omitted. */
  readonly templates?: readonly TemplateName[];
  /** The offered Looks; the first is the default. All seven when omitted. */
  readonly looks?: readonly LookName[];
  readonly countdown?: 3 | 5 | 10;
  /** Phase Timer seconds by label; the others keep their defaults. */
  readonly timers?: Partial<Record<TimerLabel, number>>;
  /** The Operator PIN (default `739104`). */
  readonly pin?: string;
  /** Turns the guest gallery on for the Event (default off, as Event setup has it). */
  readonly guestGallery?: boolean;
}

export const defaultPin = "739104";

export type GuestSurface = "start" | "startUnavailable" | "template" | "photographs" | "filters" | "keepsake" | "operatorAssistance";

type CycleDiagnostics = NonNullable<DiagnosticsResult["guestCycle"]>;

/** The wait for a capture sequence to fill `windows` more windows: the countdown, the flash, and the 800 ms pause each. */
export function captureBudgetMs(countdownSeconds: number, windows: number): number {
  return windows * (countdownSeconds * 1_000 + 2_500) + 20_000;
}

const touchSessions = new WeakMap<Page, Promise<CDPSession>>();

function touchSession(page: Page): Promise<CDPSession> {
  let session = touchSessions.get(page);
  if (session === undefined) {
    session = page.context().newCDPSession(page);
    touchSessions.set(page, session);
  }
  return session;
}

function locatorPage(locator: Locator): Page {
  return locator.page();
}

async function centreOf(locator: Locator): Promise<{ x: number; y: number }> {
  const box = await locator.boundingBox();
  if (box === null) {
    throw new Error("The control has no box on the page.");
  }
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

/** Waits until the control is on screen and not `aria-disabled`, so a tap on it isn't swallowed. */
async function expectAvailable(locator: Locator): Promise<void> {
  await expect(locator).toBeVisible();
  await expect(locator).not.toHaveAttribute("aria-disabled", "true");
}

/** One finger taps the centre of the control (CDP touch input: the Guest Screen is a touch screen). */
export async function tap(locator: Locator): Promise<void> {
  await expectAvailable(locator);
  const { x, y } = await centreOf(locator);
  const session = await touchSession(locator.page());
  await session.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: [{ x, y, id: 0 }] });
  await session.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
}

/** One finger presses on `from`, travels to `to` in steps, and lifts there. */
export async function touchDrag(from: Locator, to: Locator, steps = 12): Promise<void> {
  const start = await centreOf(from);
  const end = await centreOf(to);
  const session = await touchSession(from.page());
  await session.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: [{ ...start, id: 0 }] });
  try {
    for (let step = 1; step <= steps; step += 1) {
      const progress = step / steps;
      await session.send("Input.dispatchTouchEvent", {
        type: "touchMove",
        touchPoints: [{ x: start.x + (end.x - start.x) * progress, y: start.y + (end.y - start.y) * progress, id: 0 }],
      });
    }
    // A finger that stops before it lifts leaves no fling; a fling would swallow the next tap.
    await locatorPage(from).waitForTimeout(150);
  } finally {
    await session.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
  }
}

/** Tab (up to `max` presses) until the control holds focus, as a keyboard user would. */
export async function tabTo(page: Page, locator: Locator, max = 80): Promise<void> {
  await expect(locator).toBeVisible();
  for (let press = 0; press <= max; press += 1) {
    if (await locator.evaluate((element) => element === document.activeElement)) {
      return;
    }
    await page.keyboard.press("Tab");
  }
  throw new Error(`Tab didn't reach the control within ${String(max)} presses.`);
}

/** Tabs to the control and presses Enter. */
export async function keyboardActivate(page: Page, locator: Locator): Promise<void> {
  await tabTo(page, locator);
  await expect(locator).not.toHaveAttribute("aria-disabled", "true");
  await page.keyboard.press("Enter");
}

/** Chooses a row of a radio group with the keyboard: Tab to the group's tab stop, then the arrow keys until the target is chosen. */
export async function keyboardChoose(page: Page, group: Locator, target: Locator): Promise<void> {
  if (await target.isChecked()) {
    return;
  }
  const radios = group.getByRole("radio");
  await tabTo(page, group.getByRole("radio", { checked: true }));
  const checkedStates = await radios.evaluateAll((inputs) => inputs.map((input) => (input as HTMLInputElement).checked));
  const from = checkedStates.indexOf(true);
  const targetHandle = await target.elementHandle();
  if (targetHandle === null) {
    throw new Error("The target radio isn't in the page.");
  }
  const to = await radios.evaluateAll((inputs, element) => inputs.indexOf(element as HTMLInputElement), targetHandle);
  if (from < 0 || to < 0) {
    throw new Error("The radio group's checked row or the target row wasn't found.");
  }
  const direction = to > from ? 1 : -1;
  for (let at = from + direction; direction > 0 ? at <= to : at >= to; at += direction) {
    await page.keyboard.press(direction > 0 ? "ArrowDown" : "ArrowUp");
    await expect(radios.nth(at)).toBeChecked();
  }
}

function exact(name: string): RegExp {
  return new RegExp(`^${name.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\b`);
}

/** The Guest Screen's controls, found by role and name. */
export class GuestControls {
  readonly stepInside: Locator;
  readonly templateContinue: Locator;
  readonly takePhotographs: Locator;
  readonly stopCapture: Locator;
  readonly seePhotographs: Locator;
  readonly takeMore: Locator;
  readonly continueToFilters: Locator;
  readonly finishStrip: Locator;
  readonly retake: Locator;
  readonly move: Locator;
  readonly cancelMove: Locator;
  readonly accept: Locator;
  readonly tryAgain: Locator;
  readonly keepOriginal: Locator;
  readonly moreTime: Locator;
  readonly startOver: Locator;
  readonly retry: Locator;
  readonly confirmDialog: Locator;
  readonly warning: Locator;
  readonly stepList: Locator;
  readonly exitHold: Locator;

  constructor(readonly page: Page) {
    this.stepInside = page.getByRole("button", { name: "STEP INSIDE" });
    this.templateContinue = page.getByRole("button", { name: "CONTINUE TO PHOTOGRAPHS" });
    this.takePhotographs = page.getByRole("button", { name: "Take photographs", exact: true });
    this.stopCapture = page.getByRole("button", { name: "Stop taking photographs", exact: true });
    this.seePhotographs = page.getByRole("button", { name: "SEE MY PHOTOGRAPHS" });
    this.takeMore = page.getByRole("button", { name: "TAKE MORE PHOTOGRAPHS" });
    this.continueToFilters = page.getByRole("button", { name: /^(CONTINUE TO FILTERS|FINISH MY STRIP)/ });
    this.finishStrip = page.getByRole("button", { name: "FINISH MY STRIP" });
    this.retake = page.getByRole("button", { name: "RETAKE", exact: true });
    this.move = page.getByRole("button", { name: "MOVE", exact: true });
    this.cancelMove = page.getByRole("button", { name: "CANCEL MOVE", exact: true });
    this.accept = page.getByRole("button", { name: "ACCEPT", exact: true });
    this.tryAgain = page.getByRole("button", { name: "TRY AGAIN", exact: true });
    this.keepOriginal = page.getByRole("button", { name: "KEEP ORIGINAL", exact: true });
    this.moreTime = page.getByRole("button", { name: "MORE TIME", exact: true });
    this.startOver = page.getByRole("button", { name: "START OVER", exact: true });
    this.retry = page.getByRole("button", { name: "RETRY", exact: true });
    this.confirmDialog = page.getByRole("alertdialog");
    this.warning = page.locator("[data-phase][data-tone='light']").filter({ has: page.getByRole("button", { name: "MORE TIME" }) });
    this.stepList = page.getByRole("list", { name: "Steps" });
    this.exitHold = page.getByRole("button", { name: "Operator exit", exact: true });
  }

  surface(name: GuestSurface): Locator {
    return this.page.locator(`main[data-fotohavn-surface="${name}"]`);
  }

  /** A Template or Look row of the Guest Screen's option list. */
  option(name: string): Locator {
    return this.page.getByRole("radio", { name: exact(name) });
  }

  get optionGroup(): Locator {
    return this.page.locator("fieldset").filter({ has: this.page.getByRole("radio") });
  }

  /** A step of the step progress, a button once it is completed. */
  completedStep(label: "Template" | "Photographs" | "Filters"): Locator {
    return this.stepList.getByRole("button", { name: new RegExp(label) });
  }

  /** The strip's photograph window for a 1-based position, while no Move waits. */
  windowButton(position: number): Locator {
    return this.page.getByRole("button", { name: new RegExp(`^Photograph ${String(position)} of \\d+$`) });
  }

  confirmStartOver(): Locator {
    return this.confirmDialog.getByRole("button", { name: "START OVER", exact: true });
  }

  keepGoing(): Locator {
    return this.confirmDialog.getByRole("button", { name: "KEEP GOING", exact: true });
  }

  /** The capture numbers (`capture-N.jpg`) in the strip's windows, in order; null for an empty one. */
  async arrangement(): Promise<(number | null)[]> {
    return this.page.getByRole("group", { name: "Your Photo Strip" }).evaluate((group) =>
      Array.from(group.children).map((cell) => {
        const source = cell.querySelector("img")?.getAttribute("src") ?? "";
        const match = /capture-(\d+)\.jpg/.exec(source);
        return match === null ? null : Number(match[1]);
      }),
    );
  }
}

export interface StoredCycle {
  readonly id: string;
  readonly event_id: string;
  readonly started_at: string;
  readonly ended_at: string | null;
  readonly end_reason: string | null;
}

export interface StoredCapture {
  readonly id: string;
  readonly guest_cycle_id: string;
  readonly sequence: number;
  readonly file_name: string;
  readonly byte_length: number;
  readonly sha256: string;
  readonly width: number;
  readonly height: number;
  readonly mirrored: number;
  readonly state: "kept" | "discarded";
}

export interface StoredStrip {
  readonly guest_cycle_id: string;
  readonly template_id: string;
  readonly look_id: string;
  readonly arrangement: string;
  readonly file_name: string;
  readonly byte_length: number;
  readonly sha256: string;
  readonly width: number;
  readonly height: number;
}

/** An Event running with the Guest Screen on the virtual guest display: the handles a guest scenario needs. */
export class GuestRun {
  readonly list: EventListScreen;

  constructor(
    readonly app: AppSession,
    readonly operatorPage: Page,
    readonly eventId: string,
    readonly name: string,
    readonly options: GuestEventOptions,
  ) {
    this.list = new EventListScreen(operatorPage);
  }

  get hooks(): HookClient {
    return this.app.hooks;
  }

  get dataDir(): string {
    return this.app.dataDir;
  }

  /** The Guest page as it is now (a new one after the Guest Screen was closed and reopened). */
  guest(): Promise<Page> {
    return this.app.pageFor("guest", { timeoutMs: 20_000 });
  }

  async controls(): Promise<GuestControls> {
    return new GuestControls(await this.guest());
  }

  get countdownSeconds(): number {
    return this.options.countdown ?? 5;
  }

  /** The brain's view of the running cycle (null while none runs). */
  async cycle(): Promise<CycleDiagnostics | null> {
    return (await this.hooks.getDiagnostics()).guestCycle;
  }

  /** The Guest Screen's surface in the brain's last guest presentation. */
  async surface(): Promise<string | undefined> {
    return (await this.hooks.getDiagnostics()).presentations.find((presentation) => presentation.role === "guest")?.surface;
  }

  async waitForSurface(surface: GuestSurface, timeoutMs = 15_000): Promise<void> {
    await expect
      .poll(() => this.surface(), { timeout: timeoutMs, message: `the Guest Screen shows ${surface}` })
      .toBe(surface);
  }

  async waitForStep(step: CycleDiagnostics["step"], tab?: "camera" | "arrange", timeoutMs = 15_000): Promise<void> {
    await expect
      .poll(async () => {
        const cycle = await this.cycle();
        return cycle === null ? "none" : tab === undefined ? cycle.step : `${cycle.step}/${cycle.tab ?? ""}`;
      }, { timeout: timeoutMs, message: `the cycle is on ${step}${tab === undefined ? "" : `/${tab}`}` })
      .toBe(tab === undefined ? step : `${step}/${tab}`);
  }

  async waitForFilled(windows: number, timeoutMs: number): Promise<void> {
    await expect
      .poll(async () => (await this.cycle())?.windowsFilled ?? -1, { timeout: timeoutMs, message: `${String(windows)} windows are filled` })
      .toBeGreaterThanOrEqual(windows);
  }

  async waitForSequenceIdle(timeoutMs = 10_000): Promise<void> {
    await expect
      .poll(async () => (await this.cycle())?.sequenceRunning, { timeout: timeoutMs, message: "no capture sequence is running" })
      .toBe(false);
  }

  /** The Start surface is up and `STEP INSIDE` is available. */
  async expectStartReady(): Promise<GuestControls> {
    const controls = await this.controls();
    await expect(controls.surface("start")).toBeVisible({ timeout: 20_000 });
    await expect(controls.stepInside).not.toHaveAttribute("aria-disabled", "true", { timeout: 20_000 });
    return controls;
  }

  /** `STEP INSIDE` by touch; the Template step (or Photographs, with one Template) appears. */
  async stepInside(): Promise<GuestControls> {
    const controls = await this.expectStartReady();
    await tap(controls.stepInside);
    await expect.poll(() => this.cycle().then((cycle) => cycle !== null), { timeout: 15_000 }).toBe(true);
    return controls;
  }

  /** Taps `TAKE PHOTOGRAPHS` and waits until `windows` windows are filled in all. */
  async takePhotographsUntil(controls: GuestControls, windows: number): Promise<void> {
    const before = (await this.cycle())?.windowsFilled ?? 0;
    await tap(controls.takePhotographs);
    await this.waitForFilled(windows, captureBudgetMs(this.countdownSeconds, Math.max(1, windows - before)));
  }

  /** Taps `STOP` and waits for the sequence to end. */
  async stopCapture(controls: GuestControls): Promise<void> {
    await tap(controls.stopCapture);
    await this.waitForSequenceIdle();
  }

  async cycles(): Promise<StoredCycle[]> {
    return queryAll<StoredCycle>(
      path.join(this.dataDir, databaseFileName),
      "SELECT id, event_id, started_at, ended_at, end_reason FROM guest_cycles WHERE event_id = ? ORDER BY started_at, id",
      this.eventId,
    );
  }

  /** The Event's only Guest Cycle, polled until it exists. */
  async onlyCycle(): Promise<StoredCycle> {
    await expect.poll(async () => (await this.cycles()).length, { message: "the Event has a Guest Cycle" }).toBe(1);
    const [cycle] = await this.cycles();
    if (cycle === undefined) {
      throw new Error("No Guest Cycle.");
    }
    return cycle;
  }

  /** The Guest Cycle row once it has ended, with its end reason. */
  async endedCycle(endReason: string, timeoutMs = 15_000): Promise<StoredCycle> {
    await expect
      .poll(async () => (await this.cycles()).map((cycle) => cycle.end_reason).join(","), { timeout: timeoutMs, message: `the Guest Cycle ended as ${endReason}` })
      .toBe(endReason);
    return await this.onlyCycle();
  }

  cycleFolder(cycleId: string): string {
    return path.join(eventFolder(this.dataDir, this.eventId), "GuestCycles", cycleId);
  }

  captures(cycleId: string): StoredCapture[] {
    return queryAll<StoredCapture>(
      path.join(this.dataDir, databaseFileName),
      "SELECT id, guest_cycle_id, sequence, file_name, byte_length, sha256, width, height, mirrored, state FROM captures WHERE guest_cycle_id = ? ORDER BY sequence",
      cycleId,
    );
  }

  strip(cycleId: string): StoredStrip | undefined {
    return queryOne<StoredStrip>(
      path.join(this.dataDir, databaseFileName),
      "SELECT guest_cycle_id, template_id, look_id, arrangement, file_name, byte_length, sha256, width, height FROM photo_strips WHERE guest_cycle_id = ?",
      cycleId,
    );
  }

  /** Every kept Capture has its file with the row's size and hash; every discarded one has no file. */
  expectCaptureFiles(cycleId: string): void {
    for (const capture of this.captures(cycleId)) {
      const found = fingerprint(path.join(this.cycleFolder(cycleId), capture.file_name));
      if (capture.state === "kept") {
        expect(found, `kept ${capture.file_name}`).toEqual({ bytes: capture.byte_length, sha256: capture.sha256 });
      } else {
        expect(found, `discarded ${capture.file_name} has no file`).toBeUndefined();
      }
    }
  }

  /** The committed Photo Strip: its row, its file's hash, and its PNG at the Template's native size. Returns the row. */
  expectPhotoStrip(cycleId: string, template: TemplateName): StoredStrip {
    const row = this.strip(cycleId);
    expect(row, "the Photo Strip row").toBeDefined();
    if (row === undefined) {
      throw new Error("No Photo Strip row.");
    }
    const file = path.join(this.cycleFolder(cycleId), row.file_name);
    expect(fingerprint(file), "the Photo Strip file").toEqual({ bytes: row.byte_length, sha256: row.sha256 });
    const { width, height } = templateSizes[template];
    const png = PNG.sync.read(readFileSync(file));
    expect({ width: png.width, height: png.height }, "the PNG's decoded size").toEqual({ width, height });
    expect({ width: row.width, height: row.height }, "the row's size").toEqual({ width, height });
    return row;
  }

  /** The cycle folder holds exactly the files its Capture rows (and Photo Strip row) name, and no staging leftovers. */
  expectNoStrayFiles(cycleId: string): void {
    const expected = this.captures(cycleId)
      .filter((capture) => capture.state === "kept")
      .map((capture) => capture.file_name);
    const strip = this.strip(cycleId);
    if (strip !== undefined) {
      expected.push(strip.file_name);
    }
    expect(listFileNames(this.cycleFolder(cycleId)), "files in the cycle folder").toEqual(expected.sort());
  }

  /** `END EVENT` on the Operator Screen, confirmed. */
  async endEventFromOperator(): Promise<void> {
    await this.list.endEvent.click();
    const dialog = this.operatorPage.getByRole("alertdialog");
    await expect(dialog).toBeVisible();
    await dialog.getByRole("button", { name: "END EVENT", exact: true }).click();
    await expect.poll(async () => (await this.hooks.getDiagnostics()).guestScreenOpen, { timeout: 15_000 }).toBe(false);
    await expect(this.list.runningPanel()).toHaveCount(0);
  }

  /** Whether a photo-strip.png exists anywhere in the cycle's folder. */
  hasStripFile(cycleId: string): boolean {
    return existsSync(path.join(this.cycleFolder(cycleId), "photo-strip.png"));
  }
}

/**
 * Creates an Event through Event setup with the options (chosen camera, the virtual guest display, a PIN, offered Templates and
 * Looks, countdown, Phase Timers), starts it with `SAVE AND START`, and waits for the Guest Screen's Start.
 */
export async function startGuestEvent(context: OperatorContext, options: GuestEventOptions): Promise<GuestRun> {
  const { app, page } = context;
  await useVirtualScreens(app, 2);
  const pin = options.pin ?? defaultPin;
  const setup = await openNewEventAndFill(context, { name: options.name, display: "Virtual guest", pin });
  await waitForCommandResult(app.hooks, "SetPin");
  if (options.countdown !== undefined) {
    await setup.goTo("Hardware");
    await setup.countdown(options.countdown).click();
    await expect(setup.countdown(options.countdown)).toBeChecked();
  }
  if (options.templates !== undefined) {
    const [first] = options.templates;
    if (first === undefined) {
      throw new Error("Offer at least one Template.");
    }
    await setup.goTo("Templates");
    await setup.defaultTemplate(first).click();
    for (const name of templateNames) {
      if (!options.templates.includes(name)) {
        await setup.template(name).click();
        await expect(setup.template(name)).not.toBeChecked();
      }
    }
  }
  if (options.looks !== undefined) {
    const [first] = options.looks;
    if (first === undefined) {
      throw new Error("Offer at least one Look.");
    }
    await setup.goTo("Templates");
    await setup.defaultLook(first).click();
    for (const name of lookNames) {
      if (!options.looks.includes(name)) {
        await setup.look(name).click();
        await expect(setup.look(name)).not.toBeChecked();
      }
    }
  }
  if (options.guestGallery === true) {
    await setup.goTo("Guests");
    await setup.guestGallery.click();
    await expect(setup.guestGallery).toBeChecked();
  }
  if (options.timers !== undefined) {
    await setup.goTo("Timing");
    for (const [label, seconds] of Object.entries(options.timers)) {
      await setup.timer(label).fill(String(seconds));
    }
  }
  await setup.goTo("Guests");
  await setup.saveAndStart.click();
  const list = new EventListScreen(page);
  await expect(list.runningPanel(options.name)).toBeVisible({ timeout: 30_000 });
  const run = new GuestRun(app, page, eventIdByName(app.dataDir, options.name), options.name, options);
  const start = new GuestStartScreen(await run.guest());
  await expect(start.stepInside).toBeVisible();
  return run;
}

/** Whether any element scrolls: the document, or a region with scrollable overflow. Returns a description of each that does. */
export async function scrollingElements(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const found: string[] = [];
    const root = document.documentElement;
    if (root.scrollHeight > root.clientHeight || root.scrollWidth > root.clientWidth) {
      found.push(`document ${String(root.scrollWidth)}x${String(root.scrollHeight)} in ${String(root.clientWidth)}x${String(root.clientHeight)}`);
    }
    for (const element of Array.from(document.querySelectorAll<HTMLElement>("body, main, main *"))) {
      const style = getComputedStyle(element);
      const scrollsY = (style.overflowY === "auto" || style.overflowY === "scroll") && element.scrollHeight > element.clientHeight + 1;
      const scrollsX = (style.overflowX === "auto" || style.overflowX === "scroll") && element.scrollWidth > element.clientWidth + 1;
      if (scrollsY || scrollsX || (element === document.body && element.scrollHeight > element.clientHeight + 1)) {
        found.push(`${element.tagName.toLowerCase()}${element.className === "" ? "" : `.${String(element.className).split(" ")[0] ?? ""}`}`);
      }
    }
    return found;
  });
}
