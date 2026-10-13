# FotoHAVN

FotoHAVN contains the software and supporting material for an enclosed photography experience:

- `app/` is the self-contained desktop booth module: a .NET 10 brain and adapters (MediaCapture webcam, SQLite plus files for Event storage, SkiaSharp compositor) under a WPF host whose Operator and Guest windows each show one WebView2 running a React 19 + TypeScript UI, plus its unit, integration, and Playwright end-to-end tests, scripts, and docs.
- `website/` contains the public Next.js website and online booth.
- `brand/` contains the shared FOTOHAVN brand system: foundation, voice, color, type, photography, logo, motion, accessibility, medium guidance, DTCG tokens, master marks, and reference images.
- `app/DESIGN.md` interprets the brand system for the desktop booth.
- `deploy/fotohavn/` and the production workflow contain the immutable website deployment path.
- `marketing/reels/` contains independent Remotion projects for social video.

## Start here

- Agents: [`AGENTS.md`](AGENTS.md) and [`CONTEXT-MAP.md`](CONTEXT-MAP.md)
- Brand system: [`brand/README.md`](brand/README.md)
- Desktop module: [`app/README.md`](app/README.md)
- Desktop design (interprets the brand system): [`app/DESIGN.md`](app/DESIGN.md)
- Website development: [`website/README.md`](website/README.md)
- Website design (interprets the brand system): [`website/DESIGN.md`](website/DESIGN.md)
- Production bootstrap and recovery: [`deploy/fotohavn/docs/fotohavn-bootstrap-runbook.md`](deploy/fotohavn/docs/fotohavn-bootstrap-runbook.md)
