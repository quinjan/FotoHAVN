import { listDataFolder } from "../../../src/data";
import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

test(
  "a disk-full failure on the next write enters Operator Assistance with no partial file; clearing it and RETRY saves the Capture",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera, hooks, dataDir }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Storage fault", countdown: 3 });
    const guest = await run.expectStartReady();

    await tap(guest.stepInside);
    await tap(guest.templateContinue);
    await run.waitForStep("photographs", "camera");
    const cycleId = (await run.cycle())?.id ?? "";
    const folderPrefix = `Events/${run.eventId}/GuestCycles/${cycleId}`;

    await test.step("diskFull on the next write: the first Capture can't be saved and Operator Assistance shows", async () => {
      await hooks.injectStorageFault("diskFull", "nextWrite");
      await tap(guest.takePhotographs);
      await run.waitForSurface("operatorAssistance", 30_000);
      await expect(guest.surface("operatorAssistance")).toBeVisible();
      await expect(run.list.runningFact("step")).toHaveText("Operator Assistance");
      await expect(operatorPage.getByText("The Data folder needs attention.")).toBeVisible();
      expect(await run.cycle()).toMatchObject({ windowsFilled: 0, sequenceRunning: false, countdownRemaining: null });
    });

    await test.step("no partial file is left, and no Capture is kept", () => {
      expect(run.captures(cycleId).filter((capture) => capture.state === "kept")).toEqual([]);
      const leftovers = listDataFolder(dataDir).filter((entry) => entry.path.startsWith(`${folderPrefix}/`) && !entry.path.endsWith("/"));
      expect(leftovers, "files in the cycle folder").toEqual([]);
      const staging = listDataFolder(dataDir).filter((entry) => /staging|\.tmp|\.partial/i.test(entry.path) && entry.path.startsWith(`Events/${run.eventId}/`));
      expect(staging, "staging files of the Event").toEqual([]);
    });

    await test.step("clearing the fault and pressing RETRY returns to the Camera tab, and the next Capture is saved", async () => {
      await hooks.clearStorageFault();
      await tap(guest.retry);
      await expect(guest.surface("photographs")).toBeVisible({ timeout: 20_000 });
      await run.waitForStep("photographs", "camera");
      expect(await run.cycle()).toMatchObject({ windowsFilled: 0, sequenceRunning: false });
      await tap(guest.takePhotographs);
      await run.waitForFilled(1, 30_000);
      await run.stopCapture(guest);
      const kept = run.captures(cycleId).filter((capture) => capture.state === "kept");
      expect(kept).toHaveLength(1);
      run.expectCaptureFiles(cycleId);
      run.expectNoStrayFiles(cycleId);
    });

    await run.endEventFromOperator();
  },
);
