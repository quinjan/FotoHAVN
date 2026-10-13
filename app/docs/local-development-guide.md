# Local development guide

How to build, test, run, and publish the FOTOHAVN desktop booth on a developer machine or the booth laptop. Run every command from `app/` unless a section says otherwise. [`../AGENTS.md`](../AGENTS.md) holds the short form of the verification commands; this guide explains them. [`implementation-plan.md`](implementation-plan.md) holds milestone scope.

## Prerequisites

| Need | Version or note |
| --- | --- |
| Windows | Windows 11, x64 |
| .NET SDK | The version in [`../global.json`](../global.json) (10.0.401, rolling forward to a later feature band). `global.json` also selects the Microsoft.Testing.Platform runner that `dotnet test --solution` needs. |
| Node.js | 24.19.0 or later (CI pins 24.19.0) |
| WebView2 Runtime | The Evergreen runtime. Windows 11 includes it; install it from Microsoft if `FOTOHAVN.exe` reports that WebView2 is missing. |
| Camera | At least one connected camera for end-to-end runs (see [Camera requirement](#camera-requirement)) |
| Chromium for Playwright | Once per machine, in `ui/`: `npx playwright install chromium`. The fixture lane needs it; the end-to-end suite drives WebView2, not this Chromium. |

Packages restore from the locked files: `packages.lock.json` in every .NET project, and `package-lock.json` in `ui/` and `e2e/`. Both `ui/` and `e2e/` pin the same exact `@playwright/test` and `typescript` versions.

## Layout

| Path | Contents |
| --- | --- |
| `FotoHAVN.slnx` | The solution |
| `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` | Shared build settings (nullable, warnings as errors, analyzers, the `FotoHavnE2E` switch) and pinned package versions |
| `.config/dotnet-tools.json` | Local .NET tools: ReportGenerator |
| `src/FotoHavn.Core` | The brain: every rule and timer, no Windows APIs |
| `src/FotoHavn.Protocol`, `src/FotoHavn.Catalog`, `src/FotoHavn.Storage` | UI protocol, Template and Look catalog with the compositor, Event storage |
| `src/FotoHavn.Camera.Windows` | The webcam adapter (a thin device list in M1) |
| `src/FotoHavn.Host.Windows` | The WPF host; builds `FOTOHAVN.exe` |
| `src/FotoHavn.Host.Headless` | The headless dev host (see [Headless dev loop](#headless-dev-loop)) |
| `tests/` | xUnit v3 test projects, one per project above except the headless host |
| `ui/` | The React UI; see [`../ui/README.md`](../ui/README.md) |
| `e2e/` | The Playwright end-to-end framework that drives the real app |
| `scripts/` | `publish-field-test.ps1` |
| `output/` | Every build, test, and run output (git-ignored); see [Outputs](#outputs) |

## Verification commands

Run these in this order before completing a milestone, and fix every failure before going on. CI runs steps 1–4 and 8 with the same commands in [`../../.github/workflows/desktop.yml`](../../.github/workflows/desktop.yml). Steps 6, 7, and 9 of the [verification procedure](implementation-plan.md#verification-procedure-for-the-implementing-agent) are reviews and records, not commands.

### 1. Build .NET

```powershell
dotnet restore FotoHAVN.slnx --locked-mode
dotnet build FotoHAVN.slnx --no-restore
```

Warnings are errors. `--locked-mode` fails if a `packages.lock.json` is out of date; after changing a package version in `Directory.Packages.props`, run `dotnet restore FotoHAVN.slnx --force-evaluate` and commit the updated lock files. The projects inside `FOTOHAVN.exe` also restore for `win-x64`, so the self-contained field publish uses the same lock files and leaves them unchanged.

### 2. .NET tests and coverage

```powershell
Remove-Item output/coverage -Recurse -Force -ErrorAction Ignore
dotnet test --solution FotoHAVN.slnx --no-build --results-directory output/coverage --coverage --coverage-output-format cobertura
dotnet tool restore
dotnet tool run reportgenerator "-reports:output/coverage/*.cobertura.xml" "-targetdir:output/coverage/report" "-reporttypes:TextSummary;Cobertura" "-assemblyfilters:+FotoHavn.Core;+FotoHavn.Protocol;+FotoHavn.Storage;+FotoHavn.Catalog"
```

From M2, gate each assembly at 90% line and branch coverage:

```powershell
foreach ($assembly in 'FotoHavn.Core','FotoHavn.Protocol','FotoHavn.Storage','FotoHavn.Catalog') {
  dotnet tool run reportgenerator "-reports:output/coverage/*.cobertura.xml" "-targetdir:output/coverage/report/$assembly" "-reporttypes:TextSummary" "-assemblyfilters:+$assembly" "minimumCoverageThresholds:lineCoverage=90" "minimumCoverageThresholds:branchCoverage=90"
  if ($LASTEXITCODE -ne 0) { throw "$assembly is below 90% line or branch coverage" }
}
```

- **Delete `output/coverage` first.** Each test project writes a Cobertura file with a GUID name, so files from earlier runs accumulate and would be merged into the report.
- **No leading dash on the threshold.** Write `"minimumCoverageThresholds:lineCoverage=90"`. ReportGenerator silently ignores `"-minimumCoverageThresholds:..."` and exits 0 whatever the coverage.
- **One run per assembly.** A merged threshold can let a well-covered assembly hide a weak one, so the gate runs ReportGenerator once per gated assembly. An assembly with no branches passes the branch threshold.
- **Phasing.** M1 collects and reports coverage only: run the summary command and read `output/coverage/report/Summary.txt`. From M2, the gate loop is required, locally and in CI.
- Camera adapter tests in `FotoHavn.Camera.Windows.Tests` open real cameras. Without one they are skipped with "not run: no camera"; report them as not run, not passed.

### 3. UI

In `ui/`:

```powershell
npm ci
npm run lint
npm run typecheck
npm run test:coverage
npm run build
npm run test:fixtures
```

`test:coverage` fails below 80% lines, branches, functions, or statements. `test:fixtures` serves `dist/` and checks it in Chromium at 1280×720, 1366×768, 1920×1080, and 2560×1440, so it needs `npm run build` first and Chromium installed once with `npx playwright install chromium`.

### 4. End-to-end project

In `e2e/`:

```powershell
npm ci
npm run typecheck
```

### 5. End-to-end runs with a real camera

In `e2e/`:

```powershell
$env:FOTOHAVN_E2E_CAMERA = "Integrated Webcam"   # optional; a camera name or device id
npx playwright test --project=regression --grep "@m1"
npx playwright test --project=smoke
```

Replace `@m1` with the milestone being completed. Global setup builds the UI with `npm run build:e2e` (running `npm ci` in `ui/` first if `node_modules` is missing) and the E2E build of the host, launches the app once, applies the camera precondition, and prints the chosen camera, for example `E2E camera: Integrated Webcam (\\?\USB#VID_...\GLOBAL)`. Copy that line into the verification record. Set `FOTOHAVN_E2E_SKIP_BUILD=1` to reuse an existing E2E build in a fast loop.

Tests run one at a time (the camera is opened exclusively). Each test file gets a fresh app process with its own Data folder, WebView2 user-data folder, and debugging port, and the app must exit with code 0 after `test.exit`.

On a failure, open the HTML report with `npx playwright show-report ../output/e2e/html`, read the trace, and check the run folder under `output/e2e/runs/` for the app's stdout, stderr, `fotohavn.jsonl` log, Data folder listing, and final screenshot.

### 8. Agent docs

From the repository root:

```powershell
./scripts/verify-agent-docs.ps1
```

## Camera requirement

End-to-end runs need at least one camera that Windows lists as a video capture device. They never skip or pass without one:

- No camera: setup fails with `End-to-end tests require at least one connected camera.`
- `FOTOHAVN_E2E_CAMERA` matching no camera's name or device id: setup fails with `End-to-end tests require at least one connected camera. FOTOHAVN_E2E_CAMERA="<value>" matched none of: <camera names>.`
- Otherwise setup uses the match, or the first listed camera when the variable is unset.

Laptops often have an infrared camera for Windows Hello beside the color webcam, for example "Integrated IR Webcam" next to "Integrated Webcam". Whether Windows lists the IR camera as a video capture device depends on the driver: on the M1 development laptop it isn't listed, but on another machine it could be, and it could come first. Set `FOTOHAVN_E2E_CAMERA` to the color camera so a run never picks an IR one. The `missing-device` message prints the cameras the app can see.

An agent or machine without a camera reports "end-to-end not run: no camera"; the milestone stays incomplete.

## E2E build, test hooks, and the publish guard

The E2E build compiles the test hooks into the app:

```powershell
cd ui; npm run build:e2e; cd ..
dotnet build src/FotoHavn.Host.Windows/FotoHavn.Host.Windows.csproj -c Debug -p:FotoHavnE2E=true -o output/e2e-build
```

- `-p:FotoHavnE2E=true` defines `E2E_HOOKS` in every project. Only then are the `test.*` hooks compiled; other builds answer every `test.*` message with `hooksUnavailable`.
- The host copies `ui/dist` into `wwwroot/` beside `FOTOHAVN.exe` and removes files a previous copy left there. Without `ui/dist`, the app shows "The FOTOHAVN UI is not built."
- `FOTOHAVN.exe` takes `--data <folder>` (default `Data` beside the exe, created if missing) and, in E2E builds only, `--remote-debugging-port <port>` and `--e2e-camera <name or device id>`. Field builds never open a debugging port.
- M1 hooks: `test.listCameras` and `test.exit`. Later milestones add the rest listed in [the plan](implementation-plan.md#e2e-build-and-test-hooks).
- **Publish guard.** Any `dotnet publish` of the host with `-p:FotoHavnE2E=true` fails with `FOTOHAVN field builds must not include E2E hooks`, and so does `scripts/publish-field-test.ps1` when asked for hooks. A unit test in `FotoHavn.Host.Windows.Tests` checks by reflection that normally built assemblies contain no `.E2E` types, and the `@m1` regression suite checks the publish refusal.

To attach Playwright or DevTools by hand, start `output/e2e-build/FOTOHAVN.exe --data output/scratch/Data --remote-debugging-port 9222` and connect to `http://127.0.0.1:9222`.

## Headless dev loop

For fast UI work without the WPF host:

- **UI only.** In `ui/`, `npm run dev` starts the Vite dev server with hot reload. Outside WebView2 the page's `postMessage` calls are no-ops.
- **Headless host.** `dotnet run --project src/FotoHavn.Host.Headless` starts Kestrel on `127.0.0.1` at a random port, prints the URL, and serves the built `ui/dist` (or `--ui-dist <folder>`).

In M1 the headless host only serves static files. M2 adds the real brain, the fake camera, and the WebSocket bridge, so the headless host and `npm run dev` together run the UI against real presentations; the Playwright protocol tests in Chromium build on it.

## Publishing

```powershell
./scripts/publish-field-test.ps1
```

M1's script is minimal: it refuses E2E hooks and runs a self-contained `win-x64` Release publish of the host to `output/field-test/FOTOHAVN`. M10 completes the field-test build (packaging, the booth checklist, and the hardening checks).

## Outputs

Everything lands under `app/output/` (git-ignored):

| Path | Contents |
| --- | --- |
| `output/coverage/` | .NET Cobertura files, and `report/Summary.txt` with the ReportGenerator summary (per assembly under `report/<assembly>/` when gated) |
| `output/e2e-build/` | The E2E build: `FOTOHAVN.exe` and `wwwroot/` |
| `output/e2e/html/` | The end-to-end HTML report |
| `output/e2e/junit.xml`, `output/e2e/test-results/` | JUnit results; traces and screenshots of failures |
| `output/e2e/runs/` | One folder per app launch: Data folder, stdout and stderr, and `evidence/` (log, Data listing, final screenshot, summary) |
| `output/e2e/camera.json` | The camera global setup chose |
| `output/ui-fixtures/`, `output/fixtures/` | Fixture lane report, JUnit file, traces, and screenshots |
| `output/field-test/` | The field-test publish |

UI unit coverage goes to `ui/coverage/`.

## Hardware checklist

Manual checks automation can't prove. Each milestone's verification block names the ones it needs; M10 runs the whole list on the booth laptop. Record results in the milestone's verification record.

- [ ] A clean Windows 11 booth laptop: install, first launch, and the WebView2 runtime present.
- [ ] Touch: every Operator and Guest control by touch on the booth screen at standing distance.
- [ ] Windows touch keyboard (spike S2) appears for Event setup fields in the full-screen window.
- [ ] Display scaling at 100%, 125%, and 150%, including different scaling on each display.
- [ ] Two displays: Guest Screen on the guest monitor covering it fully, Operator Screen on the laptop.
- [ ] Guest monitor unplugged and replugged during an Event.
- [ ] Camera unplugged mid-cycle and recovered.
- [ ] A 4-hour Active Event with power and sleep settings checked.
- [ ] Touch mapping (risk R8): touches on the guest monitor land on the guest monitor. If they don't, open Control Panel > Tablet PC Settings > Setup and follow the prompts so Windows maps the touch screen to the correct display, then check again.
- [ ] Recovery from a lost PIN by closing the app.
