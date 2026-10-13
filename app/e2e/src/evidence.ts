import { cpSync, existsSync, mkdirSync, readdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import type { Page, TestInfo } from "@playwright/test";
import { listDataFolder } from "./data";

/** What one app launch leaves behind for evidence. */
export interface RunRecord {
  name: string;
  /** output/e2e/runs/<run>: holds Data/, stdout.log, stderr.log, and evidence/. */
  runDir: string;
  dataDir: string;
  exe: string;
  args: string[];
  pid: number | undefined;
  port: number;
  camera: { id: string; name: string } | undefined;
  startedAt: string;
  exit?: { code: number | null; signal: string | null; killed: boolean; at: string };
  errors: string[];
}

export function evidenceDir(run: RunRecord): string {
  return path.join(run.runDir, "evidence");
}

export function stdoutFile(run: RunRecord): string {
  return path.join(run.runDir, "stdout.log");
}

export function stderrFile(run: RunRecord): string {
  return path.join(run.runDir, "stderr.log");
}

/** Takes a screenshot without failing the caller; returns the file or undefined. */
export async function saveScreenshot(page: Page, file: string): Promise<string | undefined> {
  try {
    mkdirSync(path.dirname(file), { recursive: true });
    await page.screenshot({ path: file, fullPage: true, timeout: 5_000 });
    return file;
  } catch {
    return undefined;
  }
}

/** Takes a screenshot and attaches it to the test report. */
export async function captureTestScreenshot(page: Page, testInfo: TestInfo, name: string): Promise<void> {
  const file = await saveScreenshot(page, testInfo.outputPath(`${name}.png`));
  if (file !== undefined) {
    await testInfo.attach(name, { path: file, contentType: "image/png" });
  }
}

/**
 * Writes the evidence for a run: the app logs copied from `<data>/logs`, a Data folder listing,
 * and a summary. Safe to call more than once; never throws.
 */
export function collectRunEvidence(run: RunRecord): void {
  const dir = evidenceDir(run);
  try {
    mkdirSync(dir, { recursive: true });
    const logsDir = path.join(run.dataDir, "logs");
    if (existsSync(logsDir)) {
      cpSync(logsDir, path.join(dir, "logs"), { recursive: true, force: true });
    }
    const listing = listDataFolder(run.dataDir)
      .map((entry) => (entry.path.endsWith("/") ? entry.path : `${entry.path}  (${entry.bytes} bytes)`))
      .join("\n");
    writeFileSync(
      path.join(dir, "data-listing.txt"),
      existsSync(run.dataDir) ? `${listing}\n` : `Data folder ${run.dataDir} does not exist.\n`,
    );
    writeFileSync(path.join(dir, "summary.json"), `${JSON.stringify(run, null, 2)}\n`);
  } catch (error) {
    run.errors.push(`Evidence collection failed: ${String(error)}`);
  }
}

/** Attaches a run's evidence files to the test report. */
export async function attachRunEvidence(testInfo: TestInfo, run: RunRecord): Promise<void> {
  const dir = evidenceDir(run);
  const files: Array<[string, string, string]> = [
    ["app-summary", path.join(dir, "summary.json"), "application/json"],
    ["data-listing", path.join(dir, "data-listing.txt"), "text/plain"],
    ["app-stdout", stdoutFile(run), "text/plain"],
    ["app-stderr", stderrFile(run), "text/plain"],
    ["final-screenshot", path.join(dir, "final.png"), "image/png"],
  ];
  const logsDir = path.join(dir, "logs");
  if (existsSync(logsDir)) {
    for (const entry of readdirSync(logsDir, { withFileTypes: true })) {
      if (entry.isFile()) {
        files.push([`app-log-${entry.name}`, path.join(logsDir, entry.name), "text/plain"]);
      }
    }
  }
  for (const [name, file, contentType] of files) {
    if (existsSync(file)) {
      await testInfo.attach(name, { path: file, contentType });
    }
  }
}
