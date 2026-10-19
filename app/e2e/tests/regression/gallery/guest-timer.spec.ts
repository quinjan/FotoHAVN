import { expect, test } from "../../../src/fixtures";
import { GuestGalleryScreen, completeCycle, guestGallery } from "../../../src/gallery";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

test(
  "with clock scaling, the guest gallery shows its Phase Timer all along, fills it in the last 10 s with no MORE TIME, returns to Start when it runs out, and browsing doesn't reset it",
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
      await test.step("the timer starts when the gallery opens, and its pill shows the time left, quiet", async () => {
        await guest.open();
        const timer = (await guestGallery(hooks))?.timer;
        expect(timer?.phase).not.toBeNull();
        opened = timer?.remainingMs ?? 0;
        expect(opened).toBeGreaterThan(60_000);
        expect(opened).toBeLessThanOrEqual(120_000);
        await expect(guest.timer).toBeVisible();
        await expect(guest.timer).toHaveText(/^\d+:\d\d$/);
        await expect(guest.timer).not.toHaveAttribute("data-low");
        await expect(guest.page.getByRole("button", { name: "MORE TIME", exact: true })).toHaveCount(0);
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

      await test.step("in its last 10 s the pill fills and says so once, with no MORE TIME", async () => {
        // Scale 10 until the last 10 s are 15 s (real) away, then real time, so the checks below see them.
        await expect
          .poll(async () => (await guestGallery(hooks))?.timer.remainingMs ?? Infinity, { timeout: 30_000, intervals: [50] })
          .toBeLessThanOrEqual(25_000);
        await hooks.setClockScale(1);
        await expect(guest.timer).not.toHaveAttribute("data-low");
        await expect(guest.timer).toHaveAttribute("data-low", "true", { timeout: 30_000 });
        expect((await guestGallery(hooks))?.timer.remainingMs ?? Infinity).toBeLessThanOrEqual(10_999);
        await expect(guest.page.locator("[role='status']").filter({ hasText: /^\d+ seconds? left\.$/ })).toHaveCount(1);
        await expect(guest.page.getByRole("button", { name: "MORE TIME", exact: true })).toHaveCount(0);
        await hooks.setClockScale(10);
      });
      await test.step("when the time runs out the Guest Screen returns to Start and the gallery is closed", async () => {
        await expect((await run.controls()).surface("start")).toBeVisible({ timeout: 30_000 });
        expect(await guestGallery(hooks)).toBeNull();
        expect(await run.surface()).toBe("start");
        await expect(guest.timer).toHaveCount(0);
        await expect(guest.link).toBeVisible();
      });
    } finally {
      await hooks.setClockScale(1);
    }

    await run.endEventFromOperator();
  },
);
