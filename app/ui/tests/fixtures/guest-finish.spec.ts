import { resolve } from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { resolutionName, resolutions, screenshotDir } from "./resolutions.ts";
import { expectStageContains } from "./stage.ts";

// The M8 UI-2 views through the fixture lane (`?fixture=<name>`): Filters, Keepsake, Operator Assistance, the cycle chrome,
// and its overlays. The brain-generated `guest-cycle-*` and `guest-assistance-*` files are the real shapes; the hand-written
// `guest-ui-*` ones (src/fixtures/guestFinishFixtures.ts) cover the states the brain's files don't write. At the four
// Guest Screen resolutions each must fit without scrolling or overlap, keep every control at the 64 target, show at most one
// primary action, and pass axe. The lane's stand-in brain answers every command `rejected notAvailable`.

async function expectNoScrolling(page: Page): Promise<void> {
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

/** Nothing inside the stage spills out of it: every descendant that draws stays within the viewport and within its step. */
async function expectNoClipping(page: Page): Promise<void> {
  const spills = await page.evaluate(() => {
    const viewport = { width: document.documentElement.clientWidth, height: document.documentElement.clientHeight };
    const offenders: string[] = [];
    for (const element of document.querySelectorAll<HTMLElement>("main *")) {
      if (element.closest("[inert]") !== null && element.closest('[role="alertdialog"]') === null) {
        continue;
      }
      const box = element.getBoundingClientRect();
      if (box.width === 0 || box.height === 0 || getComputedStyle(element).position === "absolute") {
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

async function expectTargets(page: Page): Promise<void> {
  const small = await page.evaluate(() => {
    const offenders: string[] = [];
    for (const control of document.querySelectorAll<HTMLElement>("button, input, a[href]")) {
      if (control.closest("[inert]") !== null || (control instanceof HTMLInputElement && control.type === "hidden")) {
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

async function expectAtMostOnePrimary(page: Page): Promise<void> {
  // Content behind a dialog is inert, so only what can take focus counts as the view's primary actions.
  const count = await page.evaluate(() => [...document.querySelectorAll('main [data-fotohavn-action="primary"]')].filter((action) => action.closest("[inert]") === null).length);
  expect(count).toBeLessThanOrEqual(1);
}

async function expectAccessible(page: Page): Promise<void> {
  const axe = await new AxeBuilder({ page }).analyze();
  expect(axe.violations).toEqual([]);
}

async function settle(page: Page): Promise<void> {
  await page.evaluate(() => Promise.all(document.getAnimations().map((animation) => animation.finished)));
  await page.evaluate(() => document.fonts.ready);
}

async function open(page: Page, fixture: string, role: "guest" | "operator" = "guest"): Promise<void> {
  await page.goto(`/index.html?fixture=${fixture}`);
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", role);
  await settle(page);
}

interface View {
  readonly fixture: string;
  readonly surface: string;
  readonly heading: string;
  readonly texts?: readonly string[];
}

const filtersViews: readonly View[] = [
  { fixture: "guest-cycle-filters", surface: "filters", heading: "Find the feeling.", texts: ["Choose a filter for your photographs.", "Naturale", "Monochrome"] },
  { fixture: "guest-cycle-filters-composing", surface: "filters", heading: "Find the feeling.", texts: ["Preparing your strip."] },
  { fixture: "guest-cycle-filters-composition-failed", surface: "filters", heading: "Find the feeling.", texts: ["Your strip couldn't be prepared. Try again."] },
  { fixture: "guest-cycle-filters-landscape", surface: "filters", heading: "Find the feeling." },
  { fixture: "guest-cycle-timer-low", surface: "template", heading: "Make a little Moment", texts: ["0:15"] },
  { fixture: "guest-ui-filters-four-looks", surface: "filters", heading: "Find the feeling.", texts: ["Naturale", "Monochrome", "Sepia", "Classic"] },
  { fixture: "guest-ui-filters-composing", surface: "filters", heading: "Find the feeling.", texts: ["Preparing your strip."] },
  { fixture: "guest-ui-filters-composition-failed", surface: "filters", heading: "Find the feeling.", texts: ["Your strip couldn't be prepared. Try again."] },
  { fixture: "guest-ui-filters-five-looks", surface: "filters", heading: "Find the feeling." },
  { fixture: "guest-ui-filters-many-looks", surface: "filters", heading: "Find the feeling.", texts: ["Soft Monochrome", "Warm Monochrome"] },
  { fixture: "guest-ui-filters-no-thumbnails", surface: "filters", heading: "Find the feeling." },
  { fixture: "guest-ui-filters-skipped-template", surface: "filters", heading: "Find the feeling." },
  { fixture: "guest-ui-filters-timer-last-second", surface: "filters", heading: "Find the feeling.", texts: ["0:01"] },
  { fixture: "guest-ui-filters-timer-paused", surface: "filters", heading: "Find the feeling.", texts: ["0:21", "Preparing your strip."] },
  { fixture: "guest-ui-filters-no-timer", surface: "filters", heading: "Find the feeling.", texts: ["Soft Monochrome", "Warm Monochrome"] },
  { fixture: "guest-cycle-keepsake", surface: "keepsake", heading: "A little moment. Yours.", texts: ["Keep the moment close."] },
  { fixture: "guest-cycle-keepsake-landscape", surface: "keepsake", heading: "A little moment. Yours." },
  { fixture: "guest-ui-keepsake-skipped-filters", surface: "keepsake", heading: "A little moment. Yours." },
  { fixture: "guest-assistance-camera", surface: "operatorAssistance", heading: "A little interruption.", texts: ["Please ask the FOTOHAVN team for help.", "RETRY"] },
  { fixture: "guest-assistance-storage", surface: "operatorAssistance", heading: "A little interruption." },
  { fixture: "guest-assistance-display", surface: "operatorAssistance", heading: "A little interruption." },
  { fixture: "guest-ui-assistance-retrying", surface: "operatorAssistance", heading: "A little interruption.", texts: ["Trying again…"] },
];

for (const resolution of resolutions) {
  const name = resolutionName(resolution);

  test.describe(`guest finish views at ${name}`, () => {
    test.use({ viewport: resolution });

    for (const view of filtersViews) {
      test(view.fixture, async ({ page }) => {
        await open(page, view.fixture);
        await expect(page.locator(`[data-fotohavn-surface="${view.surface}"]`)).toBeVisible();
        await expect(page.getByRole("heading", { level: 1, name: view.heading })).toBeVisible();
        for (const text of view.texts ?? []) {
          await expect(page.getByText(text).first()).toBeVisible();
        }
        await expect(page.getByRole("button", { name: "Operator exit" })).toBeVisible();
        await expectNoScrolling(page);
        await expectNoClipping(page);
        await expectStageContains(page);
        await expectTargets(page);
        await expectAtMostOnePrimary(page);
        await page.screenshot({ path: resolve(screenshotDir, `${view.fixture}-${name}.png`) });
        await expectAccessible(page);
      });
    }

    test("Filters: FINISH MY STRIP is the one primary action, and the chrome's actions are not", async ({ page }) => {
      await open(page, "guest-cycle-filters");
      await expect(page.locator('main [data-fotohavn-action="primary"]')).toHaveCount(1);
      await expect(page.getByRole("button", { name: "FINISH MY STRIP" })).toHaveAttribute("aria-disabled", "false");
      await expect(page.getByRole("radiogroup").or(page.getByRole("group", { name: "Filters" }))).toBeVisible();
      await expect(page.getByRole("list", { name: "Steps" }).getByRole("listitem")).toHaveText(["01 Template, completed", "02 Photographs, completed", "03 Filters", "04 Keepsake"]);
      await expect(page.getByRole("list", { name: "Steps" }).getByRole("button")).toHaveCount(0);
      await expect(page.getByRole("button", { name: "START OVER" })).toHaveCount(0);
      await expect(page.getByRole("timer")).toBeVisible();
    });

    test("Filters: a first composition that failed holds the print's shape, blank, and offers TRY AGAIN", async ({ page }) => {
      await open(page, "guest-cycle-filters-composition-failed");
      await expect(page.getByText("Your strip couldn't be prepared. Try again.")).toBeVisible();
      await expect(page.getByRole("img", { name: "Preview of your Photo Strip" })).toHaveCount(0);
      const print = await page.locator("[data-blank]").boundingBox();
      expect(print).not.toBeNull();
      // The Template's size comes with the view, so the print stands at its Signature Strip proportion before any picture.
      expect((print?.height ?? 0) / (print?.width ?? 1)).toBeCloseTo(3, 0);
      await expect(page.getByRole("button", { name: "TRY AGAIN" })).toBeVisible();
      await expect(page.locator('main [data-fotohavn-action="primary"]')).toHaveCount(1);
    });

    test("Filters: a landscape Template reserves a turned print at once", async ({ page }) => {
      await open(page, "guest-cycle-filters-landscape");
      const preview = page.getByRole("img", { name: "Preview of your Photo Strip" });
      await expect(preview).toBeVisible();
      await expect(page.locator("[data-rotated]")).toHaveCount(1);
      const box = await page.locator("[data-rotated]").boundingBox();
      expect((box?.height ?? 0) / (box?.width ?? 1)).toBeGreaterThan(2);
    });

    test("Filters: composition failure keeps the previous strip and offers TRY AGAIN as the primary action", async ({ page }) => {
      await open(page, "guest-ui-filters-composition-failed");
      await expect(page.getByText("Your strip couldn't be prepared. Try again.")).toBeVisible();
      await expect(page.getByRole("img", { name: "Preview of your Photo Strip" })).toBeVisible();
      await expect(page.getByRole("button", { name: "TRY AGAIN" })).toBeVisible();
      await expect(page.getByRole("button", { name: "FINISH MY STRIP" })).toHaveCount(0);
      await expect(page.locator('main [data-fotohavn-action="primary"]')).toHaveCount(1);
    });

    test("Filters: a skipped step is left out and the rest are renumbered", async ({ page }) => {
      await open(page, "guest-ui-filters-skipped-template");
      await expect(page.getByRole("list", { name: "Steps" }).getByRole("listitem")).toHaveText(["01 Photographs, completed", "02 Filters", "03 Keepsake"]);
    });

    test("Keepsake: its Phase Timer and nothing to press but the exit hold, and the strip is lifted", async ({ page }) => {
      await open(page, "guest-cycle-keepsake");
      await expect(page.getByRole("timer")).toBeVisible();
      await expect(page.getByRole("button")).toHaveCount(1);
      await expect(page.getByRole("list", { name: "Steps" }).getByRole("listitem")).toHaveText(["01 Template, completed", "02 Photographs, completed", "03 Filters, completed", "04 Keepsake"]);
      await expect(page.locator('[data-shadow="lifted"]')).toHaveCount(1);
      await expect(page.getByRole("img", { name: "Your Photo Strip" })).toBeVisible();
    });

    async function expectTimerInBand(page: Page, clock: string, announcement: string | null): Promise<void> {
      const timer = page.getByRole("timer");
      await expect(timer).toHaveText(clock);
      expect(await timer.evaluate((element) => element.closest("header") !== null)).toBe(true);
      const pill = await timer.boundingBox();
      const hold = await page.getByRole("button", { name: "Operator exit" }).boundingBox();
      const steps = await page.getByRole("list", { name: "Steps" }).boundingBox();
      // In the top band, right of the step progress and just left of the exit hold, level with it.
      expect(pill?.x).toBeGreaterThanOrEqual((steps?.x ?? 0) + (steps?.width ?? 0));
      expect((pill?.x ?? 0) + (pill?.width ?? 0)).toBeLessThanOrEqual(hold?.x ?? 0);
      expect(Math.abs((pill?.y ?? 0) + (pill?.height ?? 0) / 2 - ((hold?.y ?? 0) + (hold?.height ?? 0) / 2))).toBeLessThan(2);
      const live = page.locator("header [role='status']");
      if (announcement === null) {
        await expect(live).toBeEmpty();
      } else {
        await expect(live).toHaveText(announcement);
      }
      await expect(page.getByRole("button", { name: "MORE TIME" })).toHaveCount(0);
      await expect(page.getByRole("button", { name: "START OVER" })).toHaveCount(0);
      await expect(page.locator("footer")).toHaveCount(0);
    }

    test("Phase Timer: visible all the phase long, quiet until its last seconds, with no MORE TIME", async ({ page }) => {
      await open(page, "guest-cycle-filters");
      const timer = page.getByRole("timer");
      await expect(timer).not.toHaveAttribute("data-low");
      await expectTimerInBand(page, (await timer.textContent()) ?? "", null);
      await expect(page.getByRole("button", { name: "FINISH MY STRIP" })).toHaveAttribute("aria-disabled", "false");
    });

    test("Phase Timer on Filters: the last second fills the pill, is said once, and blocks nothing", async ({ page }) => {
      await open(page, "guest-ui-filters-timer-last-second");
      await expectTimerInBand(page, "0:01", "1 second left.");
      await expect(page.getByRole("timer")).toHaveAttribute("data-low", "true");
      await expect(page.locator("[inert]")).toHaveCount(0);
      await expect(page.getByRole("button", { name: "FINISH MY STRIP" })).toHaveAttribute("aria-disabled", "false");
    });

    test("Phase Timer paused: the time stands still and says so", async ({ page }) => {
      await open(page, "guest-ui-filters-timer-paused");
      await expectTimerInBand(page, "0:21", null);
      await expect(page.getByRole("timer")).toHaveAttribute("data-paused", "true");
      await expect(page.getByRole("timer")).toHaveAccessibleName("Time left: 21 seconds, paused");
    });

    test("Phase Timer off: the band shows the step progress alone", async ({ page }) => {
      await open(page, "guest-ui-filters-no-timer");
      await expect(page.getByRole("timer")).toHaveCount(0);
    });

    test("Operator Assistance: one dark band with RETRY inverted, and the exit hold above it on the canvas", async ({ page }) => {
      await open(page, "guest-assistance-camera");
      const band = page.locator('[data-tone="dark"]');
      await expect(band).toHaveCount(1);
      const retry = page.getByRole("button", { name: "RETRY" });
      await expect(retry).toHaveAttribute("aria-disabled", "false");
      const colors = await retry.evaluate((element) => ({ fill: getComputedStyle(element).backgroundColor, label: getComputedStyle(element).color }));
      expect(colors.fill).not.toBe(colors.label);
      const bandBox = await band.boundingBox();
      const hold = await page.getByRole("button", { name: "Operator exit" }).boundingBox();
      expect(bandBox).not.toBeNull();
      expect(hold).not.toBeNull();
      expect((hold?.y ?? 0) + (hold?.height ?? 0)).toBeLessThanOrEqual(bandBox?.y ?? 0);
      expect(bandBox?.width).toBe(resolution.width);
    });

    test("Operator Assistance while retrying: RETRY is unavailable and a polite line says why", async ({ page }) => {
      await open(page, "guest-ui-assistance-retrying");
      await expect(page.getByRole("button", { name: "RETRY" })).toHaveAttribute("aria-disabled", "true");
      await expect(page.getByRole("status").filter({ hasText: "Trying again…" })).toBeVisible();
    });
  });
}

test.describe("guest finish views in reduced motion and forced colors", () => {
  test.use({ viewport: resolutions[0] });

  for (const fixture of ["guest-cycle-keepsake", "guest-cycle-filters", "guest-ui-filters-timer-last-second", "guest-assistance-camera"]) {
    test(`${fixture} with reduced motion shows everything at once, with no movement left`, async ({ page }) => {
      await page.emulateMedia({ reducedMotion: "reduce" });
      await page.goto(`/index.html?fixture=${fixture}`);
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "guest");
      // Reduced motion makes every change near-instant (1 ms in CSS): nothing is left running that takes longer.
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
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `${fixture}-reduced-motion.png`) });
    });

    test(`${fixture} stays legible and within the screen in forced colors`, async ({ page }) => {
      await page.emulateMedia({ forcedColors: "active" });
      await open(page, fixture);
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `${fixture}-forced-colors.png`) });
    });
  }

  test("Keepsake develops in over the reveal duration, once", async ({ page }) => {
    await page.goto("/index.html?fixture=guest-cycle-keepsake");
    await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "guest");
    const durations = await page.evaluate(() =>
      document
        .getAnimations()
        .map((animation) => animation.effect?.getComputedTiming().duration)
        .filter((duration): duration is number => typeof duration === "number"),
    );
    expect(durations.length).toBeGreaterThan(0);
    for (const duration of durations) {
      expect(duration).toBeLessThanOrEqual(600);
    }
  });
});

test.describe("operator panel in Operator Assistance", () => {
  test.use({ viewport: { width: 1366, height: 768 } });

  for (const [fixture, reason] of [
    ["operator-running-assistance-camera", "The camera stopped working. Check its cable, then RETRY."],
    ["operator-running-assistance-storage", "The Data folder can't be written to. Check the disk and the folder, then RETRY."],
  ] as const) {
    test(fixture, async ({ page }) => {
      await open(page, fixture, "operator");
      await expect(page.getByText(reason)).toBeVisible();
      await expect(page.getByText("Operator Assistance", { exact: true })).toBeVisible();
      await expect(page.locator('main [data-fotohavn-action="primary"]')).toHaveCount(1);
      await expect(page.getByRole("button", { name: "RETRY" })).toBeVisible();
      await expect(page.getByRole("button", { name: "NEW EVENT" })).toBeVisible();
      await page.screenshot({ path: resolve(screenshotDir, `${fixture}-1366x768.png`) });
      await expectAccessible(page);
    });
  }
});
