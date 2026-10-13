# Desktop app work

Use this guide for the desktop booth under `app/`: the .NET brain and adapters, the WPF host, the React UI in `ui/`, the end-to-end framework in `e2e/`, scripts, and docs. `docs/implementation-plan.md` holds milestone scope and detail; this guide does not repeat it.

## Load

- Read `../CONTEXT-MAP.md`, `../CONTEXT.md`, and `CONTEXT.md` for booth behavior and language.
- Read `docs/adr/0003-react-ui-in-wpf-webview2-over-a-dotnet-brain.md` before any work; it fixes the process, host, brain, and UI split.
- Read the ADR for the area you touch:
  - Camera, preview, or Capture: `docs/adr/0004-brain-owned-webcam-through-one-exclusive-mediacapture-stream.md`, plus `docs/research/` for device behavior.
  - Event storage, the Data folder, or the reconciler: `docs/adr/0005-sqlite-metadata-with-file-artifacts-for-event-storage.md`.
  - Templates, Looks, or the compositor: `docs/adr/0006-brain-side-composition-with-an-app-owned-catalog.md`.
  - Tests, coverage, E2E build, or test hooks: `docs/adr/0007-unit-first-testing-with-real-camera-end-to-end.md`.
  - Operator and Guest Screens, windows, or display binding: `docs/adr/0008-operator-and-guest-screens-on-bound-displays.md`.
- Read `../brand/README.md` and the affected `../brand/` documents, then `DESIGN.md`, before changing visuals, layout, motion, icons, or UI copy. `DESIGN.md` is the desktop's interpretation of `../brand/`.
- Read `docs/local-development-guide.md` before building, running, publishing, or using camera or display hardware.

## Boundaries

- The brain (`FotoHavn.Core`) owns every rule and timer through injected interfaces; it never calls Windows APIs.
- The UI owns rendering only: it renders the presentation for its role and sends commands. It never stores Event data or decides a rule.
- The host assigns each window its role; the page can't choose it. The host holds no domain logic.
- The Event store is the only writer of Event data: `fotohavn.db`, `Events/`, and `Cache/thumbnails/`. The host owns `logs/` and `webview2-user-data/`.
- Keep adapters thin. Move any branch that isn't hardware or OS I/O (format choice, frame freshness, frame selection, retry policy) into a `FotoHavn.Core` policy class with unit tests.
- Compile test hooks only into `FotoHavnE2E` builds (`dotnet build -p:FotoHavnE2E=true`, which compile into `obj/e2e/` and copy the E2E UI from `ui/dist-e2e`). Never publish a field build with hooks or the E2E UI; the publish guard in `Directory.Build.targets` refuses both.
- Raise coverage with tests, not new exclusions; every exclusion needs a stated reason.

## Verification

Write unit tests for each rule first, then the end-to-end scenario. Before completing a milestone, run these from `app/` in order and fix every failure before going on. There are no wrapper scripts; CI runs steps 1–4 and 8 with the same commands. M0 runs only step 8 and its review. M1 runs every step, with coverage collected and reported but not gated. From M2 on, every step applies, including the per-assembly gate. [`docs/local-development-guide.md`](docs/local-development-guide.md) explains each command.

```powershell
# 1. Build .NET (warnings are errors)
dotnet restore FotoHAVN.slnx --locked-mode
dotnet build FotoHAVN.slnx --no-restore

# 2. .NET tests with Cobertura coverage, then the ReportGenerator summary of the four gated assemblies
Remove-Item output/coverage -Recurse -Force -ErrorAction Ignore
dotnet test --solution FotoHAVN.slnx --no-build --results-directory output/coverage --coverage --coverage-output-format cobertura
dotnet tool restore
dotnet tool run reportgenerator "-reports:output/coverage/*.cobertura.xml" "-targetdir:output/coverage/report" "-reporttypes:TextSummary;Cobertura" "-assemblyfilters:+FotoHavn.Core;+FotoHavn.Protocol;+FotoHavn.Storage;+FotoHavn.Catalog"
#    Gate each assembly at 90% line and branch (from M2)
foreach ($assembly in 'FotoHavn.Core','FotoHavn.Protocol','FotoHavn.Storage','FotoHavn.Catalog') {
  dotnet tool run reportgenerator "-reports:output/coverage/*.cobertura.xml" "-targetdir:output/coverage/report/$assembly" "-reporttypes:TextSummary" "-assemblyfilters:+$assembly" "minimumCoverageThresholds:lineCoverage=90" "minimumCoverageThresholds:branchCoverage=90"
  if ($LASTEXITCODE -ne 0) { throw "$assembly is below 90% line or branch coverage" }
}
#    The Core tests again in the E2E build (obj/e2e, bin/e2e; no coverage), which cover the E2E_HOOKS glue
dotnet test --project tests/FotoHavn.Core.Tests/FotoHavn.Core.Tests.csproj -p:FotoHavnE2E=true

# 3. UI (in ui/; test:coverage fails below 80%; run `npx playwright install chromium` once before test:fixtures,
#    which also runs the headless integration lane with `dotnet run --no-build`, so step 1 must have built .NET)
npm ci; npm run lint; npm run typecheck; npm run test:coverage; npm run build; npm run test:fixtures

# 4-5. End-to-end (in e2e/; real camera required; global setup runs `npm run build:e2e` in ui/ and the E2E build below)
npm ci; npm run typecheck
$env:FOTOHAVN_E2E_CAMERA = "<camera name or device id>"   # optional; defaults to the first camera
npx playwright test --project=regression --grep "@m<N>"
npx playwright test --project=smoke
#    The E2E build on its own (in ui/: npm run build:e2e, which writes ui/dist-e2e; then from app/):
#    dotnet build src/FotoHavn.Host.Windows/FotoHavn.Host.Windows.csproj -c Debug -p:FotoHavnE2E=true -o output/e2e-build

# 8. Agent docs (from the repository root)
./scripts/verify-agent-docs.ps1
```

- Delete `output/coverage` before each test run: the coverage files have GUID names and accumulate, so stale runs would be merged in.
- Write the threshold as `"minimumCoverageThresholds:lineCoverage=90"`, with no leading dash; ReportGenerator silently ignores the dashed form. The merged summary can hide a weak assembly, so the gate runs once per assembly. An assembly with no branches passes the branch threshold.
- Read the coverage text summary and the Playwright HTML report and trace for any failure; results land in `output/`.
- With no Available Camera, report "end-to-end not run: no camera". From M1 on, a milestone without a passing end-to-end run is incomplete.
- Report `@physical-displays` scenarios as "not run: one display" when fewer than two real displays exist, and cover them with the milestone's manual checks.
- For UI changes, inspect the end-to-end screenshots at all four resolutions against `DESIGN.md` and `../brand/`; fix anything off-brand, clipped, overflowing, or scrolling on a Guest Screen.
- Do the milestone's manual hardware checks where the hardware exists.
- Add a verification record under the milestone in `docs/implementation-plan.md`: date and commit, machine and camera, pass/fail/skip counts per command, coverage per gated project, manual results, and every item not proven with its reason. Only then tick its checkboxes.

Completion requires every step that applies to the milestone, per the phasing above, to pass or be reported as not run with its reason, and every unproven hardware, display, or visual state to be named.
