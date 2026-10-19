import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

test(
  "a camera that fails after Capture 2 enters Operator Assistance; once it is back, RETRY continues the sequence from window 3",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera, hooks }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Camera fault", countdown: 3 });
    const guest = await run.expectStartReady();

    await tap(guest.stepInside);
    await tap(guest.templateContinue);
    await run.waitForStep("photographs", "camera");
    await tap(guest.takePhotographs);
    await run.waitForFilled(2, 30_000);

    await test.step("the injected failure cancels the sequence and shows Operator Assistance on both screens", async () => {
      await hooks.injectCameraFault("failed");
      await run.waitForSurface("operatorAssistance");
      await expect(guest.surface("operatorAssistance")).toBeVisible();
      await expect(guest.page.getByRole("heading", { level: 1 })).toContainText(/A little\s+interruption\./);
      await expect(guest.page.getByText("Please ask the FOTOHAVN team for help.")).toBeVisible();
      await expect(run.list.runningFact("step")).toHaveText("Operator Assistance");
      await expect(operatorPage.getByText("The camera needs attention.")).toBeVisible();
      await expect(run.list.runningFact("camera")).toHaveText("Not available");

      const cycle = await run.cycle();
      expect(cycle).toMatchObject({ step: "photographs", windowsFilled: 2, sequenceRunning: false, countdownRemaining: null });
      expect(cycle?.timer.paused).toBe(true);
      const stored = await run.onlyCycle();
      expect(run.captures(stored.id).filter((capture) => capture.state === "kept")).toHaveLength(2);
    });

    await test.step("RETRY while the camera is down fails and leaves Operator Assistance up", async () => {
      await tap(guest.retry);
      await expect(guest.retry).not.toHaveAttribute("aria-disabled", "true", { timeout: 20_000 });
      await expect(guest.surface("operatorAssistance")).toBeVisible();
    });

    await test.step("with the camera back, RETRY returns to the Camera tab with no countdown", async () => {
      await hooks.clearCameraFault();
      await expect.poll(async () => (await hooks.getDiagnostics()).camera.state, { timeout: 20_000 }).toBe("ready");
      await tap(guest.retry);
      await expect(guest.surface("photographs")).toBeVisible({ timeout: 20_000 });
      await run.waitForStep("photographs", "camera");
      expect(await run.cycle()).toMatchObject({ windowsFilled: 2, sequenceRunning: false, countdownRemaining: null });
      await expect(run.list.runningFact("step")).toHaveText("Photographs");
      await expect(run.list.runningFact("camera")).toHaveText("Ready");
    });

    await test.step("a tap on the live preview continues from window 3 and the strip fills", async () => {
      await tap(guest.takePhotographs);
      await run.waitForFilled(3, 30_000);
      await run.waitForFilled(4, 30_000);
      await run.waitForStep("photographs", "arrange");
      expect(await guest.arrangement()).toEqual([1, 2, 3, 4]);
      const cycle = await run.onlyCycle();
      expect(run.captures(cycle.id).map((capture) => `${String(capture.sequence)}:${capture.state}`)).toEqual(["1:kept", "2:kept", "3:kept", "4:kept"]);
      run.expectCaptureFiles(cycle.id);
      run.expectNoStrayFiles(cycle.id);
    });

    await run.endEventFromOperator();
  },
);
