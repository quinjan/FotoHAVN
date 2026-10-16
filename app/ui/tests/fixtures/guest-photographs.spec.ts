import { resolve } from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { resolutionName, resolutions, screenshotDir } from "./resolutions.ts";

// The Template and Photographs steps (M8 UI-1) through the fixture lane (`?fixture=guest-ui-*`, hand-written in
// src/fixtures/guestCaptureFixtures.ts): every Template, Camera, and Arrange state at the four Guest Screen resolutions.
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

async function finish(page: Page, fixture: string, suffix: string): Promise<void> {
  await expectNoScrolling(page);
  await expectTargets(page);
  expect(await page.locator('main [data-fotohavn-action="primary"]').count()).toBeLessThanOrEqual(1);
  await page.screenshot({ path: resolve(screenshotDir, `${fixture}-${suffix}.png`) });
  const axe = await new AxeBuilder({ page }).analyze();
  expect(axe.violations).toEqual([]);
}

const templateFixtures = [
  ["guest-ui-template-signature", "Signature Strip"],
  ["guest-ui-template-panorama", "Panorama Four"],
  ["guest-ui-template-gingham", "Butter Gingham"],
  ["guest-ui-template-two", "Signature Strip"],
] as const;

const cameraFixtures = [
  "guest-ui-camera-idle",
  "guest-ui-camera-panorama",
  "guest-ui-camera-waiting",
  "guest-ui-camera-countdown",
  "guest-ui-camera-flash",
  "guest-ui-camera-stopped",
  "guest-ui-camera-retaking",
] as const;

const arrangeFixtures = [
  "guest-ui-arrange-signature",
  "guest-ui-arrange-panorama",
  "guest-ui-arrange-gingham",
  "guest-ui-arrange-polka",
  "guest-ui-arrange-finish",
  "guest-ui-arrange-partial",
  "guest-ui-arrange-retained",
  "guest-ui-arrange-selected",
  "guest-ui-arrange-selected-panorama",
  "guest-ui-arrange-moving",
  "guest-ui-arrange-moving-panorama",
  "guest-ui-arrange-retake-candidate",
  "guest-ui-arrange-retake-candidate-panorama",
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
  });
}

test.describe("guest photographs beneath the guest top band", () => {
  // The cycle chrome (M8 UI-2) reserves `--app-region-top` above the step; each step must still fit the stage that is left.
  const band =
    "[data-fotohavn-cycle-step]{display:grid;grid-template-rows:var(--app-region-top) minmax(0,1fr);block-size:100%}[data-fotohavn-cycle-step]>*{grid-row:2}";

  for (const resolution of resolutions) {
    const name = resolutionName(resolution);
    test.describe(name, () => {
      test.use({ viewport: resolution });

      for (const fixture of [...templateFixtures.map(([id]) => id), ...cameraFixtures, ...arrangeFixtures]) {
        test(`${fixture} fits the stage`, async ({ page }) => {
          await open(page, fixture);
          await page.addStyleTag({ content: band });
          await expectNoScrolling(page);
          await expectTargets(page);
          await page.screenshot({ path: resolve(screenshotDir, `${fixture}-band-${name}.png`) });
        });
      }
    });
  }
});

test.describe("guest photographs, reduced motion and forced colors", () => {
  test.use({ viewport: resolutions[0] });

  for (const fixture of ["guest-ui-camera-countdown", "guest-ui-camera-flash", "guest-ui-arrange-moving", "guest-ui-arrange-retake-candidate"]) {
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
