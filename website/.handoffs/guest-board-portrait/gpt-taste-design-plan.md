# Guest board: approved portrait keepsake viewer

## Authority and scope

Implement the **first displayed mock**, preserved locally as `website/.handoffs/guest-board-portrait/selected-option-1.png` (original: `C:/Users/QUINJ3875/.codex/generated_images/01a0a308-99fd-7842-bdcf-7992cb99beb7/exec-20b7fd9c-3798-409a-a4a3-311a4639d0cc.png`). This selection means the large clipped photograph with neighbor peeks, inline note action, arrows, counter, and landscape encouragement. It does not mean the earlier numbered text proposal that required rotation before browsing.

The mock was visually inspected alongside `GuestAlbum.tsx`, `GuestAlbum.module.css`, `GuestBoardPhoto.tsx`, `guestBoardData.ts`, `DESIGN.md`, `variables.css`, `theme.css`, `tokens.json`, and `website/AGENTS.md`. Preserve existing real photographs, original strips, editorial notes, desktop scatter arrangement, section copy, and page order. Scope production changes to the guest album and its state/tests. The working tree contains unrelated work.

## Responsive composition

- Activate the one-at-a-time experience only at **max-width: 767px and orientation: portrait**. Use the exact same media condition for rendering/state effects and CSS. Portrait tablets at 768px+ keep the horizontal board.
- Retain the centered eyebrow, `Look at you.` heading, and supporting sentence. On portrait phones use 24px text gutters; heading approximately 60–72px, supporting text 16px. Leave 32–40px before the metal viewing area. Do not repeat desktop toolbar instructions above it.
- Metal is a **stationary cropped horizontal surface** extending edge-to-edge, with its upper and lower silver rails visible. The crop can be portrait-shaped; the underlying board must be wider than the crop, with no side frame suggesting a newly built vertical board. Use the existing `metal-board.webp` material, crop/repeat its middle intentionally, and keep the rail unwarped. No sticky/fixed positioning relative to the screen.
- Center one active print in that crop. At 390px viewport, target roughly 300px wide by 390px high; at 320px, roughly 240px by 325px. Use width near 76vw capped around 420px, off-white print mat, 10–12px padding, one photographic shadow, a restrained 2–4 degree tilt, and the existing silver clip (about 36–42px wide). Allow vertical room above the print for the clip. Stage height should remain steady between photographs and note faces; approximately `clamp(370px, 112vw, 540px)` is an initial target, adjusted against the actual content.
- Show 16–28px of each neighboring print at the viewport edges. They are visual browsing cues, not duplicate interactive targets or accessible slides. Neighbor photographs remain understated without opacity that washes out the real assets. Keep the material still while changing only prints.
- Show complete photographs and original Photo Strips with `object-fit: contain`; preserve original strip proportions. Strips are narrow centered keepsakes within the same stable stage, not stretched portraits. Asset `guest-trio` (12/18) matches the mock's first visible selection and is a suitable initial portrait selection.
- Under the metal, place previous / note / next in a single centered row with 24px outer gutters, 12px gaps, 48px circular arrow targets and a flexible dark center button at least 48px tall. Use the selected mock's pill-shaped center button; the user explicitly approved this visual. At 320px, both `Read the note` and `See the photograph` must fit without clipping; center label may wrap naturally if needed.
- Counter is `12 / 18` style, calculated from actual selection and data length, 14px, centered with 12px top spacing. Put `Swipe to explore.` beneath at 16px.
- After 24–32px, center a small decorative rotate-phone symbol and the exact text **`Better in landscape.`** then **`Turn your phone to see the whole board.`** Use Soft Brown on Warm Ivory, 16px/1.5. These appear only when the portrait one-photo viewer is active. Keep 32–40px breathing room below. Do not duplicate the old board footer here.
- Every landscape viewport and desktop uses the whole horizontal scatter board, with board aspect ratio around 1.5 and existing placement anchors. Remove the current `aspect-ratio: .18` mobile rule and all mobile vertical scatter overrides. Small landscape controls may wrap; the board must never become vertical.

## Interaction and state

- Previous/next and horizontal swipe browse all 18 entries with wraparound. Each navigation returns to the photo face. Preserve vertical page scrolling: use `touch-action: pan-y` and require a horizontal-dominant gesture of approximately 40px before changing slides. Do not autoplay.
- `Read the note` flips the selected print **inline**, without opening a dialog, shifting the board, or altering section height. The reverse shows the existing title and note/testimonial logic, clear editorial credit, and optional small photo context. Body note text must remain readable at 320px (minimum 16px); allow a bounded note face to scroll for longer content. `See the photograph` restores the front. Use at most a 240ms transition; reduced motion changes sides immediately.
- Keep portrait selection in persistent React state independent from whether a modal photo is open. Opening a landscape/desktop photo updates this remembered selection; returning to portrait shows that photo. CSS orientation changes must not remount/reset the selection.
- Rotation alone does not open a modal. On portrait-to-landscape, show the full scatter board and retain the selected photo ID in state; on returning, restore that photo and its side when no new selection occurred.
- If a photo/fullscreen dialog was open before rotation, keep it usable until explicitly closed. Do not orphan a dialog or silently reset photo selection. On short landscape screens, allow the dialog layer to scroll and avoid the existing fixed 410px sheet minimum forcing close/navigation controls offscreen. If a former trigger becomes hidden after rotation, restore focus to the visible portrait control or appropriate visible board control instead. Close animations must not target zero-sized hidden trigger rectangles.

## Accessibility

- Use real buttons for arrows/flip, with previous/next accessible names and `aria-pressed` for note state. Keep only the visible face accessible; hidden face and neighbor peeks are inert/aria-hidden.
- The portrait viewer has a named region. The counter announces selected photograph and position politely after navigation, without reading every decorative neighbor. Keyboard arrows work when focus is in this viewer, not globally during page reading.
- Preserve 2px high-contrast focus outlines with 3px offset, at least 44px targets (48px intended), original image alt text, image-failure note access, and scrollable content under zoom. No orientation lock or mandatory rotation.

## Validation gates

1. Focused state/interaction checks cover wraparound, flip reset on navigation, retained selection across portrait/landscape changes, modal close/focus after rotation, and unchanged editorial-versus-testimonial rendering. Run `node --test --test-isolation=none scripts/guest-board.test.mjs` from `website/` (the parent's verified sandbox-compatible invocation), plus normal lint/type validation appropriate to edited files.
2. Fresh independent plan-conformance review checks this artifact against the implementation before responsive QA; repeat with a fresh reviewer after repairs.
3. Fresh browser QA at `/fotohavn#guest-album`: 320×568, 390×844, 430×932 portrait; 568×320 and 844×390 landscape; 820×1180 tablet portrait; 1440×900 desktop. Capture photo and note at 320px, normal portrait, landscape whole board, and desktop. Confirm no page horizontal overflow and no stretched/failed assets.
4. Exercise swipe and normal vertical scroll; first/last wraparound; strip and portrait selections; flip twice; orientation while on note; open desktop/landscape dialog then rotate both directions and close; keyboard focus; reduced motion. Verify encouragement is visible only in phone portrait and the full board remains horizontal everywhere it is shown.
5. Record verified passes separately from any skipped gates in the final handoff. No unrelated section redesign or additional generated production imagery.

