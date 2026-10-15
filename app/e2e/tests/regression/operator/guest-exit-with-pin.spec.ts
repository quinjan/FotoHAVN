import { expect, test } from "../../../src/fixtures";
import { createEvent, exitHoldMs, expectEventEnded, guestPage, holdFor, holdMouseFor, useVirtualScreens } from "../../../src/operator";
import { EventListScreen, GuestStartScreen, PinDialogScreen } from "../../../src/screens/screens";

const pin = "4821";

test.describe("leaving a running Event from the Guest Screen", { tag: ["@m6"] }, () => {
  test("holding the exit control for 3 seconds opens the PIN pad, and the right PIN ends the Event exactly as END EVENT does", async ({
    app,
    operatorPage,
    camera,
    hooks,
  }) => {
    await useVirtualScreens(app, 2);
    const list = new EventListScreen(operatorPage);
    const name = "Exit by PIN";
    await createEvent({ app, page: operatorPage, camera }, { name, display: "Virtual guest", pin }, "saveAndStart");

    const guest = await guestPage(app);
    const start = new GuestStartScreen(guest);
    const dialog = new PinDialogScreen(guest);
    await expect(start.stepInside).toBeVisible();
    await expect(start.exitHold).toBeVisible();

    await test.step("a hold released after one second does nothing", async () => {
      await holdMouseFor(start.exitHold, 1_000);
      await expect(dialog.dialog).toHaveCount(0);
      await expect(start.stepInside).toBeVisible();
      expect((await hooks.getDiagnostics()).guestScreenOpen).toBe(true);
    });

    await test.step("a 3 second touch hold opens the Operator PIN dialog", async () => {
      await holdFor(start.exitHold, exitHoldMs);
      await expect(dialog.dialog).toBeVisible();
      await expect(dialog.dialog).toContainText("Enter the Event's Operator PIN to end the Event.");
      await expect(dialog.submit).toHaveAttribute("aria-disabled", "true");
    });

    await test.step("the right PIN on the pad ends the Event", async () => {
      await dialog.submitPin(pin);
      await expectEventEnded(app);
      await expect.poll(() => guest.isClosed()).toBe(true);
      const diagnostics = await hooks.getDiagnostics();
      expect(diagnostics.guestScreenOpen).toBe(false);
      expect(diagnostics.camera.state).toBe("idle");
      expect(diagnostics.wakeLock.held).toBe(false);
    });

    await test.step("the Operator Screen is back on the Event list without the running panel", async () => {
      await expect(list.main).toBeVisible();
      await expect(list.runningPanel()).toHaveCount(0);
      await expect(list.row(name)).toBeVisible();
      await expect(list.row(name)).not.toContainText("RUNNING");
      await expect(list.rowAction(name, "START")).not.toHaveAttribute("aria-disabled", "true");
    });
  });
});
