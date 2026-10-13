import { expect, test } from "@playwright/test";
import { PNG } from "pngjs";
import { contentRegion, regionDiff } from "../../../src/visual";

// Unit tests for the pure visual-comparison helpers. They use tiny synthetic images and never launch the app.

type Color = readonly [number, number, number];

const background: Color = [250, 250, 250];
const ink: Color = [20, 20, 20];
const other: Color = [200, 30, 30];
const threshold = 0.1;
const limit = 0.01;

function image(width: number, height: number, color: Color = background): PNG {
  const png = new PNG({ width, height });
  fillRect(png, 0, 0, width, height, color);
  return png;
}

function fillRect(png: PNG, x: number, y: number, width: number, height: number, color: Color): void {
  for (let row = y; row < y + height; row++) {
    for (let column = x; column < x + width; column++) {
      const i = (row * png.width + column) * 4;
      png.data[i] = color[0];
      png.data[i + 1] = color[1];
      png.data[i + 2] = color[2];
      png.data[i + 3] = 255;
    }
  }
}

test.describe("visual comparison helpers", { tag: ["@m3"] }, () => {
  test("the content region is the padded bounding box of what differs from the background", () => {
    const png = image(200, 100);
    fillRect(png, 50, 40, 30, 20, ink);
    expect(contentRegion(png)).toEqual({ x: 42, y: 32, width: 46, height: 36 });
  });

  test("the content region is clamped to the image", () => {
    const png = image(100, 60);
    fillRect(png, 0, 0, 10, 10, ink);
    fillRect(png, 95, 55, 5, 5, ink);
    expect(contentRegion(png)).toEqual({ x: 0, y: 0, width: 100, height: 60 });
  });

  test("background noise within the tolerance is not content", () => {
    const png = image(50, 50);
    fillRect(png, 10, 10, 5, 5, [252, 248, 250]);
    expect(contentRegion(png)).toBeNull();
  });

  test("a blank baseline has no content region", () => {
    expect(contentRegion(image(64, 64))).toBeNull();
  });

  test("identical images have no differing pixels", () => {
    const expected = image(200, 100);
    fillRect(expected, 50, 40, 30, 20, ink);
    const region = contentRegion(expected);
    expect(region).not.toBeNull();
    const result = regionDiff(PNG.sync.read(PNG.sync.write(expected)), expected, region!, { threshold });
    expect(result).toMatchObject({ insidePixels: 0, outsidePixels: 0, ratio: 0 });
  });

  test("a 64x64 component that renders completely wrong now fails", () => {
    const viewportArea = 1280 * 720;
    const expected = image(1280, 720);
    fillRect(expected, 608, 328, 64, 64, ink);
    const actual = image(1280, 720);
    fillRect(actual, 608, 328, 64, 64, other);

    // The old measure: 4,096 differing pixels of a 921,600 px viewport is 0.44%, under the 1% limit.
    expect(64 * 64 / viewportArea).toBeLessThan(limit);

    const region = contentRegion(expected);
    expect(region).toEqual({ x: 600, y: 320, width: 80, height: 80 });
    const result = regionDiff(actual, expected, region!, { threshold });
    expect(result.insidePixels).toBe(64 * 64);
    expect(result.outsidePixels).toBe(0);
    expect(result.ratio).toBeGreaterThan(limit);
  });

  test("a component drawn in the same place with a few stray pixels stays under the limit", () => {
    const expected = image(1280, 720);
    fillRect(expected, 400, 300, 400, 120, ink);
    const actual = image(1280, 720);
    fillRect(actual, 400, 300, 400, 120, ink);
    fillRect(actual, 500, 350, 2, 2, other);
    const result = regionDiff(actual, expected, contentRegion(expected)!, { threshold });
    expect(result.insidePixels).toBe(4);
    expect(result.ratio).toBeLessThan(limit);
  });

  test("content that appears outside the Chromium region is counted", () => {
    const expected = image(300, 200);
    fillRect(expected, 100, 80, 60, 40, ink);
    const actual = image(300, 200);
    fillRect(actual, 100, 80, 60, 40, ink);
    fillRect(actual, 10, 10, 1, 1, other);
    const result = regionDiff(actual, expected, contentRegion(expected)!, { threshold });
    expect(result.insidePixels).toBe(0);
    expect(result.outsidePixels).toBe(1);
  });

  test("images of different sizes are rejected", () => {
    const expected = image(10, 10);
    expect(() => regionDiff(image(11, 10), expected, { x: 0, y: 0, width: 10, height: 10 }, { threshold })).toThrow("Image sizes differ");
  });
});
