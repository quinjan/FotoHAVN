import { spawnSync } from "node:child_process";

/** What the soak records about the app: the host process (the brain and the WPF shell) and its whole process tree (the WebView2 processes too). */
export interface ProcessSample {
  readonly host: { readonly workingSetBytes: number; readonly privateBytes: number; readonly handles: number; readonly threads: number };
  readonly tree: { readonly processes: number; readonly workingSetBytes: number; readonly handles: number };
}

const script = `
$ErrorActionPreference = 'Stop'
$rootId = [int]$env:FH_SAMPLE_PID
$all = @(Get-CimInstance Win32_Process | Select-Object ProcessId, ParentProcessId)
$ids = [System.Collections.Generic.List[int]]::new()
$ids.Add($rootId)
for ($i = 0; $i -lt $ids.Count; $i++) {
  foreach ($child in $all) { if ($child.ParentProcessId -eq $ids[$i] -and -not $ids.Contains([int]$child.ProcessId)) { $ids.Add([int]$child.ProcessId) } }
}
$procs = @(Get-Process -Id $ids -ErrorAction SilentlyContinue)
$host_ = $procs | Where-Object { $_.Id -eq $rootId }
[pscustomobject]@{
  host = [pscustomobject]@{ workingSetBytes = $host_.WorkingSet64; privateBytes = $host_.PrivateMemorySize64; handles = $host_.HandleCount; threads = $host_.Threads.Count }
  tree = [pscustomobject]@{ processes = $procs.Count; workingSetBytes = ($procs | Measure-Object WorkingSet64 -Sum).Sum; handles = ($procs | Measure-Object HandleCount -Sum).Sum }
} | ConvertTo-Json -Compress -Depth 4
`;

/** Samples the process with this PID and its descendants. Read-only; it ends nothing. */
export function sampleProcess(pid: number): ProcessSample {
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", script], {
    encoding: "utf8",
    timeout: 60_000,
    env: { ...process.env, FH_SAMPLE_PID: String(pid) },
  });
  if (result.status !== 0) {
    throw new Error(`Sampling PID ${String(pid)} failed (exit ${String(result.status)}): ${result.stderr.trim() || result.stdout.trim()}`);
  }
  return JSON.parse(result.stdout) as ProcessSample;
}
