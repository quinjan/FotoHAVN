import { defineConfig } from "@playwright/test";
import { paths } from "./src/paths";

export default defineConfig({
  globalSetup: "./src/global-setup.ts",
  // The camera is opened exclusively, so the app runs one instance at a time.
  workers: 1,
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
  projects: [
    { name: "smoke", testDir: "./tests/smoke" },
    { name: "regression", testDir: "./tests/regression" },
  ],
});
