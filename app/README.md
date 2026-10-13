# FOTOHAVN desktop booth

The Windows app that runs a FOTOHAVN photo booth at an Event. An operator sets up and starts an Event on the Operator Screen; guests use the Guest Screen on a bound display to take Captures and get a Photo Strip.

## Stack

| Part | Technology |
| --- | --- |
| Brain | .NET 10 class library (`FotoHavn.Core`) that owns every rule and timer, with no Windows dependencies |
| Host | .NET 10 WPF with two windows (Operator Screen and Guest Screen), each with one full-screen WebView2 control |
| UI | React 19 + TypeScript + Vite, CSS Modules, Phosphor Light icons, self-hosted Cormorant Garamond and Manrope |
| Adapters | Windows `MediaCapture` webcam, SQLite plus files for Event storage, SkiaSharp compositor, wake lock |
| Tests | xUnit v3 unit and integration tests, Vitest and a Playwright fixture gallery for the UI, and a Playwright end-to-end framework that runs the real app with a real camera |

## Layout

- `src/`: the brain, protocol, catalog, storage, camera, and host projects
- `tests/`: .NET test projects
- `ui/`: the React app
- `e2e/`: the Playwright end-to-end framework
- `scripts/`: publishing and code generation
- `docs/`: ADRs, hardware research, the local development guide, and the implementation plan

## Read next

- Agent guide: [`AGENTS.md`](AGENTS.md)
- Domain language: [`CONTEXT.md`](CONTEXT.md)
- Design (interprets [`../brand/`](../brand/README.md)): [`DESIGN.md`](DESIGN.md)
- Decisions: [`docs/adr/`](docs/adr/)
- Building, running, and publishing: [`docs/local-development-guide.md`](docs/local-development-guide.md)
- Milestones and scope: [`docs/implementation-plan.md`](docs/implementation-plan.md)
