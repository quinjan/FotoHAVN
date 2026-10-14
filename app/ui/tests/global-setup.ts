import { chromium, type FullConfig } from "@playwright/test";

// The first browser launch and the first page load on a fresh machine (a hosted CI runner, a cold file cache, a virus
// scanner reading Chromium and the bundle for the first time) take far longer than any later one. Doing both here,
// once, before any worker starts, keeps that cold start out of the first tests' timeouts: the run launches Chromium,
// loads the gallery manifest and one fixture (so the bundle, the fonts, and the lazy chunks are read once), and closes.
const warmUpTimeoutMs = 90_000;

export default async function globalSetup(config: FullConfig): Promise<void> {
  const baseURL = config.projects[0]?.use.baseURL;
  if (baseURL === undefined) {
    throw new Error("The fixture lane has no baseURL to warm up.");
  }

  const browser = await chromium.launch({ timeout: warmUpTimeoutMs });
  try {
    const page = await browser.newPage();
    page.setDefaultTimeout(warmUpTimeoutMs);
    await page.goto(`${baseURL}/index.html?gallery=manifest`);
    await page.locator("#fotohavn-gallery-manifest").waitFor();
    const manifest = JSON.parse((await page.locator("#fotohavn-gallery-manifest").textContent()) ?? "{}") as {
      fixtures?: { id: string }[];
    };
    const first = manifest.fixtures?.[0];
    if (first === undefined) {
      throw new Error("The gallery manifest lists no fixtures.");
    }
    await page.goto(`${baseURL}/index.html?gallery=${encodeURIComponent(first.id)}`);
    await page.locator('html[data-fotohavn-gallery-ready="true"]').waitFor({ state: "attached" });
    await page.goto(`${baseURL}/index.html?fixture=operator-event-list-empty`);
    await page.locator('html[data-fotohavn-role="operator"]').waitFor({ state: "attached" });
  } finally {
    await browser.close();
  }
}
