# Windows UI verification host

`FotoHavn.UiVerificationHost` is the shared Windows process for visual and UI
Automation evidence. It launches the test-only state-injection build of the
real FotoHAVN application, sizes the WinUI client to the capture plan's
effective viewport, waits for the exact injection identity to report
`settled`, captures the client, and emits exact image and semantic evidence.

The host resolves its 103 fixtures from the capture plan and traceability
scenario catalogs. It verifies every target against the SHA-256 value anchored
in the `design-v1.0.1` manifest before launching FotoHAVN. Each fixture's
approved YAML annotation is loaded as the executable semantic contract for
role, state, heading, reading order, focus, announcement text and priority,
role-appropriate patterns, and target size.

## Pinned evidence environment

Qualified evidence requires the exact values in
`tools/FotoHavn.UiVerificationHost/pinned-environment.json`: Windows build,
architecture, .NET SDK, cultures, DPI, app theme, and font smoothing. The host
refuses to run when any value differs. `--allow-environment-drift` exists for
local integration diagnosis only; every resulting scenario is marked
`environment-drift` and the run remains blocking.

Use a dedicated desktop with no window covering FotoHAVN because capture reads
the real on-screen WinUI client. Do not use Remote Desktop, HDR, a custom color
profile, or display overrides for qualified evidence.

## Build and validate

```powershell
dotnet build src/FotoHavn.App/FotoHavn.App.csproj -p:UiVerificationBuild=true
dotnet build tools/FotoHavn.UiVerificationHost/FotoHavn.UiVerificationHost.csproj

dotnet run --project tools/FotoHavn.UiVerificationHost/FotoHavn.UiVerificationHost.csproj -- `
  --validate-plan `
  --repository-root .
```

Run all fixtures for the current rollout boundary:

```powershell
$app = Resolve-Path "src/FotoHavn.App/bin/Debug/net10.0-windows10.0.26100.0/win-x64/FotoHAVN.exe"
dotnet run --project tools/FotoHavn.UiVerificationHost/FotoHavn.UiVerificationHost.csproj -- `
  --repository-root . `
  --app $app `
  --output artifacts/ui-verification/batch-1 `
  --completed-through-batch 1
```

Use `--fixture saved-events.new-event.standard` repeatedly to select fixtures.
With no fixture selection, the host runs all 103.

## Deterministic transition scripts

Pass one or more `--transition <approved-id>` options. Each checked-in catalog
entry owns one approved final fixture, supplies the test-only request accepted
by FotoHAVN, invokes controls through its catalogued Automation IDs, and waits
for the expected injected state after each command. The final state is captured
through the same visual and semantic pipeline.

```json
{
  "id": "transition.guest-start.capture-countdown",
  "fixtureId": "capture.countdown-3.standard",
  "request": {
    "identity": "injection.guest-start.ready",
    "script": [
      {
        "onCommand": "StartGuestCycle",
        "injectionIdentity": "injection.capture.countdown-3",
        "announcement": "Capture in progress.",
        "announcementPriority": "polite"
      }
    ]
  },
  "actions": [
    {
      "automationId": "StartGuestCycleButton",
      "expectedInjectionIdentity": "injection.capture.countdown-3"
    }
  ]
}
```

## Evidence and rollout debt

Each fixture folder contains `actual.png`, `diff.png`, and `result.json`.
`environment.json` and `run.json` sit at the run root. Results include fixture
and injection identities, effective geometry, required check-suite IDs,
target/actual/diff SHA-256 hashes, every meaningful UI Automation element,
names, roles, states, supported patterns, tree reading order, focus, live-region
events, target-size and viewport violations, and complete pinned-versus-actual
environment data. `result.json` conforms to
[`ui-verification-result.schema.json`](ui-verification-result.schema.json).

There is no global pixel tolerance. Zero changed pixels is a match. Any visual
or semantic difference in a completed batch is `review-required`; a difference
owned by a future batch is `planned-migration-debt`. The final rollout must run
with `--completed-through-batch 5`, at which point planned migration debt can no
longer exist. Masks or tolerances require a separate named, scoped, approved,
and expiring waiver implementation; this host does not silently apply either.

The field-test publisher builds only `FotoHavn.App` with
`UiVerificationBuild=false`, rejects verification builds, scans the output for
the catalog, host, or verification paths, and records
`uiVerificationAssetsExcluded: true` and
`uiVerificationAssemblyMarkersExcluded: true` in `field-test-build.json`.
