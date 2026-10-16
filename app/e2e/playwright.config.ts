import { defineConfig, type PlaywrightTestProject } from "@playwright/test";
import { exclusivePattern } from "./src/exclusive";
import { paths } from "./src/paths";

/** How many parallel-pool test files run at once, each in its own app instance. FOTOHAVN_E2E_WORKERS overrides the default of 4. */
const workersEnvVar = "FOTOHAVN_E2E_WORKERS";
const defaultParallelWorkers = 4;

function readParallelWorkers(): number {
  const value = process.env[workersEnvVar];
  if (value === undefined || value.trim() === "") {
    return defaultParallelWorkers;
  }
  const workers = Number(value);
  if (!Number.isInteger(workers) || workers < 1) {
    throw new Error(`${workersEnvVar} must be a whole number of at least 1, not "${value}".`);
  }
  return workers;
}

const parallelWorkers = readParallelWorkers();

/**
 * One user-facing project (`smoke`, `regression`) is two Playwright projects: a parallel pool of every test that needs no
 * exclusive resource, and a serial pool of the tests tagged `@camera`, `@cursor`, `@build`, or `@timing` (see src/exclusive.ts).
 *
 * Both pools carry the same name apart from letter case: Playwright matches `--project` case-insensitively, so
 * `--project=regression` selects both pools, and `--grep` still filters both, exactly as with the one project this
 * replaces. (Playwright's `dependencies` and `teardown` would skip the CLI `--grep` for the second pool.) The upper-case
 * name is the exclusive pool. Use `--grep-invert "@camera|@cursor|@build|@timing"` to run only the parallel pool.
 *
 * The exclusive pool has one worker, and a lock (src/exclusive.ts) also keeps the exclusive tests of the smoke and
 * regression pools from running together when one command selects both.
 */
function pools(name: string, testDir: string): PlaywrightTestProject[] {
  return [
    { name, testDir, grepInvert: exclusivePattern, workers: parallelWorkers, fullyParallel: false },
    { name: name.toUpperCase(), testDir, grep: exclusivePattern, workers: 1, fullyParallel: false },
  ];
}

export default defineConfig({
  globalSetup: "./src/global-setup.ts",
  // The parallel pool, plus the exclusive pool's worker, plus one that may be waiting for the exclusive lock.
  workers: parallelWorkers + 2,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: 120_000,
  expect: { timeout: 10_000 },
  outputDir: paths.testResults,
  reporter: [
    ["html", { outputFolder: paths.htmlReport, open: "never" }],
    ["junit", { outputFile: paths.junitReport }],
    ["list"],
  ],
  use: {
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [...pools("smoke", "./tests/smoke"), ...pools("regression", "./tests/regression")],
});
