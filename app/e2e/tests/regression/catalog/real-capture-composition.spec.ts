import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import type { Page } from "@playwright/test";
import { PNG } from "pngjs";
import { expect, test } from "../../../src/fixtures";
import { openDiagnostics, waitForFreshFrames } from "../../../src/camera-diagnostics";
import { saveScreenshot } from "../../../src/evidence";
import { paths } from "../../../src/paths";

/** The four Templates with the size of their saved PNG (native orientation) and the Looks, by id and display name. */
const templates = [
  { id: "signature-strip", name: "Signature Strip", width: 600, height: 1800 },
  { id: "panorama-four", name: "Panorama Four", width: 1800, height: 600 },
  { id: "butter-gingham", name: "Butter Gingham", width: 600, height: 1800 },
  { id: "polka-keepsake", name: "Polka Keepsake", width: 600, height: 1800 },
] as const;
const looks = [
  { id: "monochrome-v1", name: "Monochrome" },
  { id: "naturale-v1", name: "Naturale" },
  { id: "soft-monochrome-v1", name: "Soft Monochrome" },
  { id: "soft-color-v1", name: "Soft Color" },
  { id: "sepia-v1", name: "Sepia" },
  { id: "warm-monochrome-v1", name: "Warm Monochrome" },
  { id: "classic-v1", name: "Classic" },
] as const;

interface Fetched {
  status: number;
  contentType: string | null;
  cacheControl: string | null;
  base64: string;
}

/** Fetches the preview from the page, as the Operator Screen's own image does, and brings the bytes back to Node. */
async function fetchPreview(page: Page, previewKey: string): Promise<Fetched> {
  return await page.evaluate(async (url) => {
    const response = await fetch(url, { cache: "no-store" });
    const bytes = new Uint8Array(await response.arrayBuffer());
    let binary = "";
    for (let at = 0; at < bytes.length; at += 0x8000) {
      binary += String.fromCharCode(...bytes.subarray(at, at + 0x8000));
    }
    return {
      status: response.status,
      contentType: response.headers.get("content-type"),
      cacheControl: response.headers.get("cache-control"),
      base64: btoa(binary),
    };
  }, `https://preview.fotohavn/${previewKey}.png`);
}

function median(values: number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor(sorted.length / 2)]!;
}

test(
  "four real captures compose into every Template × Look: each PNG has the native size, decodes, is opaque, and the COMPOSE button shows one (Panorama rotated)",
  { tag: ["@m7"] },
  async ({ app, operatorPage, hooks, camera }, testInfo) => {
    test.setTimeout(240_000);
    const screen = await openDiagnostics(operatorPage, hooks, camera);
    await waitForFreshFrames(hooks);

    await test.step("capture four real photographs", async () => {
      for (let index = 0; index < 4; index++) {
        const capture = await hooks.captureDiagnostics();
        expect(capture.width).toBeGreaterThanOrEqual(1280);
      }
    });

    const started = performance.now();
    const timings: number[] = [];
    let last: { previewKey: string; templateId: string; lookId: string } | undefined;
    const outputFolder = path.join(paths.e2eOutput, "m7");
    mkdirSync(outputFolder, { recursive: true });

    await test.step("compose every Template × Look, fetch each PNG and check it", async () => {
      for (const template of templates) {
        for (const look of looks) {
          const composeStarted = performance.now();
          const composed = await hooks.composeDiagnostics(template.id, look.id);
          timings.push(performance.now() - composeStarted);
          const label = `${template.id} × ${look.id}`;

          expect(composed.previewKey, label).toMatch(new RegExp(`^${template.id}\\.${look.id}\\.r\\d+$`));
          expect([composed.width, composed.height], `${label}: the hook's size is the Template's native size`).toEqual([
            template.width,
            template.height,
          ]);

          const fetched = await fetchPreview(operatorPage, composed.previewKey);
          expect(fetched.status, `${label}: the preview is served`).toBe(200);
          expect(fetched.contentType, label).toBe("image/png");
          expect(fetched.cacheControl, label).toBe("no-store");
          const bytes = Buffer.from(fetched.base64, "base64");
          expect(bytes.byteLength, `${label}: the hook's byteLength is the served size`).toBe(composed.byteLength);

          const png = PNG.sync.read(bytes);
          expect([png.width, png.height], `${label}: the PNG's native size`).toEqual([template.width, template.height]);
          let translucent = 0;
          for (let at = 3; at < png.data.length; at += 4) {
            if (png.data[at] !== 255) {
              translucent++;
            }
          }
          expect(translucent, `${label}: every pixel is opaque`).toBe(0);

          if (look.id === "classic-v1" && (template.id === "signature-strip" || template.id === "panorama-four")) {
            writeFileSync(path.join(outputFolder, `${template.id}.${look.id}.png`), bytes);
          }
          last = { previewKey: composed.previewKey, templateId: template.id, lookId: look.id };
        }
      }
    });

    const loopMs = performance.now() - started;
    const summary = `${String(timings.length)} compositions in ${loopMs.toFixed(0)} ms (hook round trip, fetch and decode included); hook round trip median ${median(timings).toFixed(0)} ms, min ${Math.min(...timings).toFixed(0)} ms, max ${Math.max(...timings).toFixed(0)} ms`;
    console.log(summary);
    testInfo.annotations.push({ type: "compositions", description: summary });
    expect(timings).toHaveLength(templates.length * looks.length);

    await test.step("the screen shows the last composition the hook made", async () => {
      expect(last).toBeDefined();
      await expect(screen.composition).toHaveAttribute("data-composition-key", last!.previewKey);
      const template = templates.find((candidate) => candidate.id === last!.templateId)!;
      const look = looks.find((candidate) => candidate.id === last!.lookId)!;
      await expect(screen.compositionImage).toHaveAccessibleName(`${template.name}, ${look.name}`);
      await expect(screen.composition).toContainText(`${String(template.width)}`);
    });

    await test.step("COMPOSE composes the chosen Template and Look; Panorama Four is shown rotated", async () => {
      await screen.templateSelect.selectOption({ label: "Panorama Four" });
      await screen.lookSelect.selectOption({ label: "Sepia" });
      await screen.compose.click();

      await expect(screen.composition).toHaveAttribute("data-composition-key", /^panorama-four\.sepia-v1\.r\d+$/);
      await expect(screen.compositionImage).toHaveAccessibleName("Panorama Four, Sepia");
      await expect
        .poll(() => screen.compositionImage.evaluate((image: HTMLImageElement) => image.complete && image.naturalWidth), {
          message: "the composition image loaded",
        })
        .toBe(1800);
      expect(await screen.compositionImage.evaluate((image: HTMLImageElement) => image.naturalHeight)).toBe(600);
      await expect(screen.composition.locator("[data-rotated]"), "the landscape print is shown rotated").toHaveCount(1);
      const file = path.join(outputFolder, "composition-screen-panorama.png");
      await saveScreenshot(operatorPage, file);
      await testInfo.attach("composition-screen-panorama", { path: file, contentType: "image/png" });
    });

    await test.step("a portrait Template is shown upright", async () => {
      await screen.templateSelect.selectOption({ label: "Butter Gingham" });
      await screen.lookSelect.selectOption({ label: "Classic" });
      await screen.compose.click();

      await expect(screen.composition).toHaveAttribute("data-composition-key", /^butter-gingham\.classic-v1\.r\d+$/);
      await expect
        .poll(() => screen.compositionImage.evaluate((image: HTMLImageElement) => image.complete && image.naturalWidth), {
          message: "the composition image loaded",
        })
        .toBe(600);
      await expect(screen.composition.locator("[data-rotated]")).toHaveCount(0);
      await saveScreenshot(operatorPage, path.join(outputFolder, "composition-screen-portrait.png"));
    });

    expect((await app.close()).code).toBe(0);
  },
);
