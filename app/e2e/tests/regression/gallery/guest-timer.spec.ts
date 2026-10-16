import { expect, test } from "../../../src/fixtures";
import { GuestGalleryScreen, completeCycle, guestGallery } from "../../../src/gallery";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

test(
  "with clock scaling, the guest gallery returns to Start after its timer and warning, and browsing doesn't reset the timer",
  { tag: ["@m9", "@camera"] },
  async ({ app, operatorPage, camera, hooks }) => {
    test.setTimeout(300_000);
    const run = await startGuestEvent(
      { app, page: operatorPage, camera },
      { name: "Gallery timer", countdown: 3, guestGallery: true, timers: { Keepsake: 5, "Guest gallery view": 120 } },
    );
    await completeCycle(run);
    const guest = new GuestGalleryScreen(await run.guest());
    await expect(guest.link).toBeVisible();

    await hooks.setClockScale(10);
    try {
      let opened = 0;
      await test.step("the timer starts when the gallery opens, and has no warning yet", async () => {
        await guest.open();
        const timer = (await guestGallery(hooks))?.timer;
        expect(timer?.phase).not.toBeNull();
        expect(timer?.warning).toBe(false);
        opened = timer?.remainingMs ?? 0;
        expect(opened).toBeGreaterThan(60_000);
        expect(opened).toBeLessThanOrEqual(120_000);
        await expect(guest.warning).toHaveCount(0);
      });

      await test.step("opening and closing a strip keeps the same timer running down, never restarting it", async () => {
        const samples = [opened];
        await tap(guest.strips.first());
        await expect(guest.detail).toBeVisible();
        samples.push((await guestGallery(hooks))?.timer.remainingMs ?? Number.NaN);
        await tap(guest.close);
        await expect(guest.list).toBeVisible();
        samples.push((await guestGallery(hooks))?.timer.remainingMs ?? Number.NaN);
        expect(samples.every((remaining) => remaining > 0 && remaining <= opened), `the timer never restarts: ${samples.join(", ")}`).toBe(true);
        for (let index = 1; index < samples.length; index += 1) {
          expect(samples[index], `remainingMs after step ${String(index)}: ${samples.join(", ")}`).toBeLessThan(samples[index - 1] ?? 0);
        }
      });

      await test.step("in its last 20 s the gallery warns, with the countdown and MORE TIME", async () => {
        // Scale 10 until the warning is 15 s (real) away, then real time, so the 20 s warning lasts 20 real seconds for the checks below.
        await expect
          .poll(async () => (await guestGallery(hooks))?.timer.remainingMs ?? Infinity, { timeout: 30_000, intervals: [50] })
          .toBeLessThanOrEqual(35_000);
        await hooks.setClockScale(1);
        expect((await guestGallery(hooks))?.timer.warning, "the warning has not begun yet").toBe(false);
        await expect.poll(async () => (await guestGallery(hooks))?.timer.warning, { timeout: 30_000 }).toBe(true);
        await expect(guest.warning).toBeVisible();
        await expect(guest.warning).toContainText(/\d+ SECONDS? LEFT/);
        await expect(guest.warning.getByRole("button", { name: "MORE TIME", exact: true })).toBeVisible();
        const timer = (await guestGallery(hooks))?.timer;
        expect(timer?.warning).toBe(true);
        expect(timer?.remainingMs ?? Infinity).toBeLessThanOrEqual(20_000);
        await hooks.setClockScale(10);
      });
      await test.step("when the time runs out the Guest Screen returns to Start and the gallery is closed", async () => {
        await expect((await run.controls()).surface("start")).toBeVisible({ timeout: 30_000 });
        expect(await guestGallery(hooks)).toBeNull();
        expect(await run.surface()).toBe("start");
        await expect(guest.warning).toHaveCount(0);
        await expect(guest.link).toBeVisible();
      });
    } finally {
      await hooks.setClockScale(1);
    }

    await run.endEventFromOperator();
  },
);
