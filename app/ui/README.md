# FOTOHAVN UI

React 19 + TypeScript + Vite app rendered by the WPF host's WebView2 (`https://app.fotohavn/index.html`) or served by the headless host. It renders the presentation the brain sends for its role and sends commands; it holds no rules.

## Brain connection

`src/brain/` holds the `BrainClient` (`types.ts`), one protocol implementation (`protocolClient.ts`: handshake, per-role revision ordering, command and test-hook correlation by UUID with timeouts), and three transports chosen once at startup (`createBrainClient.ts`):

| Transport | When | Notes |
| --- | --- | --- |
| WebView2 | `window.chrome.webview` exists | Posts objects; the host's messages arrive decoded |
| WebSocket | the page has `?token=<role token>` | `/bridge?token=` on the page's host, or `?brain=ws://…/bridge` for `npm run dev` against a headless host; reconnects with backoff and sends `hello` again on every new socket |
| Fixture | development and E2E builds, `?fixture=<name>` | A stand-in brain with one snapshot from the fixture lane (`src/fixtures/fixtureLane.ts`): the brain-generated messages in `src/fixtures/presentations/` by file name (a `presentation` is served as the `welcome` at its revision; `protocol-mismatch` answers every `hello` with the mismatch), plus the hand-written `operator-event-list` with rows until the brain can generate them (M4). Rejects every command with `notAvailable` and every hook with `hooksUnavailable`. `main.tsx` loads the lane with a dynamic import behind a build-time guard, so field builds (`dist/`) contain no fixture code or data |

Without any of these the page shows "Not connected." E2E builds also honor `?e2eProtocolVersion=<n>` (sent in `hello`) and expose `window.fotohavnE2E` (`getState`, `sendCommand`, `sendTest`). The root element carries `data-fotohavn-status` and, after each accepted snapshot, `data-fotohavn-role` and `data-fotohavn-revision`.

## Prerequisites

- Node 24.19.0 or later.
- Chromium for the fixture lane, once per machine: `npx playwright install chromium`.

## Commands

Run from `app/ui/`:

| Command | Does |
| --- | --- |
| `npm ci` | Installs the pinned dependencies from `package-lock.json` |
| `npm run dev` | Vite dev server |
| `npm run lint` | ESLint, zero warnings allowed |
| `npm run typecheck` | `tsc -b` over the app and tooling configs |
| `npm test` | Vitest unit tests |
| `npm run test:coverage` | Unit tests with v8 coverage to `coverage/`; fails below 80% lines, branches, functions, or statements |
| `npm run build` | Type-checks and builds the field UI to `dist/` |
| `npm run build:e2e` | Same, in Vite mode `e2e` (`.env.e2e` sets `VITE_E2E=true`), to `dist-e2e/` with an `e2e-build.txt` marker, for E2E builds of the host. It never writes `dist/`, so field builds can't copy it; the host's E2E build copies `dist-e2e/`, and the publish guard refuses any UI with the marker |
| `npm run test:fixtures` | Two Chromium projects against the E2E bundle, which it builds to `dist-e2e/` and serves with `vite preview`. `fixtures`: every fixture-lane surface (operator Event list empty and with rows, guest Start, both fatal surfaces, protocol mismatch, not connected) at 1280×720, 1366×768, 1920×1080, and 2560×1440 (visible content, no scrolling, no axe violations). `headless`: the integration lane, which starts the real headless host (`dotnet run --project ../src/FotoHavn.Host.Headless --no-build -- --ui dist-e2e --data ../output/ui-headless/…`, so build .NET first) and drives one operator and one guest page over its WebSocket bridge: both handshake with their token's role, each receives only its role's presentation, a guest `OpenNewEvent` is rejected `wrongRole`, and an operator one `notAvailable`. Screenshots go to `../output/fixtures/`; the report, JUnit file, and traces go to `../output/ui-fixtures/` |
| `npm run generate:protocol` | Regenerates `src/protocol/generated.ts` (do not edit it) from `../src/FotoHavn.Protocol/schema/protocol.schema.json`. Normally run through `../scripts/generate-protocol.ps1`, which exports the schema first; `scripts/protocol-types.test.ts` fails when the file is stale |

The fixture gallery route arrives in M3. Specs live in `tests/fixtures/` and `tests/headless/`. The files in `src/fixtures/presentations/` are written by `FotoHavn.Core` tests; regenerate them from `app/` with `$env:FOTOHAVN_UPDATE_FIXTURES='1'; dotnet test --project tests/FotoHavn.Core.Tests/FotoHavn.Core.Tests.csproj`, never by hand. `src/fixtures/inlineFixtures.ts` holds typed snapshots for unit tests.
