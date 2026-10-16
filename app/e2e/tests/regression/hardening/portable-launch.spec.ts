import { spawn, spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync } from "node:fs";
import path from "node:path";
import { expect, test } from "../../../src/fixtures";
import { paths } from "../../../src/paths";
import { findWindow, postClose } from "../../../src/win32";

const manifestPath = path.join(paths.appDir, "scripts", "webview2-fixed-version.json");
const publishScript = path.join(paths.appDir, "scripts", "publish-field-test.ps1");
// A folder with spaces in its name, under the (short) end-to-end output folder: a person unzips the field build wherever they like.
const workDir = path.join(paths.e2eOutput, "portable launch");
const zipDirectory = path.join(workDir, "zip output");
const publishDirectory = path.join(workDir, "publish output", "FOTOHAVN");
const unzipDirectory = path.join(workDir, "unzipped build with spaces");
// The folder the exe is launched from: an unrelated one that nothing may create files in, and that no process holds when workDir is removed.
const launchDirectory = path.join(paths.e2eOutput, "portable launch cwd");

/** Removes a scratch folder; WebView2's helper processes may hold files for a moment after the app exits, so it retries and never fails the test. */
function removeScratch(directory: string): void {
  try {
    rmSync(directory, { recursive: true, force: true, maxRetries: 20, retryDelay: 500 });
  } catch (error) {
    console.warn(`Could not remove ${directory}: ${error instanceof Error ? error.message : String(error)}`);
  }
}

interface LogLine {
  readonly event?: string;
  readonly kind?: string;
  readonly version?: string;
  readonly outcome?: string;
  readonly exitCode?: number;
}

function readLog(dataFolder: string): LogLine[] {
  const file = path.join(dataFolder, "logs", "fotohavn.jsonl");
  return existsSync(file)
    ? readFileSync(file, "utf8")
        .split(/\r?\n/)
        .filter((line) => line.trim() !== "")
        .map((line) => JSON.parse(line) as LogLine)
    : [];
}

interface Finished {
  readonly code: number | null;
  readonly elapsedMs: number;
}

/** Runs the field exe like a double-click from an unrelated folder and waits for it to exit. */
function runToExit(exe: string, args: readonly string[], timeoutMs: number): Promise<Finished> {
  return new Promise((resolve, reject) => {
    const startedAt = Date.now();
    const child = spawn(exe, args, { cwd: launchDirectory, stdio: "ignore" });
    const timer = setTimeout(() => {
      // Only this launch's own PID is ended, never a process by name.
      spawnSync("taskkill", ["/PID", String(child.pid), "/T", "/F"], { stdio: "ignore" });
      reject(new Error(`${exe} ${args.join(" ")} (PID ${String(child.pid)}) did not exit within ${String(timeoutMs)} ms.`));
    }, timeoutMs);
    child.once("error", (error) => {
      clearTimeout(timer);
      reject(error);
    });
    child.once("exit", (code) => {
      clearTimeout(timer);
      resolve({ code, elapsedMs: Date.now() - startedAt });
    });
  });
}

test.describe("portable launch of the field build", { tag: ["@m10", "@build"] }, () => {
  test.setTimeout(20 * 60_000);

  test.beforeAll(() => {
    removeScratch(workDir);
    removeScratch(launchDirectory);
    mkdirSync(zipDirectory, { recursive: true });
    mkdirSync(launchDirectory, { recursive: true });
  });

  test.afterAll(() => {
    removeScratch(workDir);
    removeScratch(launchDirectory);
  });

  test("the field zip unpacks into a folder with spaces, --smoke exits 0 on the bundled runtime with Data beside the exe, and a second launch exits 3", async () => {
    const manifest = JSON.parse(readFileSync(manifestPath, "utf8")) as { version: string };
    let zipPath = "";

    await test.step("pwsh scripts/publish-field-test.ps1 builds the field zip with the cached runtime", () => {
      const started = Date.now();
      const publish = spawnSync(
        "pwsh",
        ["-NoProfile", "-File", publishScript, "-Output", publishDirectory, "-ZipDirectory", zipDirectory],
        { cwd: paths.appDir, encoding: "utf8", maxBuffer: 64 * 1024 * 1024, timeout: 15 * 60_000 },
      );
      const output = `${publish.stdout}\n${publish.stderr}`;
      expect(publish.status, output.slice(-4_000)).toBe(0);
      const zips = readdirSync(zipDirectory).filter((name) => /^FOTOHAVN-field-.+\.zip$/.test(name));
      expect(zips, "one versioned zip").toHaveLength(1);
      zipPath = path.join(zipDirectory, zips[0] ?? "");
      const megabytes = statSync(zipPath).size / (1024 * 1024);
      expect(megabytes, "the zip carries the runtime").toBeGreaterThan(200);
      console.log(`Field build: ${zips[0] ?? ""}, ${megabytes.toFixed(0)} MiB, published in ${((Date.now() - started) / 1000).toFixed(0)} s`);
    });

    const root = path.join(unzipDirectory, "FOTOHAVN");
    const exe = path.join(root, "FOTOHAVN.exe");
    const dataFolder = path.join(root, "Data");

    await test.step("the zip unpacks into a path with spaces, with the exe, UI, and bundled runtime and no Data folder yet", () => {
      expect(unzipDirectory).toContain(" ");
      const unzip = spawnSync(
        "powershell.exe",
        ["-NoProfile", "-NonInteractive", "-Command", "Expand-Archive -LiteralPath $env:FH_ZIP -DestinationPath $env:FH_DESTINATION -Force"],
        { encoding: "utf8", timeout: 5 * 60_000, env: { ...process.env, FH_ZIP: zipPath, FH_DESTINATION: unzipDirectory } },
      );
      expect(unzip.status, `${unzip.stdout}\n${unzip.stderr}`).toBe(0);
      expect(existsSync(exe), "FOTOHAVN.exe").toBe(true);
      expect(existsSync(path.join(root, "wwwroot", "index.html")), "the field UI").toBe(true);
      expect(existsSync(path.join(root, "wwwroot", "e2e-build.txt")), "no E2E UI").toBe(false);
      expect(existsSync(path.join(root, "runtime", "webview2", "msedgewebview2.exe")), "the bundled runtime").toBe(true);
      expect(existsSync(path.join(root, "build-info.json"))).toBe(true);
      expect(existsSync(dataFolder), "no Data folder before the first launch").toBe(false);
    });

    await test.step("FOTOHAVN.exe --smoke exits 0, creates Data beside the exe, and runs on the Fixed Version runtime", async () => {
      const smoke = await runToExit(exe, ["--smoke"], 90_000);
      expect(smoke.code, "the smoke run's ready exit code").toBe(0);
      console.log(`--smoke exited 0 after ${(smoke.elapsedMs / 1000).toFixed(1)} s`);
      expect(existsSync(path.join(dataFolder, "fotohavn.db")), "Data/fotohavn.db beside the exe").toBe(true);
      expect(existsSync(path.join(dataFolder, "logs", "fotohavn.jsonl")), "Data/logs beside the exe").toBe(true);
      expect(existsSync(path.join(launchDirectory, "Data")), "nothing is created in the launching folder").toBe(false);

      const log = readLog(dataFolder);
      const runtime = log.find((line) => line.event === "webViewRuntime");
      expect(runtime, "the log names the WebView2 runtime").toBeDefined();
      expect(runtime?.kind).toBe("FixedVersion");
      expect(runtime?.version, "the bundled runtime is the one pinned in the manifest").toBe(manifest.version);
      expect(log.map((line) => line.event)).toContain("smokeReady");
      expect(log.find((line) => line.event === "exited")?.exitCode).toBe(0);
    });

    await test.step("while a normal launch runs on the Data folder, a second launch exits 3 at once, and the first still closes with 0", async () => {
      const first = spawn(exe, [], { cwd: launchDirectory, stdio: "ignore" });
      const firstExit = new Promise<number | null>((resolve) => first.once("exit", (code) => resolve(code)));
      try {
        await expect
          .poll(() => readLog(dataFolder).filter((line) => line.event === "ready").length, { timeout: 60_000, message: "the first launch's UI is ready" })
          .toBe(2);

        const second = await runToExit(exe, ["--smoke"], 20_000);
        expect(second.code, "a second launch on the same Data folder").toBe(3);
        console.log(`second launch exited 3 after ${String(second.elapsedMs)} ms`);

        const pid = first.pid;
        expect(pid).toBeDefined();
        const operator = findWindow(pid ?? 0, "FOTOHAVN");
        expect(operator, "the first launch's Operator window").toBeDefined();
        expect(postClose(operator?.handle ?? 0)).toBe(true);
        expect(await Promise.race([firstExit, new Promise<string>((resolve) => setTimeout(() => resolve("timeout"), 30_000))])).toBe(0);
      } finally {
        if (first.exitCode === null && first.pid !== undefined) {
          spawnSync("taskkill", ["/PID", String(first.pid), "/T", "/F"], { stdio: "ignore" });
        }
      }
      expect(readLog(dataFolder).map((line) => line.event)).toContain("instanceActivated");
    });
  });
});
