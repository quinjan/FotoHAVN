import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

test(
  "going back to Template after Captures, Butter Gingham (3 windows) and back to a 4-window Template keep the Captures, and an empty window blocks continuing",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Template change", countdown: 3 });
    const guest = await run.expectStartReady();

    async function chooseTemplate(name: string, id: string): Promise<void> {
      await tap(guest.completedStep("Template"));
      await expect(guest.surface("template")).toBeVisible();
      await tap(guest.option(name));
      await expect(guest.page.locator("[data-selected-template]")).toHaveAttribute("data-selected-template", id);
      await tap(guest.templateContinue);
      await expect(guest.surface("photographs")).toBeVisible();
    }

    await test.step("two Captures on Signature Strip, then stop", async () => {
      await tap(guest.stepInside);
      await tap(guest.templateContinue);
      await run.waitForStep("photographs", "camera");
      await run.takePhotographsUntil(guest, 2);
      await run.stopCapture(guest);
      expect((await run.cycle())?.windowsFilled).toBe(2);
    });

    await test.step("Butter Gingham has 3 windows: the two Captures fill the first two, the third is empty and blocks continuing", async () => {
      await chooseTemplate("Butter Gingham", "butter-gingham");
      await expect.poll(async () => (await run.cycle())?.windowCount).toBe(3);
      expect((await run.cycle())?.windowsFilled).toBe(2);
      await tap(guest.seePhotographs);
      await run.waitForStep("photographs", "arrange");
      expect(await guest.arrangement()).toEqual([1, 2, null]);
      await expect(guest.continueToFilters).toHaveAttribute("aria-disabled", "true");
      await expect(guest.page.getByText("Take a photograph for each empty position to continue.")).toBeVisible();
    });

    await test.step("back on Signature Strip (4 windows), two windows are empty and Filters still waits", async () => {
      await chooseTemplate("Signature Strip", "signature-strip");
      await expect.poll(async () => (await run.cycle())?.windowCount).toBe(4);
      expect((await run.cycle())?.windowsFilled).toBe(2);
      await tap(guest.seePhotographs);
      await run.waitForStep("photographs", "arrange");
      expect(await guest.arrangement()).toEqual([1, 2, null, null]);
      await expect(guest.continueToFilters).toHaveAttribute("aria-disabled", "true");
    });

    await test.step("the empty windows are filled with new Captures and Filters opens", async () => {
      await tap(guest.takeMore);
      await run.waitForStep("photographs", "camera");
      await run.takePhotographsUntil(guest, 4);
      await run.waitForStep("photographs", "arrange");
      expect(await guest.arrangement()).toEqual([1, 2, 3, 4]);
      await expect(guest.continueToFilters).not.toHaveAttribute("aria-disabled", "true");
    });

    await test.step("a smaller Template keeps the extra Capture for the Event and a larger one takes it back", async () => {
      await chooseTemplate("Butter Gingham", "butter-gingham");
      await run.waitForStep("photographs", "arrange");
      expect(await guest.arrangement()).toEqual([1, 2, 3]);
      await expect(guest.page.getByText("1 more photograph is kept for this Event.")).toBeVisible();
      await expect(guest.continueToFilters).not.toHaveAttribute("aria-disabled", "true");

      await chooseTemplate("Signature Strip", "signature-strip");
      await run.waitForStep("photographs", "arrange");
      expect(await guest.arrangement()).toEqual([1, 2, 3, 4]);
      await expect(guest.continueToFilters).not.toHaveAttribute("aria-disabled", "true");
    });

    await test.step("every Capture is still kept on disk", async () => {
      const cycle = await run.onlyCycle();
      expect(run.captures(cycle.id).map((capture) => `${String(capture.sequence)}:${capture.state}`)).toEqual(["1:kept", "2:kept", "3:kept", "4:kept"]);
      run.expectCaptureFiles(cycle.id);
    });

    await run.endEventFromOperator();
  },
);
