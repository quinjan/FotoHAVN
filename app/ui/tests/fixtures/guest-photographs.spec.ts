import { resolve } from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { resolutionName, resolutions, screenshotDir } from "./resolutions.ts";

// The Template and Photographs steps (M8 UI-1) through the fixture lane (`?fixture=<name>`): the brain-generated
// `guest-cycle-*` files, and the hand-written `guest-ui-*` ones (src/fixtures/guestCaptureFixtures.ts) for the states the
// brain's files don't write. Every Template, Camera, and Arrange state is checked at the four Guest Screen resolutions.
// Each must fit without scrolling, keep every control at its role's minimum target, show at most one primary action, and
// pass axe. The lane's stand-in brain answers every command `rejected notAvailable`, so these are rendering checks; the
// commands are unit-tested.

async function open(page: Page, fixture: string): Promise<void> {
  await page.goto(`/index.html?fixture=${fixture}`);
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "guest");
  await page.evaluate(() => document.fonts.ready);
}

async function expectNoScrolling(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => {
    const root = document.documentElement;
    return {
      horizontal: root.scrollWidth > root.clientWidth,
      vertical: root.scrollHeight > root.clientHeight,
      body: document.body.scrollHeight > document.body.clientHeight,
      regions: [...document.querySelectorAll<HTMLElement>("main *")]
        .filter((element) => {
          const style = getComputedStyle(element);
          return (
            (/auto|scroll/.test(style.overflowX) && element.scrollWidth > element.clientWidth) ||
            (/auto|scroll/.test(style.overflowY) && element.scrollHeight > element.clientHeight)
          );
        })
        .map((element) => element.outerHTML.slice(0, 80)),
    };
  });
  expect(overflow).toEqual({ horizontal: false, vertical: false, body: false, regions: [] });
}

async function expectTargets(page: Page): Promise<void> {
  const small = await page.evaluate(() => {
    const issues: string[] = [];
    for (const element of document.querySelectorAll<HTMLElement>("main button, main [role='button'], main a[href]")) {
      const box = element.getBoundingClientRect();
      if (box.width === 0 || box.height === 0) {
        continue;
      }
      const shutter = ["Take photographs", "Stop taking photographs"].includes(element.getAttribute("aria-label") ?? "");
      const minimum = shutter ? 112 : 64;
      if (box.width < minimum - 0.5 || box.height < minimum - 0.5) {
        issues.push(`${element.getAttribute("aria-label") ?? element.textContent} ${String(Math.round(box.width))}x${String(Math.round(box.height))}`);
      }
    }
    return issues;
  });
  expect(small).toEqual([]);
}

async function expectInsideViewport(page: Page, selector: string): Promise<void> {
  const viewport = page.viewportSize();
  for (const box of await page.locator(selector).evaluateAll((elements) =>
    elements.map((element) => {
      const { x, y, width, height } = element.getBoundingClientRect();
      return { x, y, width, height };
    }),
  )) {
    expect(box.x).toBeGreaterThanOrEqual(-0.5);
    expect(box.y).toBeGreaterThanOrEqual(-0.5);
    expect(box.x + box.width).toBeLessThanOrEqual((viewport?.width ?? 0) + 0.5);
    expect(box.y + box.height).toBeLessThanOrEqual((viewport?.height ?? 0) + 0.5);
  }
}

/** Every step renders inside the real cycle chrome: its band, then a stage that holds the step, all within the screen. */
async function expectStepFitsStage(page: Page): Promise<void> {
  const viewport = page.viewportSize();
  const chrome = await page.locator("[data-fotohavn-cycle-step]").evaluate((element) => {
    const band = element.querySelector("header")?.getBoundingClientRect();
    const stage = element.lastElementChild?.getBoundingClientRect();
    const step = element.lastElementChild?.firstElementChild?.getBoundingClientRect();
    return { bandBottom: band?.bottom ?? -1, stageTop: stage?.top ?? -1, stageBottom: stage?.bottom ?? -1, stepTop: step?.top ?? -1, stepBottom: step?.bottom ?? -1 };
  });
  expect(chrome.bandBottom).toBeGreaterThan(0);
  expect(chrome.stageTop).toBeGreaterThanOrEqual(chrome.bandBottom - 0.5);
  expect(chrome.stepTop).toBeGreaterThanOrEqual(chrome.stageTop - 0.5);
  expect(chrome.stepBottom).toBeLessThanOrEqual(chrome.stageBottom + 0.5);
  expect(chrome.stageBottom).toBeLessThanOrEqual((viewport?.height ?? 0) + 0.5);
}

async function finish(page: Page, fixture: string, suffix: string): Promise<void> {
  await expectNoScrolling(page);
  await expectStepFitsStage(page);
  await expectTargets(page);
  expect(await page.locator('main [data-fotohavn-action="primary"]').count()).toBeLessThanOrEqual(1);
  await page.screenshot({ path: resolve(screenshotDir, `${fixture}-${suffix}.png`) });
  const axe = await new AxeBuilder({ page }).analyze();
  expect(axe.violations).toEqual([]);
}

const templateFixtures = [
  ["guest-cycle-template", "Signature Strip"],
  ["guest-ui-template-signature", "Signature Strip"],
  ["guest-ui-template-panorama", "Panorama Four"],
  ["guest-ui-template-gingham", "Butter Gingham"],
] as const;

const cameraFixtures = [
  "guest-cycle-photographs-camera",
  "guest-cycle-photographs-camera-one-template",
  "guest-cycle-photographs-countdown",
  "guest-cycle-photographs-pausing",
  "guest-ui-camera-panorama",
  "guest-ui-camera-waiting",
  "guest-ui-camera-stopped",
  "guest-ui-camera-retaking",
] as const;

const arrangeFixtures = [
  "guest-cycle-photographs-arrange",
  "guest-cycle-photographs-arrange-landscape",
  "guest-cycle-photographs-arrange-selected",
  "guest-cycle-photographs-arrange-moving",
  "guest-cycle-photographs-arrange-one-look",
  "guest-cycle-photographs-arrange-one-look-composing",
  "guest-cycle-photographs-retaking",
  "guest-ui-arrange-gingham",
  "guest-ui-arrange-polka",
  "guest-ui-arrange-partial",
  "guest-ui-arrange-retained",
  "guest-ui-arrange-moving-panorama",
  "guest-ui-arrange-retaking-retained",
  "guest-ui-arrange-retaking-panorama",
] as const;

for (const resolution of resolutions) {
  const name = resolutionName(resolution);

  test.describe(`guest photographs at ${name}`, () => {
    test.use({ viewport: resolution });

    for (const [fixture, selected] of templateFixtures) {
      test(`Template, ${fixture.replace("guest-ui-template-", "")}`, async ({ page }) => {
        await open(page, fixture);
        await expect(page.locator('[data-fotohavn-surface="template"]')).toBeVisible();
        await expect(page.getByRole("heading", { level: 1, name: "Make a little Moment" })).toBeVisible();
        await expect(page.getByText("Choose your template.")).toBeVisible();
        await expect(page.getByRole("radio", { name: new RegExp(selected) })).toBeChecked();
        await expect(page.getByRole("button", { name: "CONTINUE TO PHOTOGRAPHS" })).toHaveAttribute("aria-disabled", "false");
        await expectInsideViewport(page, '[data-fotohavn-action="primary"]');
        await finish(page, fixture, name);
      });
    }

    for (const fixture of cameraFixtures) {
      test(`Camera, ${fixture.replace("guest-ui-camera-", "")}`, async ({ page }) => {
        await open(page, fixture);
        await expect(page.locator("[data-photographs-tab='camera']")).toBeVisible();
        await expect(page.getByRole("heading", { level: 1, name: "Find your light." })).toBeVisible();
        await expect(page.getByText("Let this moment become a memory.")).toBeVisible();
        await expect(page.getByRole("progressbar", { name: "Photographs" })).toBeVisible();
        await expect(page.locator("[data-fotohavn-crop-guide]")).toHaveCount(1);
        const shutter = page.getByRole("button", { name: /photographs$/ });
        const box = await shutter.boundingBox();
        expect(box?.width).toBeGreaterThanOrEqual(112);
        expect(box?.height).toBeGreaterThanOrEqual(112);
        await expectInsideViewport(page, "[data-photographs-tab='camera'] [role='img']");
        await finish(page, fixture, name);
      });
    }

    for (const fixture of arrangeFixtures) {
      test(`Arrange, ${fixture.replace("guest-ui-arrange-", "")}`, async ({ page }) => {
        await open(page, fixture);
        await expect(page.locator("[data-photographs-tab='arrange']")).toBeVisible();
        await expect(page.getByRole("heading", { level: 1, name: "Arrange your moment." })).toBeVisible();
        await expect(page.getByText("Drag to rearrange · Tap a photograph to edit")).toBeVisible();
        await expectInsideViewport(page, "[data-arrange-strip]");
        const strip = await page.locator("[data-arrange-strip] > div").first().boundingBox();
        expect(strip?.height).toBeGreaterThan(200);
        await finish(page, fixture, name);
      });
    }
    test("steps the brain marks skipped are left out of the progress and the rest renumbered", async ({ page }) => {
      await open(page, "guest-cycle-photographs-camera-one-template");
      await expect(page.getByRole("list", { name: "Steps" }).getByRole("listitem")).toHaveText(["01 Photographs", "02 Filters", "03 Keepsake"]);
      await open(page, "guest-cycle-photographs-arrange-one-look");
      await expect(page.getByRole("list", { name: "Steps" }).getByRole("listitem")).toHaveText(["01 Template, completed", "02 Photographs", "03 Keepsake"]);
      await expect(page.getByRole("button", { name: "FINISH MY STRIP" })).toHaveAttribute("aria-disabled", "false");
      await expect(page.getByRole("button", { name: "CONTINUE TO FILTERS" })).toHaveCount(0);
    });

    test("a Retake candidate waits in Arrange beside the original, as the brain sends it", async ({ page }) => {
      await open(page, "guest-cycle-photographs-retaking");
      await expect(page.locator("[data-photographs-tab='arrange']")).toBeVisible();
      await expect(page.getByText("Keep the new photograph for position 3, or try again.")).toBeVisible();
      await expect(page.getByRole("button", { name: "ACCEPT" })).toBeVisible();
      await expect(page.getByText("Original", { exact: true })).toBeVisible();
      await expect(page.getByText("New", { exact: true })).toBeVisible();
    });

    test("while the strip is prepared with Filters skipped, Arrange goes quiet", async ({ page }) => {
      await open(page, "guest-cycle-photographs-arrange-one-look-composing");
      await expect(page.getByText("Preparing your strip.")).toBeVisible();
      await expect(page.getByRole("button", { name: "FINISH MY STRIP" })).toHaveAttribute("aria-disabled", "true");
      await expect(page.getByRole("button", { name: "RETAKE" })).toHaveCount(0);
    });
  });
}

test.describe("guest photographs, reduced motion and forced colors", () => {
  test.use({ viewport: resolutions[0] });

  for (const fixture of [
    "guest-cycle-photographs-countdown",
    "guest-cycle-photographs-pausing",
    "guest-cycle-photographs-arrange-moving",
    "guest-cycle-photographs-retaking",
  ]) {
    test(`${fixture} with reduced motion`, async ({ page }) => {
      await page.emulateMedia({ reducedMotion: "reduce" });
      await open(page, fixture);
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `${fixture}-reduced-motion.png`) });
    });

    test(`${fixture} in forced colors stays legible and within the screen`, async ({ page }) => {
      await page.emulateMedia({ forcedColors: "active" });
      await open(page, fixture);
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `${fixture}-forced-colors.png`) });
    });
  }
});
