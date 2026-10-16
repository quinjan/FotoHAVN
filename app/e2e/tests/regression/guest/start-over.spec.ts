import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

test(
  "Start Over from Template, Photographs, and Filters ends the cycle as started_over, keeps the saved Captures, and makes no Photo Strip",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Start over", countdown: 3 });
    const guest = await run.expectStartReady();

    await test.step("Template: START OVER asks, KEEP GOING stays, and the confirmed START OVER returns to Start", async () => {
      await tap(guest.stepInside);
      await expect(guest.surface("template")).toBeVisible();
      await tap(guest.startOver);
      await expect(guest.confirmDialog).toBeVisible();
      await expect(guest.confirmDialog).toContainText("Start over?");
      await expect(guest.confirmDialog).toContainText("Your photographs stay with the Event, but no strip will be made.");
      await tap(guest.keepGoing());
      await expect(guest.confirmDialog).toHaveCount(0);
      expect((await run.cycle())?.step).toBe("template");

      await tap(guest.startOver);
      await tap(guest.confirmStartOver());
      await expect(guest.surface("start")).toBeVisible();
      expect(await run.cycle()).toBeNull();
    });

    await test.step("Photographs: one photograph is taken, then START OVER keeps it", async () => {
      await tap(guest.stepInside);
      await tap(guest.templateContinue);
      await run.waitForStep("photographs", "camera");
      await run.takePhotographsUntil(guest, 1);
      await run.stopCapture(guest);
      await tap(guest.startOver);
      await tap(guest.confirmStartOver());
      await expect(guest.surface("start")).toBeVisible();
    });

    await test.step("Filters: three photographs on Butter Gingham, then START OVER keeps them", async () => {
      await tap(guest.stepInside);
      await tap(guest.option("Butter Gingham"));
      await expect(guest.page.locator("[data-selected-template]")).toHaveAttribute("data-selected-template", "butter-gingham");
      await tap(guest.templateContinue);
      await run.waitForStep("photographs", "camera");
      await run.takePhotographsUntil(guest, 3);
      await run.waitForStep("photographs", "arrange");
      await tap(guest.continueToFilters);
      await run.waitForStep("filters");
      await tap(guest.startOver);
      await tap(guest.confirmStartOver());
      await expect(guest.surface("start")).toBeVisible();
    });

    await test.step("all three cycles ended as started_over; the saved Captures stay; no Photo Strip exists", async () => {
      await expect.poll(async () => (await run.cycles()).map((cycle) => cycle.end_reason)).toEqual(["started_over", "started_over", "started_over"]);
      const cycles = await run.cycles();
      expect(cycles.map((cycle) => run.captures(cycle.id).filter((capture) => capture.state === "kept").length)).toEqual([0, 1, 3]);
      for (const cycle of cycles) {
        expect(cycle.ended_at).not.toBeNull();
        expect(run.strip(cycle.id)).toBeUndefined();
        expect(run.hasStripFile(cycle.id)).toBe(false);
        run.expectCaptureFiles(cycle.id);
        run.expectNoStrayFiles(cycle.id);
      }
      await expect(run.list.runningFact("cycles")).toHaveText("0");
    });

    await run.endEventFromOperator();
  },
);
