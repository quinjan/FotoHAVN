import path from "node:path";
import { databaseFileName, fileExists, readUserVersion } from "../../src/data";
import { expect, test } from "../../src/fixtures";
import { EventListScreen } from "../../src/screens/screens";
import { latestMigrationVersion } from "../../src/seed";

test(
  "the app starts with an empty Data folder and creates fotohavn.db with the current schema version",
  { tag: ["@m4"] },
  async ({ app, operatorPage, hooks, dataDir }) => {
    await test.step("storage opens", async () => {
      await expect.poll(async () => (await hooks.getDiagnostics()).storage.state).toBe("ready");
    });

    await test.step("fotohavn.db exists in the run's Data folder", () => {
      expect(fileExists(path.join(dataDir, databaseFileName)), `${databaseFileName} in ${dataDir}`).toBe(true);
    });

    await test.step("its user_version is the diagnostics' schema version and the newest migration", async () => {
      const { storage } = await hooks.getDiagnostics();
      const userVersion = readUserVersion(dataDir);
      expect(storage.schemaVersion).toBe(userVersion);
      expect(userVersion).toBe(latestMigrationVersion());
      expect(userVersion).toBe(1);
      expect(storage.kind).toBeNull();
    });

    await test.step("an empty folder has nothing to repair", async () => {
      const { storage } = await hooks.getDiagnostics();
      expect(storage.reconcile).toEqual({
        stagingFilesDeleted: 0,
        orphanFilesDeleted: 0,
        artifactProblems: 0,
        cyclesMarkedUnrecoverable: 0,
        cyclesClosedAsAppClosed: 0,
        guestCyclesDeleted: 0,
        eventsDeleted: 0,
        discardedFilesDeleted: 0,
        orphanThumbnailsDeleted: 0,
        problems: 0,
      });
    });

    await test.step("the operator page shows the empty Event list", async () => {
      const screen = new EventListScreen(operatorPage);
      await expect(screen.heading).toBeVisible();
      await expect(screen.empty).toBeVisible();
    });

    await test.step("the app exits cleanly", async () => {
      expect((await app.close()).code).toBe(0);
    });
  },
);
