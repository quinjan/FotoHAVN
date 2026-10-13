import path from "node:path";
import { fileURLToPath } from "node:url";

const e2eDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const appDir = path.resolve(e2eDir, "..");
const outputDir = path.join(appDir, "output");
const e2eOutputDir = path.join(outputDir, "e2e");

/** Absolute paths shared by the config, global setup, and fixtures. */
export const paths = {
  e2eDir,
  appDir,
  uiDir: path.join(appDir, "ui"),
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
