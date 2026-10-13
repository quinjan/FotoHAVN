import { resolve } from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { resolutionName, resolutions, screenshotDir } from "./resolutions.ts";

// M2 surfaces through the fixture lane (`?fixture=<name>`, honored in development and E2E builds only): the
// brain-generated messages in src/fixtures/presentations/ by file name, plus the hand-written `operator-event-list`
// with rows (src/fixtures/fixtureLane.ts). The fixture gallery route arrives in M3.

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
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-revision", "2");

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

    test("operator starting", async ({ page }) => {
      await page.goto("/index.html?fixture=operator-starting");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");
      await expect(page.locator('[data-fotohavn-surface="starting"]')).toBeVisible();
      await expect(page.getByRole("heading", { level: 1, name: "Events" })).toBeVisible();
      await expect(page.getByRole("status").filter({ hasText: "Opening the Data folder…" })).toBeVisible();
      await expect(page.getByRole("button")).toHaveCount(0);
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `operator-starting-${name}.png`) });
      await expectAccessible(page);
    });

    test("operator Event list, with rows", async ({ page }) => {
      await page.goto("/index.html?fixture=operator-event-list");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");

      const rows = page.getByRole("list", { name: "Events" }).getByRole("listitem");
      await expect(rows).toHaveCount(2);
      await expect(rows.first()).toContainText("Santos and Reyes wedding");
      await expect(rows.first()).toContainText("Sat 21 Nov 2026");
      await expect(rows.first()).toContainText("RUNNING");
      await expect(rows.last()).toContainText("42 Guest Cycles");
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `operator-event-list-${name}.png`) });
      await expectAccessible(page);
    });

    // The Operator Screen may scroll, so only the shorter lists are held to the no-scrolling check.
    for (const [fixture, count, scrolls] of [
      ["operator-event-list-1", 1, false],
      ["operator-event-list-5", 5, true],
      ["operator-event-list-30", 30, true],
    ] as const) {
      test(`operator Event list, ${String(count)} row${count === 1 ? "" : "s"}`, async ({ page }) => {
        await page.goto(`/index.html?fixture=${fixture}`);
        await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");
        await expect(page.getByRole("list", { name: "Events" }).getByRole("listitem")).toHaveCount(count);
        await expect(page.getByRole("button", { name: "NEW EVENT" })).toBeVisible();
        expect(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
        if (!scrolls) {
          await expectNoScrolling(page);
        }
        await page.screenshot({ path: resolve(screenshotDir, `${fixture}-${name}.png`) });
        await expectAccessible(page);
      });
    }

    test("operator Event list, long name and venue", async ({ page }) => {
      await page.goto("/index.html?fixture=operator-event-list-long");
      const rows = page.getByRole("list", { name: "Events" }).getByRole("listitem");
      await expect(rows).toHaveCount(2);
      await expect(rows.first()).toContainText("Alcantara-Villanueva and de los Reyes 50th anniversary celebration, Manila Hotel");
      await expect(rows.first()).toContainText("The Grand Ballroom of the Manila Hotel");
      expect(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
      await page.screenshot({ path: resolve(screenshotDir, `operator-event-list-long-${name}.png`) });
      await expectAccessible(page);
    });

    for (const [fixture, kind, line] of [
      ["operator-storage-error-disk-full", "diskFull", "The disk is full. Free some space, then try again."],
      ["operator-storage-error-access-denied", "accessDenied", "FOTOHAVN can't write to the Data folder. Check its permissions, then try again."],
      ["operator-storage-error-locked", "locked", "Another program is using a FOTOHAVN file. Close it, then try again."],
      ["operator-storage-error-not-local-disk", "notLocalDisk", "The Data folder is on a network drive. Move FOTOHAVN to this computer's own disk."],
      ["operator-storage-error-cloud-synced", "cloudSynced", "The Data folder is inside a cloud-synced folder, which can change files while the booth runs. Move FOTOHAVN outside it."],
      ["operator-storage-error-not-writable", "notWritable", "The Data folder can't be written to. Choose a folder FOTOHAVN can write to."],
      ["operator-storage-error-integrity-failed", "integrityFailed", "The Event database didn't pass its check. Copy the Data folder somewhere safe and contact the FOTOHAVN team."],
      ["operator-storage-error-corrupt", "corrupt", "The Event database didn't pass its check. Copy the Data folder somewhere safe and contact the FOTOHAVN team."],
      ["operator-storage-error-unknown", "unknown", "Something went wrong with the Data folder. Try again."],
    ] as const) {
      test(`operator storage error, ${kind}`, async ({ page }) => {
        await page.goto(`/index.html?fixture=${fixture}`);
        await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");
        await expect(page.locator('[data-fotohavn-surface="storageError"]')).toBeVisible();
        await expect(page.getByRole("heading", { level: 1, name: "The Data folder needs attention." })).toBeVisible();
        await expect(page.getByText(line)).toBeVisible();
        await expect(page.getByText("Data folder", { exact: true })).toBeVisible();
        await expect(page.getByRole("button", { name: "TRY AGAIN" })).toBeVisible();
        await expectNoScrolling(page);
        await page.screenshot({ path: resolve(screenshotDir, `${fixture}-${name}.png`) });
        await expectAccessible(page);
      });
    }

    test("operator storage error, TRY AGAIN that the brain rejects", async ({ page }) => {
      await page.goto("/index.html?fixture=operator-storage-error-locked");
      await page.getByRole("button", { name: "TRY AGAIN" }).click();
      await expect(page.getByRole("status").filter({ hasText: "That isn't ready yet." })).toBeVisible();
      await expect(page.getByRole("button", { name: "TRY AGAIN" })).toHaveAttribute("aria-disabled", "false");
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `operator-storage-error-rejected-${name}.png`) });
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

    test("operator fatal surface", async ({ page }) => {
      await page.goto("/index.html?fixture=operator-fatal");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-revision", "3");

      await expect(page.locator('[data-fotohavn-surface="fatal"]')).toBeVisible();
      await expect(page.getByRole("heading", { level: 1, name: "Something needs attention." })).toBeVisible();
      await expect(page.getByText("FOTOHAVN stopped. Close it and open it again.")).toBeVisible();
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `operator-fatal-${name}.png`) });
      await expectAccessible(page);
    });

    test("guest fatal surface", async ({ page }) => {
      await page.goto("/index.html?fixture=guest-fatal");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "guest");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-revision", "2");

      await expect(page.locator('[data-fotohavn-surface="fatal"]')).toBeVisible();
      await expect(page.getByRole("heading", { level: 1, name: "A little interruption." })).toBeVisible();
      await expect(page.getByText("FOTOHAVN stopped.")).toHaveCount(0);
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `guest-fatal-${name}.png`) });
      await expectAccessible(page);
    });

    test("protocol mismatch screen", async ({ page }) => {
      await page.goto("/index.html?fixture=protocol-mismatch");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-status", "protocolMismatch");
      await expect(page.locator("html")).not.toHaveAttribute("data-fotohavn-role", /.+/);

      await expect(page.locator('[data-fotohavn-screen="protocolMismatch"]')).toBeVisible();
      await expect(page.getByRole("heading", { level: 1, name: "A little mix-up." })).toBeVisible();
      await expect(page.getByText("Booth version 3 · Screen version 2")).toBeVisible();
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `protocol-mismatch-${name}.png`) });
      await expectAccessible(page);
    });

    test("protocol mismatch from the E2E version override", async ({ page }) => {
      await page.goto("/index.html?fixture=guest-start&e2eProtocolVersion=0");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-status", "protocolMismatch");
      await expect(page.locator('[data-fotohavn-screen="protocolMismatch"]')).toBeVisible();
    });

    test("not connected screen", async ({ page }) => {
      await page.goto("/index.html");
      await expect(page.locator("html")).toHaveAttribute("data-fotohavn-status", "notConnected");
      await expect(page.getByRole("heading", { level: 1, name: "Not connected." })).toBeVisible();
      const links = await page.getByRole("navigation", { name: "Fixtures" }).getByRole("link").allTextContents();
      expect(links).toEqual([...links].sort());
      expect(links).toEqual(
        expect.arrayContaining([
          "guest-fatal",
          "guest-start",
          "operator-event-list",
          "operator-event-list-empty",
          "operator-event-list-30",
          "operator-fatal",
          "operator-storage-error-disk-full",
          "protocol-mismatch",
        ]),
      );
      await expectNoScrolling(page);
      await expectAccessible(page);
    });
  });
}
