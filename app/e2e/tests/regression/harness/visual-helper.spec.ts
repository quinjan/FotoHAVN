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

  test.describe("1-pixel displacement tolerance", () => {
    const ebony: Color = [38, 32, 30];
    const softBrown: Color = [140, 110, 90];
    const fill: Color = [236, 228, 216];

    /** A 64x64 component: a 1-px Ebony border around a light fill, on the page background. */
    function box(x: number, y: number, border: boolean = true): PNG {
      const png = image(1280, 720);
      fillRect(png, x, y, 64, 64, border ? ebony : fill);
      if (border) fillRect(png, x + 1, y + 1, 62, 62, fill);
      return png;
    }

    function compare(actual: PNG, expected: PNG) {
      const region = contentRegion(expected);
      expect(region).not.toBeNull();
      return regionDiff(actual, expected, region!, { threshold });
    }

    test("a rectangle edge drawn 1 px to the side passes, in every direction", () => {
      const expected = box(600, 320);
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1]] as const) {
        const result = compare(box(600 + dx, 320 + dy), expected);
        expect(result, `shifted by ${String(dx)},${String(dy)}`).toMatchObject({ insidePixels: 0, outsidePixels: 0 });
      }
    });

    test("a 1-px line drawn 1 px away passes", () => {
      const expected = image(300, 200);
      fillRect(expected, 100, 80, 60, 40, fill);
      fillRect(expected, 100, 80, 1, 40, ebony);
      const actual = image(300, 200);
      fillRect(actual, 100, 80, 60, 40, fill);
      fillRect(actual, 101, 80, 1, 40, ebony);
      const result = regionDiff(actual, expected, contentRegion(expected)!, { threshold });
      expect(result.insidePixels).toBe(0);
    });

    test("a 1-px line that is missing fails even though the pixel beside it looks like the background", () => {
      const expected = image(300, 200);
      fillRect(expected, 100, 80, 60, 40, fill);
      fillRect(expected, 100, 80, 1, 40, ebony);
      const actual = image(300, 200);
      fillRect(actual, 100, 80, 60, 40, fill);
      const result = regionDiff(actual, expected, contentRegion(expected)!, { threshold });
      expect(result.insidePixels).toBe(40);
    });

    test("an edge drawn 2 px to the side fails", () => {
      const result = compare(box(602, 320), box(600, 320));
      expect(result.outsidePixels).toBe(0);
      expect(result.insidePixels).toBeGreaterThan(0);
      expect(result.ratio).toBeGreaterThan(limit);
    });

    test("a border that is missing entirely fails", () => {
      const result = compare(box(600, 320, false), box(600, 320));
      // The 252 border pixels have no Ebony anywhere around them in the other image.
      expect(result.insidePixels).toBe(252);
      expect(result.ratio).toBeGreaterThan(limit);
    });

    test("a border that is missing along with its fill fails", () => {
      const expected = image(1280, 720);
      fillRect(expected, 600, 320, 64, 64, ebony);
      fillRect(expected, 601, 321, 62, 62, background);
      const result = compare(image(1280, 720), expected);
      expect(result.insidePixels).toBe(252);
      expect(result.ratio).toBeGreaterThan(limit);
    });

    test("a changed fill color fails (Ebony to Soft Brown)", () => {
      const expected = image(1280, 720);
      fillRect(expected, 600, 320, 64, 64, ebony);
      const actual = image(1280, 720);
      fillRect(actual, 600, 320, 64, 64, softBrown);
      const result = compare(actual, expected);
      expect(result.insidePixels).toBe(64 * 64);
      expect(result.ratio).toBeGreaterThan(limit);
    });

    test("a missing text-like glyph block fails", () => {
      const expected = image(1280, 720);
      fillRect(expected, 600, 320, 80, 24, fill);
      // Four 'glyphs': 6x12 blocks with a 1-px counter, 2 px apart.
      for (let glyph = 0; glyph < 4; glyph++) {
        fillRect(expected, 606 + glyph * 8, 326, 6, 12, ebony);
        fillRect(expected, 608 + glyph * 8, 329, 2, 6, fill);
      }
      const actual = image(1280, 720);
      fillRect(actual, 600, 320, 80, 24, fill);
      const result = compare(actual, expected);
      expect(result.insidePixels).toBeGreaterThan(4 * 40);
      expect(result.ratio).toBeGreaterThan(limit);
    });

    test("a fully changed 64x64 component fails", () => {
      const expected = box(600, 320);
      const actual = image(1280, 720);
      fillRect(actual, 600, 320, 64, 64, softBrown);
      fillRect(actual, 610, 330, 44, 44, ebony);
      const result = compare(actual, expected);
      expect(result.ratio).toBeGreaterThan(limit);
    });

    test("a blank render of a baseline with content fails", () => {
      const result = compare(image(1280, 720), box(600, 320));
      expect(result.insidePixels).toBeGreaterThan(0);
      expect(result.ratio).toBeGreaterThan(limit);
    });

    test("content a 1-px shift can't explain outside the region still fails", () => {
      const expected = box(600, 320);
      const actual = box(600, 320);
      fillRect(actual, 20, 20, 1, 1, ebony);
      expect(compare(actual, expected)).toMatchObject({ insidePixels: 0, outsidePixels: 1 });
    });

    test("a 1-px shift does not hide a wrong pixel next to it", () => {
      const expected = box(600, 320);
      const actual = box(601, 320);
      fillRect(actual, 620, 340, 3, 3, softBrown);
      const result = compare(actual, expected);
      expect(result.insidePixels).toBe(9);
    });

    test("the diff image marks only the pixels that still differ", () => {
      const expected = box(600, 320);
      const actual = box(601, 320);
      fillRect(actual, 620, 340, 1, 1, softBrown);
      const diff = new PNG({ width: 1280, height: 720 });
      const result = regionDiff(actual, expected, contentRegion(expected)!, { threshold, diff });
      expect(result.insidePixels).toBe(1);
      const i = (340 * 1280 + 620) * 4;
      expect([diff.data[i], diff.data[i + 1], diff.data[i + 2]]).toEqual([255, 0, 0]);
      const untouched = (100 * 1280 + 100) * 4;
      expect(diff.data[untouched]).toBeGreaterThan(200);
    });
  });
});
