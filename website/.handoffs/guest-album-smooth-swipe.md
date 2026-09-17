# Guest album smooth swipe

## Decision — 2026-09-18

The user selected B: smooth swipe. The guest album now uses native touch momentum, centered scroll snapping, smooth previous/next buttons, and looping boundaries in mobile portrait mode. The existing note flip and the desktop/landscape board remain available. There is no autoplay or prototype switcher.

The full two-option prototype is archived at commit `c621e01` on `codex/archive-guest-album-motion-options`. The implementation stays in the requested worktree `C:/Quinjan/Repos/FotoHAVN-guest-album-motion`, branch `codex/prototype-guest-album-motion`. Main was not modified or merged.

## Implementation

`GuestAlbumCarousel.tsx` owns native scrolling and reports settled selection to the existing album reducer. Tripled photograph positions let the track recenter invisibly after crossing either boundary. Rapid button taps accumulate against the pending target. A scroll-idle fallback covers browsers without scrollend. Resize handling ignores hidden layouts and restores the selected photograph when returning to portrait. Reduced motion uses immediate button navigation; existing CSS disables the note flip animation.

The prototype implementation and artifacts have been removed from this branch's current tree; the archive retains them. No implementation issue was supplied.

## Verification

- TypeScript and scoped ESLint passed.
- Production build passed; all three routes prerendered successfully.
- All 11 guest-board tests passed. The server-render test mocks the extracted carousel while continuing to check the board's 18 named photograph buttons.
- Chromium at 390x844: next, note flip with correct content, and three rapid taps advancing three photographs.
- Simulated native touch swipe advanced to the next photograph and snapped into position.
- Rotated to 844x390 and back to 320x720: board visible in landscape and selected photograph preserved.
- 320px: forward wrap from the final photograph to the first; reduced-motion previous wrapped back; no page-wide horizontal overflow.
- Arrow-key navigation passed; desktop at 1440x900 retained the board and hid the mobile carousel. No prototype bar remained.
- Screenshots: `website/output/playwright/smooth-swipe-320.png` and `smooth-swipe-note-390.png`.
- Physical iOS/Android testing was not available; browser emulation does not establish device-specific performance.

Run from `website`: `npm run dev -- --port 3017`. Preview: http://localhost:3017/#guest-album (mobile portrait).

## Tactile details — 2026-09-18

User requested varied note orientations, restored clips, and keepsake assets on some notes. Added identity-seeded angles from -4.5 to +4.5 degrees, so repeated loop copies and revisited notes never jump to a new tilt. Every print has the existing silver clip. Six photographs carry existing teddy, bunny, ribbon, or ticket accents; decorations stay outside the turning sheet, moving with the carousel without blocking input or entering the accessibility tree.

Checked 320px teddy/ticket notes and 390px bunny notes, corrected corner/label overlap, and verified no horizontal page overflow. The 18 photographs have 11 distinct tilts; clip and accent counts are consistent across the three loop copies. TypeScript, scoped ESLint, and diff checks passed. Browser console reported no errors or warnings. This visual-only follow-up did not rerun the production build or unchanged unit suite.
