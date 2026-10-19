import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, tap, tapEvenIfUnavailable } from "../../../src/guest-cycle";

test(
  "no way back: no START OVER, MORE TIME, or stop on any step, the step progress only shows where the guest is, and a tap on the running preview doesn't stop the sequence",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "No way back", countdown: 3, timers: { Keepsake: 5 } });
    const guest = await run.expectStartReady();

    await test.step("Template: the step progress has no controls, and nothing offers to start over or add time", async () => {
      await tap(guest.stepInside);
      await expect(guest.surface("template")).toBeVisible();
      await expect(guest.removedControls).toHaveCount(0);
      await expect(guest.step("Template")).toHaveAttribute("aria-current", "step");
    });

    await test.step("Photographs: a tap on the completed Template step does nothing", async () => {
      await tap(guest.templateContinue);
      await run.waitForStep("photographs", "camera");
      await expect(guest.removedControls).toHaveCount(0);
      await tapEvenIfUnavailable(guest.step("Template"));
      await guest.page.waitForTimeout(1_000);
      expect((await run.cycle())?.step).toBe("photographs");
      await expect(guest.surface("photographs")).toBeVisible();
    });

    await test.step("tap to start, then a tap while the sequence runs is not a stop: every window fills", async () => {
      await expect(guest.tapToStart).toBeVisible();
      await tap(guest.takePhotographs);
      await expect.poll(async () => (await run.cycle())?.sequenceRunning, { timeout: 15_000 }).toBe(true);
      await expect(guest.takePhotographs).toHaveAttribute("aria-disabled", "true");
      await expect(guest.tapToStart).toHaveCount(0);
      await tapEvenIfUnavailable(guest.takePhotographs);
      await expect(guest.removedControls).toHaveCount(0);
      await run.waitForFilled(4, 90_000);
      await run.waitForStep("photographs", "arrange");
      expect(await guest.arrangement()).toEqual([1, 2, 3, 4]);
    });

    await test.step("Arrange and Filters: the step progress still leads nowhere back", async () => {
      await tapEvenIfUnavailable(guest.step("Template"));
      await guest.page.waitForTimeout(1_000);
      expect((await run.cycle())?.tab).toBe("arrange");
      await tap(guest.continueToFilters);
      await run.waitForStep("filters");
      await tapEvenIfUnavailable(guest.step("Photographs"));
      await guest.page.waitForTimeout(1_000);
      expect((await run.cycle())?.step).toBe("filters");
      await expect(guest.removedControls).toHaveCount(0);
    });

    await test.step("the cycle finishes as a guest-finished one", async () => {
      await tap(guest.finishStrip);
      await expect(guest.surface("keepsake")).toBeVisible({ timeout: 30_000 });
      await expect(guest.removedControls).toHaveCount(0);
      const cycle = await run.endedCycle("completed", 30_000);
      expect(run.captures(cycle.id).map((capture) => `${String(capture.sequence)}:${capture.state}`)).toEqual(["1:kept", "2:kept", "3:kept", "4:kept"]);
      run.expectPhotoStrip(cycle.id, "Signature Strip");
    });

    await run.endEventFromOperator();
  },
);
