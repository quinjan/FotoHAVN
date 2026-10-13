import path from "node:path";
import { databaseFileName, fileExists } from "../../../src/data";
import { captureTestScreenshot } from "../../../src/evidence";
import { expect, test } from "../../../src/fixtures";
import type { StorageFaultKind } from "../../../src/hooks";
import { EventListScreen, StorageErrorScreen } from "../../../src/screens/screens";

const cases: readonly { kind: StorageFaultKind; line: string }[] = [
  { kind: "diskFull", line: "The disk is full. Free some space, then try again." },
  { kind: "accessDenied", line: "FOTOHAVN can't write to the Data folder. Check its permissions, then try again." },
  { kind: "locked", line: "Another program is using a FOTOHAVN file. Close it, then try again." },
];

// One app per fault kind: each describe block has its own launch arguments, so the fixture starts a fresh app for it.
for (const { kind, line } of cases) {
  test.describe(`storage fault at startup: ${kind}`, { tag: ["@m4"] }, () => {
    test.use({ appOptions: { args: ["--e2e-storage-fault", kind] } });

    test(`shows the storage error screen, and clearing the fault plus TRY AGAIN recovers`, async ({
      app,
      operatorPage,
      hooks,
      dataDir,
    }, testInfo) => {
      const error = new StorageErrorScreen(operatorPage);

      await test.step("storage reports the error and the Operator Screen says so", async () => {
        await expect.poll(async () => (await hooks.getDiagnostics()).storage.state).toBe("error");
        const { storage } = await hooks.getDiagnostics();
        expect(storage.kind).toBe(kind);
        expect(storage.schemaVersion).toBeNull();
        expect(storage.reconcile).toBeNull();
        await expect(error.main).toBeVisible();
        await expect(error.heading).toBeVisible();
        await expect(error.line(line)).toBeVisible();
        await expect(error.dataFolderLabel).toHaveText("Data folder");
        await expect(error.dataPath).toHaveText(dataDir);
        await expect(error.tryAgain).toBeVisible();
        expect(fileExists(path.join(dataDir, databaseFileName)), "no database while the fault is active").toBe(false);
        await captureTestScreenshot(operatorPage, testInfo, `storage-error-${kind}`);
      });

      await test.step("TRY AGAIN while the fault is still active leaves the error screen up", async () => {
        await error.tryAgain.click();
        await expect(error.main.getByRole("status").filter({ hasText: "The Data folder still needs attention." })).toBeVisible();
        await expect(error.heading).toBeVisible();
        expect((await hooks.getDiagnostics()).storage).toMatchObject({ state: "error", kind });
      });

      await test.step("clearing the fault and choosing TRY AGAIN opens storage and shows the Event list", async () => {
        await hooks.clearStorageFault();
        await error.tryAgain.click();
        await expect.poll(async () => (await hooks.getDiagnostics()).storage.state).toBe("ready");
        const screen = new EventListScreen(operatorPage);
        await expect(screen.heading).toBeVisible();
        await expect(screen.empty).toBeVisible();
        await expect(error.main).toHaveCount(0);
        const { storage } = await hooks.getDiagnostics();
        expect(storage.kind).toBeNull();
        expect(storage.schemaVersion).toBe(1);
        expect(fileExists(path.join(dataDir, databaseFileName)), "the database exists once storage opened").toBe(true);
      });

      await test.step("the app exits cleanly", async () => {
        expect((await app.close()).code).toBe(0);
      });
    });
  });
}
