import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Win32 window and input helpers for the hardening specs: windows by process, foreground, a real Alt+F4 through
 * `SendInput`, `WM_CLOSE`, and minimize. Each call runs `win32-helper.ps1` once (Windows PowerShell compiles a tiny
 * C# class, about a second). The helper targets windows by handle and never moves the mouse.
 */
const helperScript = path.join(path.dirname(fileURLToPath(import.meta.url)), "win32-helper.ps1");

export interface WindowInfo {
  readonly handle: number;
  readonly title: string;
  readonly visible: boolean;
  readonly minimized: boolean;
  readonly foreground: boolean;
}

function run<T>(operation: string, options: { processId?: number; handle?: number } = {}): T {
  const args = ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", helperScript, "-Operation", operation];
  if (options.processId !== undefined) {
    args.push("-ProcessId", String(options.processId));
  }
  if (options.handle !== undefined) {
    args.push("-Handle", String(options.handle));
  }
  const result = spawnSync("powershell.exe", args, { encoding: "utf8", timeout: 60_000 });
  if (result.status !== 0) {
    throw new Error(`The Win32 helper (${operation}) failed with exit ${String(result.status)}: ${result.stderr.trim() || result.stdout.trim()}`);
  }
  return JSON.parse(result.stdout) as T;
}

/** The visible, titled top-level windows of a process. */
export function listWindows(processId: number): WindowInfo[] {
  return run<WindowInfo[]>("windows", { processId });
}

/** The window of the process with this exact title, if it is open. */
export function findWindow(processId: number, title: string): WindowInfo | undefined {
  return listWindows(processId).find((window) => window.title === title);
}

/** The handle of the foreground window, 0 when there is none. */
export function foregroundHandle(): number {
  return run<{ handle: number }>("foregroundWindow").handle;
}

/** Asks Windows to put the window in the foreground (restoring it if minimized); resolves whether it got there. */
export function bringToForeground(handle: number): boolean {
  return run<{ foreground: boolean }>("foreground", { handle }).foreground;
}

/**
 * Brings the window to the foreground and sends Alt+F4 with `SendInput`, as a person at the keyboard would. `sent` is false when
 * the window could not take the foreground (nothing was sent then: the key would go to another application).
 */
export function sendAltF4(handle: number): { foreground: boolean; sent: boolean } {
  let result = { foreground: false, sent: false };
  // Another application opening a window on this machine can take the foreground between the two steps; the helper then sends nothing.
  for (let attempt = 0; attempt < 3 && !result.sent; attempt += 1) {
    result = run<{ foreground: boolean; sent: boolean }>("sendAltF4", { handle });
  }
  return result;
}

/** Posts `WM_CLOSE` to the window, as its close button or a taskbar "Close window" does. */
export function postClose(handle: number): boolean {
  return run<{ posted: boolean }>("postClose", { handle }).posted;
}

export function minimizeWindow(handle: number): boolean {
  return run<{ minimized: boolean }>("minimize", { handle }).minimized;
}
