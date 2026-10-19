import type { Page } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

/** Counts the Phase Timer's polite announcements: each time its status region (the pill's sibling) gets text. */
async function recordAnnouncements(page: Page): Promise<void> {
  await page.evaluate(() => {
    const announcements: string[] = [];
    (window as unknown as { __timerAnnouncements: string[] }).__timerAnnouncements = announcements;
    new MutationObserver(() => {
      for (const region of Array.from(document.querySelectorAll<HTMLElement>("[role='timer'] + [role='status']"))) {
        const text = region.textContent ?? "";
        if (text !== "" && !(region as HTMLElement & { __seen?: boolean }).__seen) {
          (region as HTMLElement & { __seen?: boolean }).__seen = true;
          announcements.push(text);
        }
      }
    }).observe(document.body, { subtree: true, childList: true, characterData: true });
  });
}

function announcements(page: Page): Promise<string[]> {
  return page.evaluate(() => (window as unknown as { __timerAnnouncements: string[] }).__timerAnnouncements.slice());
}

test(
  "the Phase Timer shows on every timed step from its start, fills in its last 10 s and says so once, can't be extended, and running out carries the cycle on",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera, hooks }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent(
      { app, page: operatorPage, camera },
      {
        name: "Phase Timer",
        countdown: 3,
        timers: { Template: 40, "Photographs: capture": 300, "Photographs: review": 300, Filters: 300, Keepsake: 30 },
      },
    );
    const guest = await run.expectStartReady();
    await recordAnnouncements(guest.page);

    await hooks.setClockScale(5);
    try {
      await test.step("on the Template step the pill shows the time left from the start, in the top band, quiet, with no MORE TIME", async () => {
        await tap(guest.stepInside);
        await expect(guest.surface("template")).toBeVisible();
        await expect(guest.phaseTimer).toBeVisible();
        await expect(guest.phaseTimer).toHaveText(/^0:\d\d$/);
        await expect(guest.phaseTimer).not.toHaveAttribute("data-low");
        expect(await guest.phaseTimer.evaluate((element) => element.closest("header") !== null)).toBe(true);
        await expect(guest.removedControls).toHaveCount(0);
        expect((await run.cycle())?.timer.phase).toBe("template");
        expect(await announcements(guest.page)).toEqual([]);
      });

      await test.step("in its last 10 s the pill fills and says so once", async () => {
        await expect.poll(async () => (await run.cycle())?.timer.remainingMs ?? Infinity, { timeout: 30_000 }).toBeLessThanOrEqual(10_000);
        await expect(guest.phaseTimer).toHaveAttribute("data-low", "true");
        await expect.poll(() => announcements(guest.page)).toHaveLength(1);
        expect((await announcements(guest.page))[0]).toMatch(/^\d+ seconds? left\.$/);
        await expect(guest.removedControls).toHaveCount(0);
      });

      await test.step("when the Template's time runs out the cycle moves on with the selected Template, and the capture phase's pill starts quiet", async () => {
        await run.waitForStep("photographs", "camera", 30_000);
        await expect.poll(async () => (await run.cycle())?.timer.phase).toBe("photographsCapture");
        await expect(guest.phaseTimer).toBeVisible();
        await expect(guest.phaseTimer).not.toHaveAttribute("data-low");
        expect(await announcements(guest.page)).toHaveLength(1);
      });

      await test.step("Arrange and Filters show their own timers; the Keepsake shows its pill too, fills, and returns to Start", async () => {
        await run.takePhotographsUntil(guest, 4);
        await run.waitForStep("photographs", "arrange");
        await expect.poll(async () => (await run.cycle())?.timer.phase).toBe("photographsReview");
        await expect(guest.phaseTimer).toBeVisible();
        await tap(guest.continueToFilters);
        await run.waitForStep("filters");
        await expect(guest.phaseTimer).toBeVisible();
        await tap(guest.finishStrip);
        await expect(guest.surface("keepsake")).toBeVisible({ timeout: 30_000 });
        await expect.poll(async () => (await run.cycle())?.timer.phase).toBe("keepsake");
        await expect(guest.phaseTimer).toBeVisible();
        await expect(guest.phaseTimer).toHaveAttribute("data-low", "true", { timeout: 30_000 });
        await expect(guest.removedControls).toHaveCount(0);
        await expect(guest.surface("start")).toBeVisible({ timeout: 30_000 });
      });
    } finally {
      await hooks.setClockScale(1);
    }

    expect(await announcements(guest.page), "one announcement each for the Template's and the Keepsake's last seconds").toHaveLength(2);
    await run.endedCycle("completed", 30_000);
    await run.endEventFromOperator();
  },
);
