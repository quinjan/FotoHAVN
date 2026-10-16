import type { Page } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

/** Counts the polite announcements the Phase Timer warning makes: each time its status region gets text. */
async function recordAnnouncements(page: Page): Promise<void> {
  await page.evaluate(() => {
    const announcements: string[] = [];
    (window as unknown as { __warningAnnouncements: string[] }).__warningAnnouncements = announcements;
    new MutationObserver(() => {
      for (const region of Array.from(document.querySelectorAll<HTMLElement>("[data-phase] [role='status']"))) {
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
  return page.evaluate(() => (window as unknown as { __warningAnnouncements: string[] }).__warningAnnouncements.slice());
}

test(
  "the warning shows in the last 20 s and MORE TIME restarts the phase, announced once; the Keepsake never warns",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera, hooks }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent(
      { app, page: operatorPage, camera },
      {
        name: "More time",
        countdown: 3,
        timers: { Template: 40, "Photographs: capture": 300, "Photographs: review": 300, Filters: 300, Keepsake: 30 },
      },
    );
    const guest = await run.expectStartReady();
    await recordAnnouncements(guest.page);

    await hooks.setClockScale(5);
    try {
      await test.step("on the Template step the warning appears once 20 s of the 40 s are left, and says so once", async () => {
        await tap(guest.stepInside);
        await expect(guest.surface("template")).toBeVisible();
        await expect(guest.warning).toHaveCount(0);
        await expect.poll(async () => (await run.cycle())?.timer.warning, { timeout: 30_000 }).toBe(true);
        await expect(guest.warning).toBeVisible();
        await expect(guest.warning).toContainText(/\d+ SECONDS? LEFT/);
        const cycle = await run.cycle();
        expect(cycle?.timer.phase).toBe("template");
        expect(cycle?.timer.remainingMs ?? Infinity).toBeLessThanOrEqual(20_000);
        await expect.poll(() => announcements(guest.page)).toHaveLength(1);
        expect((await announcements(guest.page))[0]).toMatch(/^\d+ seconds? left\. Choose MORE TIME/);
      });

      await test.step("MORE TIME restarts the phase's full time and the warning goes away", async () => {
        await tap(guest.moreTime);
        await expect(guest.warning).toHaveCount(0);
        await expect
          .poll(async () => (await run.cycle())?.timer, { message: "the timer restarted" })
          .toMatchObject({ phase: "template", warning: false });
        expect((await run.cycle())?.timer.remainingMs ?? 0).toBeGreaterThan(30_000);
        expect(await announcements(guest.page)).toHaveLength(1);
        await expect(guest.surface("template")).toBeVisible();
      });

      await test.step("it warns again at the next 20 s, with one more announcement", async () => {
        await expect.poll(async () => (await run.cycle())?.timer.warning, { timeout: 30_000 }).toBe(true);
        await expect(guest.warning).toBeVisible();
        await expect.poll(() => announcements(guest.page)).toHaveLength(2);
        await tap(guest.templateContinue);
        await run.waitForStep("photographs", "camera");
        await expect(guest.warning).toHaveCount(0);
      });

      await test.step("finish the cycle; the Keepsake shows no warning and no MORE TIME, even in its last 20 s", async () => {
        await run.takePhotographsUntil(guest, 4);
        await run.waitForStep("photographs", "arrange");
        await tap(guest.continueToFilters);
        await run.waitForStep("filters");
        await tap(guest.finishStrip);
        await expect(guest.surface("keepsake")).toBeVisible({ timeout: 30_000 });
        expect((await run.cycle())?.timer).toMatchObject({ phase: "keepsake", warning: false });
        await expect.poll(async () => (await run.cycle())?.timer.remainingMs ?? 99_999, { timeout: 30_000 }).toBeLessThan(15_000);
        await expect(guest.moreTime).toHaveCount(0);
        await expect(guest.warning).toHaveCount(0);
        await expect(guest.startOver).toHaveCount(0);
        expect((await run.cycle())?.timer.warning).toBe(false);
        await expect(guest.surface("start")).toBeVisible({ timeout: 30_000 });
      });
    } finally {
      await hooks.setClockScale(1);
    }

    expect((await announcements(guest.page)).length, "no announcement after the Template's two warnings").toBe(2);
    await run.endEventFromOperator();
  },
);
