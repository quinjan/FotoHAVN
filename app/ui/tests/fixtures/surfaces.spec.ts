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

async function expectNoHorizontalScrolling(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
  expect(overflow).toBe(false);
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

    // The camera diagnostics surface (M5): the brain's generated fixtures for ready, checking, privacy denied, in use,
    // and no cameras, plus hand-written ones for the other reasons. An E2E build shows the controls; the fixture
    // brain answers every hook `hooksUnavailable`.
    for (const [fixture, state, reason, line] of [
      ["operator-camera-diagnostics-ready", "ready", null, "Ready"],
      ["operator-camera-diagnostics-checking", "checking", null, "Checking"],
      ["operator-camera-diagnostics-choose-camera", "idle", null, "Choose a camera."],
      ["operator-camera-diagnostics-privacy-denied", "notAvailable", "privacyDenied", "Not available. Camera access is off in Windows privacy settings."],
      ["operator-camera-diagnostics-in-use", "inUseByAnotherApp", "inUseByAnotherApp", "In use by another app"],
      ["operator-camera-diagnostics-reason-stale", "notAvailable", "stale", "Not available. The camera stopped sending pictures."],
      ["operator-camera-diagnostics-reason-failed", "notAvailable", "failed", "Not available. The camera stopped working."],
      ["operator-camera-diagnostics-reason-removed", "notAvailable", "removed", "Not available. The camera was disconnected."],
      ["operator-camera-diagnostics-reason-exclusive-lost", "notAvailable", "exclusiveLost", "Not available. Another app took over the camera."],
      ["operator-camera-diagnostics-reason-no-supported-format", "notAvailable", "noSupportedFormat", "Not available. This camera has no picture size FOTOHAVN can use."],
      ["operator-camera-diagnostics-reason-not-found", "notAvailable", "notFound", "Not available. FOTOHAVN can't find this camera."],
      ["operator-camera-diagnostics-reason-unknown", "notAvailable", "unknown", "Not available. Something went wrong with the camera."],
    ] as const) {
      test(`operator camera diagnostics, ${fixture.replace("operator-camera-diagnostics-", "")}`, async ({ page }) => {
        await page.goto(`/index.html?fixture=${fixture}`);
        await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");
        const surface = page.locator('[data-fotohavn-surface="cameraDiagnostics"]');
        await expect(surface).toBeVisible();
        await expect(page.getByText("CAMERA DIAGNOSTICS")).toBeVisible();
        await expect(page.getByRole("heading", { level: 1, name: "Check the camera." })).toBeVisible();
        await expect(page.locator(`[data-camera-state="${state}"]`)).toBeVisible();
        if (reason !== null) {
          await expect(page.locator(`[data-camera-reason="${reason}"]`)).toBeVisible();
        }
        await expect(page.getByText(line, { exact: true })).toBeVisible();

        // Only a ready camera shows a picture (the fixture brain's pattern); every other state waits, with words.
        await expect(page.locator("[data-state]")).toHaveAttribute("data-state", state === "ready" ? "live" : "waiting");
        if (state === "ready") {
          await expect(page.locator("canvas")).toHaveJSProperty("width", 1280);
        }

        const capture = page.getByRole("button", { name: "CAPTURE" });
        await expect(capture).toBeVisible();
        await expect(capture).toHaveAttribute("aria-disabled", state === "ready" ? "false" : "true");
        await expect(page.getByRole("switch", { name: "Mirror" })).toBeVisible();

        await expectNoHorizontalScrolling(page);
        await page.screenshot({ path: resolve(screenshotDir, `${fixture}-${name}.png`) });
        await expectAccessible(page);
      });
    }

    // The last composition (M7): a print from the lane's placeholder image, turned a quarter turn for the landscape Template.
    for (const [fixture, alt, rotated, size] of [
      ["operator-camera-diagnostics-composed", "Signature Strip, Sepia", false, "600 × 1800"],
      ["operator-camera-diagnostics-composed-landscape", "Panorama Four, Classic", true, "1800 × 600"],
    ] as const) {
      test(`operator camera diagnostics, ${fixture.replace("operator-camera-diagnostics-", "")}`, async ({ page }) => {
        await page.goto(`/index.html?fixture=${fixture}`);
        await expect(page.locator('[data-fotohavn-surface="cameraDiagnostics"]')).toBeVisible();
        const image = page.getByRole("img", { name: alt });
        await expect(image).toBeVisible();
        await expect(image).toHaveAttribute("src", /^data:image\/svg\+xml,/);
        await expect.poll(() => image.evaluate((element: HTMLImageElement) => element.complete && element.naturalWidth > 0)).toBe(true);
        if (rotated) {
          await expect(image.locator("..")).toHaveAttribute("data-rotated", "true");
        } else {
          await expect(image.locator("..")).not.toHaveAttribute("data-rotated", /.*/);
        }
        await expect(page.getByText(size, { exact: true })).toBeVisible();
        await expectNoHorizontalScrolling(page);
        await page.screenshot({ path: resolve(screenshotDir, `${fixture}-${name}.png`) });
        await expectAccessible(page);
      });
    }

    test("operator camera diagnostics, no cameras", async ({ page }) => {
      await page.goto("/index.html?fixture=operator-camera-diagnostics-no-cameras");
      await expect(page.locator('[data-fotohavn-surface="cameraDiagnostics"]')).toBeVisible();
      await expect(page.getByText("No camera found.")).toBeVisible();
      await expect(page.getByText("Connect a camera and it will appear here.")).toBeVisible();
      await expect(page.getByRole("radio")).toHaveCount(0);
      await expect(page.getByRole("button", { name: "CAPTURE" })).toBeVisible();
      await expectNoHorizontalScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `operator-camera-diagnostics-no-cameras-${name}.png`) });
      await expectAccessible(page);
    });

    test("operator camera diagnostics, a hook the booth won't run says so politely", async ({ page }) => {
      await page.goto("/index.html?fixture=operator-camera-diagnostics-ready");
      await page.getByRole("button", { name: "CAPTURE" }).click();
      await expect(page.getByRole("status").filter({ hasText: "That didn't work. Try again." })).toBeVisible();
      await page.getByRole("switch", { name: "Mirror" }).click();
      await expect(page.getByRole("status").filter({ hasText: "That didn't work. Try again." })).toBeVisible();
      await expectNoHorizontalScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `operator-camera-diagnostics-hook-notice-${name}.png`) });
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
      await expect(page.getByText("Booth version 5 · Screen version 4")).toBeVisible();
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
      // The page lists every fixture, so it grows with each milestone: it may scroll down, never sideways.
      await expectNoHorizontalScrolling(page);
      await expectAccessible(page);
    });
  });
}
