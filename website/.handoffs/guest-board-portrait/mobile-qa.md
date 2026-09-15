# Fresh mobile browser QA

Result: **one P2 requires repair**; core browsing and rotation checks passed. This is the pre-narrow-control-repair evidence, captured on 2026-09-15.

## Finding

**P2 — 320px flip changes section height.** At 320 x 568, the center button changes from 137 x 48px (`Read the note`) to 137 x 61.1875px (`See the photograph`). The label remains readable but wraps. Album height changes from 948.765625px to 961.953125px. The stage remains exactly 390px tall. Reserve enough row/button height for both labels; repeat fresh conformance and narrow QA after repair. Parent notified immediately.

## Verified interactions

- At 320px, initial selection is guest-trio, 12/18. Flip to inline note and back worked, without a dialog; next click moved to guest-solo, 13/18. Note body is 16px, its bounded face scrolls, and both labels fit horizontally. Arrow targets are 48 x 48px with 24px outside gutters and 12px gaps.
- At 390 x 844, actual CUA pointer drag from (288,235) to (96,238) advanced 13 -> 14. CUA vertical scrolling moved the stage by 127px and retained photo14. A predominantly vertical pointer drag did not advance. These are desktop pointer/wheel browser tests, **not physical touchscreen validation**.
- Focused ArrowRight changed note14 to photo15. Repeated focused keys reached18 then1; ArrowLeft from1 returned18. Right while focus was outside the viewer left selection1 unchanged.
- Selection14 and its note side survived 390 -> 844 -> 390 viewport rotation. Rotation alone did not open a dialog.
- At844 x390, opening `Room for three.` selected12; its dialog survived rotation to390 x844. Explicit close returned to photo12 and focused the visible portrait `Read the note` button. Tab then Shift+Tab retained an inspectable visible keyboard focus state.
- At568 x320, opened the original sepia strip, scrolled the modal, and flipped to its note. The note survived568 ->320 ->568. Explicit close completed (confirmed by subsequent tablet accessibility snapshot with no modal). Immediate close metrics were captured during animation; they are not evidence of final landscape focus.
- At390px the strip uses a narrow centered sheet, with loaded source image and `object-fit: contain`. Screenshot shows the four-frame strip; the sticky header overlaps the upper screenshot edge due to scroll position, so it is not a complete isolated-strip capture.
- Photo/note faces alternate in the accessibility tree. Neighbors do not become extra navigation targets. No autoplay observed.

## Responsive geometry

| Viewport | Document client/scroll width | Board or portrait stage |
|---|---:|---|
|320 x568|305 /305|390px stage; tilted print about248 x337px|
|390 x844|375 /375|431.25px stage; tilted print about304 x389px|
|430 x932|415 /415|Portrait view, controls and encouragement present|
|568 x320|553 /553|Horizontal full board517 x351.33px|
|844 x390|829 /829|Horizontal full board746.13 x506.75px|
|820 x1180|805 /805|Horizontal full board724.5 x492.33px|
|1440 x900|1425 /1425|Horizontal full board1282.5 x864.33px|

The15px difference is the in-app browser scrollbar. No page horizontal overflow occurred. Portrait encouragement was in the accessibility tree only in phone portrait; landscape/tablet/desktop used the full board. The JSON's `hint: block` is the descendant's computed display, **not effective visibility**: its portrait-viewer ancestor is hidden in full-board layouts.

The landscape full board does **not** fit vertically in one screen. It remains horizontal and can be explored with section scrolling. At568px, modal content height541px exceeds the320px viewport; its layer has `overflow:auto`. Actual scrollTop changed0 ->221, bringing navigation to y173–221 and the44px close button to y229–273. Controls were usable. The844px modal likewise required scrolling. No orientation lock was present.

## Evidence and limits

- Detailed geometry, state sequence, image loading information and logs: `mobile-qa-evidence.json`.
- Screenshots: `qa-320-photo.png`, `qa-320-note.png`, `qa-320-note-stage.png`, `qa-390-full-strip.png`, `qa-390-rotation-close-focus.png`, `qa-430-portrait.png`, `qa-568-landscape-board.png`, `qa-568-modal-note.png`, `qa-844-landscape-board.png`, `qa-844-modal-controls.png`, `qa-820-tablet-board.png`, `qa-1440-desktop-board.png`.
- Tablet capture contains the whole board. Desktop capture contains most of the board but its bottom extends below the viewport; a second lower capture is needed for complete desktop visual evidence. Landscape screenshots similarly show scroll-position portions rather than the entire board at once.
- No error-level console messages. Four Next Image warnings reported `fill` parents with static position on guest-solo, guest-together, friends, and sepia-strip during responsive changes. Visible portrait images inspected were loaded and contained. These warnings are recorded rather than claimed clean.
- Reduced motion is source/conformance evidence only; no browser emulation capability was used. Physical touch inertia, real-device rotation, browser zoom, image-failure injection, fullscreen-board interactions, and all18 individual notes were not tested in this bounded pass.
- The initial844px modal geometry query incorrectly looked for explicit `[role=dialog]`; this is a native `dialog`, so those three JSON records contain viewport/focus only. Subsequent568px modal metrics use the actual DOM and are complete.

Read the review context, approved plan, implementation handoff and final conformance. No production edits or root design-qa edits. Reset viewport and closed the dedicated hidden tab; browser released to parent before writing this report.
