# Portrait inline booth UI implementation

## Ready for parent browser verification

Implemented the approved option 1 composition in `src/components/BoothExplorer.tsx` and `src/components/BoothExplorer.module.css` only. Camera and scene tests remain parent-owned.

- Narrow portrait query: `(max-width: 767px) and (orientation: portrait)`.
- Compact heading (40–52px) and “Come a little closer.” copy.
- Full container-relative width studio with a 480px canvas; fallback photograph receives the same height. Removed inset border and stage-label overlay in portrait. Background uses the existing off-white page surface.
- Normal-flow swipe hint followed by four 48px controls in left / minus / plus / right order, using installed Phosphor icons and existing scene commands. Portrait keyboard traversal follows that order; wider visual order is retained through CSS.
- Labeled native 16px select includes all six views and disabled “Custom view” placeholder. Selection shares state and Inside/Bench auto-open behavior with existing buttons.
- Side-by-side select and Ebony curtain button, each 132px wide at a 320px viewport (24px gutters and 8px gap), with 4px horizontal padding. Open/Close curtain labels use 16px text. Verify actual font and native select chrome in browser.
- Reset, live status, description, retry, and disclaimer preserved. Inactive control alternatives use `display: none`; no duplicate IDs.

## Validation

- PASS focused ESLint: `node node_modules/eslint/bin/eslint.js src/components/BoothExplorer.tsx`.
- PASS TypeScript: `node node_modules/typescript/bin/tsc --noEmit --incremental false`.
- Read root and website AGENTS.md plus installed Next.js `node_modules/next/dist/docs/01-app/03-api-reference/01-directives/use-client.md` before edits.
- Visually inspected selected `option-1-inline.png` and read the approved local plan and DESIGN.md.
- Browser screenshots, 320px native text measurement, fallback, touch scrolling, orientation, independent conformance, and fresh visual QA remain parent-coordinated gates. No browser session started here.
- No commits or deployments. Unrelated working-tree changes preserved.
