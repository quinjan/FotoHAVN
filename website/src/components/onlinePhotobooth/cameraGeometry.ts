import { coverCrop } from "./presets";

/** Match the existing 4:3 source normalization, then the template's final crop. */
export function cameraPreviewGeometry(width: number, height: number, ratio: number) {
  if (![width, height, ratio].every((value) => Number.isFinite(value) && value > 0)) return null;
  const normalized = coverCrop(width, height);
  const crop = coverCrop(normalized.width, normalized.height, ratio);
  return {
    width: `${width / crop.width * 100}%`,
    height: `${height / crop.height * 100}%`,
    left: `${-(normalized.x + crop.x) / crop.width * 100}%`,
    top: `${-(normalized.y + crop.y) / crop.height * 100}%`,
  };
}
