import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

test(
  "the running-Event panel follows the cycle: its step and camera state, and the completed count rising when the cycle finishes",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Panel live", countdown: 3 });
    const guest = await run.expectStartReady();
    const { list } = run;

    await test.step("on Start the panel says Start, camera Ready, and no completed cycle", async () => {
      await expect(list.runningFact("step")).toHaveText("Start");
      await expect(list.runningFact("camera")).toHaveText("Ready");
      await expect(list.runningFact("cycles")).toHaveText("0");
    });

    await test.step("Template, then Photographs while the photographs are taken, with the camera Ready", async () => {
      await tap(guest.stepInside);
      await expect(list.runningFact("step")).toHaveText("Template");
      await tap(guest.templateContinue);
      await expect(list.runningFact("step")).toHaveText("Photographs");
      await run.takePhotographsUntil(guest, 4);
      await run.waitForStep("photographs", "arrange");
      await expect(list.runningFact("step")).toHaveText("Photographs");
      await expect(list.runningFact("camera")).toHaveText("Ready");
      await expect(list.runningFact("cycles")).toHaveText("0");
    });

    await test.step("Filters, then Keepsake: the completed count rises as the Photo Strip is saved", async () => {
      await tap(guest.continueToFilters);
      await expect(list.runningFact("step")).toHaveText("Filters");
      await expect(list.runningFact("cycles")).toHaveText("0");
      await tap(guest.finishStrip);
      await expect(list.runningFact("step")).toHaveText("Keepsake");
      await expect(list.runningFact("cycles")).toHaveText("1");
      await expect(list.runningFact("camera")).toHaveText("Ready");
    });

    await test.step("when the Keepsake ends, the panel is back on Start with the count kept", async () => {
      await expect(guest.surface("start")).toBeVisible({ timeout: 45_000 });
      await expect(list.runningFact("step")).toHaveText("Start");
      await expect(list.runningFact("cycles")).toHaveText("1");
    });

    await run.endEventFromOperator();
  },
);
