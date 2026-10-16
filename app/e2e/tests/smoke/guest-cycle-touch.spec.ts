import { expect, test } from "../../src/fixtures";
import { tap, startGuestEvent, touchDrag } from "../../src/guest-cycle";

test(
  "with touch only, a guest steps inside, picks the Template, takes four photographs, swaps two by drag, retakes one, chooses a Look, and the Keepsake returns to Start with one completed cycle on disk",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Touch cycle" });
    const guest = await run.controls();

    await test.step("STEP INSIDE shows the Template step; CONTINUE TO PHOTOGRAPHS opens the camera", async () => {
      await run.stepInside();
      await expect(guest.surface("template")).toBeVisible();
      await tap(guest.templateContinue);
      await run.waitForStep("photographs", "camera");
    });

    await test.step("TAKE PHOTOGRAPHS fills four windows and opens Arrange", async () => {
      await run.takePhotographsUntil(guest, 4);
      await run.waitForStep("photographs", "arrange");
      await expect(guest.windowButton(4)).toBeVisible();
      expect(await guest.arrangement()).toEqual([1, 2, 3, 4]);
    });

    await test.step("dragging the first photograph onto the third swaps them", async () => {
      await touchDrag(guest.windowButton(1), guest.windowButton(3));
      await expect.poll(() => guest.arrangement()).toEqual([3, 2, 1, 4]);
    });

    await test.step("Retake of the second photograph: the new one is accepted and replaces it", async () => {
      await tap(guest.windowButton(2));
      await expect(guest.page.getByText("Photograph 2 is selected.")).toBeVisible();
      await tap(guest.retake);
      await expect(guest.accept).toBeVisible({ timeout: 30_000 });
      await tap(guest.accept);
      await expect.poll(() => guest.arrangement()).toEqual([3, 5, 1, 4]);
    });

    await test.step("Filters: choosing Sepia and FINISH MY STRIP reach the Keepsake", async () => {
      await tap(guest.continueToFilters);
      await run.waitForStep("filters");
      await tap(guest.option("Sepia"));
      await expect(guest.page.locator("[data-selected-look]")).toHaveAttribute("data-selected-look", "sepia-v1");
      await tap(guest.finishStrip);
      await expect(guest.surface("keepsake")).toBeVisible({ timeout: 30_000 });
    });

    await test.step("the Keepsake ends on its own and the booth returns to Start", async () => {
      await expect(guest.surface("start")).toBeVisible({ timeout: 45_000 });
      await expect(run.list.runningFact("cycles")).toHaveText("1");
    });

    await test.step("the database and files hold one completed cycle, four kept Captures, one deleted file, and a Photo Strip at the Template's native size", async () => {
      const cycle = await run.onlyCycle();
      expect(cycle.end_reason).toBe("completed");
      expect(cycle.ended_at).not.toBeNull();

      const captures = run.captures(cycle.id);
      expect(captures.map((capture) => `${String(capture.sequence)}:${capture.state}`)).toEqual([
        "1:kept",
        "2:discarded",
        "3:kept",
        "4:kept",
        "5:kept",
      ]);
      run.expectCaptureFiles(cycle.id);

      const strip = run.expectPhotoStrip(cycle.id, "Signature Strip");
      expect(strip.template_id).toBe("signature-strip");
      expect(strip.look_id).toBe("sepia-v1");
      const idOf = (sequence: number): string => captures.find((capture) => capture.sequence === sequence)?.id ?? "";
      expect(JSON.parse(strip.arrangement)).toEqual([idOf(3), idOf(5), idOf(1), idOf(4)]);
      run.expectNoStrayFiles(cycle.id);
    });

    await run.endEventFromOperator();
  },
);
