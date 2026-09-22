# FotoHAVN website

The public FotoHAVN brand site and browser-based online booth are implemented with Next.js. Route and link behavior comes from `site.config.ts`, `next.config.ts`, and `src/app/`.

## Development

```powershell
npm ci
npm run dev
```

Open `http://localhost:3000` and use the routes currently present in `src/app/`. Restart the development server after changing base-path configuration.

## Verification

Run focused scripts from `package.json` while iterating. Run the complete local gate before handoff:

```powershell
npm run check
```

Visual and interaction changes also require rendered-browser checks at the widths listed in `AGENTS.md`.

## Authority

- `CONTEXT.md` defines website, online-booth, inquiry, consent, and analytics language.
- `DESIGN.md` and its declared token files define the visual and copy direction.
- `docs/website-analytics.md` defines the fixed privacy-safe measurement contract.
- `.handoffs/`, `design-qa.md`, and generated designs are evidence or proposals rather than current authority.

## Production

The website is built as an immutable standalone container through the manually dispatched production workflow. Read the deployment ADR and `../deploy/fotohavn/AGENTS.md` before changing or running that path.

Follow [the Microsoft Clarity production runbook](docs/microsoft-clarity-production-runbook.md) when enabling or changing analytics.
