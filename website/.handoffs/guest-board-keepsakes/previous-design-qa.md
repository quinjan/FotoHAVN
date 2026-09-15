# Guest board collage — QA

final result: passed

The user requested a natural, busy, full board with irregular placement and overlapping photographs. This supersedes the deliberately orderly arrangement in the earlier option-2 implementation. Previous QA is archived at `.handoffs/guest-board-collage/previous-design-qa.md`.

## Changes

- Replaced the five/four/two-column layout with stable, irregular composition anchors keyed to the ten photograph IDs.
- Increased print sizes, varied angles from -13 to +13 degrees, and layered strips across larger guest photographs. Small paper corners may extend across the inner frame, like a physical board.
- Desktop and tablet share a wide collage; phones use a tall, staggered collage with natural scrolling.
- Hover and keyboard focus raise the entire print above neighbouring prints. Transparent parts of each positioning wrapper do not intercept another photograph's click.
- Preserved the existing photographs, full strip assets, editorial notes, flip/lift/return and full-window board interaction. Updated responsive image sizes for larger prints.
- The arrangement stays stable during browsing and reopening, so a print returns to a predictable place. It is an art-directed scattered arrangement, not a fresh random draw on every render.

## Evidence

Evidence directory: `.handoffs/guest-board-collage/`.

- `desktop-fullscreen.png`: 1440x1000 browser viewport, density 1. Dense silver-framed collage with actual overlaps and varied scale. All ten photographs opened their matching IDs through exposed click areas.
- `tablet-fullscreen.png`: 820x1180. Full collage fits across the viewport; no horizontal overflow.
- `mobile-0.png`, `mobile-1.png`, `mobile-2.png`: 320x720, successive positions through the tall full-window board. Every one of the ten prints had a clear 44x44 target verified with browser hit testing at the centre and four corners.
- Phone friends photograph opens and flips to its editorial note. Return restores focus to the friends print while fullscreen remains open.
- Previous/next, reduced motion and the existing state reducer remain unchanged. Existing board tests passed 8/8.
- Production build including TypeScript passed; focused ESLint on both edited TSX/TS files passed; git diff whitespace check passed.

Actual clicked IDs: sepia-strip, friends, classic-strip, couple, monochrome-strip, family, smiles, naturale-strip, weekend, keepsakes. Rapid automation needed to wait for the return animation before the next interaction; settled interactions and keyboard return were verified.

## Visual assessment and limits

Existing Cormorant/Manrope type, warm palette and silver/grey board material retained. Print captions remain legible when raised; overlap is intentional and the complete photograph is available in the viewer. All photos continue to use real source files with no invented guest testimonials. The existing small opaque clip backing is unchanged.

This pass used the existing-project redesign skill's audit and targeted layout improvements. No new asset generation or independent subagent review. Browser checks used viewport emulation, not a physical touch device. The production bundle was built; a separate production browser server was not started for this CSS-focused pass. Existing broader hero/3D work is outside this change.
