import { expect, test } from "../../../src/fixtures";
import { keyboardActivate, keyboardChoose, startGuestEvent } from "../../../src/guest-cycle";

// The keyboard needs the real window's focus, so this spec belongs to the cursor class with the other real-window input specs.
test(
  "with the keyboard only, a guest runs the whole cycle, rearranging with Move instead of drag, and the strip is saved",
  { tag: ["@m8", "@camera", "@cursor"] },
  async ({ app, operatorPage, camera }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Keyboard cycle" });
    const guest = await run.controls();
    const { page } = guest;

    await test.step("Start, Template, and the live preview (the one start control) by Tab and Enter", async () => {
      await expect(guest.stepInside).not.toHaveAttribute("aria-disabled", "true");
      await keyboardActivate(page, guest.stepInside);
      await expect(guest.surface("template")).toBeVisible();
      await keyboardActivate(page, guest.templateContinue);
      await run.waitForStep("photographs", "camera");
      await keyboardActivate(page, guest.takePhotographs);
      await run.waitForFilled(4, 60_000);
      await run.waitForStep("photographs", "arrange");
      expect(await guest.arrangement()).toEqual([1, 2, 3, 4]);
    });

    await test.step("Move swaps the first photograph with the third", async () => {
      await keyboardActivate(page, guest.windowButton(1));
      await expect(page.getByText("Photograph 1 is selected.")).toBeVisible();
      await keyboardActivate(page, guest.move);
      await expect(page.getByText("Choose where photograph 1 goes.")).toBeVisible();
      await keyboardActivate(page, page.getByRole("button", { name: "Move here: swap with position 3", exact: true }));
      await expect.poll(() => guest.arrangement()).toEqual([3, 2, 1, 4]);
    });

    await test.step("Retake of the second photograph is accepted", async () => {
      await keyboardActivate(page, guest.windowButton(2));
      await expect(page.getByText("Photograph 2 is selected.")).toBeVisible();
      await keyboardActivate(page, guest.retake);
      await run.retakeCapture(guest, (control) => keyboardActivate(page, control));
      await keyboardActivate(page, guest.accept);
      await expect.poll(() => guest.arrangement()).toEqual([3, 5, 1, 4]);
    });

    await test.step("Filters: the arrow keys choose Sepia and FINISH MY STRIP reaches the Keepsake", async () => {
      await keyboardActivate(page, guest.continueToFilters);
      await run.waitForStep("filters");
      await keyboardChoose(page, guest.optionGroup, guest.option("Sepia"));
      await keyboardActivate(page, guest.finishStrip);
      await expect(guest.surface("keepsake")).toBeVisible({ timeout: 30_000 });
    });

    await test.step("the cycle is completed with the keyboard's arrangement and Look", async () => {
      const cycle = await run.endedCycle("completed", 30_000);
      const captures = run.captures(cycle.id);
      expect(captures.filter((capture) => capture.state === "kept")).toHaveLength(4);
      expect(captures.find((capture) => capture.sequence === 2)?.state).toBe("discarded");
      run.expectCaptureFiles(cycle.id);
      const strip = run.expectPhotoStrip(cycle.id, "Signature Strip");
      expect(strip.look_id).toBe("sepia-v1");
      const idOf = (sequence: number): string => captures.find((capture) => capture.sequence === sequence)?.id ?? "";
      expect(JSON.parse(strip.arrangement)).toEqual([idOf(3), idOf(5), idOf(1), idOf(4)]);
    });

    await run.endEventFromOperator();
  },
);
