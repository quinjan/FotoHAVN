# Responsive stack typography repair 05

Date: 2026-08-22  
result: blocked

## Bounded production change

- Modified only `website/src/components/MiddleExperience.module.css`.
- Added a 768–1023px `.stackCopy h3` rule: `max-width: 100%`, `font-size: clamp(2.2rem, 4.6vw, 2.75rem)`, and `line-height: 0.96`.
- Added a max-767 `.stackHeader h2` override: `font-size: clamp(2.35rem, 11.5vw, 3.5rem)`.
- Removed `.stackHeader h2` from the max-360 shared `2.55rem` override so the dedicated mobile clamp remains authoritative at 320px.
- No copy, grid, gutter, padding, image, component, GSAP, transform, overflow, or visibility rule changed. No nowrap, transform scaling, clipping, or discretionary copy break was introduced.
- The passing mobile card-H3 rule remains unchanged at `clamp(1.95rem, 9.75vw, 2.75rem)` / `line-height: 0.98`. The 1024-and-up base stack typography and GSAP branch are unchanged.

## Computed source sizing

| Viewport | Target selector | New computed font size |
|---:|---|---:|
| 320 | mobile chapter `.stackHeader h2` | 37.6px |
| 375 | mobile chapter `.stackHeader h2` | 43.125px |
| 390 | mobile chapter `.stackHeader h2` | 44.85px |
| 768 | tablet card `.stackCopy h3` | 35.328px |
| 820 | tablet card `.stackCopy h3` | 37.72px |
| 1023 | tablet card `.stackCopy h3` | 44px cap |
| 1024+ | card headings | unchanged base rule |

Using the pass-3 text-range measurements as a proportional forecast, not as fresh rendered evidence, the 768 `KEEP THE PHOTOGRAPH` right edge should move from 386.46px to about 329.2px before media at 331.66px, and the 820 edge to about 345px before media at 353.33px. The 320 chapter heading's longest-line edge should move inside the recorded 280.80px content boundary. Fresh Browser measurement remains required for acceptance.

## Checks and blocker

- `git diff --check -- website/src/components/MiddleExperience.module.css` passed; output contained only Git's LF-to-CRLF working-copy warning.
- `npx eslint src/components/MiddleExperience.tsx` passed with exit 0; the only output was the pre-existing npm user-config warning for `email`.
- Integrated lint, TypeScript, and production build are intentionally pending for the orchestrator after all work settles.
- The requested in-app Browser could not be selected (`Browser is not available: iab`), so fresh rendered measurements at 320/375/390/768/820/1024 are pending. No substitute browser surface was used.

## Fresh-verifier instructions

1. Open `http://localhost:3011/` in a fresh in-app Browser session after fonts load.
2. At 768 and 820, measure every `.stackCopy h3` text-range rectangle plus `.stackCopy` and `.stackMedia`. Require every visible glyph/right edge to remain before `stackMedia.left`, with complete copy and no image paint-over.
3. At 320, 375, and 390, measure every line of `.stackHeader h2`. Require left/right edges to remain inside the 24px content boundaries, with no clipping, overlap, transform scaling, or descendant overflow.
4. At 320/375/390, reconfirm the separate `.stackCopy h3` repair remains in-bounds and cards/media remain vertical, static, and gap-separated.
5. At 1024, confirm the tablet rule is inactive and GSAP card typography/geometry remains unchanged. At 1280/1440, confirm desktop stack headings retain their prior size, copy/media separation, and motion behavior.
6. Verify `documentElement.scrollWidth === clientWidth` plus descendant text/card/media bounds at all six requested widths.
7. Run integrated lint, TypeScript, and build checks, then rerun both independent gates. This repair does not claim either gate passes.

result: blocked
