import type { Page } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

/** Records, from the page itself, whether the Arrange tab ever showed "Preparing your strip." with every photograph window unavailable. */
async function recordPreparing(page: Page): Promise<void> {
  await page.evaluate(() => {
    const record = { preparing: false, windowsDisabled: false };
    (window as unknown as { __preparing: typeof record }).__preparing = record;
    new MutationObserver(() => {
      const arrange = document.querySelector('[data-photographs-tab="arrange"]');
      if (arrange?.textContent?.includes("Preparing your strip.") === true) {
        record.preparing = true;
        const windows = Array.from(arrange.querySelectorAll<HTMLElement>('[role="group"] button'));
        record.windowsDisabled = windows.length > 0 && windows.every((window) => window.getAttribute("aria-disabled") === "true");
      }
    }).observe(document.body, { subtree: true, childList: true, characterData: true, attributes: true });
  });
}

test(
  "an Event with one Template and one Look skips both steps: Photographs follows STEP INSIDE and FINISH MY STRIP composes with the only Look",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent(
      { app, page: operatorPage, camera },
      { name: "Skipped steps", countdown: 3, templates: ["Panorama Four"], looks: ["Sepia"] },
    );
    const guest = await run.expectStartReady();
    await recordPreparing(guest.page);

    await test.step("STEP INSIDE lands on Photographs, and the step progress leaves out Template and Filters", async () => {
      await tap(guest.stepInside);
      await expect(guest.surface("photographs")).toBeVisible();
      await run.waitForStep("photographs", "camera");
      await expect(guest.surface("template")).toHaveCount(0);
      await expect(guest.stepList.getByRole("listitem")).toHaveText([/01\s*Photographs/, /02\s*Keepsake/]);
    });

    await test.step("the Arrange primary reads FINISH MY STRIP and Back has nowhere to go", async () => {
      await run.takePhotographsUntil(guest, 4);
      await run.waitForStep("photographs", "arrange");
      await expect(guest.finishStrip).toBeVisible();
      await expect(guest.page.getByRole("button", { name: /CONTINUE TO FILTERS/ })).toHaveCount(0);
      await expect(guest.stepList.getByRole("button")).toHaveCount(0);
    });

    await test.step("FINISH MY STRIP composes with the only Look: the windows go quiet, 'Preparing your strip.' shows, and the Keepsake follows", async () => {
      await tap(guest.finishStrip);
      await expect(guest.surface("keepsake")).toBeVisible({ timeout: 30_000 });
      const preparing = await guest.page.evaluate(() => (window as unknown as { __preparing: { preparing: boolean; windowsDisabled: boolean } }).__preparing);
      expect(preparing, "the compose state was shown on Arrange").toEqual({ preparing: true, windowsDisabled: true });
      await expect(guest.page.getByRole("img", { name: "Your Photo Strip" })).toBeVisible();
    });

    await test.step("the strip is on disk with the only Look, in the native size of Panorama Four", async () => {
      const cycle = await run.endedCycle("completed");
      const strip = run.expectPhotoStrip(cycle.id, "Panorama Four");
      expect(strip.template_id).toBe("panorama-four");
      expect(strip.look_id).toBe("sepia-v1");
      expect(run.captures(cycle.id).filter((capture) => capture.state === "kept")).toHaveLength(4);
      run.expectNoStrayFiles(cycle.id);
    });

    await run.endEventFromOperator();
  },
);
