<!-- BEGIN:nextjs-agent-rules -->

# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->

# FotoHAVN website work

Use these project rules with the managed Next.js block above.

## Load

- Read `../CONTEXT.md` and `CONTEXT.md` before changing website behavior or product language.
- Read `DESIGN.md` before changing visuals, layout, motion, imagery, icons, or customer-facing copy.
- Read `docs/website-analytics.md` before changing analytics, consent, inquiry tracking, or privacy behavior.
- Read `../deploy/fotohavn/docs/adr/0002-deploy-website-as-an-immutable-container.md` and `../deploy/fotohavn/AGENTS.md` for container or production work.

## Sources

- Resolve routes, base paths, external links, and section anchors from `site.config.ts`, `next.config.ts`, and the app tree.
- Resolve commands from `package.json`; use focused scripts while iterating and `npm run check` for the complete local gate.
- Treat `DESIGN.md` and its declared token files as visual authority. Treat `.handoffs/`, `design-qa.md`, generated concepts, and screenshots as evidence or proposals.

## Verification

- Run `npm run check` before declaring the website scope complete.
- For visual or interaction changes, inspect the rendered site at desktop, tablet, 390-pixel, and 320-pixel widths; exercise affected interactions and check overflow and browser diagnostics.
- For online-camera changes, separate synthetic-browser coverage from physical phone, lens-switching, permission, and touch validation.
- For analytics changes, complete the browser acceptance checklist in `docs/website-analytics.md` and preserve full masking of customer-created media.

Completion requires command results, rendered-browser evidence for affected UI, and explicit device or production limits.
