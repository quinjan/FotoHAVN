import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, devices } from "@playwright/test";

// Fixture lane: renders the UI in Chromium through the fixture transport (`?fixture=`), which only development and
// E2E builds honor, so the web server builds the E2E bundle (dist-e2e/) and previews it; the field build in dist/ is
// left alone. The browser comes from `npx playwright install chromium`.
const uiDir = dirname(fileURLToPath(import.meta.url));
const resultsDir = resolve(uiDir, "../output/ui-fixtures");
const port = 4173;

export default defineConfig({
  testDir: "tests/fixtures",
  outputDir: resolve(resultsDir, "test-results"),
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [
    ["list"],
    ["html", { outputFolder: resolve(resultsDir, "report"), open: "never" }],
    ["junit", { outputFile: resolve(resultsDir, "junit.xml") }],
  ],
  use: {
    baseURL: `http://127.0.0.1:${String(port)}`,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"], deviceScaleFactor: 1 },
    },
  ],
  webServer: {
    command: `npx vite build --mode e2e && npx vite preview --mode e2e --host 127.0.0.1 --port ${String(port)} --strictPort`,
    url: `http://127.0.0.1:${String(port)}/index.html`,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
