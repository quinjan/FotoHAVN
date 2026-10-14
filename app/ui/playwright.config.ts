import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, devices } from "@playwright/test";
import { fixturePort } from "./scripts/fixture-port.ts";

// `npm run test:fixtures` runs two Chromium projects against the E2E bundle (dist-e2e/), which the web server builds and
// previews; the field build in dist/ is left alone. The browser comes from `npx playwright install chromium`.
// - `fixtures`: the fixture lane (`?fixture=`, honored only in development and E2E builds) at four resolutions.
// - `headless`: the integration lane, the real headless host (`dotnet run --no-build`, so build .NET first) serving
//   dist-e2e/ to an operator and a guest page over its WebSocket bridge.
const uiDir = dirname(fileURLToPath(import.meta.url));
const resultsDir = resolve(uiDir, "../output/ui-fixtures");
// FOTOHAVN_FIXTURE_PORT (default 4173) moves the preview server so parallel worktrees can run the lane side by side.
const port = fixturePort();
const ci = process.env.CI !== undefined && process.env.CI !== "";

export default defineConfig({
  testDir: "tests",
  outputDir: resolve(resultsDir, "test-results"),
  // Launches Chromium and loads the gallery once before the workers start, so the cold start isn't a test's to pay.
  globalSetup: "./tests/global-setup.ts",
  fullyParallel: true,
  forbidOnly: ci,
  // A hosted 4-vCPU runner is shared by the preview server, Chromium, and (in the headless lane) the .NET host, so CI
  // caps the workers and gives each test and each assertion more room. Local runs keep Playwright's default
  // parallelism and its tighter limits.
  ...(ci ? { workers: 2 } : {}),
  timeout: ci ? 45_000 : 30_000,
  expect: { timeout: ci ? 10_000 : 5_000 },
  // A safety net on hosted runners only, not a fix: the known causes of flakiness (cold start, a per-test budget a
  // multi-fixture test outgrows, fixtures captured mid-animation) are removed by the global setup, the per-fixture
  // budget, and the gallery's settle step. A test that passes only on retry is still reported "flaky" (list, GitHub
  // annotation, JUnit), so it stays visible.
  retries: ci ? 1 : 0,
  reporter: [
    ["list"],
    ...(ci ? ([["github"]] as const) : []),
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
      name: "fixtures",
      testDir: "tests/fixtures",
      use: { ...devices["Desktop Chrome"], deviceScaleFactor: 1 },
    },
    {
      name: "headless",
      testDir: "tests/headless",
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
