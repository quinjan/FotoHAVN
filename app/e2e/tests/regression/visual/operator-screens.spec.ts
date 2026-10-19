import { mkdirSync } from "node:fs";
import path from "node:path";
import type { Page, TestInfo } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { resolutionName, resolutions } from "../../../src/gallery";
import type { HookClient } from "../../../src/hooks";
import { createEvent, endEventFromOperator, useVirtualScreens } from "../../../src/operator";
import { paths } from "../../../src/paths";
import { seedOperatorEvents } from "../../../src/seed";
import { EventListScreen, EventSetupScreen } from "../../../src/screens/screens";

/** Where the operator screenshots go: a visual record for review against DESIGN.md, not a pixel comparison. */
const shotsDir = path.join(paths.e2eOutput, "operator-screens");

/** Every visible control meets the Operator Screen's 48 × 48 CSS px minimum (the same measure as the fixture lane). */
async function smallTargets(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const controls = document.querySelectorAll<HTMLElement>(
      "main button, main a[href], main input, main select, main textarea, main [role='button'], main [tabindex]:not([tabindex='-1'])",
    );
    return Array.from(controls)
      .map((control) => ({ control, box: control.getBoundingClientRect() }))
      .filter(({ box }) => box.width > 0 && box.height > 0)
      .filter(({ box }) => box.width + 0.5 < 48 || box.height + 0.5 < 48)
      .map(({ control, box }) => `${(control.getAttribute("aria-label") ?? control.textContent).trim().slice(0, 40)} ${String(Math.round(box.width))}x${String(Math.round(box.height))}`);
  });
}

async function scrollsHorizontally(page: Page): Promise<boolean> {
  return page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth || document.body.scrollWidth > document.body.clientWidth,
  );
}

/** Each of the four resolutions: size the real Operator window, wait for the layout, check it, and save a screenshot. */
async function recordAtEveryResolution(
  page: Page,
  hooks: HookClient,
  testInfo: TestInfo,
  state: string,
  ready: () => Promise<void>,
): Promise<void> {
  mkdirSync(shotsDir, { recursive: true });
  for (const resolution of resolutions) {
    const name = resolutionName(resolution);
    await test.step(`${state} at ${name}`, async () => {
      await hooks.setWindowBounds("operator", resolution.width, resolution.height);
      await expect
        .poll(() => page.evaluate(() => Math.round(window.innerWidth * window.devicePixelRatio)), { message: `the window is ${name} wide` })
        .toBe(resolution.width);
      await ready();
      await page.evaluate(() => document.fonts.ready);
      await page.waitForTimeout(250);
      expect(await scrollsHorizontally(page), `${state} at ${name} scrolls sideways`).toBe(false);
      expect(await smallTargets(page), `${state} at ${name}: controls under 48 × 48`).toEqual([]);
      const file = path.join(shotsDir, `${state}-${name}.png`);
      await page.screenshot({ path: file, fullPage: true });
      await testInfo.attach(`${state}-${name}`, { path: file, contentType: "image/png" });
    });
  }
}

test.describe("operator screens in the real host: empty Event list and Event setup", { tag: ["@m6"] }, () => {
  test("the empty Event list, a new Event's setup, and setup with errors fit the four resolutions", async ({ app, operatorPage, hooks }, testInfo) => {
    await useVirtualScreens(app, 2);
    const list = new EventListScreen(operatorPage);
    const setup = new EventSetupScreen(operatorPage);

    await recordAtEveryResolution(operatorPage, hooks, testInfo, "list-empty", async () => {
      await expect(list.empty).toBeVisible();
    });

    await list.newEvent.click();
    await recordAtEveryResolution(operatorPage, hooks, testInfo, "setup-new", async () => {
      await expect(setup.heading).toBeVisible();
      await expect(setup.name).toBeVisible();
    });

    await setup.goTo("Templates");
    await recordAtEveryResolution(operatorPage, hooks, testInfo, "setup-templates", async () => {
      await expect(setup.templatePreview("Signature Strip")).toHaveCount(1);
      await expect(setup.templatePreview("Signature Strip")).toBeVisible();
    });

    await setup.goTo("Guests");
    await setup.save.click();
    await expect(setup.statusLine(/need attention/).first()).toBeVisible();
    await recordAtEveryResolution(operatorPage, hooks, testInfo, "setup-errors", async () => {
      await expect(setup.name).toHaveAccessibleDescription(/Name the Event\./);
      await expect(setup.statusLine(/need attention/).first()).toBeVisible();
    });

    await setup.back.click();
    await expect(list.main).toBeVisible();
  });
});

test.describe("operator screens in the real host: Event list with Events and a running Event", { tag: ["@m6", "@camera"] }, () => {
  test.use({ appOptions: { seed: seedOperatorEvents } });

  test("the Event list with Events, and with the running-Event panel, fit the four resolutions", async ({ app, operatorPage, camera, hooks }, testInfo) => {
    const screens = await useVirtualScreens(app, 2);
    const list = new EventListScreen(operatorPage);

    await recordAtEveryResolution(operatorPage, hooks, testInfo, "list-with-events", async () => {
      await expect(list.rows).toHaveCount(3);
    });

    const name = "Running now";
    await createEvent({ app, page: operatorPage, camera }, { name, venue: "Harbour Hall", display: screens.guest?.name ?? "", pin: "4821" }, "saveAndStart");
    await recordAtEveryResolution(operatorPage, hooks, testInfo, "list-running", async () => {
      await expect(list.runningPanel(name)).toBeVisible();
      await expect(list.rows).toHaveCount(4);
    });

    await endEventFromOperator(app, operatorPage);
  });
});
