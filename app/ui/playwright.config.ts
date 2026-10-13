import { existsSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, devices } from "@playwright/test";

// Fixture gallery lane: renders the built UI in Chromium. Run `npm run build` first;
// the browser comes from `npx playwright install chromium`.
const uiDir = dirname(fileURLToPath(import.meta.url));
const resultsDir = resolve(uiDir, "../output/ui-fixtures");
const port = 4173;

if (!existsSync(resolve(uiDir, "dist/index.html"))) {
  throw new Error("ui/dist/index.html is missing. Run `npm run build` before `npm run test:fixtures`.");
}

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
    command: `npx vite preview --host 127.0.0.1 --port ${String(port)} --strictPort`,
    url: `http://127.0.0.1:${String(port)}/index.html`,
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
});
