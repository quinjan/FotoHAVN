# On the Other Side — guest board design QA

Date: 2026-09-10. Scope: selected option 2 for Look at you, every posted print clickable, published 2x6 strips, animated lift and fullscreen board. The user approved editorial captions for now. Previous root report is archived at `.handoffs/guest-board-implementation/previous-design-qa.md`.

final result: passed

## Findings and repair history

No actionable P0/P1/P2 finding remains in the checked states. Main agent performed the checks; no independent design/code review is claimed.

1. **[P2, repaired] Oversized desktop paper and competing return control.** Early selected-note view crowded the heading and concealed too much board. Evidence: `desktop-note-v2.png` and paired `comparison-initial.png`. Reduced viewer width 440px to 400px and maximum paper height 570px to 500px; moved Return below paper/actions; tightened album/toolbar spacing. Post-fix: `desktop-note-final.png`, `comparison-final.png`, `comparison-note-detail.png`. Heading and full board are visible at 1440x1200 with a readable paper hierarchy.
2. **[P2, repaired] Modal layout movement.** Scrollbar removal could change underlying page width. Added `scrollbar-gutter: stable` to root. A settled desktop native-coordinate click held scrollY 3580 and section top 88.42px before/after opening. Earlier movement also involved smooth anchor scrolling or locator auto-scroll, so those observations alone were not treated as proof of the CSS cause. Final desktop evidence preserves the board position.
3. **[P2, repaired] Fullscreen return could lose keyboard focus.** Initial animation completion scheduled focus before React reliably removed board inertness, leaving BODY focused. Restoration now occurs in the layout effect after selected index becomes null. Failure reproduced; repair verified in development and final production: fullscreen -> Tab -> Enter on sepia-strip -> Escape restores that exact strip; Enter reopens it; Escape from the board returns to View board fullscreen. This repair does not change appearance.
4. **[P2, repaired] Ineligible native fullscreen enhancement.** Final API review established that a dialog cannot be a native fullscreen target ([MDN](https://developer.mozilla.org/en-US/docs/Web/API/Element/requestFullscreen)). Removed the silently rejected request and associated unreachable cleanup. Fullscreen remains the already verified edge-to-edge, scrollable board dialog, preserving browser controls. This does not change the tested visual state or interaction; OS-level fullscreen takeover is not claimed.
5. **[P3, accepted follow-up] Tiny opaque clip backing.** Two genuine-alpha requests returned painted checkerboards; neither is shipped. A grey-backed raster fallback is used only on grey board and stays behind during lift. Its tile has slight tonal variation under magnification. A verified true-alpha replacement can improve blending; genuine alpha is not claimed.

## Comparison truth and normalization

All evidence below is in `.handoffs/guest-board-implementation/` unless otherwise stated.

- Source: `approved-design.png`, 1536x1024 raster, displayed option 2. The bottom workflow rail is annotation, not website UI. Physical source: `../guest-board-ideation/metal-board-reference.png`.
- Implementation: http://localhost:3000/FOTOHAVN#guest-album, existing Next app. Separate final production smoke used port 3002.
- Matched state: warm theme, friends photograph opened, reverse note shown. Mock sample quote is deliberately replaced by user-approved editorial text and actual guest photograph.
- Early capture `desktop-note-v2.png`: 1440x1000 pixels/CSS viewport, density 1. Final `desktop-note-final.png`: 1440x1200 pixels/CSS viewport, density 1. Taller final viewport includes the complete real ten-print board; not a pixel-identical whole-frame match.
- Full-view paired inputs `comparison-initial.png` and `comparison-final.png`: 1540x640. Source crop 1536x900 excludes workflow rail; render crops begin y88, heights 912 early / 1061 final. Each is aspect-preserving contained in 760x640 with letterboxing; source LEFT, render RIGHT. Main agent opened and judged the actual paired inputs.
- Focused paired `comparison-note-detail.png`: 860x560. Source paper x525/y250/435x555; render paper x513/y332/400x501. Both aspect-preserving contained in 420x560. This permits legible typography, image and content-policy assessment.
- `scripts/compare-guest-board.mjs` reproduces the comparisons. Final production screenshots corroborate packaging and the focus repair, not a substitute for visual comparison.
- Post-repair production was also paired in `comparison-production.png` using the same final crop, viewport and note state. Main agent opened this combined input and found no new actionable P0/P1/P2 visual drift. Production section top remained 88.42px and scrollY 3580 during the native-coordinate opening.

## Required fidelity surfaces

**Fonts and typography.** Computed heading family is Cormorant Garamond with existing serif fallbacks; supporting text is Manrope with existing sans fallbacks. Heading retains 400 weight, line-height 1, tight tracking and italic you. Serif note text and sans controls preserve the editorial hierarchy. Phone text wraps without truncation; the flip action can occupy two lines at 320px while retaining a generous target. Native font antialiasing/generated glyphs are not pixel-equivalent to the concept.

**Spacing and layout rhythm.** Silver-framed grey board remains dominant with a centered lifted paper. Five desktop columns, four tablet columns and two readable phone columns replace a miniature scaled canvas. Varied strip/portrait widths and mild rotations retain clipped-print character. Arrangement is more orderly than the concept's miniature overlaps to preserve independent hit areas. Return/actions sit outside the turning paper. Canonical 4px button radii intentionally replace incidental concept pills. Raster nine-slicing preserves frame corners.

**Colors and tokens.** Existing warm ivory, off-white paper, ebony, soft brown and dark walnut variables retained. Grey brushed metal/silver follow the source without introducing a new palette. Warm translucent modal washes, visible focus outlines and existing photo elevation. No new gradients or generic decorative cards. Contrast visually inspected; no automated WCAG contrast audit claimed.

**Image quality and assets.** Six real Evia photographs remain unchanged. Four published full strips retain near-2x6 ratios and all frames; object-contain on board and front. Reverse portrait thumbnails intentionally crop; reverse strips stay contained. Responsive candidates load and dimension checks pass. Empty board/clip are generated rasters from the selected visual, not CSS/SVG artwork; generated people do not replace guests. Source photograph resolution remains a natural close-up limit. Clip backing limitation is disclosed above.

**Copy/content.** Each print has a unique title, meaningful alt text and honest editorial note. Reverse explicitly credits An editorial note from FOTOHAVN. No invented guest names, quotations or ratings. Optional testimonial data stays empty. Fake More photographs omitted because all ten records are displayed. Added fullscreen instructions/actions describe working functionality. Phosphor icons provide consistent fullscreen, close, turn and arrows; every posted print is a named native button.

## Browser evidence and interactions

| CSS viewport / state | Observed result | Evidence |
| --- | --- | --- |
| 1440x1000 desktop rest/photo/early note | Ten loaded prints; complete strips; zero horizontal overflow; early sizing drove repair. | desktop-board-v1.png, desktop-photo-v1.png, desktop-note-v2.png |
| 1440x1200 final desktop note | Distinct heading, complete board and readable central paper. | desktop-board-final.png, desktop-note-final.png, paired final/detail comparisons |
| Desktop fullscreen | Every one of ten buttons individually opened its matching ID and returned. | desktop-fullscreen.png and live ID readbacks |
| 320x720 phone | Two-column board, no horizontal overflow, readable photo/note/actions; natural board scrolling; last print reachable. | mobile-320-board.png, mobile-320-photo.png, mobile-320-note.png, mobile-320-fullscreen.png |
| 820x1180 tablet | Four columns, no horizontal overflow; entire sepia strip and reachable controls. | tablet-820-board.png, tablet-strip-viewer.png |
| Final production 320x720, density 1 | Ten loaded prints; resting client/scroll widths 305px in 320px viewport. Keyboard-opened sepia note; Escape restored exact print, then fullscreen exit restored launch button and root overflow. | production-mobile-note.png and live focus readbacks |
| Final production 1440x1200, density 1 | Computed Cormorant/Manrope; all images loaded; friends editorial note rendered. | production-desktop-note.png |

All fullscreen selected IDs matched: sepia-strip, friends, classic-strip, couple, monochrome-strip, family, smiles, naturale-strip, weekend, keepsakes. Photo/note/previous/next/return operate; nested Escape closes photo first, then board. Outside content is modal/inert; scroll lock releases. Final production and development console checks returned no warnings/errors. Fullscreen is an edge-to-edge browser-viewport dialog, not native OS fullscreen.

## Automated checks and limits

- Board 8/8, hero 12/12, booth 4/4: **24/24 passed** after the focus repair. Board fixtures cover every record/index, invalid selection, flip/navigation wrap, fullscreen retention, finite lift geometry, reduced-motion skip/cancellation, responsive strip dimensions and actual server-rendered named buttons.
- Full lint, standalone TypeScript and final production build passed. `git diff --check` passed; existing Windows line-ending advisories are not application errors.
- Physical touchscreen, mobile browser chrome resizing, screen reader, text zoom and live OS reduced-motion preference were not exercised. Reduced motion is source/fixture verified.
- Full-window board view is live verified. Browser/OS-level native fullscreen is not used or claimed.
- After removing the rejected native API call, board tests and production build passed again, and focused lint passed. Final 1280x720 production smoke verified the modal's 1265px content width (15px stable scrollbar gutter), full 720px height, keyboard-opened sepia print, exact focus restoration, fullscreen exit/scroll cleanup and an empty warning/error log. There was no visual change to the already paired final note state.
- Image failure fallback is implemented/source reviewed, not newly network-failure injected. No-JS modal interaction is not claimed.
- Hero/booth tests are regressions, not new full visual approval of those sections. No independent reviewer pass claimed.
- No form submission, external message, commit, push, deployment or dependency installation. Existing dirty work and the user's port-3000 server remain preserved.

## Implementation checklist

- [x] Selected option 2 and actual booth-board reference honored.
- [x] Ten clickable photographs including four complete published strips.
- [x] Lift, turn, previous/next, return and fullscreen operational.
- [x] Editorial-only content and honest attribution.
- [x] Desktop/tablet/320px checks and paired full/detail comparisons.
- [x] Keyboard regression repaired and verified in production.
- [x] Tests, lint, types and build passed; evidence limits recorded.
- [ ] Optional P3: replace grey-backed clip with a verified true-alpha raster.
