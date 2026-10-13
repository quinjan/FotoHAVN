import { spawn, spawnSync, type ChildProcess } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { createInterface } from "node:readline";
import { fileURLToPath } from "node:url";

const uiDir = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const appDir = resolve(uiDir, "..");

/** The E2E UI the integration lane serves (built by the Playwright web server: `vite build --mode e2e`). */
export const e2eUiFolder = resolve(uiDir, "dist-e2e");

/** Each run's Data folders (git-ignored). */
export const headlessOutputDir = resolve(appDir, "output/ui-headless");

/** The headless host's machine-readable startup line (the last line it prints before serving). */
export interface HeadlessListening {
  readonly event: "listening";
  readonly port: number;
  readonly url: string;
  readonly bridgeUrl: string;
  readonly operatorUrl: string;
  readonly guestUrl: string;
  readonly operatorToken: string;
  readonly guestToken: string;
  readonly mediaUrl: string;
  readonly previewUrl: string;
  readonly dataFolder: string;
  readonly uiFolder: string | null;
}

export interface HeadlessHost {
  readonly listening: HeadlessListening;
  /** Everything the host printed so far (stdout and stderr). */
  output(): string;
  stop(): Promise<void>;
}

function isListening(value: unknown): value is HeadlessListening {
  return typeof value === "object" && value !== null && (value as { event?: unknown }).event === "listening";
}

/**
 * Starts the headless host (`dotnet run --no-build`, so .NET must be built first, as CI does) with the given UI and
 * Data folders, and resolves with its parsed `{"event":"listening",…}` line.
 */
export async function startHeadlessHost(options: {
  readonly uiFolder: string;
  readonly dataFolder: string;
  readonly timeoutMs?: number;
}): Promise<HeadlessHost> {
  if (!existsSync(resolve(options.uiFolder, "index.html"))) {
    throw new Error(`${options.uiFolder}/index.html is missing; run "npx vite build --mode e2e" in ui/ first.`);
  }
  const child = spawn(
    "dotnet",
    ["run", "--project", "src/FotoHavn.Host.Headless", "--no-build", "--", "--ui", options.uiFolder, "--data", options.dataFolder],
    { cwd: appDir, stdio: ["ignore", "pipe", "pipe"] },
  );
  let output = "";
  child.stderr.on("data", (chunk: Buffer) => {
    output += chunk.toString("utf8");
  });

  try {
    const listening = await new Promise<HeadlessListening>((resolvePromise, reject) => {
      const timer = setTimeout(() => {
        reject(new Error(`The headless host didn't print its listening line in time.\n${output}`));
      }, options.timeoutMs ?? 60_000);
      createInterface({ input: child.stdout }).on("line", (line) => {
        output += `${line}\n`;
        if (line.startsWith('{"event":"listening"')) {
          const parsed: unknown = JSON.parse(line);
          if (isListening(parsed)) {
            clearTimeout(timer);
            resolvePromise(parsed);
          }
        }
      });
      child.once("error", (error) => {
        clearTimeout(timer);
        reject(error);
      });
      child.once("exit", (code) => {
        clearTimeout(timer);
        reject(new Error(`The headless host exited with code ${String(code)} before listening.\n${output}`));
      });
    });
    return { listening, output: () => output, stop: () => stop(child) };
  } catch (error) {
    await stop(child);
    throw error;
  }
}

/** Ends `dotnet run` and the host it started: by PID, with its process tree. */
async function stop(child: ChildProcess): Promise<void> {
  if (child.pid === undefined || child.exitCode !== null || child.signalCode !== null) {
    return;
  }
  const exited = new Promise<void>((resolvePromise) => {
    child.once("exit", () => {
      resolvePromise();
    });
  });
  if (process.platform === "win32") {
    spawnSync("taskkill", ["/PID", String(child.pid), "/T", "/F"], { stdio: "ignore" });
  } else {
    child.kill("SIGTERM");
  }
  await Promise.race([exited, new Promise((resolvePromise) => setTimeout(resolvePromise, 10_000))]);
}
