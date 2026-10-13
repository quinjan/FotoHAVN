/**
 * The live preview's camera geometry, ported (not imported) from the website's online booth:
 * `website/src/components/onlinePhotobooth/presets.ts` (`coverCrop`), `cameraGeometry.ts` (`cameraPreviewGeometry`),
 * and `CameraFrameGuide.tsx` (`cameraGuide`). A camera frame is first normalized to a centered 4:3 crop, then the
 * target photograph window's ratio is center-cropped from that, exactly as the website frames its photographs.
 * The tests carry the website's cases. Pure functions only; no DOM.
 */

/** A rectangle in source pixels. */
export interface PhotoRect {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

/** A crop guide's box: in view pixels from `cameraGuide`, or in fractions of the frame from `cameraGuideFraction`. */
export interface GuideBox {
  readonly left: number;
  readonly top: number;
  readonly width: number;
  readonly height: number;
}

/** The website's preview placement: CSS percentages that map the template crop onto the frame. */
export interface PreviewGeometry {
  readonly width: string;
  readonly height: string;
  readonly left: string;
  readonly top: string;
}

function usable(...values: readonly number[]): boolean {
  return values.every((value) => Number.isFinite(value) && value > 0);
}

/** The largest centered crop of `targetRatio` (width / height, default 4:3) inside a width × height image. */
export function coverCrop(width: number, height: number, targetRatio = 4 / 3): PhotoRect {
  if (width <= 0 || height <= 0 || !Number.isFinite(width + height)) {
    throw new Error("The photograph has no usable dimensions.");
  }
  const cropWidth = Math.min(width, height * targetRatio);
  const cropHeight = cropWidth / targetRatio;
  return { x: (width - cropWidth) / 2, y: (height - cropHeight) / 2, width: cropWidth, height: cropHeight };
}

/** Match the 4:3 source normalization, then the template's final crop, as CSS percentages for a covering preview. */
export function cameraPreviewGeometry(width: number, height: number, ratio: number): PreviewGeometry | null {
  if (!usable(width, height, ratio)) {
    return null;
  }
  const normalized = coverCrop(width, height);
  const crop = coverCrop(normalized.width, normalized.height, ratio);
  return {
    width: `${String((width / crop.width) * 100)}%`,
    height: `${String((height / crop.height) * 100)}%`,
    left: `${String((-(normalized.x + crop.x) / crop.width) * 100)}%`,
    top: `${String((-(normalized.y + crop.y) / crop.height) * 100)}%`,
  };
}

/**
 * The crop guide for a source frame contained (letterboxed, centered) in a view: the normalized 4:3 crop, then the
 * window's `ratio` (width / height), in view pixels. Null when any size isn't a positive finite number.
 */
export function cameraGuide(
  sourceWidth: number,
  sourceHeight: number,
  viewWidth: number,
  viewHeight: number,
  ratio: number,
): GuideBox | null {
  if (!usable(sourceWidth, sourceHeight, viewWidth, viewHeight, ratio)) {
    return null;
  }
  const source = coverCrop(sourceWidth, sourceHeight);
  const width = Math.min(source.width, source.height * ratio);
  const height = Math.min(source.height, source.width / ratio);
  const scale = Math.min(viewWidth / sourceWidth, viewHeight / sourceHeight);
  return {
    left: (viewWidth - width * scale) / 2,
    top: (viewHeight - height * scale) / 2,
    width: width * scale,
    height: height * scale,
  };
}

/**
 * The same guide as fractions (0–1) of the frame itself, for a preview whose box always has the frame's proportion,
 * so the overlay needs no measuring: `cameraGuide` with the view equal to the source.
 */
export function cameraGuideFraction(sourceWidth: number, sourceHeight: number, ratio: number): GuideBox | null {
  const guide = cameraGuide(sourceWidth, sourceHeight, sourceWidth, sourceHeight, ratio);
  if (guide === null) {
    return null;
  }
  return {
    left: guide.left / sourceWidth,
    top: guide.top / sourceHeight,
    width: guide.width / sourceWidth,
    height: guide.height / sourceHeight,
  };
}
