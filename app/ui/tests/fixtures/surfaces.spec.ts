import { resolve } from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { resolutionName, resolutions, screenshotDir } from "./resolutions.ts";

// M2 surfaces through the fixture transport (`?fixture=`, honored in development and E2E builds only). The inline
// fixtures in src/fixtures/inlineFixtures.ts stand in until wave C points this lane at the brain-generated files; the
// fixture gallery route arrives in M3.

async function expectNoScrolling(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => {
    const root = document.documentElement;
    return {
      horizontal: root.scrollWidth > root.clientWidth,
      vertical: root.scrollHeight > root.clientHeight,
    };
  });
  expect(overflow).toEqual({ horizontal: false, vertical: false });
}

async function expectAccessible(page: Page): Promise<void> {
  const axe = await new AxeBuilder({ page }).analyze();
  expect(axe.violations).toEqual([]);
}

for (const resolution of resolutions) {
  const name = resolutionName(resolution);

  test.describe(`surfaces at ${name}`, () => {
    test.use({ viewport: resolution });

    test("operator Event list, empty", async ({ page }) => {
      await page.goto("/index.html?fixture=operator-event-list-empty");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-revision", "1");

      await expect(page).toHaveTitle("FOTOHAVN");
      await expect(page.getByRole("heading", { level: 1, name: "Events" })).toBeVisible();
      await expect(page.getByText("No Events yet.")).toBeVisible();
      const newEvent = page.getByRole("button", { name: "NEW EVENT" });
      await expect(newEvent).toBeVisible();
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `operator-event-list-empty-${name}.png`) });
      await expectAccessible(page);

      await newEvent.click();
      await expect(page.getByRole("status").filter({ hasText: "That isn't ready yet." })).toBeVisible();
      await expectAccessible(page);
    });

    test("operator Event list, with rows", async ({ page }) => {
      await page.goto("/index.html?fixture=operator-event-list");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");

      const rows = page.getByRole("list", { name: "Events" }).getByRole("listitem");
      await expect(rows).toHaveCount(2);
      await expect(rows.first()).toContainText("Santos and Reyes wedding");
      await expect(rows.first()).toContainText("RUNNING");
      await expect(rows.last()).toContainText("42 Guest Cycles");
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `operator-event-list-${name}.png`) });
      await expectAccessible(page);
    });

    test("guest start placeholder", async ({ page }) => {
      await page.goto("/index.html?fixture=guest-start");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "guest");

      await expect(page.getByRole("heading", { level: 1, name: "Small room. Whole other world." })).toBeVisible();
      await expect(page.getByText("FOTOHAVN", { exact: true })).toBeVisible();
      await expect(page.getByText("Come a little closer.")).toBeVisible();
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `guest-start-${name}.png`) });
      await expectAccessible(page);
    });

    test("protocol mismatch screen", async ({ page }) => {
      await page.goto("/index.html?fixture=guest-start&e2eProtocolVersion=0");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-status", "protocolMismatch");
      await expect(page.locator("html")).not.toHaveAttribute("data-fotohavn-role", /.+/);

      await expect(page.locator('[data-fotohavn-screen="protocolMismatch"]')).toBeVisible();
      await expect(page.getByRole("heading", { level: 1, name: "A little mix-up." })).toBeVisible();
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `protocol-mismatch-${name}.png`) });
      await expectAccessible(page);
    });

    test("not connected screen", async ({ page }) => {
      await page.goto("/index.html");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-status", "notConnected");
      await expect(page.getByRole("heading", { level: 1, name: "Not connected." })).toBeVisible();
      await expect(page.getByRole("navigation", { name: "Fixtures" }).getByRole("link")).toHaveCount(3);
      await expectNoScrolling(page);
      await expectAccessible(page);
    });
  });
}
