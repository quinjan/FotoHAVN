import pixelmatch from "pixelmatch";
import type { PNG } from "pngjs";

// Pure image helpers for the WebView2 visual comparison. The tolerance is measured over the fixture's content region,
// not the viewport, so a small component that renders wrong can't hide in a large, empty frame.
//
// The baselines come from headless Chromium's software rasterizer and the actual screenshots from WebView2's GPU
// compositor. The two place fractional layout values (clamp() sizes, CSS rotations) and anti-alias edges up to one pixel
// apart, so a pixel only counts as different when no 1-pixel displacement explains it (see `regionDiff`).

export interface Region {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

export interface RegionDiffOptions {
  /** Per-pixel color distance, as a pixelmatch threshold (0 strict, 1 lenient). */
  readonly threshold: number;
  /** Optional output image the differing pixels are drawn into. */
  readonly diff?: PNG;
}

export interface RegionDiff {
  readonly region: Region;
  readonly regionArea: number;
  readonly insidePixels: number;
  readonly outsidePixels: number;
  /** insidePixels / regionArea. */
  readonly ratio: number;
}

/** A pixel within this per-channel distance of the background color is background. */
export const backgroundTolerance = 2;
/** Pixels added on every side of the content bounding box. */
export const regionPadding = 8;

/**
 * The bounding box of the pixels that differ from the background (the color of the top-left pixel), padded and clamped
 * to the image. Returns null when every pixel is background: a blank fixture has no region.
 */
export function contentRegion(png: PNG, tolerance = backgroundTolerance, padding = regionPadding): Region | null {
  const { width, height, data } = png;
  if (width === 0 || height === 0) return null;
  const [r0, g0, b0, a0] = [data[0] ?? 0, data[1] ?? 0, data[2] ?? 0, data[3] ?? 0];

  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const i = (y * width + x) * 4;
      const isBackground =
        Math.abs((data[i] ?? 0) - r0) <= tolerance &&
        Math.abs((data[i + 1] ?? 0) - g0) <= tolerance &&
        Math.abs((data[i + 2] ?? 0) - b0) <= tolerance &&
        Math.abs((data[i + 3] ?? 0) - a0) <= tolerance;
      if (isBackground) continue;
      if (x < minX) minX = x;
      if (x > maxX) maxX = x;
      if (y < minY) minY = y;
      if (y > maxY) maxY = y;
    }
  }
  if (maxX < 0) return null;

  const x = Math.max(0, minX - padding);
  const y = Math.max(0, minY - padding);
  return { x, y, width: Math.min(width - 1, maxX + padding) - x + 1, height: Math.min(height - 1, maxY + padding) - y + 1 };
}

interface Pixels {
  readonly data: Uint8Array;
  readonly width: number;
  readonly height: number;
}

/** Copies a w x h block whose top-left corner is (x, y). The block must lie inside the image. */
function crop(image: Pixels, x: number, y: number, w: number, h: number): Uint8Array {
  const out = new Uint8Array(w * h * 4);
  for (let row = 0; row < h; row++) {
    const start = ((y + row) * image.width + x) * 4;
    out.set(image.data.subarray(start, start + w * 4), row * w * 4);
  }
  return out;
}

/**
 * Compares a w x h block of `a` (top-left at ax, ay) with a block of `b` (top-left at bx, by) using pixelmatch's own
 * per-pixel color distance (HyAB distance in OKLab, over `threshold`) and returns one flag per block pixel: 1 when the pair
 * is further apart than the threshold. With includeAA the pairs are compared by color alone; without it pixelmatch also
 * leaves out pixels it recognizes as anti-aliasing. diffMask makes pixelmatch draw only the differing pixels, which is how they
 * are read.
 */
function mismatchFlags(a: Pixels, ax: number, ay: number, b: Pixels, bx: number, by: number, w: number, h: number, threshold: number, includeAA: boolean): Uint8Array {
  const marks = new Uint8Array(w * h * 4);
  pixelmatch(crop(a, ax, ay, w, h), crop(b, bx, by, w, h), marks, w, h, { threshold, includeAA, diffMask: true });
  const flags = new Uint8Array(w * h);
  for (let i = 0; i < flags.length; i++) flags[i] = marks[i * 4 + 3] === 0 ? 0 : 1;
  return flags;
}

/**
 * Per-pixel mask (1 = differs) of the pixels that are a real difference. A pixel is a candidate when its colors differ at
 * the same position and pixelmatch's own anti-aliasing detection doesn't excuse it (that is the comparison this rule
 * refines, so it can only reduce the count). A candidate is excused only when a 1-pixel displacement explains it in BOTH
 * directions: the actual
 * pixel matches some pixel of the expected image's 3x3 neighborhood AND the expected pixel matches some pixel of the actual
 * image's 3x3 neighborhood (all within the same color threshold). Requiring both is what keeps the check strict: a 1-px
 * line that is missing (or added) has a neighbor of the other image that looks like the changed pixel, but the line's own
 * color appears nowhere around it in the other image, so it still counts. Neighbors outside the image don't exist.
 */
function differenceMask(actual: Pixels, expected: Pixels, threshold: number): Uint8Array {
  const { width, height } = actual;
  const mask = new Uint8Array(width * height);
  const same = mismatchFlags(actual, 0, 0, expected, 0, 0, width, height, threshold, false);

  // Only the candidates' bounding box can change, so the 16 shifted comparisons run on that box alone.
  let x0 = width;
  let y0 = height;
  let x1 = -1;
  let y1 = -1;
  for (let i = 0; i < same.length; i++) {
    if (same[i] === 0) continue;
    const x = i % width;
    const y = (i - x) / width;
    if (x < x0) x0 = x;
    if (x > x1) x1 = x;
    if (y < y0) y0 = y;
    if (y > y1) y1 = y;
  }
  if (x1 < 0) return mask;
  const actualMatchesExpectedNeighbor = new Uint8Array(width * height);
  const expectedMatchesActualNeighbor = new Uint8Array(width * height);

  for (let dy = -1; dy <= 1; dy++) {
    for (let dx = -1; dx <= 1; dx++) {
      if (dx === 0 && dy === 0) continue;
      // The candidates' columns/rows that have an in-bounds neighbor at (dx, dy).
      const left = Math.max(x0, -dx);
      const top = Math.max(y0, -dy);
      const w = Math.min(x1 + 1, width - dx) - left;
      const h = Math.min(y1 + 1, height - dy) - top;
      if (w <= 0 || h <= 0) continue;
      const actualVsShiftedExpected = mismatchFlags(actual, left, top, expected, left + dx, top + dy, w, h, threshold, true);
      const shiftedActualVsExpected = mismatchFlags(actual, left + dx, top + dy, expected, left, top, w, h, threshold, true);
      for (let row = 0; row < h; row++) {
        for (let column = 0; column < w; column++) {
          const k = row * w + column;
          const p = (top + row) * width + left + column;
          if (actualVsShiftedExpected[k] === 0) actualMatchesExpectedNeighbor[p] = 1;
          if (shiftedActualVsExpected[k] === 0) expectedMatchesActualNeighbor[p] = 1;
        }
      }
    }
  }
  for (let i = 0; i < mask.length; i++) {
    mask[i] = same[i] === 1 && !(actualMatchesExpectedNeighbor[i] === 1 && expectedMatchesActualNeighbor[i] === 1) ? 1 : 0;
  }
  return mask;
}

/**
 * Counts the pixels that differ inside the region (the ratio's numerator) and anywhere outside it (which must be zero).
 * A pixel differs only when no 1-pixel displacement explains it; see `differenceMask`.
 */
export function regionDiff(actual: PNG, expected: PNG, region: Region, options: RegionDiffOptions): RegionDiff {
  if (actual.width !== expected.width || actual.height !== expected.height) {
    throw new Error(`Image sizes differ: ${String(actual.width)}x${String(actual.height)} vs ${String(expected.width)}x${String(expected.height)}.`);
  }
  const { width, height } = actual;
  const mask = differenceMask(actual, expected, options.threshold);
  let insidePixels = 0;
  let outsidePixels = 0;
  for (let i = 0; i < mask.length; i++) {
    if (mask[i] === 0) continue;
    const x = i % width;
    const y = (i - x) / width;
    if (x >= region.x && x < region.x + region.width && y >= region.y && y < region.y + region.height) insidePixels++;
    else outsidePixels++;
  }

  if (options.diff) {
    const out = options.diff.data;
    for (let i = 0; i < width * height; i++) {
      const p = i * 4;
      if (mask[i] === 1) {
        out[p] = 255;
        out[p + 1] = 0;
        out[p + 2] = 0;
      } else {
        // Matching pixels as a faint gray, like pixelmatch's own diff image.
        const gray = 255 + ((0.299 * (expected.data[p] ?? 0) + 0.587 * (expected.data[p + 1] ?? 0) + 0.114 * (expected.data[p + 2] ?? 0)) - 255) * 0.1;
        out[p] = out[p + 1] = out[p + 2] = gray;
      }
      out[p + 3] = 255;
    }
  }
  const regionArea = region.width * region.height;
  return { region, regionArea, insidePixels, outsidePixels, ratio: insidePixels / regionArea };
}

