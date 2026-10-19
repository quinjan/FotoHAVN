import { expect, test } from "../../../src/fixtures";
import { defaultPin, startGuestEvent, tap } from "../../../src/guest-cycle";
import { exitHoldMs, expectEventEnded, findDigitsInLogs, findDigitsInStore, holdFor } from "../../../src/operator";
import { PinDialogScreen } from "../../../src/screens/screens";

test.describe("leaving during a Guest Cycle", { tag: ["@m8", "@camera"] }, () => {
  test("END EVENT on the Operator Screen mid-cycle ends the cycle as operator_exit and keeps the Captures", async ({ app, operatorPage, camera }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "End by operator", countdown: 3 });
    const guest = await run.expectStartReady();

    await tap(guest.stepInside);
    await tap(guest.templateContinue);
    await run.waitForStep("photographs", "camera");
    // A sequence can't be stopped, so the cycle is left mid-way on the Arrange tab, before any Photo Strip.
    await run.takePhotographsUntil(guest, 4);
    await run.waitForStep("photographs", "arrange");

    await run.endEventFromOperator();

    const cycle = await run.endedCycle("operator_exit");
    expect(cycle.ended_at).not.toBeNull();
    expect(run.captures(cycle.id).map((capture) => `${String(capture.sequence)}:${capture.state}`)).toEqual(["1:kept", "2:kept", "3:kept", "4:kept"]);
    run.expectCaptureFiles(cycle.id);
    expect(run.strip(cycle.id)).toBeUndefined();
    expect(run.hasStripFile(cycle.id)).toBe(false);
  });

  test("the hold and PIN on the Guest Screen mid-cycle end it the same way, with the PIN nowhere on disk", async ({ app, operatorPage, camera, hooks, dataDir }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "End by PIN", countdown: 3 });
    const guest = await run.expectStartReady();
    const dialog = new PinDialogScreen(guest.page);

    await tap(guest.stepInside);
    await tap(guest.templateContinue);
    await run.waitForStep("photographs", "camera");
    await run.takePhotographsUntil(guest, 4);
    await run.waitForStep("photographs", "arrange");

    await test.step("a 3 second touch hold on the exit control opens the PIN pad over the cycle", async () => {
      await holdFor(guest.exitHold, exitHoldMs);
      await expect(dialog.dialog).toBeVisible();
      await expect(dialog.submit).toHaveAttribute("aria-disabled", "true");
      expect((await run.cycle())?.step).toBe("photographs");
    });

    await test.step("a wrong PIN is refused and the cycle goes on", async () => {
      for (const digit of "0000") {
        await tap(dialog.key(digit));
      }
      await tap(dialog.submit);
      await expect(dialog.wrongPin).toBeVisible();
      expect((await run.cycle())?.step).toBe("photographs");
    });

    await test.step("the right PIN ends the Event", async () => {
      for (const digit of defaultPin) {
        await tap(dialog.key(digit));
      }
      await tap(dialog.submit);
      await expectEventEnded(app);
    });

    const cycle = await run.endedCycle("operator_exit");
    expect(run.captures(cycle.id).map((capture) => `${String(capture.sequence)}:${capture.state}`)).toEqual(["1:kept", "2:kept", "3:kept", "4:kept"]);
    run.expectCaptureFiles(cycle.id);
    expect(run.strip(cycle.id)).toBeUndefined();
    expect(findDigitsInStore(dataDir, defaultPin)).toEqual([]);
    expect(findDigitsInLogs(dataDir, defaultPin)).toEqual([]);
    expect((await hooks.getDiagnostics()).guestScreenOpen).toBe(false);
  });
});
