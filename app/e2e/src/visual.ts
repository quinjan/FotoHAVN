import pixelmatch from "pixelmatch";
import type { PNG } from "pngjs";

// Pure image helpers for the WebView2 visual comparison. The tolerance is measured over the fixture's content region,
// not the viewport, so a small component that renders wrong can't hide in a large, empty frame.

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

/** Counts the pixels that differ inside the region (the ratio's numerator) and anywhere outside it (which must be zero). */
export function regionDiff(actual: PNG, expected: PNG, region: Region, options: RegionDiffOptions): RegionDiff {
  if (actual.width !== expected.width || actual.height !== expected.height) {
    throw new Error(`Image sizes differ: ${String(actual.width)}x${String(actual.height)} vs ${String(expected.width)}x${String(expected.height)}.`);
  }
  const { width, height } = actual;
  const ignoreOutside = new Uint8Array(width * height).fill(1);
  const ignoreInside = new Uint8Array(width * height);
  for (let y = region.y; y < region.y + region.height; y++) {
    for (let x = region.x; x < region.x + region.width; x++) {
      ignoreOutside[y * width + x] = 0;
      ignoreInside[y * width + x] = 1;
    }
  }

  // Ignored pixels still count as neighbors for anti-aliasing detection, so the split doesn't change what differs.
  const pixelOptions = { threshold: options.threshold };
  const insidePixels = pixelmatch(actual.data, expected.data, null, width, height, { ...pixelOptions, ignoreMask: ignoreOutside });
  const outsidePixels = pixelmatch(actual.data, expected.data, null, width, height, { ...pixelOptions, ignoreMask: ignoreInside });
  if (options.diff) pixelmatch(actual.data, expected.data, options.diff.data, width, height, pixelOptions);
  const regionArea = region.width * region.height;
  return { region, regionArea, insidePixels, outsidePixels, ratio: insidePixels / regionArea };
}
