import { existsSync } from "node:fs";
import { databaseFileName, queryOne } from "../../../src/data";
import { expect, test } from "../../../src/fixtures";
import { GuestGalleryScreen, OperatorGalleryScreen, captureIds, completeCycle, openRunningGallery, thumbnailFiles } from "../../../src/gallery";
import { startGuestEvent } from "../../../src/guest-cycle";
import path from "node:path";

test(
  "the operator deletes a Guest Cycle: it disappears from both views at once, and its folder and thumbnails are removed after cleanup",
  { tag: ["@m9", "@camera"] },
  async ({ app, operatorPage, camera, hooks, dataDir }) => {
    test.setTimeout(420_000);
    const run = await startGuestEvent(
      { app, page: operatorPage, camera },
      { name: "Delete probe", countdown: 3, guestGallery: true, timers: { Keepsake: 5 } },
    );
    const kept = await completeCycle(run);
    const doomed = await completeCycle(run);
    const doomedSources = [doomed.id, ...captureIds(dataDir, doomed.id)];
    const keptSources = [kept.id, ...captureIds(dataDir, kept.id)];

    const gallery = await openRunningGallery(run);
    const guest = new GuestGalleryScreen(await run.guest());
    await guest.open();

    await test.step("both views show both cycles, and their thumbnails are in the cache", async () => {
      await expect(gallery.items).toHaveCount(2);
      await expect(guest.strips).toHaveCount(2);
      await expect.poll(() => thumbnailFiles(dataDir, doomedSources).length, { message: "the cycle's thumbnails exist" }).toBeGreaterThan(0);
      expect(existsSync(run.cycleFolder(doomed.id))).toBe(true);
    });

    await test.step("DELETE and its confirmation remove the newest cycle from both views in the same turn", async () => {
      await gallery.deleteItem(0);
      // One diagnostics snapshot is one point in time: when the operator's list has dropped the cycle, the guest's row has too.
      await expect
        .poll(
          async () => {
            const snapshot = (await hooks.getDiagnostics()).gallery;
            return snapshot?.operator?.itemCount === 1 ? `guest ${String(snapshot.guest?.stripCount)}` : "waiting";
          },
          { timeout: 15_000, message: "the operator's list and the guest's row drop the cycle together" },
        )
        .toBe("guest 1");
      await expect(gallery.items).toHaveCount(1);
      await expect(guest.strips).toHaveCount(1);
      await expect(guest.list).toBeVisible();
      await expect(gallery.confirmDialog).toHaveCount(0);
    });

    await test.step("the cycle's folder, rows, and thumbnails are removed after cleanup; the other cycle keeps its own", async () => {
      await expect.poll(() => existsSync(run.cycleFolder(doomed.id)), { timeout: 60_000, message: "the cycle folder is removed" }).toBe(false);
      await expect.poll(() => thumbnailFiles(dataDir, doomedSources), { timeout: 60_000, message: "the cycle's thumbnails are removed" }).toEqual([]);
      await expect
        .poll(() => queryOne(path.join(dataDir, databaseFileName), "SELECT id FROM guest_cycles WHERE id = ?", doomed.id), { timeout: 60_000 })
        .toBeUndefined();
      expect(captureIds(dataDir, doomed.id)).toEqual([]);
      expect(existsSync(run.cycleFolder(kept.id))).toBe(true);
      expect(thumbnailFiles(dataDir, keptSources).length).toBeGreaterThan(0);
    });

    await new OperatorGalleryScreen(operatorPage).backToEvents.click();
    await run.endEventFromOperator();
  },
);
