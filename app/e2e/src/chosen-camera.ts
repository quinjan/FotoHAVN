import { existsSync, readFileSync, writeFileSync } from "node:fs";
import type { Camera } from "./camera";
import { paths } from "./paths";

/** Global setup sets this (JSON) for the test workers it spawns; the file is the fallback. */
export const chosenCameraEnvVar = "FOTOHAVN_E2E_CHOSEN_CAMERA";

export function storeChosenCamera(camera: Camera): void {
  const json = JSON.stringify({ id: camera.id, name: camera.name });
  process.env[chosenCameraEnvVar] = json;
  writeFileSync(paths.cameraFile, `${json}\n`);
}

export function readChosenCamera(): Camera {
  const json =
    process.env[chosenCameraEnvVar] ?? (existsSync(paths.cameraFile) ? readFileSync(paths.cameraFile, "utf8") : undefined);
  if (json === undefined) {
    throw new Error("No E2E camera was chosen. Run the tests through Playwright so global setup checks the camera.");
  }
  const parsed = JSON.parse(json) as Partial<Camera>;
  if (typeof parsed.id !== "string" || typeof parsed.name !== "string") {
    throw new Error(`The chosen E2E camera is malformed: ${json}`);
  }
  return { id: parsed.id, name: parsed.name };
}
