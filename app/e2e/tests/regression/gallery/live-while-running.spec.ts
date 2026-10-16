import { expect, test } from "../../../src/fixtures";
import { GuestGalleryScreen, completeCycle, openRunningGallery } from "../../../src/gallery";
import { startGuestEvent } from "../../../src/guest-cycle";

declare global {
  interface Window {
    __galleryDocument?: string;
  }
}

test(
  "while an Event runs, a cycle completed on the Guest Screen appears in the operator's gallery without reloading, and deleting it removes it from a guest gallery open at the same moment",
  { tag: ["@m9", "@camera"] },
  async ({ app, operatorPage, camera, hooks }) => {
    test.setTimeout(420_000);
    const run = await startGuestEvent(
      { app, page: operatorPage, camera },
      { name: "Live gallery", countdown: 3, guestGallery: true, timers: { Keepsake: 5 } },
    );
    const gallery = await openRunningGallery(run);
    const sameDocument = async () => operatorPage.evaluate(() => window.__galleryDocument);

    await test.step("the running Event's gallery is open and empty, on a document we can recognise", async () => {
      await expect(gallery.empty).toBeVisible();
      await expect(gallery.main.getByText("RUNNING", { exact: true })).toBeVisible();
      await operatorPage.evaluate(() => {
        window.__galleryDocument = "the document the gallery opened in";
      });
      expect(await sameDocument()).toBe("the document the gallery opened in");
    });

    await test.step("each cycle completed on the Guest Screen appears in the open list, with no reload", async () => {
      await completeCycle(run);
      await expect(gallery.items).toHaveCount(1);
      await expect(gallery.empty).toHaveCount(0);
      await completeCycle(run);
      await expect(gallery.items).toHaveCount(2);
      expect((await hooks.getDiagnostics()).gallery?.operator).toMatchObject({ eventId: run.eventId, itemCount: 2 });
      expect(await sameDocument(), "the operator page was reloaded").toBe("the document the gallery opened in");
    });

    const guest = new GuestGalleryScreen(await run.guest());
    await guest.open();
    await expect(guest.strips).toHaveCount(2);

    await test.step("deleting one cycle removes its strip from the guest gallery at the moment the operator's list drops it", async () => {
      await gallery.deleteItem(0);
      await expect
        .poll(
          async () => {
            const snapshot = (await hooks.getDiagnostics()).gallery;
            return snapshot?.operator?.itemCount === 1 ? `guest ${String(snapshot.guest?.stripCount)}` : "waiting";
          },
          { timeout: 15_000 },
        )
        .toBe("guest 1");
      await expect(gallery.items).toHaveCount(1);
      await expect(guest.strips).toHaveCount(1);
      await expect(guest.list).toBeVisible();
    });

    await test.step("deleting the last completed cycle returns the guest to Start in the same turn, with no link left", async () => {
      await gallery.deleteItem(0);
      await expect
        .poll(
          async () => {
            const snapshot = await hooks.getDiagnostics();
            if (snapshot.gallery?.operator?.itemCount !== 0) {
              return "waiting";
            }
            const surface = snapshot.presentations.find((presentation) => presentation.role === "guest")?.surface;
            return `guest ${String(snapshot.gallery.guest)} on ${String(surface)}`;
          },
          { timeout: 15_000 },
        )
        .toBe("guest null on start");
      await expect((await run.controls()).surface("start")).toBeVisible();
      await expect(guest.link).toHaveCount(0);
      await expect(gallery.empty).toBeVisible();
      expect(await sameDocument(), "the operator page was reloaded").toBe("the document the gallery opened in");
    });

    await gallery.backToEvents.click();
    await run.endEventFromOperator();
  },
);
