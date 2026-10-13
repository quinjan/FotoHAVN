import path from "node:path";
import { fileURLToPath } from "node:url";

const e2eDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const appDir = path.resolve(e2eDir, "..");
const outputDir = path.join(appDir, "output");

/**
 * Redirects a run's results (reports, test results, runs/, camera.json) away from output/e2e/. The camera-precondition
 * regression sets it for the nested Playwright run it starts, so that run never clears or overwrites the outer run's files.
 */
export const outputEnvVar = "FOTOHAVN_E2E_OUTPUT";

const outputOverride = process.env[outputEnvVar];
const e2eOutputDir =
  outputOverride === undefined || outputOverride.trim() === "" ? path.join(outputDir, "e2e") : path.resolve(outputOverride);

/** Absolute paths shared by the config, global setup, and fixtures. */
export const paths = {
  e2eDir,
  appDir,
  uiDir: path.join(appDir, "ui"),
  /** Written by `npm run build:e2e`; the E2E build of the host copies it to wwwroot/. */
  uiDistE2E: path.join(appDir, "ui", "dist-e2e"),
  hostProject: "src/FotoHavn.Host.Windows/FotoHavn.Host.Windows.csproj",
  e2eBuildDir: path.join(outputDir, "e2e-build"),
  exe: path.join(outputDir, "e2e-build", "FOTOHAVN.exe"),
  e2eOutput: e2eOutputDir,
  htmlReport: path.join(e2eOutputDir, "html"),
  junitReport: path.join(e2eOutputDir, "junit.xml"),
  testResults: path.join(e2eOutputDir, "test-results"),
  /** One folder per app launch: its Data folder, stdout/stderr, logs, and listing. */
  runs: path.join(e2eOutputDir, "runs"),
  /** Written by global setup; read by the camera fixture. */
  cameraFile: path.join(e2eOutputDir, "camera.json"),
} as const;

/** The path relative to app/, with forward slashes, for messages. */
export function fromApp(absolutePath: string): string {
  return path.relative(appDir, absolutePath).split(path.sep).join("/");
}
