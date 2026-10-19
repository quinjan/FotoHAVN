import { expect, test } from "../../src/fixtures";
import { GuestGalleryScreen, abandonedCycle, completeCycle, guestGallery, openRunningGallery, operatorGallery } from "../../src/gallery";
import { captureTestScreenshot } from "../../src/evidence";
import { startGuestEvent, tap } from "../../src/guest-cycle";

test(
  "after two completed cycles and one a guest walked away from (the Phase Timers finish its strip; there is no START OVER), the operator gallery shows three completed entries, and the guest view (enabled) shows all three strips",
  { tag: ["@m9", "@camera"] },
  async ({ app, operatorPage, camera, hooks }) => {
    test.setTimeout(420_000);
    const run = await startGuestEvent(
      { app, page: operatorPage, camera },
      { name: "Gallery after cycles", countdown: 3, guestGallery: true, timers: { Keepsake: 5 } },
    );

    await test.step("two guests finish a Photo Strip and a third walks away on the Template step", async () => {
      await completeCycle(run);
      await completeCycle(run);
      const abandoned = await abandonedCycle(run);
      expect(run.strip(abandoned.id)).toBeDefined();
      await expect(run.list.runningFact("cycles")).toHaveText("3");
    });

    const gallery = await openRunningGallery(run);
    await test.step("the operator gallery of the running Event lists all three, newest first, each completed", async () => {
      await expect(gallery.heading).toBeVisible();
      await expect(gallery.items).toHaveCount(3);
      expect(await operatorGallery(hooks)).toMatchObject({ eventId: run.eventId, itemCount: 3, nextCursor: null, detailId: null });
      const captions = await gallery.captions();
      expect(captions[0]).toContain("Completed");
      expect(captions[0]).toContain("Signature Strip");
      expect(captions[1]).toContain("Completed");
      expect(captions[1]).toContain("Signature Strip");
      expect(captions[2]).toContain("Completed");
      expect(captions[2]).toContain("Signature Strip");
      await expect(gallery.main.getByText("RUNNING", { exact: true })).toBeVisible();
      await captureTestScreenshot(operatorPage, test.info(), "operator-gallery");
    });

    await test.step("the Start screen offers the guest gallery, which holds all three completed strips", async () => {
      const guest = new GuestGalleryScreen(await run.guest());
      await expect(guest.link).toBeVisible();
      await guest.open();
      await expect(guest.strips).toHaveCount(3);
      await expect(guest.strips.first().getByRole("img", { name: "Photo Strip from this Event" })).toBeVisible();
      expect(await guestGallery(hooks)).toMatchObject({ stripCount: 3, nextCursor: null, detailId: null });
      await captureTestScreenshot(guest.page, test.info(), "guest-gallery");
      await tap(guest.backToStart);
      await expect(guest.link).toBeVisible();
    });

    await gallery.backToEvents.click();
    await run.endEventFromOperator();
  },
);
