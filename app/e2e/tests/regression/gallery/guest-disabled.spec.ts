import { expect, test } from "../../../src/fixtures";
import { GuestGalleryScreen, completeCycle, galleryDiagnostics } from "../../../src/gallery";
import { startGuestEvent } from "../../../src/guest-cycle";
import { sendCommand } from "../../../src/hooks";

test(
  "with the guest gallery off, the Start screen has no link, and the guest gallery surface can't be reached by any guest command",
  { tag: ["@m9", "@camera"] },
  async ({ app, operatorPage, camera, hooks }) => {
    test.setTimeout(300_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Gallery off", countdown: 3, timers: { Keepsake: 5 } });
    const cycle = await completeCycle(run);
    const guest = new GuestGalleryScreen(await run.guest());
    const controls = await run.expectStartReady();

    await test.step("a completed Photo Strip exists, yet the Start screen shows no gallery link", async () => {
      expect(run.strip(cycle.id)).toBeDefined();
      await expect(controls.surface("start")).toBeVisible();
      await expect(guest.link).toHaveCount(0);
      await expect(guest.page.getByText("GOOD COMPANY")).toHaveCount(0);
      await expect(guest.page.getByRole("button", { name: /GOOD COMPANY|GALLERY/i })).toHaveCount(0);
    });

    await test.step("every guest gallery command is refused as not available, and the Guest Screen stays on Start", async () => {
      const refused = { status: "rejected", reason: "notAvailable" };
      expect(await sendCommand(guest.page, "OpenGuestGallery", {})).toEqual(refused);
      expect(await sendCommand(guest.page, "OpenGuestStrip", { guestCycleId: cycle.id })).toEqual(refused);
      expect(await sendCommand(guest.page, "LoadMoreGuestStrips", { cursor: "anything" })).toEqual(refused);
      expect(await sendCommand(guest.page, "CloseGuestStrip", {})).toEqual(refused);
      await expect(controls.surface("start")).toBeVisible();
      expect(await run.surface()).toBe("start");
      expect(await galleryDiagnostics(hooks)).toBeNull();
      await expect(guest.link).toHaveCount(0);
    });

    await test.step("the operator's gallery commands are refused to the guest by role, and nothing opens", async () => {
      const wrongRole = { status: "rejected", reason: "wrongRole" };
      expect(await sendCommand(guest.page, "OpenGallery", { eventId: run.eventId })).toEqual(wrongRole);
      expect(await sendCommand(guest.page, "OpenGuestCycle", { guestCycleId: cycle.id })).toEqual(wrongRole);
      expect(await sendCommand(guest.page, "DeleteGuestCycle", { guestCycleId: cycle.id })).toEqual(wrongRole);
      expect(await galleryDiagnostics(hooks)).toBeNull();
      expect(await run.surface()).toBe("start");
    });

    await run.endEventFromOperator();
  },
);
