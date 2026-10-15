import { resolve } from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { resolutionName, resolutions, screenshotDir } from "./resolutions.ts";

// The Guest Screen shell through the fixture lane (`?fixture=<name>`): Start, Start unavailable, the exit hold, and the
// PIN dialog, at the four Guest Screen resolutions. Every view must fit without scrolling and pass axe (not under forced
// colors, where axe reads the emulated system colors as the page's own). The `guest-shell-*`
// fixtures are hand-written (src/fixtures/inlineFixtures.ts); the lane's stand-in brain answers every command
// `rejected notAvailable`.

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

async function expectAccessible(page: Page): Promise<void> {
  const axe = await new AxeBuilder({ page }).analyze();
  expect(axe.violations).toEqual([]);
}

async function open(page: Page, fixture: string): Promise<void> {
  await page.goto(`/index.html?fixture=${fixture}`);
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "guest");
}

async function expectInsideViewport(page: Page, selector: string): Promise<void> {
  const box = await page.locator(selector).first().boundingBox();
  const viewport = page.viewportSize();
  expect(box).not.toBeNull();
  expect(viewport).not.toBeNull();
  if (box !== null && viewport !== null) {
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.y).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(viewport.width);
    expect(box.y + box.height).toBeLessThanOrEqual(viewport.height);
  }
}

for (const resolution of resolutions) {
  const name = resolutionName(resolution);

  test.describe(`guest shell at ${name}`, () => {
    test.use({ viewport: resolution });

    async function finish(page: Page, fixture: string): Promise<void> {
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `${fixture}-${name}.png`) });
      await expectAccessible(page);
    }

    test("Start, ready", async ({ page }) => {
      await open(page, "guest-shell-start-ready");
      await expect(page.getByRole("heading", { level: 1, name: "Small room. Whole other world." })).toBeVisible();
      await expect(page.getByText("FOTOHAVN", { exact: true })).toBeVisible();
      await expect(page.getByText("Come a little closer.")).toBeVisible();
      await expect(page.getByRole("button", { name: "STEP INSIDE" })).toHaveAttribute("aria-disabled", "false");
      await expect(page.getByRole("button", { name: "SEE THE GOOD COMPANY" })).toHaveCount(0);
      const control = page.getByRole("button", { name: "Operator exit" });
      await expect(control).toBeVisible();
      const target = await control.boundingBox();
      expect(target?.width).toBeGreaterThanOrEqual(64);
      expect(target?.height).toBeGreaterThanOrEqual(64);
      const stepInside = await page.getByRole("button", { name: "STEP INSIDE" }).boundingBox();
      expect(stepInside?.height).toBeGreaterThanOrEqual(64);
      await expectInsideViewport(page, '[aria-label="Operator exit"]');
      await finish(page, "guest-shell-start-ready");
    });

    test("Start, ready with the gallery link", async ({ page }) => {
      await open(page, "guest-shell-start-gallery");
      await expect(page.getByRole("button", { name: "STEP INSIDE" })).toBeVisible();
      const link = page.getByRole("button", { name: "SEE THE GOOD COMPANY" });
      await expect(link).toBeVisible();
      const box = await link.boundingBox();
      expect(box?.height).toBeGreaterThanOrEqual(64);
      await finish(page, "guest-shell-start-gallery");
    });

    test("Start, STEP INSIDE not available yet", async ({ page }) => {
      await open(page, "guest-shell-start-not-ready");
      await expect(page.getByRole("button", { name: "STEP INSIDE" })).toHaveAttribute("aria-disabled", "true");
      await finish(page, "guest-shell-start-not-ready");
    });

    test("STEP INSIDE the brain turns down shows a quiet notice", async ({ page }) => {
      await open(page, "guest-shell-start-ready");
      await page.getByRole("button", { name: "STEP INSIDE" }).click();
      await expect(page.getByRole("status").filter({ hasText: "One moment. The booth is getting ready." })).toBeVisible();
      await finish(page, "guest-shell-start-notice");
    });

    for (const [fixture, heading, supporting] of [
      ["guest-shell-start-unavailable-camera", "The booth is getting ready.", "One moment."],
      ["guest-shell-start-unavailable-storage", "The booth is almost ready.", "Thank you for waiting."],
    ] as const) {
      test(`Start unavailable, ${fixture.replace("guest-shell-start-unavailable-", "")}`, async ({ page }) => {
        await open(page, fixture);
        await expect(page.locator('[data-fotohavn-surface="startUnavailable"]')).toBeVisible();
        await expect(page.getByRole("heading", { level: 1, name: heading })).toBeVisible();
        await expect(page.getByText(supporting)).toBeVisible();
        await expect(page.getByRole("button", { name: "STEP INSIDE" })).toHaveCount(0);
        await expect(page.getByRole("button", { name: "Operator exit" })).toBeVisible();
        await finish(page, fixture);
      });
    }

    test("exit hold in progress", async ({ page }) => {
      await open(page, "guest-shell-start-ready");
      const control = page.getByRole("button", { name: "Operator exit" });
      const box = await control.boundingBox();
      expect(box).not.toBeNull();
      if (box !== null) {
        await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
        await page.mouse.down();
      }
      await expect(page.getByText(/Keep holding: \d seconds?/)).toBeVisible();
      await page.waitForTimeout(700);
      await expectInsideViewport(page, '[data-phase="holding"]');
      await finish(page, "guest-shell-exit-hold");
      await page.mouse.up();
      await expect(page.getByText(/Keep holding/)).toHaveCount(0);
    });

    test("exit hold in progress with reduced motion draws no ring, only text", async ({ page }) => {
      await page.emulateMedia({ reducedMotion: "reduce" });
      await open(page, "guest-shell-start-ready");
      await page.getByRole("button", { name: "Operator exit" }).focus();
      await page.keyboard.down("Enter");
      await expect(page.getByText(/Keep holding: \d seconds?/)).toBeVisible();
      await expect(page.locator('[data-motion="reduced"]')).toHaveCount(1);
      await expect(page.locator("circle")).toHaveCount(1);
      await finish(page, "guest-shell-exit-hold-reduced-motion");
      await page.keyboard.up("Enter");
    });

    for (const [fixture, check] of [
      ["guest-shell-pin-dialog", "4 to 6 digits."],
      ["guest-shell-pin-dialog-wrong", "That PIN isn't right. Try again."],
      ["guest-shell-pin-dialog-locked", "Too many tries. The pad is locked for now."],
    ] as const) {
      test(`PIN dialog, ${fixture.replace("guest-shell-pin-dialog", "").replace("-", "") || "fresh"}`, async ({ page }) => {
        await open(page, fixture);
        const dialog = page.getByRole("dialog", { name: "Operator PIN" });
        await expect(dialog).toBeVisible();
        await expect(dialog.getByText(check)).toBeVisible();
        await expect(page.getByRole("button", { name: "Operator exit" })).toHaveCount(0);
        const key = await dialog.getByRole("button", { name: "5", exact: true }).boundingBox();
        expect(key?.width).toBeGreaterThanOrEqual(64);
        expect(key?.height).toBeGreaterThanOrEqual(64);
        const panel = await dialog.boundingBox();
        const viewport = page.viewportSize();
        expect(panel).not.toBeNull();
        if (panel !== null && viewport !== null) {
          expect(panel.y).toBeGreaterThanOrEqual(0);
          expect(panel.y + panel.height).toBeLessThanOrEqual(viewport.height);
        }
        // The content behind the dialog can't be reached.
        await expect(page.locator("[inert]")).not.toHaveCount(0);
        await page.keyboard.press("Tab");
        const insideDialog = await page.evaluate(() => document.activeElement?.closest('[role="dialog"]') !== null);
        expect(insideDialog).toBe(true);
        await finish(page, fixture);
      });
    }

    test("PIN dialog with wrong PIN says the tries left", async ({ page }) => {
      await open(page, "guest-shell-pin-dialog-wrong");
      await expect(page.getByText("3 tries left.")).toBeVisible();
    });

    test("PIN dialog locked shows the countdown", async ({ page }) => {
      await open(page, "guest-shell-pin-dialog-locked");
      await expect(page.getByText("Try again in 27 seconds.")).toBeVisible();
      await expect(page.getByRole("button", { name: "END EVENT" })).toHaveAttribute("aria-disabled", "true");
    });
  });
}

test.describe("guest shell in forced colors", () => {
  test.use({ viewport: resolutions[0] });

  for (const fixture of ["guest-shell-start-ready", "guest-shell-pin-dialog-wrong"]) {
    test(`${fixture} stays legible and within the screen`, async ({ page }) => {
      await page.emulateMedia({ forcedColors: "active" });
      await open(page, fixture);
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `${fixture}-forced-colors.png`) });
    });
  }
});
