import { spawn, spawnSync, type ChildProcess } from "node:child_process";
import { randomBytes } from "node:crypto";
import { createWriteStream, existsSync, mkdirSync, readFileSync } from "node:fs";
import { createServer } from "node:net";
import path from "node:path";
import { chromium, type Browser, type Page } from "@playwright/test";
import type { Camera } from "./camera";
import { collectRunEvidence, evidenceDir, saveScreenshot, stderrFile, stdoutFile, type RunRecord } from "./evidence";
import { HookClient, type Role } from "./hooks";
import { fromApp, paths } from "./paths";

/** The UI is served from this virtual host inside WebView2. */
export const appOrigin = "https://app.fotohavn/";

export const e2eBuildCommand = `dotnet build ${paths.hostProject} -c Debug -p:FotoHavnE2E=true -o output/e2e-build`;

export interface LaunchOptions {
  /** A short name for the run folder, such as the test file name. */
  name: string;
  /** Passed as `--e2e-camera <id>` when set. */
  camera?: Camera | undefined;
  /** Defaults to a fresh `Data` folder (not yet created) in the run folder. */
  dataDir?: string;
  startupTimeoutMs?: number;
}

export interface ExitResult {
  code: number | null;
  signal: string | null;
}

export interface AppSession {
  readonly run: RunRecord;
  readonly dataDir: string;
  readonly port: number;
  readonly browser: Browser;
  /**
   * The Operator page as last found. After `test.crashUi("operator")` the host recreates its WebView2, so a new page
   * replaces it; use {@link AppSession.pageFor} to find the current one.
   */
  readonly operatorPage: Page;
  /** The same as {@link AppSession.operatorPage}. */
  readonly page: Page;
  /**
   * The live page whose handshake gave it `role` (root `data-fotohavn-role`, status `connected`). Waits for it, for
   * example for the Guest page after `test.openGuestScreen` or a page after `test.crashUi`.
   */
  pageFor(role: Role, options?: { timeoutMs?: number }): Promise<Page>;
  /** Hooks through the Operator page's own brain connection. */
  readonly hooks: HookClient;
  readonly isClosed: boolean;
  /**
   * Shuts the app down through `test.exit` and waits for it to exit.
   * Kills it by PID and throws if it doesn't exit in time. Collects evidence either way.
   */
  close(options?: { timeoutMs?: number }): Promise<ExitResult>;
}

const defaultStartupTimeoutMs = 60_000;
const defaultExitTimeoutMs = 20_000;
const defaultPageTimeoutMs = 10_000;

export function assertE2EBuildExists(): void {
  if (!existsSync(paths.exe)) {
    throw new Error(
      `The E2E build of FOTOHAVN is missing: ${fromApp(paths.exe)} does not exist. ` +
        `Run the tests without FOTOHAVN_E2E_SKIP_BUILD=1 so global setup builds it, ` +
        `or build it from app/ with: ${e2eBuildCommand}`,
    );
  }
}

/** Starts the E2E build with a fresh Data folder, connects over CDP, and waits for the operator handshake. */
export async function launchApp(options: LaunchOptions): Promise<AppSession> {
  assertE2EBuildExists();

  const runDir = path.join(paths.runs, `${timestamp()}-${slug(options.name)}-${randomBytes(2).toString("hex")}`);
  mkdirSync(runDir, { recursive: true });
  const dataDir = options.dataDir ?? path.join(runDir, "Data");
  const port = await freePort();
  const args = ["--data", dataDir, "--remote-debugging-port", String(port)];
  if (options.camera !== undefined) {
    args.push("--e2e-camera", options.camera.id);
  }

  const run: RunRecord = {
    name: options.name,
    runDir,
    dataDir,
    exe: paths.exe,
    args,
    pid: undefined,
    port,
    camera: options.camera,
    startedAt: new Date().toISOString(),
    errors: [],
  };

  const child = spawn(paths.exe, args, { cwd: paths.e2eBuildDir, stdio: ["ignore", "pipe", "pipe"] });
  run.pid = child.pid;
  child.stdout?.pipe(createWriteStream(stdoutFile(run)));
  child.stderr?.pipe(createWriteStream(stderrFile(run)));

  let killed = false;
  const exited = new Promise<ExitResult>((resolve) => {
    child.once("error", (error) => {
      run.errors.push(`Spawn failed: ${error.message}`);
      resolve({ code: null, signal: null });
    });
    child.once("exit", (code, signal) => {
      run.exit = { code, signal, killed, at: new Date().toISOString() };
      resolve({ code, signal });
    });
  });

  let browser: Browser | undefined;
  try {
    const startupTimeoutMs = options.startupTimeoutMs ?? defaultStartupTimeoutMs;
    const deadline = Date.now() + startupTimeoutMs;
    await waitForDebugger(port, exited, deadline, run);
    browser = await chromium.connectOverCDP(`http://127.0.0.1:${port}`, { timeout: remaining(deadline) });
    // Readiness: the Operator page completed its handshake (the `welcome` set the root's role and status).
    const page = await findRolePage(browser, "operator", deadline, run);
    return createSession(run, child, exited, browser, page, () => {
      killed = true;
      if (run.exit !== undefined) {
        run.exit.killed = true;
      }
    });
  } catch (error) {
    killed = true;
    killTree(child);
    await Promise.race([exited, delay(5_000)]);
    await browser?.close().catch(() => undefined);
    run.errors.push(`Launch failed: ${error instanceof Error ? error.message : String(error)}`);
    collectRunEvidence(run);
    throw new Error(
      `FOTOHAVN failed to start (${error instanceof Error ? error.message : String(error)}). ` +
        `Evidence: ${fromApp(runDir)}${stderrTail(run)}`,
      { cause: error },
    );
  }
}

function createSession(
  run: RunRecord,
  child: ChildProcess,
  exited: Promise<ExitResult>,
  browser: Browser,
  page: Page,
  markKilled: () => void,
): AppSession {
  let operatorPage = page;
  let closing: Promise<ExitResult> | undefined;

  const pageFor = async (role: Role, options: { timeoutMs?: number } = {}): Promise<Page> => {
    if (role === "operator" && (await hasRole(operatorPage, role))) {
      return operatorPage;
    }
    const found = await findRolePage(browser, role, Date.now() + (options.timeoutMs ?? defaultPageTimeoutMs), run);
    if (role === "operator") {
      operatorPage = found;
    }
    return found;
  };
  const hooks = new HookClient(() => pageFor("operator"));

  const close = async (timeoutMs: number): Promise<ExitResult> => {
    for (const role of ["operator", "guest"] as const) {
      const rolePage = await pageFor(role, { timeoutMs: 1_000 }).catch(() => undefined);
      if (rolePage !== undefined) {
        await saveScreenshot(rolePage, path.join(evidenceDir(run), role === "operator" ? "final.png" : "final-guest.png"));
      }
    }
    try {
      await hooks.exit();
    } catch (error) {
      run.errors.push(`test.exit failed: ${error instanceof Error ? error.message : String(error)}`);
    }
    const result = await Promise.race([exited, delay(timeoutMs).then(() => undefined)]);
    await browser.close().catch(() => undefined);
    if (result === undefined) {
      markKilled();
      killTree(child);
      await Promise.race([exited, delay(5_000)]);
      run.errors.push(`Did not exit within ${timeoutMs} ms after test.exit; killed PID ${child.pid}.`);
      collectRunEvidence(run);
      throw new Error(
        `FOTOHAVN did not exit within ${timeoutMs} ms after test.exit, so PID ${child.pid} was killed. ` +
          `Evidence: ${fromApp(run.runDir)}`,
      );
    }
    collectRunEvidence(run);
    return result;
  };

  return {
    run,
    dataDir: run.dataDir,
    port: run.port,
    browser,
    get operatorPage() {
      return operatorPage;
    },
    get page() {
      return operatorPage;
    },
    pageFor,
    hooks,
    get isClosed() {
      return closing !== undefined;
    },
    close(options = {}) {
      closing ??= close(options.timeoutMs ?? defaultExitTimeoutMs);
      return closing;
    },
  };
}

async function waitForDebugger(
  port: number,
  exited: Promise<ExitResult>,
  deadline: number,
  run: RunRecord,
): Promise<void> {
  const url = `http://127.0.0.1:${port}/json/version`;
  let lastError = "no response";
  while (Date.now() < deadline) {
    if (run.exit !== undefined || run.errors.length > 0) {
      throw new Error(`the app exited before its debugging port opened (exit code ${run.exit?.code ?? "none"})`);
    }
    try {
      const response = await fetch(url, { signal: AbortSignal.timeout(2_000) });
      if (response.ok) {
        return;
      }
      lastError = `HTTP ${response.status}`;
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error);
    }
    await Promise.race([exited, delay(250)]);
  }
  throw new Error(`the debugging port ${port} did not answer at ${url} in time (${lastError})`);
}

/** The app's live pages: every page at the app origin that isn't closed. */
export function appPages(browser: Browser): Page[] {
  return browser
    .contexts()
    .flatMap((context) => context.pages())
    .filter((candidate) => !candidate.isClosed() && candidate.url().startsWith(appOrigin));
}

/**
 * Whether the page completed its handshake as `role`: the UI sets the root's `data-fotohavn-role` from the brain's
 * `welcome` and `data-fotohavn-status="connected"`. A crashed or navigating page answers false.
 */
export async function hasRole(page: Page, role: Role): Promise<boolean> {
  if (page.isClosed()) {
    return false;
  }
  const probe = page
    .evaluate(() => {
      const root = document.documentElement;
      return root.dataset.fotohavnStatus === "connected" ? (root.dataset.fotohavnRole ?? null) : null;
    })
    .catch(() => null);
  return (await Promise.race([probe, delay(2_000).then(() => null)])) === role;
}

/** Waits for the live page whose handshake gave it `role`. */
async function findRolePage(browser: Browser, role: Role, deadline: number, run: RunRecord): Promise<Page> {
  while (Date.now() < deadline) {
    if (run.exit !== undefined) {
      throw new Error(`the app exited before its ${role} page completed its handshake (exit code ${run.exit.code ?? "none"})`);
    }
    for (const page of appPages(browser)) {
      if (await hasRole(page, role)) {
        return page;
      }
    }
    await delay(200);
  }
  const urls = browser
    .contexts()
    .flatMap((context) => context.pages())
    .map((page) => page.url());
  throw new Error(`no ${role} page at ${appOrigin} completed its handshake in time (pages: ${urls.join(", ") || "none"})`);
}

/** Ends the app and its WebView2 processes. */
function killTree(child: ChildProcess): void {
  if (child.pid === undefined || child.exitCode !== null) {
    return;
  }
  if (process.platform === "win32") {
    spawnSync("taskkill", ["/PID", String(child.pid), "/T", "/F"], { stdio: "ignore" });
  } else {
    child.kill("SIGKILL");
  }
}

export function freePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = createServer();
    server.unref();
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      if (address === null || typeof address === "string") {
        server.close();
        reject(new Error("Could not allocate a free TCP port."));
        return;
      }
      server.close(() => resolve(address.port));
    });
  });
}

function stderrTail(run: RunRecord): string {
  try {
    const text = readFileSync(stderrFile(run), "utf8").trim();
    return text === "" ? "" : `\nApp stderr (tail):\n${text.split(/\r?\n/).slice(-20).join("\n")}`;
  } catch {
    return "";
  }
}

function remaining(deadline: number): number {
  return Math.max(1_000, deadline - Date.now());
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function timestamp(): string {
  return new Date().toISOString().replace(/[-:]/g, "").replace(/\..+$/, "").replace("T", "-");
}

function slug(name: string): string {
  return (
    name
      .toLowerCase()
      .replace(/\.spec\.ts$/, "")
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "")
      .slice(0, 60) || "app"
  );
}
