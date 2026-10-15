import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, rmSync } from "node:fs";
import path from "node:path";
import { cameraEnvVar, describeCamera, selectCamera, type Camera } from "./camera";
import { storeChosenCamera } from "./chosen-camera";
import { e2eBuildCommand, launchApp } from "./launch";
import { fromApp, paths } from "./paths";

/**
 * Builds the E2E configuration (unless FOTOHAVN_E2E_SKIP_BUILD=1), then launches the app once,
 * applies the camera precondition, and stores the chosen camera for the tests.
 */
export default async function globalSetup(): Promise<void> {
  prepareOutput();
  if (process.env.FOTOHAVN_E2E_SKIP_BUILD === "1") {
    console.log(`FOTOHAVN_E2E_SKIP_BUILD=1: reusing ${fromApp(paths.exe)}`);
  } else {
    buildE2E();
  }
  const camera = await checkCamera();
  storeChosenCamera(camera);
  console.log(describeCamera(camera));
}

/** Clears this run's camera file and runs/ folder; FOTOHAVN_E2E_OUTPUT points a nested run at its own folder. */
function prepareOutput(): void {
  mkdirSync(paths.e2eOutput, { recursive: true });
  rmSync(paths.cameraFile, { force: true });
  rmSync(path.join(paths.e2eOutput, "exclusive.lock"), { recursive: true, force: true });
  try {
    rmSync(paths.runs, { recursive: true, force: true });
  } catch (error) {
    console.warn(`Could not clear ${fromApp(paths.runs)}: ${String(error)}`);
  }
  mkdirSync(paths.runs, { recursive: true });
}

function buildE2E(): void {
  if (!existsSync(path.join(paths.uiDir, "package.json"))) {
    throw new Error(`The UI project is missing: ${fromApp(paths.uiDir)}/package.json does not exist.`);
  }
  if (!existsSync(path.join(paths.uiDir, "node_modules"))) {
    run("npm ci", paths.uiDir);
  }
  // build:e2e writes ui/dist-e2e (never ui/dist), which only the E2E build of the host copies to wwwroot/.
  run("npm run build:e2e", paths.uiDir);
  if (!existsSync(path.join(paths.uiDistE2E, "index.html"))) {
    throw new Error(`"npm run build:e2e" did not write ${fromApp(paths.uiDistE2E)}/index.html.`);
  }
  run(e2eBuildCommand, paths.appDir);
}

function run(command: string, cwd: string): void {
  console.log(`> ${command}  (in ${fromApp(cwd) || "."})`);
  // A shell resolves npm.cmd on Windows; every argument here is a fixed string.
  const result = spawnSync(command, { cwd, shell: true, stdio: "inherit" });
  if (result.error !== undefined) {
    throw new Error(`Could not run "${command}": ${result.error.message}`);
  }
  if (result.status !== 0) {
    throw new Error(`"${command}" failed with exit code ${result.status ?? "none"} (in ${fromApp(cwd) || "."}).`);
  }
}

async function checkCamera(): Promise<Camera> {
  const session = await launchApp({ name: "global-setup" });
  let cameras: Camera[] = [];
  let listError: unknown;
  try {
    cameras = await session.hooks.listCameras();
  } catch (error) {
    listError = error;
  }
  const exit = await session.close();
  if (listError !== undefined) {
    throw listError;
  }
  if (exit.code !== 0) {
    throw new Error(
      `FOTOHAVN exited with code ${exit.code ?? "none"} after test.exit in global setup. Evidence: ${fromApp(session.run.runDir)}`,
    );
  }
  return selectCamera(cameras, process.env[cameraEnvVar]);
}
