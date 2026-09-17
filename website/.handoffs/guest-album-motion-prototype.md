# Guest album mobile motion — throwaway comparison

Question: does a continuous ribbon or direct swipe with smooth button navigation make the guest album feel more natural?

Branch: `codex/prototype-guest-album-motion`.
Worktree: `C:/Quinjan/Repos/FotoHAVN-guest-album-motion`.
Based on main commit `823d78f`; unrelated uncommitted main-worktree edits were left in place.

From `website`, run `npm ci` once, then `npm run prototype:album`.

- A: http://localhost:3017/?variant=A#guest-album — continuous 32 px/s ribbon, reversible direction, manual pause. Touch interaction and reading a note pause it until Play is pressed. Offscreen/hidden-tab movement stops; reduced motion disables autoplay.
- B: http://localhost:3017/?variant=B#guest-album — native touch scrolling and momentum, centered scroll snapping, smooth previous/next buttons, looping ends.

Use a portrait viewport below 768px. Both share the real homepage, imagery, and copy. A floating bottom switcher changes the URL; arrow keys outside the album also switch variants. Arrow keys within the album navigate photos. The ordinary route has no prototype. The prototype and switcher are gated out of production.

Notes open below the photographs instead of using the original flip animation, so the horizontal-motion comparison stays clear. This is an intentional prototype simplification for review, not an approved replacement of the existing note interaction.

## Browser checks — 2026-09-17

- Examined screenshots at 320×720 and 390×844; no page-wide horizontal overflow. Corrected two-line strip captions at 320px.
- B: button advance, previous from 1 to 18, next from 18 to 1, and note content verified. Chromium CDP touch swipe advanced 1 to 2 and settled at the next snap position.
- A: observed 36px movement over approximately 1.1s; pause produced zero movement. Reversing produced -17px over approximately 0.5s; reduced motion produced zero movement.
- 1440×900: prototype hidden, original desktop board visible.
- TypeScript passed after `next typegen`; scoped ESLint and `git diff --check` passed. No test suite added or production build run for this throwaway prototype.
- Initial browser run had four Next Image positioning warnings for hidden desktop prototype images; moved their positioning rule outside the mobile media query. Final reload reported zero errors and zero warnings.
- Browser emulation only; physical iOS/Android feel still needs user review.

Screenshots: `website/output/playwright/album-*.png`.

## Decision

Pending user comparison. No winner selected, no production motion change approved, no implementation issue supplied. Keep this branch as the prototype source and link it from the implementation issue when one is selected. The smoother swipe is the more direct candidate for deliberate browsing; the ribbon is available to judge ambient movement.
