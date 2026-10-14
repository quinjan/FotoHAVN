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
| `src/FotoHavn.Host.Headless` | The headless dev host: the real brain over a WebSocket bridge (see [Headless host](#headless-host)) |
| `tests/FotoHavn.Storage.CrashHarness` | Test infrastructure: a console program the crash tests run as a child process (see [Event storage](#event-storage-the-data-folder-faults-and-crash-tests)) |
| `tests/` | xUnit v3 test projects, one per project above (`FotoHavn.Host.Headless.Tests` runs the headless host on a random loopback port) |
| `ui/` | The React UI; see [`../ui/README.md`](../ui/README.md) |
| `e2e/` | The Playwright end-to-end framework that drives the real app |
| `scripts/` | `publish-field-test.ps1`, `generate-protocol.ps1`, and `generate-tokens.mjs` (brand tokens to `ui/src/styles/brand-tokens.css`) |
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

From M2, gate each assembly at 90% line and branch coverage, then run the Core tests again in the E2E build:

```powershell
foreach ($assembly in 'FotoHavn.Core','FotoHavn.Protocol','FotoHavn.Storage','FotoHavn.Catalog') {
  dotnet tool run reportgenerator "-reports:output/coverage/*.cobertura.xml" "-targetdir:output/coverage/report/$assembly" "-reporttypes:TextSummary" "-assemblyfilters:+$assembly" "minimumCoverageThresholds:lineCoverage=90" "minimumCoverageThresholds:branchCoverage=90"
  if ($LASTEXITCODE -ne 0) { throw "$assembly is below 90% line or branch coverage" }
}
dotnet test --project tests/FotoHavn.Core.Tests/FotoHavn.Core.Tests.csproj -p:FotoHavnE2E=true
```

- **Delete `output/coverage` first.** Each test project writes a Cobertura file with a GUID name, so files from earlier runs accumulate and would be merged into the report.
- **No leading dash on the threshold.** Write `"minimumCoverageThresholds:lineCoverage=90"`. ReportGenerator silently ignores `"-minimumCoverageThresholds:..."` and exits 0 whatever the coverage.
- **One run per assembly.** A merged threshold can let a well-covered assembly hide a weak one, so the gate runs ReportGenerator once per gated assembly. An assembly with no branches passes the branch threshold.
- **Phasing.** M1 collected and reported coverage only. From M2, the gate loop is required, locally and in CI.
- **The E2E-build Core run.** The brain's `#if E2E_HOOKS` glue (`tests/FotoHavn.Core.Tests/Brains/E2EHookGlueTests.cs`) compiles only into the E2E build, so it runs in this separate `dotnet test`. It builds into `obj/e2e/` and `bin/e2e/` (see [E2E build](#e2e-build-test-hooks-and-the-publish-guard)), collects no coverage, and so never mixes with the gated field-build numbers; the field build's `hooksUnavailable` cases are compiled out of it.
- **The headless tests.** `FotoHavn.Host.Headless.Tests` starts the real headless host on a random loopback port and talks to it with `ClientWebSocket` and raw TCP (handshake, role isolation, tokens, oversize frames, media and preview routes).
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

`test:coverage` fails below 80% lines, branches, functions, or statements. `lint` runs ESLint and then stylelint, which rejects hex colors, raw `cubic-bezier()` and durations, and raw `z-index` outside `src/styles/brand-tokens.css` and `src/styles/app-tokens.css` (see [`../DESIGN.md`](../DESIGN.md#tokens-and-implementation-files)). After a change to [`../../brand/tokens/tokens.json`](../../brand/tokens/tokens.json), run `npm run generate:tokens` and commit `src/styles/brand-tokens.css`; `scripts/brand-tokens.test.ts` fails while it is stale. `test:fixtures` builds the E2E bundle (`vite build --mode e2e` to `dist-e2e/`, never `dist/`), serves it with `vite preview`, and runs two Chromium projects:

- `fixtures`: the component fixture lane renders every fixture the gallery discovers (`ui/src/components/**/*.fixtures.tsx`, at `?gallery=<id>`) at 1280×720, 1366×768, 1920×1080, and 2560×1440, plus reduced motion and forced colors at 1280×720, with screenshots in `output/fixtures/components/<id>/`, axe, overflow, target-size, and one-primary-action checks; and the fixture lane (`?fixture=<name>`) renders every brain-generated presentation fixture, plus the hand-written Event list with rows, at the same four resolutions, with no scrolling and no axe violations.
- `headless`: the integration lane starts the real [headless host](#headless-host) with `dotnet run --project src/FotoHavn.Host.Headless --no-build -- --ui ui/dist-e2e --data output/ui-headless/<run>/Data`, parses its listening line, and drives one operator and one guest page over the WebSocket bridge with the UI's own client. It needs the .NET build from step 1.

Install Chromium once with `npx playwright install chromium`.

The preview server listens on port 4173 with `--strictPort`. When another checkout or worktree already uses that port, set `FOTOHAVN_FIXTURE_PORT` for the run (`$env:FOTOHAVN_FIXTURE_PORT = "4174"; npm run test:fixtures`); `playwright.config.ts` and `vite.config.ts` read it, and an invalid value fails the run. The headless lane starts its own host on a random port and is unaffected.

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
npx playwright test --project=regression --grep "@m2"
npx playwright test --project=smoke
```

Replace `@m2` with the milestone being completed; the earlier suites keep passing too. The `@m5` camera specs check the real webcam on whatever frames it delivers (see [the diagnostics screen](#the-camera-diagnostics-screen-the-privacy-shutter-and-checking-the-camera)); `jpeg-js` decodes the saved Captures. Global setup builds the UI with `npm run build:e2e` (running `npm ci` in `ui/` first if `node_modules` is missing) and the E2E build of the host, launches the app once, applies the camera precondition, and prints the chosen camera, for example `E2E camera: Integrated Webcam (\\?\USB#VID_...\GLOBAL)`. Copy that line into the verification record. Set `FOTOHAVN_E2E_SKIP_BUILD=1` to reuse an existing E2E build in a fast loop. Set `FOTOHAVN_E2E_OUTPUT` to write a run's reports, `runs/`, and `camera.json` somewhere other than `output/e2e/`; the `@m1` camera-precondition regression uses it for the nested `smoke` runs it starts to check global setup itself.

Tests run one at a time (the camera is opened exclusively). Each test file gets a fresh app process with its own Data folder, WebView2 user-data folder, and debugging port, and the app must exit with code 0 after `test.exit`. The launch fixture is ready when the Operator page completes its handshake (the root's `data-fotohavn-role="operator"` and `data-fotohavn-status="connected"`, set from the brain's `welcome`). Tests find pages by role: `operatorPage`, and `app.pageFor("guest")` after `test.openGuestScreen`. Most scenarios use `test.useVirtualDisplays` (two halves of the real primary display). `@physical-displays` scenarios need two real displays; with one they are reported skipped with "not run: one display", never passed.

On a failure, open the HTML report with `npx playwright show-report ../output/e2e/html`, read the trace, and check the run folder under `output/e2e/runs/` for the app's stdout, stderr, `fotohavn.jsonl` log, Data folder listing, and final screenshot.

The `@m3` visual regression (`regression/visual/fixture-gallery-webview2`) compares the real WebView2's fixture screenshots with the Chromium fixture lane's baselines in `output/fixtures/components/`, so run `npm run test:fixtures` in `ui/` first (step 3); a missing baseline fails with that instruction. It allows at most 1% of pixels to differ per screenshot (pixelmatch threshold 0.1) and writes the maximum, mean, and worst ratios to `output/e2e/visual-summary.json`. A run takes about 7 minutes for the four resolutions.

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

### The camera diagnostics screen, the privacy shutter, and checking the camera

Until Event setup exists (M6), the E2E build has a camera diagnostics screen on the Operator Screen: the camera list, a live preview, the camera's state, the format, the page's measured frame rate, the booth's own report (delivered fps, dropped frames, frame age), a `Mirror` switch, and `CAPTURE`. Drive it with the hooks (`window.fotohavnE2E.sendTest("test.openCameraDiagnostics", null)`, then `test.selectDiagnosticsCamera` with `{ deviceId }`), or run `smoke/camera-live`. It exists only in E2E builds; a field build has none of its controls. `CAPTURE` files land in `<data>/diagnostics/` as `capture-yyyyMMdd-HHmmss-fff.jpg`.

- **Privacy shutter.** Many laptops have a physical shutter or a "camera off" key. While it is closed, the camera still streams, but Windows delivers a placeholder (black with a crossed-out camera icon, about 20 fps at 1920×1080) instead of the scene. The automated `@m5` checks assert only properties that hold for any frames (fresh, at or above the floor, decodable, not blank), so they pass on the placeholder. They can't judge a real scene: open the shutter and look at the preview as a manual check.
- **Check the camera outside FOTOHAVN.** Open the Windows Camera app first. It should show your picture (or the placeholder when the shutter is closed). Close it before an end-to-end run: the booth opens the camera exclusively, and while another app holds it the screen reads "In use by another app". If the Camera app shows nothing at all, check Settings → Privacy & security → Camera: with camera access off, FOTOHAVN shows "Not available. Camera access is off in Windows privacy settings."

## Catalog and compositor

`FotoHavn.Catalog` holds the Templates and Looks (the website's ids and geometry), the embedded artwork and fonts, and `SkiaCompositor`, which both hosts hand to the brain. It composes a Photo Strip at the Template's native size as an opaque PNG.

- **Golden images.** `tests/FotoHavn.Catalog.Tests/Golden/composition-hashes.json` pins the decoded-pixel hash of every Template × Look strip (28) and of each blank preview (4). After an intended change to the compositor, a kernel, or the artwork, regenerate them from `app/`:

  ```powershell
  $env:FOTOHAVN_UPDATE_GOLDEN = '1'; dotnet test --project tests/FotoHavn.Catalog.Tests/FotoHavn.Catalog.Tests.csproj; Remove-Item Env:FOTOHAVN_UPDATE_GOLDEN
  ```

  The run rewrites the hash file and writes the 32 PNGs to `output/golden/`; look at them before committing, then run the tests again without the variable to prove they pass.
- **Compose hooks (E2E builds only).** On the camera diagnostics screen, `test.captureDiagnostics` keeps the last four Captures in the brain. `test.composeDiagnostics { templateId, lookId }` composes the newest Captures that fit the Template's windows (oldest of them first; fewer Captures leave windows empty) and `test.composeBlankPreview { templateId }` composes an empty Template in Naturale. Each answers `{ previewKey, width, height, byteLength }`, and the PNG is served at `https://preview.fotohavn/<previewKey>.png` (headless: `/preview/<previewKey>.png`) with `Cache-Control: no-store`. The screen's `Template` and `Look` selects with `COMPOSE` and `BLANK PREVIEW` call the same hooks and show the result (Panorama Four is shown rotated). `@m7` `real-capture-composition` drives all of this against the real camera and leaves a composed PNG and screenshots in `output/e2e/m7/`.
- **Speed (spike S3).** `dotnet test --project tests/FotoHavn.Catalog.Tests --filter-method "*Composing_a_strip*"`, run without `--coverage`, measures the compositor and writes `output/speed/s3-compositor-speed.txt`. Run it on the booth laptop; the result for the development machine is in [ADR 0006](adr/0006-brain-side-composition-with-an-app-owned-catalog.md#spike-s3-result).

## E2E build, test hooks, and the publish guard

The E2E build compiles the test hooks into the app:

```powershell
cd ui; npm run build:e2e; cd ..
dotnet build src/FotoHavn.Host.Windows/FotoHavn.Host.Windows.csproj -c Debug -p:FotoHavnE2E=true -o output/e2e-build
```

- `-p:FotoHavnE2E=true` defines `E2E_HOOKS` in every project. Only then are the `test.*` hooks compiled; other builds answer every `test.*` message with `hooksUnavailable`.
- A hook build (`FotoHavnE2E=true`, or `E2E_HOOKS` defined any other way) restores and compiles into each project's own `obj/e2e/` and `bin/e2e/`, never the `obj/Debug/` or `bin/Debug/` of normal builds, so a later build, test run, or `dotnet publish --no-build` can't pick up a hook assembly. Every hook assembly also carries `[assembly: AssemblyMetadata("FotoHavnE2E", "true")]`.
- `npm run build:e2e` writes the E2E UI (`VITE_E2E=true`) to `ui/dist-e2e/` with an `e2e-build.txt` marker; `npm run build` writes the field UI to `ui/dist/`. The E2E build of the host copies `ui/dist-e2e` into `wwwroot/` beside `FOTOHAVN.exe`, every other build copies `ui/dist`, and each removes files a previous copy left there. A field build fails if `ui/dist` holds the marker. Without the UI folder, the app shows "The FOTOHAVN UI is not built."
- `FOTOHAVN.exe` takes `--data <folder>` (default `Data` beside the exe, created if missing) and, in E2E builds only, `--remote-debugging-port <port>`, `--e2e-camera <name or device id>`, and `--e2e-storage-fault <diskFull|accessDenied|locked>` (see [Event storage](#event-storage-the-data-folder-faults-and-crash-tests)). A field build reads the E2E flags and ignores them. Field builds never open a debugging port.
- M2 hooks travel as protocol `test` messages over each page's own brain connection and come back as `testResult` (`hooksUnavailable` in other builds). The E2E UI exposes `window.fotohavnE2E` (`kind`, `getState()`, `sendCommand(name, payload)`, `sendTest(name, args)`), which `e2e/src/hooks.ts` uses with the generated `TestHookMap` types. Hooks: `test.getDiagnostics`, `test.useVirtualDisplays`, `test.disconnectDisplay`, `test.connectDisplay`, `test.openGuestScreen` and `test.closeGuestScreen` (development stand-ins for starting and ending an Event until M6), `test.setWindowBounds` (sizes a window's client area, the page viewport, to the given physical pixels, even beyond the screen), `test.crashUi`, `test.listCameras`, and `test.exit`. M5 adds the camera hooks: `test.openCameraDiagnostics`, `test.selectDiagnosticsCamera`, `test.setDiagnosticsMirror`, `test.captureDiagnostics` (the host saves `<data>/diagnostics/capture-yyyyMMdd-HHmmss-fff.jpg`), `test.injectCameraFault`, and `test.clearCameraFault`; `test.getDiagnostics` carries a `camera` block. M7 adds `test.composeDiagnostics` and `test.composeBlankPreview` (see [Catalog and compositor](#catalog-and-compositor)). Later milestones add the rest listed in [the plan](implementation-plan.md#e2e-build-and-test-hooks).
- **Publish guard.** It fails a publish of any project with `FOTOHAVN field builds must not include E2E hooks` when:
  - the publish asks for hooks (`-p:FotoHavnE2E=true` or `E2E_HOOKS` in `DefineConstants`);
  - a `FOTOHAVN` or `FotoHavn.*` assembly it would copy carries the `FotoHavnE2E` marker or an `.E2E` namespace, which also covers `--no-build` publishes (MSBuild target `FotoHavnRefuseE2EPublishedFiles` in `Directory.Build.targets`);
  - the UI it would copy is the E2E build (`wwwroot/e2e-build.txt`).

  `scripts/publish-field-test.ps1` refuses requests for hooks with the same text, and after publishing it checks the output's assemblies and `wwwroot/` again. Unit tests in `FotoHavn.Host.Windows.Tests` check that normally built assemblies have no `.E2E` types and no marker, and the `@m1` regression suite checks every refusal, including a `--no-build` publish right after an E2E build.

To attach Playwright or DevTools by hand, start `output/e2e-build/FOTOHAVN.exe --data output/scratch/Data --remote-debugging-port 9222` and connect to `http://127.0.0.1:9222`.

## Event storage: the Data folder, faults, and crash tests

- **The Data folder.** Both hosts open one `SqliteEventStore` on the Data folder (`--data <dir>`, default `Data` beside the exe; the headless host takes the same `--data`). It holds `fotohavn.db` (SQLite, WAL), `Events/<event>/GuestCycles/<cycle>/` (Captures and Photo Strips), and `Cache/thumbnails/`; the host adds `logs/` and `webview2-user-data/`. Only the store writes the first three. Run it on the computer's own disk: a network drive, a OneDrive, Dropbox, Google Drive, or iCloud folder, or a read-only folder is refused. Opening runs the startup reconciler, which hashes every kept Capture and Photo Strip, so a large Data folder starts more slowly (about 0.8 s for 300 cycles and 534 MiB on the development laptop; the timing test fails above 10 s).
- **Starting and the storage error screen.** The brain opens the store on its executor and the operator surface is `starting` (protocol version 3) until the first open answers, then `eventList` or `storageError`. The storage error screen names the cause (disk full, access denied, locked, not a local disk, cloud-synced folder, not writable, integrity check failed, corrupt, or unknown) and the Data folder path; `RETRY` sends `RetryStorage`, which opens the store again. While a retry runs, the screen stays up.
- **Fault flag (E2E builds only).** `--e2e-storage-fault <diskFull|accessDenied|locked>` fails every file write from before the store opens, so `OpenAsync` fails at its first write and the operator sees the storage error screen. The `test.injectStorageFault(kind, scope)` and `test.clearStorageFault` hooks do the same at run time (scope `nextWrite` or `allWrites`). Clear the fault with the hook, then `RetryStorage`. Try it:

  ``powershell
  dotnet build src/FotoHavn.Host.Windows/FotoHavn.Host.Windows.csproj -c Debug -p:FotoHavnE2E=true -o output/e2e-build
  output/e2e-build/FOTOHAVN.exe --data output/scratch/Data --e2e-storage-fault diskFull
  ``

  In E2E runs, wait for `test.getDiagnostics` `storage.state` to be `ready` or `error` (wire `opening`, `ready`, `error`) before asserting a screen.
- **Crash tests.** `tests/FotoHavn.Storage.Tests/Crash` seeds a Data folder through the real store, runs `FotoHavn.Storage.CrashHarness` as a child process (`dotnet crash-harness/FotoHavn.Storage.CrashHarness.dll --data <dir> --scenario <capture|strip|eventDelete|cycleDelete> --crash-at <step|none>`) that kills itself right after each named step (`CrashPoint.All`), then opens a fresh store on the folder and checks the invariants. They run in the normal `dotnet test --solution` run and in CI. The test project's build copies the harness beside the test assembly. Run only them with:

  ``powershell
  dotnet test --project tests/FotoHavn.Storage.Tests --no-build --filter-class FotoHavn.Storage.Tests.Crash.CrashRecoveryTests
  ``

  The real fault tests (`Faults/RealFaultTests`) use a read-only file and a handle opened with `FileShare.None`; the 300-cycle start-up timing test (`Reconciliation/StartupTimingTests`) writes about 540 MiB under the test output folder and runs alone.

## Protocol schema and types

The C# records in `src/FotoHavn.Protocol` are the source of the UI and brain protocol. After changing any command, presentation, envelope, or hook record (and bumping `ProtocolVersion.Current` for an incompatible change), run from `app/`:

```powershell
./scripts/generate-protocol.ps1                     # schema + ui/src/protocol/generated.ts
./scripts/generate-protocol.ps1 -UpdateSnapshots    # also rewrites tests/FotoHavn.Protocol.Tests/Snapshots
```

- It exports `src/FotoHavn.Protocol/schema/protocol.schema.json` with `JsonSchemaExporter` (by running the schema drift test with `FOTOHAVN_UPDATE_PROTOCOL=1`), then runs `npm run generate:protocol` in `ui/`, which generates `ui/src/protocol/generated.ts` with `json-schema-to-typescript`. Run `npm ci` in `ui/` once first.
- Commit the schema, `generated.ts`, and any snapshot changes with the record change. Review snapshot diffs: they are the wire format.
- Drift fails the normal verification: `SchemaDriftTests` in `dotnet test` when the schema is stale, `scripts/protocol-types.test.ts` in `npm run test:coverage` when `generated.ts` is stale, and `SnapshotTests` when a serialization changed.

**Presentation fixtures.** `FotoHavn.Core` tests write brain-generated messages to `ui/src/fixtures/presentations/<name>.json` (one compact message per file, LF), which the UI's fixture lane renders by name. `PresentationFixtureTests` fails when a file is stale, missing, or extra; regenerate them after a presentation change with:

```powershell
$env:FOTOHAVN_UPDATE_FIXTURES = '1'; dotnet test --project tests/FotoHavn.Core.Tests/FotoHavn.Core.Tests.csproj; Remove-Item Env:FOTOHAVN_UPDATE_FIXTURES
```

## Headless host

`FotoHavn.Host.Headless` runs the real brain without WPF: Kestrel on `127.0.0.1`, a WebSocket bridge that speaks the same envelope as WebView2, the built UI, and the media and preview routes. It uses fake displays (`fake-primary` and `fake-guest`, 1920×1080 each) and no camera yet.

```powershell
dotnet run --project src/FotoHavn.Host.Headless -- --data output/scratch/Data
dotnet run --project src/FotoHavn.Host.Headless -- --data output/scratch/Data --port 5180 --ui ui/dist-e2e
```

| Option | Default |
| --- | --- |
| `--data <dir>` | `Data` beside the executable; created at start |
| `--port <n>` | 0, a random free port on `127.0.0.1` |
| `--ui <dir>` (alias `--ui-dist`) | The nearest `ui/dist` with an `index.html` above the working directory |

Options also take the `--name=value` form; an unknown or invalid option prints a message to stderr and exits with code 2. `dotnet run` starts the host in the directory it was invoked from, so relative paths resolve from `app/`. Stop it with Ctrl+C. After a solution build, `--no-build` (before the `--`) skips the build, as the integration lane does.

At startup it prints human-readable lines, then exactly one JSON line, always the last startup line, for tooling:

```text
FOTOHAVN headless host listening on http://127.0.0.1:58908/
data: C:\…\Data
ui: C:\…\ui\dist
operator: http://127.0.0.1:58908/?token=<64 hex>
guest: http://127.0.0.1:58908/?token=<64 hex>
Press Ctrl+C to stop.
{"event":"listening","port":58908,"url":"http://127.0.0.1:58908/","bridgeUrl":"ws://127.0.0.1:58908/bridge","operatorUrl":"…","guestUrl":"…","operatorToken":"…","guestToken":"…","mediaUrl":"http://127.0.0.1:58908/media/","previewUrl":"http://127.0.0.1:58908/preview/","dataFolder":"…","uiFolder":"…"}
```

- **Tokens decide the role.** Each start creates one random 256-bit token per role. Open `operatorUrl` or `guestUrl`; the page connects to `ws://127.0.0.1:<port>/bridge?token=<token>`, and the token maps to its role. A missing or unknown token gets HTTP 401; a request whose `Host` isn't loopback gets 400, and a non-loopback `Origin` gets 403.
- **Static UI.** Any other extension-less GET serves `index.html`, so `/?token=` loads the UI.
- **Media and previews.** `/media/<path>` serves what `https://media.fotohavn/<path>` serves in the WPF host, and `/preview/<key>.png` what `https://preview.fotohavn/<key>.png` serves (M2's UI shows no media yet; the UI maps those URLs to these routes when it does). Both are GET/HEAD only and 404 for anything refused or missing. Media paths follow `MediaPathPolicy` (only `events/<eventId>/guest-cycles/<guestCycleId>/<file>` under `Data/Events` and `thumbnails/<file>` under `Data/Cache/thumbnails`); previews come from the preview source, which is empty until M7.
- **Hooks.** A normal build answers every `test.*` message with `hooksUnavailable`. Built with `-p:FotoHavnE2E=true` it has host hooks with no windows: `setWindowBounds` is `notSupported`, `crashUi` closes that role's sockets, `listCameras` lists a fake camera, and `exit` stops the host.

## UI transports and query parameters

The UI picks one transport at startup (`ui/src/brain/createBrainClient.ts`; details in [`../ui/README.md`](../ui/README.md)):

| Transport | When |
| --- | --- |
| WebView2 | Inside the WPF host (`window.chrome.webview` exists); the host assigns the role per window |
| WebSocket | The page has `?token=<role token>`; it connects to `/bridge` on its own host, or to `?brain=ws://127.0.0.1:<port>/bridge` |
| Fixture | Development and E2E builds only, with `?fixture=<name>`: a stand-in brain serving one fixture from `ui/src/fixtures/presentations/` (or the hand-written `operator-event-list`) |

Otherwise the page shows "Not connected." (with fixture links in development and E2E builds). E2E builds also honor `?e2eProtocolVersion=<n>`, the version sent in `hello`, which makes the brain answer `protocolMismatch`. Field builds contain no fixture code: `main.tsx` loads the fixture lane with a dynamic import behind a build-time guard.

Development and E2E builds also serve the component fixture gallery, with no brain: `?gallery` lists and renders every component fixture, `?gallery=<fixture id>` renders one alone in the full viewport, and `?gallery=manifest` publishes the list the fixture lane reads. `vite.config.ts` fails a field build that bundles the gallery, the fixture lane, or a fixture file. Component conventions are in [`../ui/src/components/README.md`](../ui/src/components/README.md).

## Headless dev loop

For fast UI work without the WPF host, run the headless host and the Vite dev server together:

```powershell
dotnet run --project src/FotoHavn.Host.Headless -- --data output/scratch/Data --port 5180
cd ui; npm run dev
```

Then open `http://localhost:5173/?token=<operator token>&brain=ws://127.0.0.1:5180/bridge` (and the guest token in a second tab) for hot reload against real presentations. For a surface without a brain, open `http://localhost:5173/?fixture=<name>`; for components, open `http://localhost:5173/?gallery` or `?gallery=<fixture id>`. Playwright can also attach to the E2E build through its debugging port (see [E2E build](#e2e-build-test-hooks-and-the-publish-guard)).

## Publishing

```powershell
./scripts/publish-field-test.ps1
```

M1's script is minimal: it refuses E2E hooks, builds the field UI (`npm ci` if `ui/node_modules` is missing, then `npm run build`), runs a self-contained `win-x64` Release publish of the host to `output/field-test/FOTOHAVN`, and fails if the output has a hook assembly or the E2E UI marker. It needs PowerShell 7. M10 completes the field-test build (packaging, the booth checklist, and the hardening checks).

## Outputs

Everything lands under `app/output/` (git-ignored):

| Path | Contents |
| --- | --- |
| `output/coverage/` | .NET Cobertura files, and `report/Summary.txt` with the ReportGenerator summary (per assembly under `report/<assembly>/` when gated) |
| `output/e2e-build/` | The E2E build: `FOTOHAVN.exe` and `wwwroot/` (from `ui/dist-e2e`); its intermediates are in each project's `obj/e2e/` |
| `output/e2e/html/` | The end-to-end HTML report |
| `output/e2e/junit.xml`, `output/e2e/test-results/` | JUnit results; traces and screenshots of failures |
| `output/e2e/runs/` | One folder per app launch: Data folder, stdout and stderr, and `evidence/` (log, Data listing, final screenshot, summary) |
| `output/e2e/camera.json` | The camera global setup chose |
| `output/e2e/m7/` | `@m7` composition evidence: composed PNGs and screenshots of the composition screen |
| `output/golden/` | The 32 golden PNGs written when `FOTOHAVN_UPDATE_GOLDEN=1` |
| `output/speed/` | `s3-compositor-speed.txt` from the S3 compositor speed test |
| `output/ui-fixtures/`, `output/fixtures/` | Fixture and headless lane report, JUnit file, traces, and screenshots |
| `output/ui-headless/` | Data folders of the headless integration lane (removed after each run) |
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
