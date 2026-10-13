---
status: accepted
date: 2026-10-13
---

# Unit-first testing with real-camera end-to-end

The desktop booth is proven mostly by unit tests with enforced coverage gates, then by integration tests, then by a Playwright TypeScript end-to-end framework that launches the real WPF app with a real camera. End-to-end runs use a separate E2E build with test-only hooks that a field build can never contain.

Replaces: the corresponding part of the deleted WinUI 3 app: its UI verification host, state injection, and release simulation records, and the `ui-verification-batch-3.yml`, `-4.yml`, and `-5.yml` workflows. xUnit v3 stays the .NET test framework.

## Context

- Every rule lives in the brain behind injected interfaces ([ADR 0003](0003-react-ui-in-wpf-webview2-over-a-dotnet-brain.md)), so most behavior can be proven without a window, camera, or display.
- The camera, WebView2, displays, and touch can only be proven on real hardware, and GitHub's hosted runners have no camera.
- Phase Timers, the PIN lockout, and the Keepsake preview run for seconds or minutes, so long scenarios need faster time without hiding timing bugs.
- Agents implement milestones and must report exactly what they proved and what they couldn't.

## Decision

**Unit tests first (D29).** Each task writes the unit tests for its rules first. If an adapter grows a branch that isn't hardware or OS I/O (format choice, frame freshness, capture-frame selection, retry policy), that branch moves into a policy class in `FotoHavn.Core` with unit tests.

**Coverage gates.**

| Area | Framework | Line and branch threshold |
| --- | --- | --- |
| `FotoHavn.Core` | xUnit v3, FsCheck model-based tests | 90% |
| `FotoHavn.Protocol` | xUnit v3 | 90% |
| `FotoHavn.Storage` | xUnit v3 against real temporary SQLite files | 90% |
| `FotoHavn.Catalog` | xUnit v3, golden images | 90% |
| `app/ui/src` logic (brain client, view-model mapping, hooks, utilities, component behavior) | Vitest + Testing Library | 80% |
| `FotoHavn.Camera.Windows`, `FotoHavn.Host.Windows`, `FotoHavn.Host.Headless` | Integration and end-to-end | Excluded |

- .NET coverage is collected by `dotnet test` with `Microsoft.Testing.Extensions.CodeCoverage` in Cobertura format and checked by ReportGenerator, pinned as a local .NET tool, through `minimumCoverageThresholds` filtered to the four gated assemblies. UI coverage uses Vitest's v8 coverage with `thresholds` in `vitest.config.ts`.
- Each gated assembly (`FotoHavn.Core`, `FotoHavn.Protocol`, `FotoHavn.Storage`, `FotoHavn.Catalog`) must individually reach 90% line and branch coverage. The merged threshold flag alone isn't enough, because a well-covered assembly can hide a weak one: check each assembly in the per-assembly summary, or run ReportGenerator once per assembly. M1 pins the exact method.
- Exclusions are limited to generated files, entry points, and fixtures, each listed with its reason in the config file. A gap is fixed with tests, not exclusions.
- The gates apply from M2. In M1 they only check that coverage is collected and reported.

**Integration tests.** Storage crash tests kill a child process at every commit step and assert the reconciler result; fault tests inject disk-full, access-denied, and locked-file errors. The headless host runs the real brain with a fake camera over WebSocket in Chromium. A fixture gallery renders brain-generated presentation fixtures at 1280×720, 1366×768, 1920×1080, and 2560×1440 with axe, reduced motion, and forced colors. `FotoHavn.Camera.Windows.Tests` opens a real webcam and reports "not run", never passed, where there is none.

**End-to-end framework (D30).**

- A standalone Playwright TypeScript project in `app/e2e/` with `smoke` and `regression` projects, one worker (the camera is exclusive), and scenarios tagged by milestone (`@m1` … `@m10`).
- Each test file launches a fresh app process with its own temporary Data folder, WebView2 user-data folder, and debugging port, and connects through CDP. Both windows share one browser process, so the fixture exposes `operatorPage` and `guestPage` by the role the host assigns.
- Most scenarios use virtual displays mapped onto the real monitor. Scenarios tagged `@physical-displays` need two real displays and are reported "not run: one display" without them.
- **A real camera is mandatory.** Global setup fails the whole run with "End-to-end tests require at least one connected camera." when there is no Available Camera; it never skips or passes. `FOTOHAVN_E2E_CAMERA` chooses a camera, otherwise the first listed one is used and printed.
- Tests assert properties of real frames, not pixels. Pixel-exact checks stay in the catalog's golden-image unit tests.
- Locators use role, label, and visible text first; `data-testid` only where there's no accessible handle.
- `smoke` covers the critical path of every built milestone in under 5 minutes and runs before every merge that touches `app/`. `regression` covers every scenario at each milestone's end and before a field build.
- End-to-end tests run on a local Windows machine or the booth laptop, never on GitHub's hosted runners. Results go to `app/output/e2e/`.

**E2E build and test hooks (D31).**

- `dotnet build -p:FotoHavnE2E=true` defines `E2E_HOOKS`. Only then are hooks compiled into the brain, host, and UI (the UI build reads `VITE_E2E=true`).
- Hooks travel as `test.*` messages over the same bridge, and the brain rejects them unless compiled with `E2E_HOOKS`. They cover camera listing, clock scaling, camera and storage fault injection, diagnostics, window sizing, virtual displays with connects and disconnects, and UI crashes.
- Clock scaling speeds up brain timers only; camera frames stay real-time. Every timer rule also has unit tests at real durations on the fake clock, and the smoke suite runs at real speed (risk R7).
- **Publish guard.** `publish-field-test.ps1` and an MSBuild target refuse to publish a field build with `FotoHavnE2E=true`, and a unit test checks by reflection that a field build's assemblies contain no hook types.

**Verification and reporting.**

- There are no wrapper scripts; agents and CI run the same commands directly, as listed in the implementation plan's [verification procedure](../implementation-plan.md#verification-procedure-for-the-implementing-agent) and recorded in [`app/AGENTS.md`](../../AGENTS.md).
- GitHub CI (`.github/workflows/desktop.yml`, Windows runner) runs build, unit, integration, UI, and end-to-end typecheck steps, but not end-to-end tests.
- An agent without a camera reports "end-to-end not run: no camera" and doesn't declare the milestone complete.
- Each milestone lists its manual hardware checks, and every report names what stays unproven.

## Considered options

- **Mainly end-to-end or screenshot verification**, as in the deleted app's UI verification host, was rejected. It is slow, needs hardware, and can't reach every rule branch.
- **A fake camera in end-to-end runs** was rejected for the end-to-end layer. The fake camera serves unit, headless, and fixture tests; only a real camera proves the adapter, WebView2 preview, and capture path.
- **Skipping end-to-end runs when no camera is present** was rejected. A skipped run would look like a pass.
- **Test hooks in every build behind a runtime flag** was rejected. A runtime flag could be switched on in a field build; compiling the hooks only into the E2E build, with the field build guarded against them, rules that out.
- **Running end-to-end tests in GitHub CI** isn't possible on hosted runners without a camera. A self-hosted runner on the booth laptop, labelled `camera`, may run them later.

## Consequences

- From M1 on, no milestone is complete without a real-camera end-to-end run and a verification record that names the machine and camera. M0 has no code to run end to end.
- End-to-end results depend on the camera and machine (risk R6), so the `regression` suite runs on the booth laptop before every field build.
- The field build has no debugging port and no hooks, so its portable-launch test checks only the process and files.
- Camera and host adapters must stay thin, because their code isn't covered by the gate.
