import { describe, expect, it } from "vitest";
import { cameraGuide, cameraGuideFraction, cameraPreviewGeometry, coverCrop, type GuideBox } from "./cameraGeometry.ts";

// Cases ported from the website's tests: website/scripts/online-photobooth.test.mjs (coverCrop, the exact camera
// preview) and website/scripts/online-photobooth-preview.test.mjs (camera framing).

/** The photograph windows of the catalog's Templates (implementation plan, Catalog), width / height. */
const windowRatios = {
  "signature-strip": 600 / 450,
  "panorama-four": 450 / 600,
  "butter-gingham": 547 / 522,
  "polka-keepsake": 513 / 390,
} as const;

function expectBox(actual: GuideBox | null, expected: GuideBox): void {
  expect(actual).not.toBeNull();
  for (const key of ["left", "top", "width", "height"] as const) {
    expect(actual?.[key]).toBeCloseTo(expected[key], 10);
  }
}

describe("coverCrop", () => {
  it("takes the largest centered 4:3 crop by default", () => {
    expect(coverCrop(1200, 1600)).toEqual({ x: 0, y: 350, width: 1200, height: 900 });
    expect(coverCrop(1600, 900)).toEqual({ x: 200, y: 0, width: 1200, height: 900 });
  });

  it("takes another ratio when asked", () => {
    expect(coverCrop(1200, 900, 3 / 4)).toEqual({ x: 262.5, y: 0, width: 675, height: 900 });
  });

  it("refuses an image with no usable dimensions", () => {
    expect(() => coverCrop(0, 0)).toThrow("The photograph has no usable dimensions.");
    expect(() => coverCrop(1280, Number.POSITIVE_INFINITY)).toThrow();
  });
});

describe("cameraGuide", () => {
  it("matches the normalized 4:3 source and portrait and landscape template crops", () => {
    expect(cameraGuide(1280, 720, 1200, 600, 0.75)).toEqual({ left: 375, top: 0, width: 450, height: 600 });
    expect(cameraGuide(720, 1280, 320, 480, 4 / 3)).toEqual({ left: 25, top: 138.75, width: 270, height: 202.5 });
  });

  it("is null for an unusable size or ratio", () => {
    expect(cameraGuide(0, 720, 1200, 600, 0.75)).toBeNull();
    expect(cameraGuide(1280, 720, 1200, 0, 0.75)).toBeNull();
    expect(cameraGuide(1280, 720, 1200, 600, Number.NaN)).toBeNull();
    expect(cameraGuide(1280, Number.POSITIVE_INFINITY, 1200, 600, 0.75)).toBeNull();
  });
});

describe("cameraGuideFraction", () => {
  it("is the guide in fractions of the frame, for each catalog window on a 16:9 frame", () => {
    // 1920 × 1080 normalizes to a centered 1440 × 1080; each window is center-cropped from that.
    expectBox(cameraGuideFraction(1920, 1080, windowRatios["signature-strip"]), {
      left: 240 / 1920,
      top: 0,
      width: 1440 / 1920,
      height: 1,
    });
    expectBox(cameraGuideFraction(1920, 1080, windowRatios["panorama-four"]), {
      left: (1920 - 810) / 2 / 1920,
      top: 0,
      width: 810 / 1920,
      height: 1,
    });
    for (const ratio of Object.values(windowRatios)) {
      const guide = cameraGuideFraction(1920, 1080, ratio);
      expect(guide).not.toBeNull();
      if (guide !== null) {
        expect(guide.left + guide.width / 2).toBeCloseTo(0.5, 10);
        expect(guide.top + guide.height / 2).toBeCloseTo(0.5, 10);
        expect((guide.width * 1920) / (guide.height * 1080)).toBeCloseTo(ratio, 10);
      }
    }
  });

  it("agrees with cameraGuide in any view that holds the frame's proportion", () => {
    const fraction = cameraGuideFraction(1280, 720, 0.75);
    const pixels = cameraGuide(1280, 720, 640, 360, 0.75);
    expect(fraction).not.toBeNull();
    expect(pixels).not.toBeNull();
    if (fraction !== null && pixels !== null) {
      expect(fraction.left * 640).toBeCloseTo(pixels.left, 10);
      expect(fraction.top * 360).toBeCloseTo(pixels.top, 10);
      expect(fraction.width * 640).toBeCloseTo(pixels.width, 10);
      expect(fraction.height * 360).toBeCloseTo(pixels.height, 10);
    }
  });

  it("is null for an unusable size or ratio", () => {
    expect(cameraGuideFraction(0, 1080, 0.75)).toBeNull();
    expect(cameraGuideFraction(1920, 1080, 0)).toBeNull();
  });
});

describe("cameraPreviewGeometry", () => {
  it("maps template crops back onto portrait and landscape sensor pixels", () => {
    for (const [width, height] of [
      [720, 1280],
      [1920, 1080],
      [1280, 960],
    ] as const) {
      for (const ratio of Object.values(windowRatios)) {
        const geometry = cameraPreviewGeometry(width, height, ratio);
        expect(geometry).not.toBeNull();
        if (geometry === null) {
          continue;
        }
        const normalized = coverCrop(width, height);
        const crop = coverCrop(normalized.width, normalized.height, ratio);
        const previewCropWidth = width / (parseFloat(geometry.width) / 100);
        const previewCropHeight = height / (parseFloat(geometry.height) / 100);
        expect(Math.abs(previewCropWidth - crop.width)).toBeLessThan(0.00001);
        expect(Math.abs(previewCropHeight - crop.height)).toBeLessThan(0.00001);
        expect(Math.abs((-parseFloat(geometry.left) / 100) * crop.width - normalized.x - crop.x)).toBeLessThan(0.00001);
        expect(Math.abs((-parseFloat(geometry.top) / 100) * crop.height - normalized.y - crop.y)).toBeLessThan(0.00001);
      }
    }
  });

  it("is null for an unusable size or ratio", () => {
    expect(cameraPreviewGeometry(0, 1280, 0.75)).toBeNull();
    expect(cameraPreviewGeometry(720, 1280, Number.NaN)).toBeNull();
  });
});
