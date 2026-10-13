import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

/** The four Guest Screen reference resolutions, in physical pixels. */
export const resolutions = [
  { width: 1280, height: 720 },
  { width: 1366, height: 768 },
  { width: 1920, height: 1080 },
  { width: 2560, height: 1440 },
] as const;

export type Resolution = (typeof resolutions)[number];

export function resolutionName({ width, height }: Resolution): string {
  return `${String(width)}x${String(height)}`;
}

/** Screenshots land in app/output/fixtures/ (git-ignored). */
export const screenshotDir = resolve(dirname(fileURLToPath(import.meta.url)), "../../../output/fixtures");
