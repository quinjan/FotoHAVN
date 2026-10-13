import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import type { Page, TestInfo } from "@playwright/test";
import pixelmatch from "pixelmatch";
import { PNG } from "pngjs";
import { expect, test } from "../../../src/fixtures";
import {
  chromiumBaselinePath,
  openFixture,
  readManifest,
  resolutionName,
  resolutions,
  returnToApp,
  type ManifestFixture,
} from "../../../src/gallery";
import { fromApp, paths } from "../../../src/paths";

// Proves the real WebView2 renders every component fixture as the Chromium fixture lane does: the same fixture, the same
// viewport size, captured the same way (app/ui/tests/fixtures/components.spec.ts), compared with that lane's baselines
// in app/output/fixtures/components/<id>/<w>x<h>.png. Run `npm run test:fixtures` in app/ui first.

/** Per-pixel color distance, as a pixelmatch threshold (0 strict, 1 lenient). */
const pixelThreshold = 0.1;
/** The most pixels that may differ in one screenshot. */
const maxDiffRatio = 0.01;

interface Measurement {
  readonly id: string;
  readonly resolution: string;
  readonly ratio: number;
  readonly differingPixels: number;
}

const measurementsFile = path.join(paths.e2eOutput, "visual-measurements.json");
const summaryFile = path.join(paths.e2eOutput, "visual-summary.json");

/** Keeps every resolution's measurements across tests (a failed test restarts the worker, so memory isn't enough). */
function recordMeasurements(resolution: string, measured: readonly Measurement[]): void {
  const previous: Measurement[] = existsSync(measurementsFile) ? (JSON.parse(readFileSync(measurementsFile, "utf8")) as Measurement[]) : [];
  const all = [...previous.filter((measurement) => measurement.resolution !== resolution), ...measured];
  mkdirSync(path.dirname(measurementsFile), { recursive: true });
  writeFileSync(measurementsFile, JSON.stringify(all));
  const ratios = all.map((measurement) => measurement.ratio);
  writeFileSync(
    summaryFile,
    JSON.stringify(
      {
        threshold: pixelThreshold,
        maxAllowedRatio: maxDiffRatio,
        comparisons: ratios.length,
        maxRatio: Math.max(0, ...ratios),
        meanRatio: ratios.length === 0 ? 0 : ratios.reduce((sum, ratio) => sum + ratio, 0) / ratios.length,
        worst: [...all].sort((a, b) => b.ratio - a.ratio).slice(0, 10),
      },
      null,
      2,
    ),
  );
}

for (const resolution of resolutions) {
  const name = resolutionName(resolution);

  test(`every component fixture renders in WebView2 like the Chromium lane at ${name}`, { tag: ["@m3"] }, async ({ operatorPage: page, hooks }, testInfo) => {
    test.setTimeout(20 * 60_000);
    const failures: string[] = [];
    const measured: Measurement[] = [];

    try {
      await test.step(`size the Operator window's page viewport to ${name}`, async () => {
        const window = await hooks.setWindowBounds("operator", resolution.width, resolution.height);
        expect(window.role).toBe("operator");
      });
      const manifest = await readManifest(page);
      expect(manifest.length, "the gallery manifest lists fixtures").toBeGreaterThan(0);

      const viewport = await page.evaluate(() => ({ width: window.innerWidth, height: window.innerHeight, devicePixelRatio: window.devicePixelRatio }));
      expect(
        viewport,
        `the Operator page viewport is ${String(viewport.width)}x${String(viewport.height)} at ${String(viewport.devicePixelRatio)}x, not ${name} at 1x`,
      ).toEqual({ width: resolution.width, height: resolution.height, devicePixelRatio: 1 });

      for (const fixture of manifest) {
        try {
          await test.step(`${fixture.id} at ${name}`, async () => {
            measured.push(await compareFixture(page, fixture, resolution, name, testInfo));
          });
        } catch (error) {
          failures.push(error instanceof Error ? (error.message.split("\n")[0] ?? "") : String(error));
        }
      }

      const ratios = measured.map((measurement) => measurement.ratio);
      testInfo.annotations.push({ type: "fixtures compared", description: `${String(measured.length)} of ${String(manifest.length)} at ${name}` });
      testInfo.annotations.push({ type: "max diff ratio", description: `${(Math.max(0, ...ratios) * 100).toFixed(4)}% (limit ${(maxDiffRatio * 100).toFixed(1)}%)` });
    } finally {
      recordMeasurements(name, measured);
      await returnToApp(page);
    }

    expect(failures, `${String(failures.length)} fixture(s) differ from the Chromium baselines at ${name}:\n${failures.join("\n")}`).toEqual([]);
  });
}

async function compareFixture(
  page: Page,
  fixture: ManifestFixture,
  resolution: { width: number; height: number },
  name: string,
  testInfo: TestInfo,
): Promise<Measurement> {
  const baselinePath = chromiumBaselinePath(fixture.id, resolution);
  if (!existsSync(baselinePath)) {
    throw new Error(`Missing Chromium baseline ${fromApp(baselinePath)}. Run "npm run test:fixtures" in app/ui first (verification step 3).`);
  }

  await openFixture(page, fixture.id, fixture.role);
  const actual = PNG.sync.read(await page.screenshot());
  const expected = PNG.sync.read(readFileSync(baselinePath));
  if (actual.width !== expected.width || actual.height !== expected.height) {
    throw new Error(
      `${fixture.id} at ${name}: size mismatch, WebView2 ${String(actual.width)}x${String(actual.height)}, ` +
        `Chromium baseline ${String(expected.width)}x${String(expected.height)}.`,
    );
  }

  const diff = new PNG({ width: actual.width, height: actual.height });
  const differingPixels = pixelmatch(actual.data, expected.data, diff.data, actual.width, actual.height, { threshold: pixelThreshold });
  const ratio = differingPixels / (actual.width * actual.height);
  const measurement: Measurement = { id: fixture.id, resolution: name, ratio, differingPixels };

  if (ratio > maxDiffRatio) {
    const files = [
      ["actual", PNG.sync.write(actual)],
      ["expected", PNG.sync.write(expected)],
      ["diff", PNG.sync.write(diff)],
    ] as const;
    for (const [kind, data] of files) {
      const file = testInfo.outputPath(`${fixture.id}-${name}-${kind}.png`);
      mkdirSync(path.dirname(file), { recursive: true });
      writeFileSync(file, data);
      await testInfo.attach(`${fixture.id}-${name}-${kind}`, { path: file, contentType: "image/png" });
    }
    throw new Error(
      `${fixture.id} at ${name}: ${String(differingPixels)} pixels differ (${(ratio * 100).toFixed(3)}%, limit ${(maxDiffRatio * 100).toFixed(1)}%).`,
    );
  }
  return measurement;
}
