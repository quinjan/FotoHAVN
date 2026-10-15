import path from "node:path";
import { expect, test } from "../../../src/fixtures";
import { eventFolder } from "../../../src/operator";
import { operatorSeed, seedOperatorEvents } from "../../../src/seed";
import { EventListScreen } from "../../../src/screens/screens";

test.use({ appOptions: { seed: seedOperatorEvents } });

test.describe("opening an Event's folder", { tag: ["@m6"] }, () => {
  test("OPEN FOLDER records one request for the Event's folder under the Data folder, and launches nothing", async ({
    operatorPage,
    hooks,
    dataDir,
  }) => {
    const list = new EventListScreen(operatorPage);
    const { id, name } = operatorSeed.seaside;

    expect((await hooks.getDiagnostics()).openFolderRequests).toEqual([]);
    await list.rowAction(name, "OPEN FOLDER").click();

    await expect.poll(async () => (await hooks.getDiagnostics()).openFolderRequests.length).toBe(1);
    const [request] = (await hooks.getDiagnostics()).openFolderRequests;
    expect(path.normalize(request?.path ?? "").toLowerCase()).toBe(path.normalize(eventFolder(dataDir, id)).toLowerCase());
    expect(path.normalize(request?.path ?? "").toLowerCase().startsWith(path.normalize(dataDir).toLowerCase() + path.sep)).toBe(true);
    expect(Number.isNaN(Date.parse(request?.at ?? ""))).toBe(false);

    await test.step("another Event's row asks for its own folder", async () => {
      await list.rowAction(operatorSeed.harbour.name, "OPEN FOLDER").click();
      await expect.poll(async () => (await hooks.getDiagnostics()).openFolderRequests.length).toBe(2);
      const requests = (await hooks.getDiagnostics()).openFolderRequests;
      expect(path.normalize(requests[1]?.path ?? "").toLowerCase()).toBe(path.normalize(eventFolder(dataDir, operatorSeed.harbour.id)).toLowerCase());
    });
  });
});
