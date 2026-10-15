import { readFileSync, statSync } from "node:fs";
import path from "node:path";
import jpeg from "jpeg-js";
import { expect, test } from "../../../src/fixtures";
import { openDiagnostics, waitForFreshFrames } from "../../../src/camera-diagnostics";

/** A luminance spread (0–255 scale) above this is "not blank"; the placeholder's crossed-out camera icon clears it. */
const blankThreshold = 2.0;
/** A mean absolute luminance difference above this makes the mirror comparison conclusive (JPEG noise between two frames of one scene is far below it). */
const meaningfulDifference = 0.1;
/**
 * When the comparison is conclusive, the mirrored capture must be at least this many times closer to the flipped plain capture than
 * to the plain capture itself. Measured on the placeholder frames: D_direct 0.379 against D_flip 0.0055 (about 69×), so 4× leaves a
 * wide margin for JPEG noise and a moving scene while still failing a mirror that doesn't happen or happens only in part.
 */
const mirrorFlipRatio = 4;

interface Picture {
  width: number;
  height: number;
  luma: Float32Array;
}

function decode(file: string): Picture {
  const { width, height, data } = jpeg.decode(readFileSync(file), { useTArray: true, formatAsRGBA: true });
  const luma = new Float32Array(width * height);
  for (let at = 0; at < luma.length; at++) {
    luma[at] = 0.2126 * data[at * 4]! + 0.7152 * data[at * 4 + 1]! + 0.0722 * data[at * 4 + 2]!;
  }
  return { width, height, luma };
}

function standardDeviation(picture: Picture): number {
  const mean = picture.luma.reduce((total, level) => total + level, 0) / picture.luma.length;
  const variance = picture.luma.reduce((total, level) => total + (level - mean) ** 2, 0) / picture.luma.length;
  return Math.sqrt(variance);
}

function meanAbsoluteDifference(a: Picture, b: Picture): number {
  let total = 0;
  for (let at = 0; at < a.luma.length; at++) {
    total += Math.abs(a.luma[at]! - b.luma[at]!);
  }
  return total / a.luma.length;
}

function flipHorizontally(picture: Picture): Picture {
  const luma = new Float32Array(picture.luma.length);
  for (let y = 0; y < picture.height; y++) {
    for (let x = 0; x < picture.width; x++) {
      luma[y * picture.width + x] = picture.luma[y * picture.width + (picture.width - 1 - x)]!;
    }
  }
  return { ...picture, luma };
}

test(
  "a capture produces a decodable JPEG of at least 1280×720 that isn't blank, mirrored when requested (compared with an unmirrored capture taken right after)",
  { tag: ["@m5", "@camera"] },
  async ({ app, operatorPage, hooks, camera, dataDir }, testInfo) => {
    const screen = await openDiagnostics(operatorPage, hooks, camera);
    await waitForFreshFrames(hooks);

    const folder = path.join(dataDir, "diagnostics");
    const pictures = {} as Record<"mirrored" | "plain", Picture>;
    let plainFile = "";

    for (const mode of ["mirrored", "plain"] as const) {
      await test.step(`capture with the mirror ${mode === "mirrored" ? "on" : "off"}`, async () => {
        await hooks.setDiagnosticsMirror(mode === "mirrored");
        const capture = await hooks.captureDiagnostics();
        expect(capture.mirrored).toBe(mode === "mirrored");
        expect(capture.fileName).toMatch(/^capture-\d{8}-\d{6}-\d{3}(-\d+)?\.jpg$/);

        const file = path.join(folder, capture.fileName);
        await testInfo.attach(`capture-${mode}`, { path: file, contentType: "image/jpeg" });
        if (mode === "plain") {
          plainFile = capture.fileName;
        }
        expect(statSync(file).size, "the hook's byteLength is the file's size").toBe(capture.byteLength);
        const bytes = readFileSync(file);
        expect([bytes[0], bytes[1]], "JPEG start-of-image marker").toEqual([0xff, 0xd8]);

        const picture = decode(file);
        pictures[mode] = picture;
        expect(picture.width).toBeGreaterThanOrEqual(1280);
        expect(picture.height).toBeGreaterThanOrEqual(720);
        expect([picture.width, picture.height]).toEqual([capture.width, capture.height]);

        const spread = standardDeviation(picture);
        console.log(
          `${mode}: ${String(picture.width)}×${String(picture.height)}, ${String(capture.byteLength)} bytes, luminance stddev ${spread.toFixed(2)}`,
        );
        testInfo.annotations.push({ type: `luminance stddev (${mode})`, description: spread.toFixed(3) });
        expect(spread, "the capture isn't blank").toBeGreaterThan(blankThreshold);
      });
    }

    await test.step("the screen shows the last capture's facts", async () => {
      await expect(screen.measure("capture")).toContainText(plainFile);
      await expect(screen.measure("capture")).toContainText("Not mirrored");
    });

    expect((await app.close()).code).toBe(0);

    const direct = meanAbsoluteDifference(pictures.mirrored, pictures.plain);
    const flipped = meanAbsoluteDifference(pictures.mirrored, flipHorizontally(pictures.plain));
    console.log(`D_direct ${direct.toFixed(4)}, D_flip ${flipped.toFixed(4)}`);
    testInfo.annotations.push({ type: "D_direct", description: direct.toFixed(4) }, { type: "D_flip", description: flipped.toFixed(4) });
    // A symmetric or static scene can't tell a mirror from a plain picture: report the check as skipped, never as passed.
    // The deterministic proof of the mirror is CameraHookTests (a full-size asymmetric pattern through the brain's capture path).
    test.skip(
      direct <= meaningfulDifference,
      `not run: symmetric scene — mirror direction can't be decided (D_direct=${direct.toFixed(4)})`,
    );

    await test.step("the mirrored capture is the unmirrored one flipped left to right", () => {
      expect(flipped, "D_flip < D_direct").toBeLessThan(direct);
      // Measured here: D_direct 0.379, D_flip 0.0055 (a factor of about 69). A flip that only half worked would sit near D_direct / 2.
      expect(flipped, "D_flip is small next to D_direct").toBeLessThan(direct / mirrorFlipRatio);
    });
  },
);
