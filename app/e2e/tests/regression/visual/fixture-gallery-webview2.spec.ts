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
import { moveOsCursor } from "../../../src/os-cursor";
import { fromApp, paths } from "../../../src/paths";
import { contentRegion, regionDiff, type Region } from "../../../src/visual";

// Proves the real WebView2 renders every component fixture as the Chromium fixture lane does: the same fixture, the same
// viewport size, captured the same way (app/ui/tests/fixtures/components.spec.ts), compared with that lane's baselines
// in app/output/fixtures/components/<id>/<w>x<h>.png. Run `npm run test:fixtures` in app/ui first.
//
// The tolerance is measured over the fixture's content region (the padded bounding box of what Chromium drew), so a small
// component can't pass with a wrong render inside a large, empty viewport. Nothing may differ outside that region.

/** Per-pixel color distance, as a pixelmatch threshold (0 strict, 1 lenient). */
const pixelThreshold = 0.1;
/** The most pixels that may differ, as a share of the content region. */
const maxDiffRatio = 0.01;

interface Measurement {
  readonly id: string;
  readonly resolution: string;
  readonly ratio: number;
  readonly differingPixels: number;
  readonly region: Region;
  readonly regionPixels: number;
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
        measuredOver: "content region of the Chromium baseline",
        worst: [...all].sort((a, b) => b.ratio - a.ratio).slice(0, 10),
      },
      null,
      2,
    ),
  );
}

for (const resolution of resolutions) {
  const name = resolutionName(resolution);

  test(`every component fixture renders in WebView2 like the Chromium lane at ${name}`, { tag: ["@m3", "@cursor"] }, async ({ operatorPage: page, hooks }, testInfo) => {
    test.setTimeout(20 * 60_000);
    const failures: string[] = [];
    const measured: Measurement[] = [];

    try {
      let windowBounds = { x: 0, y: 0 };
      await test.step(`size the Operator window's page viewport to ${name}`, async () => {
        const window = await hooks.setWindowBounds("operator", resolution.width, resolution.height);
        expect(window.role).toBe("operator");
        windowBounds = window.bounds;
      });
      // The real cursor, not only the page's pointer, must not rest over a control (see parkPointer).
      moveOsCursor(windowBounds.x + 1, windowBounds.y + 1);
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
            measured.push(await compareFixture(page, fixture, resolution, name, testInfo, measured));
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

/**
 * Leaves no control hovered. The window sits under the real OS cursor, which WebView2 keeps hovering whatever control it rests on
 * (the text area's border, the guest-cycle image's scale), and which the test moves out of the way once per resolution; the page's
 * own pointer is parked in the top-left corner too, where no fixture has a control. The hover state is then verified, so a
 * hovered control fails here instead of showing up as a pixel difference, and the transitions that un-hovering starts are awaited.
 */
async function parkPointer(page: Page): Promise<void> {
  await page.mouse.move(0, 0);
  await expect
    .poll(
      async () => {
        const hoveredAwayFromTheCorner = await page.evaluate(() => {
          const corner = document.elementFromPoint(0, 0);
          return Array.from(document.querySelectorAll(":hover")).filter((element) => !corner || !element.contains(corner)).map((element) => element.tagName);
        });
        if (hoveredAwayFromTheCorner.length > 0) {
          await page.mouse.move(1, 1);
          await page.mouse.move(0, 0);
        }
        return hoveredAwayFromTheCorner;
      },
      { message: "no control is hovered once the pointer is parked", timeout: 5_000 },
    )
    .toEqual([]);
  // Un-hovering starts a CSS transition back (the guest-cycle image's scale); wait until none is running, or the screenshot would
  // catch the control part-way. Infinite animations are not transitions and are left alone.
  await page.evaluate(async () => {
    await new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
    await Promise.all(document.getAnimations().filter((animation) => animation instanceof CSSTransition).map((animation) => animation.finished.catch(() => undefined)));
  });
}

async function compareFixture(
  page: Page,
  fixture: ManifestFixture,
  resolution: { width: number; height: number },
  name: string,
  testInfo: TestInfo,
  measured: Measurement[],
): Promise<Measurement> {
  const baselinePath = chromiumBaselinePath(fixture.id, resolution);
  if (!existsSync(baselinePath)) {
    throw new Error(`Missing Chromium baseline ${fromApp(baselinePath)}. Run "npm run test:fixtures" in app/ui first (verification step 3).`);
  }

  await openFixture(page, fixture.id, fixture.role);
  // The real window sits under the real OS cursor, so a control it happens to rest on takes its :hover state (the text area's
  // border, the guest-cycle image's hover scale) and the screenshot would depend on where the cursor was left. Park the page's
  // pointer in the top-left corner, where no fixture has a control; Chromium's hover state follows the latest pointer position.
  await parkPointer(page);
  // Playwright's headless Chromium (the baselines' engine) launches with --hide-scrollbars; WebView2 draws its Fluent overlay
  // scrollbar indicator. Hide it so the comparison covers what the app renders, not the engine's scrollbar chrome. Overlay
  // scrollbars take no layout space, so nothing moves.
  await page.addStyleTag({ content: "* { scrollbar-width: none !important; }" });
  const actual = PNG.sync.read(await page.screenshot());
  const expected = PNG.sync.read(readFileSync(baselinePath));
  if (actual.width !== expected.width || actual.height !== expected.height) {
    throw new Error(
      `${fixture.id} at ${name}: size mismatch, WebView2 ${String(actual.width)}x${String(actual.height)}, ` +
        `Chromium baseline ${String(expected.width)}x${String(expected.height)}.`,
    );
  }

  const region = contentRegion(expected);
  if (!region) {
    throw new Error(`${fixture.id} at ${name}: the Chromium baseline is blank, so there is nothing to compare. Fix the fixture.`);
  }
  const diff = new PNG({ width: actual.width, height: actual.height });
  const result = regionDiff(actual, expected, region, { threshold: pixelThreshold, diff });
  const { ratio, insidePixels: differingPixels, outsidePixels } = result;
  const measurement: Measurement = { id: fixture.id, resolution: name, ratio, differingPixels, region, regionPixels: result.regionArea };
  if (ratio > maxDiffRatio || outsidePixels > 0) {
    measured.push(measurement); // a failing fixture is still measured, so the summary shows the worst ratios
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
      `${fixture.id} at ${name}: ${String(differingPixels)} of ${String(result.regionArea)} content-region pixels differ ` +
        `(${(ratio * 100).toFixed(3)}%, limit ${(maxDiffRatio * 100).toFixed(1)}%), ${String(outsidePixels)} differ outside the region.`,
    );
  }
  return measurement;
}
