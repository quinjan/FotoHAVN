# Header brand target repair 03

Date: 2026-08-22  
Production scope: `website/src/components/SiteChrome.module.css` only  
result: passed

## Repair

The existing `.brand` anchor now uses `min-width: 44px`, `min-height: 44px`, `display: inline-flex`, and `align-items: center`. Its text, typography, destination, sticky/header architecture, 72px navigation height, grid placement, and authored focus rule are unchanged. No TSX or other production file was edited by this repair.

## In-app Browser evidence

Shared preview: `http://localhost:3011/`

| Viewport | Brand target | Bar / placement | Nearest menu geometry | Overflow |
|---|---:|---|---|---|
| 1440 x 1000 | 121.26 x 44 CSS px | 1280 x 72; target y=14..58 | desktop navigation; MENU hidden | `scrollWidth=clientWidth=1425` |
| 820 x 1180 | 121.26 x 44 CSS px | 708.80 x 72; target y=14..58 | MENU 64 x 44; 491.54px clear gap | `scrollWidth=clientWidth=805` |
| 390 x 844 | 121.26 x 44 CSS px | 327.20 x 72; target y=14..58 | MENU 64 x 44; 109.94px clear gap | `scrollWidth=clientWidth=375` |
| 320 x 720 | 121.26 x 44 CSS px | 256.80 x 72; target y=14..58 | MENU 64 x 44; 39.54px clear gap | `scrollWidth=clientWidth=305` |

At every viewport, computed brand styles reported `min-width: 44px`, `min-height: 44px`, flex alignment, and a rendered height of exactly 44 CSS px. The scrollbar gutter accounts for the 15px difference between requested viewport width and document client width; equality between document `scrollWidth` and `clientWidth` confirms no horizontal document overflow.

Keyboard focus placed the brand anchor in `document.activeElement` at 1440, 820, 390, and 320. The visible focus state remained a solid current-color outline; the authored `2px` outline and `3px` offset declarations were not changed.

MENU/CLOSE was exercised at 820, 390, and 320. In each case `aria-expanded` changed `false -> true -> false`, the controlled navigation changed hidden `true -> false -> true`, the dropdown began at y=72, and no overlap or horizontal overflow appeared. The final 820 browser-console read contained no warnings or errors.

## Static verification

- `npm run lint` — passed (exit 0).
- `npx tsc --noEmit` — passed (exit 0).
- `npm run build` — passed (exit 0; Next.js 16.3.2 production build, TypeScript, static page generation).
- `git diff --check -- website/src/components/SiteChrome.module.css` — passed; only the repository's existing LF/CRLF notice was emitted.

## Files

- Modified production file: `website/src/components/SiteChrome.module.css`
- Added handoff: `website/.handoffs/repair-header-brand-target-03.md`

## Fresh-verifier instructions

1. Inspect only the `.brand` delta and confirm no header label, destination, typography, focus selector, 72px height, breakpoint, or menu architecture changed.
2. In a fresh Browser session at 1440, 820, 390, and 320, measure `a[aria-label="FOTOHVN, back to the top"]`; require both dimensions to be at least 44 CSS px.
3. At 820, 390, and 320, open and close MENU, confirm `aria-expanded` and `hidden` synchronize, and verify the brand and menu targets do not overlap.
4. Tab to the brand at every viewport and confirm a visible focus outline.
5. Require `document.documentElement.scrollWidth === document.documentElement.clientWidth`, an error-free console, and passing lint, typecheck, and build.

This handoff reports only the bounded repair result. It does not claim that either independent orchestration gate has passed.
