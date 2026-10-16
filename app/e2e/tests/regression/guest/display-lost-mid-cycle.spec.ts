import { virtualGuestId } from "../../../src/displays";
import { expect, test } from "../../../src/fixtures";
import { GuestControls, startGuestEvent, tap } from "../../../src/guest-cycle";

test(
  "losing the guest display during a countdown stops it and shows Operator Assistance on the Operator Screen; reconnecting and RETRY reopen the Guest Screen on the same display and continue from the next empty window",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera, hooks }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Display lost", countdown: 5 });
    const guest = await run.expectStartReady();
    const guestBounds = (await hooks.getDiagnostics()).windows.find((window) => window.role === "guest")?.bounds;
    expect(guestBounds).toBeDefined();

    await tap(guest.stepInside);
    await tap(guest.templateContinue);
    await run.waitForStep("photographs", "camera");
    await tap(guest.takePhotographs);
    await run.waitForFilled(1, 30_000);

    await test.step("during the next countdown, the guest display is disconnected", async () => {
      await expect.poll(async () => (await run.cycle())?.countdownRemaining, { timeout: 15_000 }).not.toBeNull();
      await hooks.disconnectDisplay(virtualGuestId);
      await expect.poll(async () => (await hooks.getDiagnostics()).guestScreenOpen, { timeout: 15_000 }).toBe(false);
    });

    await test.step("the countdown stops, the Operator Screen shows Operator Assistance with the display reason, and the Capture stays", async () => {
      await expect(run.list.runningFact("step")).toHaveText("Operator Assistance");
      await expect(operatorPage.getByText("The guest display isn't connected.")).toBeVisible();
      await expect(run.list.runningFact("display")).toHaveText("Disconnected");
      await expect(run.list.runningPanel().getByRole("button", { name: "RETRY", exact: true })).toBeVisible();
      expect(await run.cycle()).toMatchObject({ windowsFilled: 1, sequenceRunning: false, countdownRemaining: null });
      const cycle = await run.onlyCycle();
      expect(cycle.end_reason).toBeNull();
      expect(run.captures(cycle.id).filter((capture) => capture.state === "kept")).toHaveLength(1);
    });

    await test.step("reconnecting and RETRY on the panel reopen the Guest Screen on the same display, back on the Camera tab", async () => {
      await hooks.connectDisplay(virtualGuestId);
      await run.list.runningPanel().getByRole("button", { name: "RETRY", exact: true }).click();
      await expect.poll(async () => (await hooks.getDiagnostics()).guestScreenOpen, { timeout: 30_000 }).toBe(true);
      const reopened = new GuestControls(await run.guest());
      await expect(reopened.surface("photographs")).toBeVisible({ timeout: 20_000 });
      await run.waitForStep("photographs", "camera");
      expect((await hooks.getDiagnostics()).windows.find((window) => window.role === "guest")?.bounds).toEqual(guestBounds);
      expect(await run.cycle()).toMatchObject({ windowsFilled: 1, sequenceRunning: false, countdownRemaining: null });
      await expect(run.list.runningFact("step")).toHaveText("Photographs");
      await expect(run.list.runningFact("display")).toHaveText("Connected");

      await test.step("the sequence continues from the next empty window", async () => {
        await tap(reopened.takePhotographs);
        await run.waitForFilled(2, 30_000);
        await run.stopCapture(reopened);
        const cycle = await run.onlyCycle();
        expect(run.captures(cycle.id).map((capture) => `${String(capture.sequence)}:${capture.state}`)).toEqual(["1:kept", "2:kept"]);
        run.expectCaptureFiles(cycle.id);
      });
    });

    await run.endEventFromOperator();
  },
);
