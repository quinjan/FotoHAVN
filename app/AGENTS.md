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
- Compile test hooks only into `FotoHavnE2E` builds (`dotnet build -p:FotoHavnE2E=true`). Never publish a field build with hooks.
- Raise coverage with tests, not new exclusions; every exclusion needs a stated reason.

## Verification

Write unit tests for each rule first, then the end-to-end scenario. Before completing a milestone, run these from `app/` in order and fix every failure before going on. There are no wrapper scripts; CI runs the same commands. M0 runs only step 8 and its review. M1 runs every step, with the coverage threshold checked for collection only (run ReportGenerator without `minimumCoverageThresholds`). From M2 on, every step applies.

```powershell
# 1. Build .NET (warnings are errors)
dotnet restore FotoHAVN.slnx --locked-mode
dotnet build FotoHAVN.slnx --no-restore

# 2. .NET tests with Cobertura coverage, gated at 90% line and branch in each gated assembly
#    Exact coverage flags: confirmed in M1.
dotnet test --solution FotoHAVN.slnx --no-build -- --coverage --coverage-output-format cobertura
dotnet tool run reportgenerator "-reports:**/TestResults/**/*.cobertura.xml" "-targetdir:output/coverage" "-reporttypes:TextSummary;Cobertura" "-assemblyfilters:+FotoHavn.Core;+FotoHavn.Protocol;+FotoHavn.Storage;+FotoHavn.Catalog" "-minimumCoverageThresholds:lineCoverage=90" "-minimumCoverageThresholds:branchCoverage=90"

# 3. UI (in ui/; test:coverage fails below 80%)
npm ci; npm run lint; npm run typecheck; npm run test:coverage; npm run build; npm run test:fixtures

# 4-5. End-to-end (in e2e/; real camera required; set FOTOHAVN_E2E_CAMERA to choose one)
npm ci; npm run typecheck
npx playwright test --project=regression --grep "@m<N>"
npx playwright test --project=smoke

# 8. Agent docs (from the repository root)
./scripts/verify-agent-docs.ps1
```

- M1 must configure the test runner as Microsoft.Testing.Platform (`"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`) so the `dotnet test --solution` command above works; the exact flags are confirmed in M1.
- Each gated assembly (`FotoHavn.Core`, `FotoHavn.Protocol`, `FotoHavn.Storage`, `FotoHavn.Catalog`) must individually reach 90% line and branch coverage. The merged threshold flag alone isn't enough: check each assembly in the per-assembly summary, or run ReportGenerator once per assembly. M1 pins the exact method.
- Read the coverage text summary and the Playwright HTML report and trace for any failure; results land in `output/`.
- With no Available Camera, report "end-to-end not run: no camera". From M1 on, a milestone without a passing end-to-end run is incomplete.
- Report `@physical-displays` scenarios as "not run: one display" when fewer than two real displays exist, and cover them with the milestone's manual checks.
- For UI changes, inspect the end-to-end screenshots at all four resolutions against `DESIGN.md` and `../brand/`; fix anything off-brand, clipped, overflowing, or scrolling on a Guest Screen.
- Do the milestone's manual hardware checks where the hardware exists.
- Add a verification record under the milestone in `docs/implementation-plan.md`: date and commit, machine and camera, pass/fail/skip counts per command, coverage per gated project, manual results, and every item not proven with its reason. Only then tick its checkboxes.

Completion requires every step that applies to the milestone, per the phasing above, to pass or be reported as not run with its reason, and every unproven hardware, display, or visual state to be named.
