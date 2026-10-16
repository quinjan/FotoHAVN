import { resolve } from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, test, type CDPSession, type Page } from "@playwright/test";
import { resolutionName, resolutions, screenshotDir } from "./resolutions.ts";

// M9: the Event Gallery's operator views (list, detail, delete confirmation) and the guest gallery and strip detail, through the
// fixture lane (`?fixture=<name>`). The hand-written `operator-ui-gallery-*` and `guest-ui-gallery-*` files stand in for the brain's.
// Guest views at the four Guest Screen resolutions: no page scroll (only the strip row scrolls, horizontally), nothing clipped,
// 64 targets, at most one primary action, axe. Operator views: 48 targets, no horizontal scroll, axe. The lane's stand-in brain
// answers every command `rejected notAvailable`.

interface View {
  readonly fixture: string;
  readonly surface: string;
  readonly heading: string;
  readonly texts?: readonly string[];
}

const operatorViews: readonly View[] = [
  { fixture: "operator-ui-gallery-mixed", surface: "eventGallery", heading: "Event Gallery.", texts: ["Completed", "Started over", "Ended by operator", "App closed", "Unrecoverable", "No Photo Strip", "These photographs can't be found.", "SHOW MORE"] },
  { fixture: "operator-ui-gallery-end", surface: "eventGallery", heading: "Event Gallery." },
  { fixture: "operator-ui-gallery-loading-more", surface: "eventGallery", heading: "Event Gallery.", texts: ["Loading more photographs."] },
  { fixture: "operator-ui-gallery-load-failed", surface: "eventGallery", heading: "Event Gallery.", texts: ["Couldn't load these photographs.", "TRY AGAIN"] },
  { fixture: "operator-ui-gallery-large", surface: "eventGallery", heading: "Event Gallery." },
  { fixture: "operator-ui-gallery-large-more", surface: "eventGallery", heading: "Event Gallery.", texts: ["SHOW MORE"] },
  { fixture: "operator-ui-gallery-running", surface: "eventGallery", heading: "Event Gallery.", texts: ["RUNNING", "New photographs appear here as guests finish."] },
  { fixture: "operator-ui-gallery-running-assistance", surface: "eventGallery", heading: "Event Gallery.", texts: ["The camera needs attention.", "GO TO THE RUNNING EVENT"] },
  { fixture: "operator-ui-gallery-delete-list", surface: "eventGallery", heading: "Event Gallery.", texts: ["Delete this Guest Cycle?", "and its 5 files will be removed from this computer. This can't be undone.", "DELETE GUEST CYCLE", "KEEP GUEST CYCLE"] },
  { fixture: "operator-ui-gallery-detail", surface: "guestCycleDetail", heading: "Guest Cycle.", texts: ["Time", "Outcome", "Template", "Look", "Captures", "OPEN FOLDER", "DELETE"] },
  { fixture: "operator-ui-gallery-detail-landscape", surface: "guestCycleDetail", heading: "Guest Cycle.", texts: ["Panorama Four"] },
  { fixture: "operator-ui-gallery-detail-no-strip", surface: "guestCycleDetail", heading: "Guest Cycle.", texts: ["No Photo Strip", "Started over"] },
  { fixture: "operator-ui-gallery-detail-unavailable", surface: "guestCycleDetail", heading: "Guest Cycle.", texts: ["These photographs can't be found."] },
  { fixture: "operator-ui-gallery-detail-delete", surface: "guestCycleDetail", heading: "Guest Cycle.", texts: ["Delete this Guest Cycle?", "KEEP GUEST CYCLE"] },
  { fixture: "operator-ui-gallery-detail-delete-one-file", surface: "guestCycleDetail", heading: "Guest Cycle.", texts: ["and its file will be removed"] },
  { fixture: "operator-ui-gallery-detail-delete-failed", surface: "guestCycleDetail", heading: "Guest Cycle.", texts: ["The Guest Cycle couldn't be deleted. Try again in a moment."] },
  // The brain-generated fixtures (FotoHavn.Core tests write them).
  { fixture: "operator-gallery-empty", surface: "eventGallery", heading: "Event Gallery.", texts: ["No photographs yet."] },
  { fixture: "operator-gallery-large", surface: "eventGallery", heading: "Event Gallery.", texts: ["SHOW MORE"] },
  { fixture: "operator-gallery-load-failed", surface: "eventGallery", heading: "Event Gallery.", texts: ["Couldn't load these photographs.", "TRY AGAIN"] },
  { fixture: "operator-gallery-loading", surface: "eventGallery", heading: "Event Gallery.", texts: ["Loading photographs."] },
  { fixture: "operator-gallery-mixed-outcomes", surface: "eventGallery", heading: "Event Gallery.", texts: ["Completed", "Started over", "Ended by operator", "App closed", "Unrecoverable", "No Photo Strip", "These photographs can't be found."] },
  { fixture: "operator-gallery-partial", surface: "eventGallery", heading: "Event Gallery.", texts: ["Completed", "Started over"] },
  { fixture: "operator-gallery-running-live", surface: "eventGallery", heading: "Event Gallery.", texts: ["RUNNING", "New photographs appear here as guests finish."] },
  { fixture: "operator-guest-cycle-detail-completed", surface: "guestCycleDetail", heading: "Guest Cycle.", texts: ["Time", "Outcome", "Template", "Look", "Captures", "OPEN FOLDER", "DELETE"] },
  { fixture: "operator-guest-cycle-detail-delete-confirmation", surface: "guestCycleDetail", heading: "Guest Cycle.", texts: ["Delete this Guest Cycle?", "DELETE GUEST CYCLE", "KEEP GUEST CYCLE"] },
  { fixture: "operator-guest-cycle-detail-no-strip", surface: "guestCycleDetail", heading: "Guest Cycle.", texts: ["No Photo Strip", "Started over"] },
];

const guestViews: readonly View[] = [
  { fixture: "guest-ui-gallery-four-strips", surface: "guestGallery", heading: "Look at you." },
  { fixture: "guest-ui-gallery-many-strips", surface: "guestGallery", heading: "Look at you." },
  { fixture: "guest-ui-gallery-loading-more", surface: "guestGallery", heading: "Look at you." },
  { fixture: "guest-ui-gallery-loading-first", surface: "guestGallery", heading: "Look at you.", texts: ["Loading photographs."] },
  { fixture: "guest-ui-gallery-timer-warning", surface: "guestGallery", heading: "Look at you.", texts: ["SECONDS LEFT", "MORE TIME"] },
  { fixture: "guest-ui-strip-detail", surface: "guestStripDetail", heading: "", texts: ["CLOSE", "BACK TO START"] },
  { fixture: "guest-ui-strip-detail-landscape", surface: "guestStripDetail", heading: "" },
  { fixture: "guest-ui-strip-detail-timer-warning", surface: "guestStripDetail", heading: "", texts: ["SECOND LEFT", "MORE TIME"] },
  // The brain-generated fixtures (FotoHavn.Core tests write them).
  { fixture: "guest-gallery-one", surface: "guestGallery", heading: "Look at you.", texts: ["BACK TO START"] },
  { fixture: "guest-gallery-four", surface: "guestGallery", heading: "Look at you." },
  { fixture: "guest-gallery-many", surface: "guestGallery", heading: "Look at you." },
  { fixture: "guest-gallery-timer-warning", surface: "guestGallery", heading: "Look at you.", texts: ["MORE TIME"] },
  { fixture: "guest-strip-detail", surface: "guestStripDetail", heading: "", texts: ["CLOSE", "BACK TO START"] },
  { fixture: "guest-strip-detail-timer-warning", surface: "guestStripDetail", heading: "", texts: ["MORE TIME"] },
  { fixture: "guest-start-gallery-link", surface: "start", heading: "", texts: ["SEE THE GOOD COMPANY"] },
];

/** A one-finger drag from one point to another, in small steps. */
async function swipe(session: CDPSession, from: { x: number; y: number }, to: { x: number; y: number }): Promise<void> {
  const steps = 20;
  const finger = (x: number, y: number) => [{ x, y, id: 1 }];
  await session.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: finger(from.x, from.y) });
  for (let step = 1; step <= steps; step += 1) {
    await session.send("Input.dispatchTouchEvent", {
      type: "touchMove",
      touchPoints: finger(from.x + ((to.x - from.x) * step) / steps, from.y + ((to.y - from.y) * step) / steps),
    });
    await new Promise((done) => setTimeout(done, 16));
  }
  await session.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
}
async function settle(page: Page): Promise<void> {
  await page.evaluate(() => Promise.all(document.getAnimations().map((animation) => animation.finished)));
  await page.evaluate(() => document.fonts.ready);
}

async function open(page: Page, fixture: string, role: "guest" | "operator"): Promise<void> {
  await page.goto(`/index.html?fixture=${fixture}`);
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", role);
  await settle(page);
}

async function expectNoPageScroll(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => {
    const root = document.documentElement;
    return {
      horizontal: root.scrollWidth > root.clientWidth,
      vertical: root.scrollHeight > root.clientHeight,
      body: document.body.scrollHeight > document.body.clientHeight,
    };
  });
  expect(overflow).toEqual({ horizontal: false, vertical: false, body: false });
}

async function expectNoHorizontalScrolling(page: Page): Promise<void> {
  expect(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
}

/** Nothing the gallery draws spills out of the viewport, except inside the strip row, which clips and scrolls on its own. */
async function expectNoClipping(page: Page): Promise<void> {
  const spills = await page.evaluate(() => {
    const viewport = { width: document.documentElement.clientWidth, height: document.documentElement.clientHeight };
    const offenders: string[] = [];
    for (const element of document.querySelectorAll<HTMLElement>("main *")) {
      const box = element.getBoundingClientRect();
      if (box.width === 0 || box.height === 0 || getComputedStyle(element).position === "absolute") {
        continue;
      }
      if (element.closest("[data-fotohavn-scroll-region]") !== null && element.closest("[data-fotohavn-scroll-region]") !== element) {
        const row = element.closest("[data-fotohavn-scroll-region]")?.getBoundingClientRect();
        if (row !== undefined && (box.top < row.top - 1 || box.bottom > row.bottom + 1)) {
          offenders.push(`in row: ${element.tagName.toLowerCase()}.${element.className}`);
        }
        continue;
      }
      if (box.right > viewport.width + 1 || box.bottom > viewport.height + 1 || box.left < -1 || box.top < -1) {
        offenders.push(`${element.tagName.toLowerCase()}.${element.className}`);
      }
    }
    return offenders;
  });
  expect(spills).toEqual([]);
}

async function expectGuestTargets(page: Page): Promise<void> {
  const small = await page.evaluate(() => {
    const offenders: string[] = [];
    for (const control of document.querySelectorAll<HTMLElement>("main button, main input, main a[href]")) {
      if (control.closest("[inert]") !== null) {
        continue;
      }
      const box = control.getBoundingClientRect();
      if (box.width === 0 && box.height === 0) {
        continue;
      }
      if (box.width < 63.5 || box.height < 63.5) {
        offenders.push(`${control.tagName.toLowerCase()} ${(control.getAttribute("aria-label") ?? control.textContent).trim().slice(0, 24)} ${String(Math.round(box.width))}x${String(Math.round(box.height))}`);
      }
    }
    return offenders;
  });
  expect(small).toEqual([]);
}

/** Every visible operator control meets the Operator Screen's 48 × 48 minimum; the pointer-only print targets are inside it by size. */
async function expectOperatorTargets(page: Page, fixture: string): Promise<void> {
  const small = await page.evaluate(() => {
    const controls = document.querySelectorAll<HTMLElement>("main button, main a[href], main [role='button']");
    return [...controls]
      .filter((control) => control.closest("[inert]") === null || control.closest("[role='alertdialog']") !== null)
      .map((control) => ({ control, box: control.getBoundingClientRect() }))
      .filter(({ box }) => box.width > 0 && box.height > 0)
      .filter(({ box }) => box.width + 0.5 < 48 || box.height + 0.5 < 48)
      .map(({ control, box }) => `${control.textContent.trim()} ${String(Math.round(box.width))}x${String(Math.round(box.height))}`);
  });
  expect(small, `${fixture} controls smaller than 48 x 48`).toEqual([]);
}

async function expectAtMostOnePrimary(page: Page): Promise<void> {
  const count = await page.evaluate(() => [...document.querySelectorAll('main [data-fotohavn-action="primary"]')].filter((action) => action.closest("[inert]") === null).length);
  expect(count).toBeLessThanOrEqual(1);
}

async function expectAccessible(page: Page, { forcedColors = false } = {}): Promise<void> {
  // Forced colors repaint the page in system colors, which axe's contrast check can't see: that run checks the rest.
  const axe = await new AxeBuilder({ page }).disableRules(["page-has-heading-one", ...(forcedColors ? ["color-contrast"] : [])]).analyze();
  expect(axe.violations).toEqual([]);
}

for (const resolution of resolutions) {
  const name = resolutionName(resolution);

  test.describe(`operator gallery views at ${name}`, () => {
    test.use({ viewport: resolution });

    for (const view of operatorViews) {
      test(view.fixture, async ({ page }) => {
        await open(page, view.fixture, "operator");
        await expect(page.locator(`main[data-fotohavn-surface="${view.surface}"]`)).toBeVisible();
        await expect(page.getByRole("heading", { level: 1, name: view.heading })).toBeVisible();
        for (const text of view.texts ?? []) {
          await expect(page.getByText(text).first()).toBeVisible();
        }
        await expectNoHorizontalScrolling(page);
        await expectOperatorTargets(page, view.fixture);
        await expectAtMostOnePrimary(page);
        await page.screenshot({ path: resolve(screenshotDir, `${view.fixture}-${name}.png`), fullPage: !view.fixture.includes("large") });
        await expectAccessible(page);
      });
    }
  });

  test.describe(`guest gallery views at ${name}`, () => {
    test.use({ viewport: resolution });

    for (const view of guestViews) {
      test(view.fixture, async ({ page }) => {
        await open(page, view.fixture, "guest");
        await expect(page.locator(`main[data-fotohavn-surface="${view.surface}"]`)).toBeVisible();
        if (view.heading !== "") {
          await expect(page.getByRole("heading", { level: 1, name: view.heading })).toBeVisible();
        }
        for (const text of view.texts ?? []) {
          await expect(page.getByText(text).first()).toBeVisible();
        }
        await expect(page.getByRole("button", { name: "Operator exit" })).toBeVisible();
        await expectNoPageScroll(page);
        await expectNoClipping(page);
        await expectGuestTargets(page);
        await expectAtMostOnePrimary(page);
        await page.screenshot({ path: resolve(screenshotDir, `${view.fixture}-${name}.png`) });
        await expectAccessible(page);
      });
    }
  });
}

test.describe("operator gallery, behaviour", () => {
  test.use({ viewport: resolutions[0] });

  test("a 320-Guest-Cycle list scrolls the page, lists every item once, and keeps SHOW MORE out when everything is loaded", async ({ page }) => {
    await open(page, "operator-ui-gallery-large", "operator");
    await expect(page.getByRole("list", { name: "Guest Cycles" }).getByRole("listitem")).toHaveCount(320);
    await expect(page.getByRole("button", { name: "SHOW MORE" })).toHaveCount(0);
    await page.evaluate(() => { window.scrollTo(0, document.documentElement.scrollHeight); });
    const last = page.getByRole("list", { name: "Guest Cycles" }).getByRole("listitem").last();
    await expect(last).toBeInViewport();
    await expectNoHorizontalScrolling(page);
    await page.screenshot({ path: resolve(screenshotDir, "operator-ui-gallery-large-scrolled-1280x720.png") });
  });

  test("Photo Strips keep their real proportion, and landscape ones stay in native orientation", async ({ page }) => {
    await open(page, "operator-ui-gallery-mixed", "operator");
    const ratios = await page.evaluate(() =>
      [...document.querySelectorAll<HTMLElement>("main [data-fit='scale']")].map((print) => {
        const box = print.getBoundingClientRect();
        return { ratio: box.height / box.width, rotated: print.hasAttribute("data-rotated") };
      }),
    );
    expect(ratios.length).toBeGreaterThan(1);
    expect(ratios.some((entry) => entry.ratio > 2.5)).toBe(true);
    expect(ratios.some((entry) => entry.ratio < 0.6)).toBe(true);
    expect(ratios.every((entry) => !entry.rotated)).toBe(true);
  });

  test("keyboard order is BACK TO EVENTS, then each Guest Cycle's OPEN and DELETE, then SHOW MORE", async ({ page }) => {
    await open(page, "operator-ui-gallery-mixed", "operator");
    const order: string[] = [];
    for (let step = 0; step < 18; step += 1) {
      await page.keyboard.press("Tab");
      order.push(await page.evaluate(() => document.activeElement?.textContent.trim() ?? ""));
    }
    expect(order.slice(0, 5)).toEqual(["BACK TO EVENTS", "OPEN", "DELETE", "OPEN", "DELETE"]);
    expect(order).toContain("SHOW MORE");
    expect(order.indexOf("SHOW MORE")).toBe(15);
  });

  test("the delete confirmation names the time, counts the files, and opens on KEEP GUEST CYCLE", async ({ page }) => {
    await open(page, "operator-ui-gallery-detail-delete", "operator");
    const dialog = page.getByRole("alertdialog", { name: "Delete this Guest Cycle?" });
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(/The Guest Cycle from \d{1,2} [A-Z][a-z]{2}, \d{1,2}:\d{2} (AM|PM) and its 5 files will be removed from this computer\. This can't be undone\./);
    await expect(dialog.getByRole("button", { name: "KEEP GUEST CYCLE" })).toBeFocused();
    await expect(dialog.getByRole("button")).toHaveText(["DELETE GUEST CYCLE", "KEEP GUEST CYCLE"]);
    await expect(page.locator("main > div[inert]")).toHaveCount(1);
  });
});

test.describe("guest gallery, behaviour", () => {
  test.use({ viewport: resolutions[0], hasTouch: true });

  test("the strip row scrolls sideways by touch and the page never moves", async ({ page }) => {
    await open(page, "guest-ui-gallery-many-strips", "guest");
    const row = page.locator("[data-fotohavn-scroll-region]");
    const before = await row.evaluate((element) => ({ left: element.scrollLeft, width: element.scrollWidth, client: element.clientWidth }));
    expect(before.width).toBeGreaterThan(before.client * 3);
    expect(before.left).toBe(0);

    const box = await row.boundingBox();
    expect(box).not.toBeNull();
    const session = await page.context().newCDPSession(page);
    const centerX = (box?.x ?? 0) + (box?.width ?? 0) / 2;
    const centerY = (box?.y ?? 0) + (box?.height ?? 0) / 2;
    // A finger dragging left across the row: raw touch events, which the browser turns into a pan.
    await swipe(session, { x: centerX + 300, y: centerY }, { x: centerX - 300, y: centerY });
    await expect.poll(() => row.evaluate((element) => element.scrollLeft)).toBeGreaterThan(200);
    // A finger dragging up or down over the row scrolls nothing: not the page, and not the row.
    await swipe(session, { x: centerX, y: centerY + 150 }, { x: centerX, y: centerY - 150 });
    expect(await row.evaluate((element) => element.scrollTop)).toBe(0);
    expect(await page.evaluate(() => ({ x: window.scrollX, y: window.scrollY }))).toEqual({ x: 0, y: 0 });
    await expectNoPageScroll(page);
  });

  test("only the strip row is a scrolling region: horizontal, panning on x, and the page around it fixed", async ({ page }) => {
    await open(page, "guest-ui-gallery-many-strips", "guest");
    expect(await page.locator("[data-fotohavn-scroll-region]").count()).toBe(1);
    const style = await page.locator("[data-fotohavn-scroll-region]").evaluate((element) => {
      const computed = getComputedStyle(element);
      return { x: computed.overflowX, y: computed.overflowY, touch: computed.touchAction, contain: computed.overscrollBehaviorX };
    });
    expect(style).toEqual({ x: "auto", y: "hidden", touch: "pan-x", contain: "contain" });
    const scrollers = await page.evaluate(() =>
      [...document.querySelectorAll<HTMLElement>("main *")]
        .filter((element) => {
          const style = getComputedStyle(element);
          return /(auto|scroll)/.test(`${style.overflowX} ${style.overflowY}`) && (element.scrollWidth > element.clientWidth || element.scrollHeight > element.clientHeight);
        })
        .map((element) => element.hasAttribute("data-fotohavn-scroll-region")),
    );
    expect(scrollers).toEqual([true]);
  });

  test("landscape strips turn a quarter counterclockwise in the row and the detail, and portrait ones don't", async ({ page }) => {
    await open(page, "guest-ui-gallery-many-strips", "guest");
    const rotated = await page.evaluate(() => [...document.querySelectorAll("main [data-fit='scale']")].map((print) => print.hasAttribute("data-rotated")));
    expect(rotated.filter(Boolean).length).toBe(8);
    expect(rotated.filter((turned) => !turned).length).toBe(32);
    await open(page, "guest-ui-strip-detail-landscape", "guest");
    await expect(page.locator("main [data-rotated]")).toHaveCount(1);
  });

  test("a strip's detail shows its Captures and no metadata, only its alternative text", async ({ page }) => {
    await open(page, "guest-ui-strip-detail", "guest");
    await expect(page.getByRole("img", { name: "Photo Strip from this Event" })).toBeVisible();
    await expect(page.getByRole("img", { name: "Photograph from this Event" })).toHaveCount(4);
    const text = await page.locator("main").innerText();
    for (const metadata of ["Template", "Look", "Completed", "Started over", "DELETE", "OPEN FOLDER", "Captures"]) {
      expect(text).not.toContain(metadata);
    }
  });

  test("the warning sits clear of BACK TO START and the Captures, with MORE TIME at 64", async ({ page }) => {
    for (const fixture of ["guest-ui-gallery-timer-warning", "guest-ui-strip-detail-timer-warning"]) {
      await open(page, fixture, "guest");
      const warning = page.locator('[data-phase="guest-gallery"]');
      await expect(warning).toBeVisible();
      const more = await warning.getByRole("button", { name: "MORE TIME" }).boundingBox();
      expect(more?.height).toBeGreaterThanOrEqual(64);
      const box = await warning.boundingBox();
      const back = await page.getByRole("button", { name: "BACK TO START" }).boundingBox();
      expect(box).not.toBeNull();
      expect(back).not.toBeNull();
      const apart = (box?.x ?? 0) >= (back?.x ?? 0) + (back?.width ?? 0) || (box?.y ?? 0) >= (back?.y ?? 0) + (back?.height ?? 0);
      expect(apart, `${fixture}: the warning overlaps BACK TO START`).toBe(true);
      expect((box?.x ?? 0) + (box?.width ?? 0)).toBeLessThanOrEqual(resolutions[0].width);
      expect((box?.y ?? 0) + (box?.height ?? 0)).toBeLessThanOrEqual(resolutions[0].height);
    }
  });

  test("keyboard order on the row is the exit hold, each strip, then BACK TO START", async ({ page }) => {
    await open(page, "guest-ui-gallery-four-strips", "guest");
    const order: string[] = [];
    for (let step = 0; step < 6; step += 1) {
      await page.keyboard.press("Tab");
      order.push(
        await page.evaluate(() => {
          const active = document.activeElement;
          return active === null ? "" : (active.getAttribute("aria-label") ?? active.querySelector("img")?.getAttribute("alt") ?? active.textContent.trim());
        }),
      );
    }
    expect(order).toEqual([
      "Photo Strip from this Event",
      "Photo Strip from this Event",
      "Photo Strip from this Event",
      "Photo Strip from this Event",
      "BACK TO START",
      "Operator exit",
    ]);
  });
});

test.describe("gallery views in reduced motion and forced colors", () => {
  test.use({ viewport: resolutions[0] });

  const views: readonly [string, "guest" | "operator"][] = [
    ["operator-ui-gallery-mixed", "operator"],
    ["operator-ui-gallery-detail", "operator"],
    ["operator-ui-gallery-detail-delete", "operator"],
    ["guest-ui-gallery-many-strips", "guest"],
    ["guest-ui-gallery-timer-warning", "guest"],
    ["guest-ui-strip-detail-landscape", "guest"],
  ];

  for (const [fixture, role] of views) {
    test(`${fixture} with reduced motion has no movement left`, async ({ page }) => {
      await page.emulateMedia({ reducedMotion: "reduce" });
      await page.goto(`/index.html?fixture=${fixture}`);
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", role);
      const slow = await page.evaluate(() =>
        document
          .getAnimations()
          .filter((animation) => {
            const duration = animation.effect?.getComputedTiming().duration;
            return typeof duration === "number" && duration > 1;
          })
          .map((animation) => (animation instanceof CSSAnimation ? animation.animationName : "transition")),
      );
      expect(slow).toEqual([]);
      if (role === "guest") {
        await expectNoPageScroll(page);
      }
      await page.screenshot({ path: resolve(screenshotDir, `${fixture}-reduced-motion.png`) });
    });

    test(`${fixture} stays legible in forced colors`, async ({ page }) => {
      await page.emulateMedia({ forcedColors: "active" });
      await open(page, fixture, role);
      if (role === "guest") {
        await expectNoPageScroll(page);
      } else {
        await expectNoHorizontalScrolling(page);
      }
      await page.screenshot({ path: resolve(screenshotDir, `${fixture}-forced-colors.png`) });
      await expectAccessible(page, { forcedColors: true });
    });
  }
});
