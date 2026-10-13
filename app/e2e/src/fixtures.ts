import path from "node:path";
import { test as base, expect, type Page } from "@playwright/test";
import type { Camera } from "./camera";
import { readChosenCamera } from "./chosen-camera";
import { attachRunEvidence, captureTestScreenshot } from "./evidence";
import type { HookClient } from "./hooks";
import { launchApp, type AppSession } from "./launch";
import { fromApp } from "./paths";

/** How a test file (or `test.describe` block) launches its app, set with `test.use({ appOptions: { ... } })`. */
export interface AppOptions {
  /** Extra command-line arguments, such as `["--e2e-storage-fault", "diskFull"]`. */
  args?: readonly string[];
  /** Seeds the Data folder before the app starts (see `seed.ts`). */
  seed?: (dataDir: string) => void | Promise<void>;
}

function sameOptions(a: AppOptions, b: AppOptions): boolean {
  return a.seed === b.seed && (a.args ?? []).join("\n") === (b.args ?? []).join("\n");
}

/**
 * Keeps one app process per test file: the first test in a file launches it,
 * and it is shut down when the next file starts, when a test fails, or when the worker ends.
 * A test whose launch options differ from the running app's (another `test.describe` with its own `appOptions`)
 * gets a fresh app, so each launch sees its own seed and arguments.
 */
class AppManager {
  private current: { file: string; options: AppOptions; session: AppSession } | undefined;

  async sessionFor(file: string, camera: Camera, options: AppOptions): Promise<AppSession> {
    if (
      this.current !== undefined &&
      this.current.file === file &&
      sameOptions(this.current.options, options) &&
      !this.current.session.isClosed
    ) {
      return this.current.session;
    }
    await this.closeCurrent();
    const session = await launchApp({
      name: path.basename(file),
      camera,
      ...(options.args === undefined ? {} : { args: options.args }),
      ...(options.seed === undefined ? {} : { seed: options.seed }),
    });
    this.current = { file, options, session };
    return session;
  }

  async closeCurrent(): Promise<void> {
    const session = this.current?.session;
    this.current = undefined;
    if (session === undefined || session.isClosed) {
      return;
    }
    await closeCleanly(session);
  }
}

export async function closeCleanly(session: AppSession): Promise<void> {
  const exit = await session.close();
  if (exit.code !== 0) {
    throw new Error(
      `FOTOHAVN exited with code ${exit.code ?? "none"} instead of 0. Evidence: ${fromApp(session.run.runDir)}`,
    );
  }
}

interface TestFixtures {
  /** Launch options for this test's app; the default launches on an empty Data folder with no extra arguments. */
  appOptions: AppOptions;
  /** The running app for this test file. */
  app: AppSession;
  /** The Operator page (https://app.fotohavn/, handshaken as `operator`) as of the start of the test. */
  operatorPage: Page;
  /** The same as `operatorPage`, so a test never gets a stray Chromium page by accident. */
  page: Page;
  hooks: HookClient;
  /** The run's Data folder, passed as `--data`. */
  dataDir: string;
}

interface WorkerFixtures {
  /** The camera chosen by global setup and passed as `--e2e-camera`. */
  camera: Camera;
  appManager: AppManager;
}

export const test = base.extend<TestFixtures, WorkerFixtures>({
  camera: [
    async ({}, use) => {
      await use(readChosenCamera());
    },
    { scope: "worker" },
  ],
  appManager: [
    async ({}, use) => {
      const manager = new AppManager();
      await use(manager);
      await manager.closeCurrent();
    },
    { scope: "worker" },
  ],
  appOptions: [{}, { option: true }],
  app: async ({ appManager, camera, appOptions }, use, testInfo) => {
    const session = await appManager.sessionFor(testInfo.file, camera, appOptions);
    await use(session);
    if (!session.isClosed) {
      for (const role of ["operator", "guest"] as const) {
        const rolePage = await session.pageFor(role, { timeoutMs: 1_000 }).catch(() => undefined);
        if (rolePage !== undefined) {
          await captureTestScreenshot(rolePage, testInfo, `end-of-test-${role}`);
        }
      }
    }
    if (testInfo.status !== testInfo.expectedStatus) {
      // Tear the app down now so its evidence belongs to the failing test.
      await appManager.closeCurrent().catch((error: unknown) => {
        session.run.errors.push(`Teardown after failure: ${String(error)}`);
      });
    }
    if (session.isClosed) {
      await session.close().catch(() => undefined);
      await attachRunEvidence(testInfo, session.run);
    }
  },
  operatorPage: async ({ app }, use) => {
    await use(await app.pageFor("operator"));
  },
  page: async ({ operatorPage }, use) => {
    await use(operatorPage);
  },
  hooks: async ({ app }, use) => {
    await use(app.hooks);
  },
  dataDir: async ({ app }, use) => {
    await use(app.dataDir);
  },
});

export { expect };
