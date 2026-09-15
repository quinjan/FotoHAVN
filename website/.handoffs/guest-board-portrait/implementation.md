# Portrait guest board implementation

## Implemented scope

The approved first mock is implemented in the existing guest album. Production edits are limited to:

- `src/components/GuestAlbum.tsx`: a named portrait keepsake region with one photograph, decorative neighboring photographs, inline note flip, previous/next buttons, scoped keyboard arrows, pointer swipe, counter, and portrait landscape encouragement. Uses the supplied material, photographs, and clip plus the installed Phosphor `DeviceRotateIcon`.
- `src/components/GuestAlbum.module.css`: the phone portrait composition at `(max-width: 767px) and (orientation: portrait)`, pill note action, rail-to-rail crop of a wider stationary metal surface, steady print stage, contained images, narrow original strips, scrollable note, and reduced-motion handling. Removed the vertical mobile scatter layout; all full-board views retain the horizontal 1.5 aspect ratio and original anchors. Short landscape dialogs can scroll to their controls with a 360px sheet and no forced 410px minimum.
- `src/components/guestBoardData.ts`: remembered `selectedIndex` independent of modal `index`, initialized to `guest-trio` (12/18), portrait navigation and swipe classification, selection synchronization during modal navigation, and retained keepsake/side after close.
- `scripts/guest-board.test.mjs`: updated fullscreen-close expectations and added portrait wraparound, note reset, cross-view selection, and horizontal-versus-vertical gesture checks.

The portrait and full-board DOM remain mounted. CSS alone switches their visibility, using one media condition; no orientation effect remounts or resets state. Rotation therefore does not open or dismiss a dialog. Focus return selects a visible connected trigger, fullscreen close button, portrait note control, or board fullscreen button. A hidden zero-sized trigger is excluded from close animation geometry.

Existing source and uncommitted work were preserved. No additional production imagery was generated and no unrelated sections were changed.

## Verified checks

From `website/`:

- `node --test --test-isolation=none scripts/guest-board.test.mjs`: **11/11 passed**.
- `npx eslint src/components/GuestAlbum.tsx src/components/guestBoardData.ts scripts/guest-board.test.mjs`: **passed**, no lint findings.
- `npx tsc --noEmit`: **passed**.
- Inspected the selected mock, existing component/data/motion/image code, repository `website/AGENTS.md`, local Next.js `use-client.md`, and local design guidance before implementation.

The npm invocations emitted only the existing unknown user config `email` notice.

## Independent verification still required

No browser interaction or screenshots were performed by this implementation agent. Fresh conformance must precede responsive QA. The parent coordinates those gates and the full build.

Browser verification must establish actual rendered photo and note fit (especially the 320px pill label), portrait-only encouragement, horizontal full-board composition, orientation preservation, pointer swipe versus normal page/note scroll, keyboard operation, image failures, reduced motion, and accessible focus after closing dialogs that span an orientation change. State tests establish reducer behavior; they do not substitute for rendered rotation/focus or native touch testing.
