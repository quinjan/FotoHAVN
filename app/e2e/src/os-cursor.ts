import { spawnSync } from "node:child_process";

/**
 * Moves the real Windows cursor to a screen position (physical pixels). The visual comparison needs it: the real cursor resting
 * over a fixture's control makes WebView2 keep that control in its `:hover` state, and it re-asserts that state after the page's
 * synthetic pointer is parked, so the page's own pointer can't be relied on alone.
 */
export function moveOsCursor(x: number, y: number): void {
  const script = [
    "Add-Type -Namespace FotoHavn -Name NativeCursor -MemberDefinition '[DllImport(\"user32.dll\")] public static extern bool SetProcessDPIAware(); [DllImport(\"user32.dll\")] public static extern bool SetCursorPos(int x, int y);'",
    "[void][FotoHavn.NativeCursor]::SetProcessDPIAware()",
    `if (-not [FotoHavn.NativeCursor]::SetCursorPos(${String(Math.round(x))}, ${String(Math.round(y))})) { exit 1 }`,
  ].join("; ");
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", script], { stdio: "ignore", timeout: 30_000 });
  if (result.status !== 0) {
    throw new Error(`Moving the OS cursor to ${String(x)},${String(y)} failed (exit ${String(result.status)}).`);
  }
}
