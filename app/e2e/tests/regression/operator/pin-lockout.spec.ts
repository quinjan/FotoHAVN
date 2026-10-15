import { expect, test } from "../../../src/fixtures";
import { sendCommand } from "../../../src/hooks";
import { createEvent, exitHoldMs, expectEventEnded, guestPage, holdKeyFor, useVirtualScreens } from "../../../src/operator";
import { EventListScreen, GuestStartScreen, PinDialogScreen } from "../../../src/screens/screens";

const pin = "4821";
const wrongPin = "0000";
const maxWrongAttempts = 5;

test.describe("the PIN lockout", { tag: ["@m6", "@camera"] }, () => {
  test("five wrong PINs lock the pad with a countdown; the lockout ends on the brain clock, and the right PIN then ends the Event", async ({
    app,
    operatorPage,
    camera,
    hooks,
  }) => {
    await useVirtualScreens(app, 2);
    const list = new EventListScreen(operatorPage);
    const name = "Locked out";
    await createEvent({ app, page: operatorPage, camera }, { name, display: "Virtual guest", pin }, "saveAndStart");

    const guest = await guestPage(app);
    const start = new GuestStartScreen(guest);
    const dialog = new PinDialogScreen(guest);

    try {
      await test.step("a held Enter key on the focused exit control opens the PIN dialog (the keyboard hold)", async () => {
        await expect(start.exitHold).toBeVisible();
        await holdKeyFor(start.exitHold, "Enter", exitHoldMs);
        await expect(dialog.dialog).toBeVisible();
      });

      await test.step("each wrong PIN says so and counts the tries left", async () => {
        for (let attempt = 1; attempt < maxWrongAttempts; attempt += 1) {
          await dialog.submitPin(wrongPin);
          await expect(dialog.wrongPin).toBeVisible();
          const left = maxWrongAttempts - attempt;
          await expect(dialog.triesLeft).toHaveText(left === 1 ? "1 try left." : `${String(left)} tries left.`);
        }
      });

      await test.step("the fifth wrong PIN locks the pad with a visible countdown", async () => {
        await dialog.submitPin(wrongPin);
        await expect(dialog.locked).toBeVisible();
        await expect(dialog.countdown).toBeVisible();
        const seconds = Number(/(\d+)/.exec((await dialog.countdown.textContent()) ?? "")?.[1]);
        expect(seconds).toBeGreaterThan(20);
        expect(seconds).toBeLessThanOrEqual(30);
        await expect(dialog.triesLeft).toHaveCount(0);
      });

      await test.step("a sixth submission is blocked on the pad and rejected pinLocked by the brain", async () => {
        await expect(dialog.submit).toHaveAttribute("aria-disabled", "true");
        await expect(dialog.key("1")).toHaveAttribute("aria-disabled", "true");
        expect(await sendCommand(guest, "SubmitPin", { digits: pin })).toEqual({ status: "rejected", reason: "pinLocked" });
        expect((await hooks.getDiagnostics()).guestScreenOpen).toBe(true);
        await expect(dialog.locked).toBeVisible();
      });

      await test.step("at a 10× clock the lockout ends within a few seconds, and the pad works again", async () => {
        const started = Date.now();
        await hooks.setClockScale(10);
        await expect(dialog.locked).toHaveCount(0, { timeout: 8_000 });
        const waited = Date.now() - started;
        console.log(`the lockout ended after ${String(waited)} ms at a 10x clock`);
        expect(waited).toBeLessThan(6_000);
        await expect(dialog.countdown).toHaveCount(0);
        await expect(dialog.wrongPin).toHaveCount(0);
        await expect(dialog.key("1")).not.toHaveAttribute("aria-disabled", "true");
      });

      await test.step("the right PIN ends the Event", async () => {
        await dialog.submitPin(pin);
        await expectEventEnded(app);
        await expect.poll(() => guest.isClosed()).toBe(true);
        await expect(list.runningPanel()).toHaveCount(0);
      });
    } finally {
      await hooks.setClockScale(1);
    }
    expect((await hooks.getDiagnostics()).clockScale).toBe(1);
  });
});
