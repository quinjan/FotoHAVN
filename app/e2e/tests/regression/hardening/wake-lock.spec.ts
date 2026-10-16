import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, defaultPin } from "../../../src/guest-cycle";
import { expectEventEnded, holdFor, exitHoldMs } from "../../../src/operator";
import { GuestStartScreen, PinDialogScreen } from "../../../src/screens/screens";

test.describe("the wake lock", { tag: ["@m10", "@camera"] }, () => {
  test("is held exactly while an Event is active, named by its ID, and released by END EVENT and by the guest PIN exit", async ({
    app,
    operatorPage,
    camera,
    hooks,
  }) => {
    test.setTimeout(240_000);
    const context = { app, page: operatorPage, camera };

    await test.step("before any Event the lock is not held and has no reason", async () => {
      expect((await hooks.getDiagnostics()).wakeLock).toEqual({ held: false, reason: null });
    });

    await test.step("an Event started and ended with END EVENT holds the lock from its start to its end", async () => {
      const run = await startGuestEvent(context, { name: "Wake lock operator end", countdown: 3 });
      const diagnostics = await hooks.getDiagnostics();
      expect(diagnostics.wakeLock.held).toBe(true);
      expect(diagnostics.wakeLock.reason, "the reason names the Event by its ID").toBe(`FOTOHAVN Event ${run.eventId} is running`);
      expect(diagnostics.wakeLock.reason).not.toContain(run.name);
      expect(diagnostics.guestScreenOpen).toBe(true);

      await run.endEventFromOperator();
      await expectEventEnded(app);
      expect((await hooks.getDiagnostics()).wakeLock).toEqual({ held: false, reason: null });
    });

    await test.step("a second Event holds it again under its own ID, and the guest PIN exit releases it", async () => {
      const run = await startGuestEvent(context, { name: "Wake lock guest exit", countdown: 3 });
      const diagnostics = await hooks.getDiagnostics();
      expect(diagnostics.wakeLock).toEqual({ held: true, reason: `FOTOHAVN Event ${run.eventId} is running` });

      const guest = await run.guest();
      const start = new GuestStartScreen(guest);
      const dialog = new PinDialogScreen(guest);
      await expect(start.exitHold).toBeVisible();
      await holdFor(start.exitHold, exitHoldMs);
      await expect(dialog.dialog).toBeVisible();
      expect((await hooks.getDiagnostics()).wakeLock.held, "the lock stays held while the PIN pad is open").toBe(true);

      await dialog.submitPin(defaultPin);
      await expectEventEnded(app);
      expect((await hooks.getDiagnostics()).wakeLock).toEqual({ held: false, reason: null });
    });
  });
});
