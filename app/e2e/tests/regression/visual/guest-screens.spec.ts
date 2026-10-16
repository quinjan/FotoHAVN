import { mkdirSync } from "node:fs";
import path from "node:path";
import type { Page, TestInfo } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { resolutionName, resolutions } from "../../../src/gallery";
import { GuestControls, scrollingElements, startGuestEvent, tap, type GuestRun } from "../../../src/guest-cycle";
import { moveOsCursor } from "../../../src/os-cursor";
import { paths } from "../../../src/paths";

/** Where the Guest Screen screenshots go: a visual record for review against DESIGN.md, not a pixel comparison. */
const shotsDir = path.join(paths.e2eOutput, "guest-screens");

async function imagesLoaded(page: Page): Promise<boolean> {
  return page.evaluate(() => Array.from(document.images).every((image) => image.complete && image.naturalWidth > 0));
}

/** Controls under the pointer. The real cursor is parked on the Operator window, so none of the Guest Screen's may be hovered. */
async function hoveredControls(page: Page): Promise<string[]> {
  return page.evaluate(() =>
    Array.from(document.querySelectorAll<HTMLElement>(":hover"))
      .filter((element) => element.matches("button, a, input, label, [role='button'], [role='radio']"))
      .map((element) => element.getAttribute("aria-label") ?? element.textContent?.trim().slice(0, 30) ?? element.tagName),
  );
}

type Ready = (guest: GuestControls) => Promise<void>;

/** Each of the four resolutions: size the real Guest window, wait for the layout, check it has no scrollbar, and save a screenshot. */
async function recordAtEveryResolution(run: GuestRun, testInfo: TestInfo, state: string, ready: Ready): Promise<void> {
  mkdirSync(shotsDir, { recursive: true });
  for (const resolution of resolutions) {
    const name = resolutionName(resolution);
    await test.step(`${state} at ${name}`, async () => {
      await run.hooks.setWindowBounds("guest", resolution.width, resolution.height);
      const page = await run.guest();
      await expect
        .poll(() => page.evaluate(() => Math.round(window.innerWidth * window.devicePixelRatio)), { message: `the window is ${name} wide` })
        .toBe(resolution.width);
      await ready(new GuestControls(page));
      await page.evaluate(() => document.fonts.ready);
      await page.mouse.move(1, 1);
      await expect.poll(() => hoveredControls(page), { message: `${state} at ${name}: a control is hovered`, timeout: 5_000 }).toEqual([]);
      await expect.poll(() => imagesLoaded(page), { message: `${state} at ${name}: images are loaded` }).toBe(true);
      await page.waitForTimeout(250);
      expect(await scrollingElements(page), `${state} at ${name} scrolls`).toEqual([]);
      const file = path.join(shotsDir, `${state}-${name}.png`);
      await page.screenshot({ path: file });
      await testInfo.attach(`${state}-${name}`, { path: file, contentType: "image/png" });
    });
  }
}

test(
  "every Guest Screen step in the real host at the four resolutions has no scrollbar and is saved for review",
  { tag: ["@m8", "@camera", "@cursor"] },
  async ({ app, operatorPage, camera, hooks }, testInfo) => {
    test.setTimeout(900_000);
    const run = await startGuestEvent(
      { app, page: operatorPage, camera },
      {
        name: "Guest screens",
        countdown: 10,
        timers: { Template: 40, "Photographs: capture": 300, "Photographs: review": 300, Filters: 300, Keepsake: 60 },
      },
    );
    const windows = (await hooks.getDiagnostics()).windows;
    const original = windows.find((window) => window.role === "guest")?.bounds;
    const operatorBounds = windows.find((window) => window.role === "operator")?.bounds;
    if (operatorBounds === undefined || original === undefined) {
      throw new Error("The Operator or Guest window has no bounds.");
    }
    moveOsCursor(operatorBounds.x + 1, operatorBounds.y + 1);
    const record = (state: string, ready: Ready): Promise<void> => recordAtEveryResolution(run, testInfo, state, ready);
    let guest = await run.expectStartReady();

    await record("start", async (g) => {
      await expect(g.surface("start")).toBeVisible();
      await expect(g.stepInside).not.toHaveAttribute("aria-disabled", "true");
    });

    await test.step("Start unavailable while the camera is down", async () => {
      await hooks.injectCameraFault("failed");
      await run.waitForSurface("startUnavailable");
      await record("start-unavailable", async (g) => {
        await expect(g.surface("startUnavailable")).toBeVisible();
      });
      await hooks.clearCameraFault();
      await run.waitForSurface("start", 30_000);
      guest = await run.controls();
      await expect(guest.stepInside).not.toHaveAttribute("aria-disabled", "true", { timeout: 20_000 });
    });

    await test.step("Template, then its Phase Timer warning", async () => {
      await tap(guest.stepInside);
      await record("template", async (g) => {
        await expect(g.surface("template")).toBeVisible();
      });
      await expect.poll(async () => (await run.cycle())?.timer.warning, { timeout: 40_000 }).toBe(true);
      await record("template-warning", async (g) => {
        await expect(g.warning).toBeVisible();
      });
      await tap(guest.templateContinue);
      await run.waitForStep("photographs", "camera");
    });

    await test.step("Photographs: Camera idle, and the countdown of the first photograph", async () => {
      await record("photographs-camera", async (g) => {
        await expect(g.surface("photographs")).toBeVisible();
        await expect(g.takePhotographs).not.toHaveAttribute("aria-disabled", "true");
      });
      await tap(guest.takePhotographs);
      await expect.poll(async () => (await run.cycle())?.countdownRemaining ?? 0, { timeout: 15_000 }).toBeGreaterThanOrEqual(9);
      await record("photographs-countdown", async (g) => {
        await expect(g.surface("photographs")).toBeVisible();
        await expect.poll(async () => (await run.cycle())?.sequenceRunning).toBe(true);
      });
      await run.waitForFilled(4, 150_000);
      await run.waitForStep("photographs", "arrange");
    });

    await test.step("Photographs: Arrange, with a photograph selected, a Retake candidate, and the Start Over question", async () => {
      await record("photographs-arrange", async (g) => {
        await expect(g.windowButton(4)).toBeVisible();
      });
      await tap(guest.windowButton(2));
      await expect(guest.page.getByText("Photograph 2 is selected.")).toBeVisible();
      await record("photographs-arrange-selected", async (g) => {
        await expect(g.page.getByText("Photograph 2 is selected.")).toBeVisible();
      });
      await tap(guest.retake);
      await expect(guest.accept).toBeVisible({ timeout: 40_000 });
      await record("photographs-retake-candidate", async (g) => {
        await expect(g.accept).toBeVisible();
      });
      await tap(guest.keepOriginal);
      await expect(guest.accept).toHaveCount(0);
      await tap(guest.startOver);
      await record("start-over-question", async (g) => {
        await expect(g.confirmDialog).toBeVisible();
      });
      await tap(guest.keepGoing());
      await expect(guest.confirmDialog).toHaveCount(0);
    });

    await test.step("Filters, and Operator Assistance when the camera fails there", async () => {
      await tap(guest.continueToFilters);
      await run.waitForStep("filters");
      await record("filters", async (g) => {
        await expect(g.surface("filters")).toBeVisible();
        await expect(g.finishStrip).not.toHaveAttribute("aria-disabled", "true");
      });
      await hooks.injectCameraFault("failed");
      await run.waitForSurface("operatorAssistance");
      await record("operator-assistance", async (g) => {
        await expect(g.surface("operatorAssistance")).toBeVisible();
      });
      await hooks.clearCameraFault();
      await expect.poll(async () => (await hooks.getDiagnostics()).camera.state, { timeout: 20_000 }).toBe("ready");
      await tap(guest.retry);
      await run.waitForSurface("filters", 20_000);
    });

    await test.step("Keepsake", async () => {
      await tap(guest.finishStrip);
      await expect(guest.surface("keepsake")).toBeVisible({ timeout: 30_000 });
      await record("keepsake", async (g) => {
        await expect(g.surface("keepsake")).toBeVisible();
        await expect(g.page.getByRole("img", { name: "Your Photo Strip" })).toBeVisible();
        await g.page.waitForTimeout(800);
      });
    });

    await hooks.setWindowBounds("guest", original.width, original.height);
    await run.endEventFromOperator();
  },
);
