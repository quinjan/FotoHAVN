# FOTOHAVN UI

React 19 + TypeScript + Vite app rendered by the WPF host's WebView2 (`https://app.fotohavn/index.html`). M1 ships a placeholder page that posts `{ type: "ready" }` to the host after mount.

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
| `npm run test:fixtures` | Fixture lane: serves `dist/` with `vite preview` and checks it in Chromium at 1280×720, 1366×768, 1920×1080, and 2560×1440 (visible content, no scrolling, no axe violations). Run a build first. Screenshots go to `../output/fixtures/`; the report, JUnit file, and traces go to `../output/ui-fixtures/` |

The fixture gallery route replaces the placeholder test in M3; its specs live in `tests/fixtures/`.
