# Guest board portrait: fresh conformance v3

Result: **passed** after repair 2, on 2026-09-15. No production edits.

## Authority and scope

Fresh independent review of the complete approved `gpt-taste-design-plan.md`, selected `selected-option-1.png`, prior `conformance-final.md`, `repair-2.md`, `mobile-qa.md`, and review context. Read the gpt-taste skill, website AGENTS and design authority, current GuestAlbum component/CSS, photo component and reducer/data. The selected section plan takes precedence over unrelated whole-page skill suggestions.

This gate checks full plan conformance in source, the unchanged 390px composition, and the targeted 320px repair. It does not replace the next fresh responsive QA gate.

## Rendered findings

A new hidden in-app browser tab loaded `http://localhost:3000/fotohavn#guest-album`. After hydration, the existing section link aligned captures. Initial selection remained the original guest-trio, 12 / 18.

| Viewport and face | Album height | Stage height | Note button |
|---|---:|---:|---:|
| 390 x 844 photo | 943.375px | 431.25px | 207 x 48px |
| 390 x 844 note | 943.375px | 431.25px | 207 x 48px |
| 320 x 568 photo | 964.765625px | 390px | 137 x 64px |
| 320 x 568 note | 964.765625px | 390px | 137 x 64px |

- The 320px flip no longer changes section or control height. Both labels fit; See the photograph wraps naturally into two lines. The print remains approximately 248.49 x 336.69px including tilt.
- At 390px the original 48px control and prior 943.375px section height remain intact. The print is approximately 304.24 x 389.40px including tilt. Both neighbor peeks visibly contain photographs. The large contained original image, silver clip, off-white mat, modest tilt, wide cropped metal surface with top/bottom rails, editorial heading, arrows and pill action retain the approved composition.
- Note flips inline, revealing Room for three., the existing editorial text and FOTOHAVN credit; the photo leaves the accessibility tree and the note enters it. The action reports pressed state. No dialog appears during these flips.
- The counter, swipe hint and exact landscape encouragement are present. The 320px lower capture includes the encouragement and readable wrapped control. No horizontal page overflow at 390px, 320px or the 844px hidden-parent check (client and scroll widths agree).
- Visible active/neighbor images completed loading and use contain. Source URLs resolve to the existing 640w real-photo variants. Density-corrected DOM naturalWidth values are not physical source dimensions.

## Full plan source conformance

- Portrait mode is controlled solely by `(max-width: 767px) and (orientation: portrait)`; both layouts stay mounted, with no rendering/state media-query mismatch or orientation reset. All other sizes retain the original 1.5 horizontal scatter board and anchors. No vertical mobile scatter rules remain.
- The wider-than-viewport metal pseudo-element exposes no side frame. Stage sizing is independent of face and selection. Original images/strips are contained; strip fronts are narrow and centered. Both neighbors are inert and aria-hidden.
- Persistent selectedIndex is independent of modal index. Opening or navigating a modal updates remembered selection. Adjacent navigation wraps and returns to photo. Rotation changes neither selection nor side and does not implicitly open or close a modal.
- Horizontal-dominant gestures require 40px and use pan-y. Arrows are scoped to the viewer. No autoplay. The inline flip is 240ms, with immediate reduced-motion side changes.
- Named region, real button controls, aria-pressed, polite atomic position text, hidden/inert inactive faces, original alt text, focus outlines, 48px arrows, minimum 16px note body and bounded note scrolling match the plan. Photo failure keeps note access.
- Modal recovery checks connected visible targets; close animations skip hidden zero-sized triggers. Short landscape layer allows scrolling and removes the fixed sheet minimum.
- Repair 2 changes only narrow portrait button min-height to 64px at <=389px and gives portraitImage/neighbor positioned base rules. Portrait neighbor absolute positioning remains in effect. These changes preserve the approved design and do not add behavior or imagery.

## Console and hidden image parents

Fresh-tab captured warning/error logs: **zero warnings and zero errors** through initial full-board load, 390px, 320px and a final 844 x 390 transition. The hidden active-image and both neighbor parents compute to relative in landscape, confirming the repair. The silver clip has a static hidden parent but is an explicitly sized image, not a fill image. Some hidden responsive photo sources become deferred/empty during layout switching; these are hidden nodes, not evidence of a visible asset failure. This bounded observation does not claim every lazy-load/navigation sequence was exercised.

## Evidence and limits

- `conformance-v3-comparison.png`: selected mock, live 390px photo, live 390px note, live 320px lower note/control proof, left to right. Panels are resized to common width for comparison; screenshots are preserved separately.
- `conformance-v3-390-photo.png` and `conformance-v3-390-note.png`: original section-aligned screenshots. Sticky navigation is visible; encouragement lies below the viewport.
- `conformance-v3-320-photo.png` and `conformance-v3-320-note.png`: original lower-stage/control screenshots. The upper print extends above these captures; section/stage stability is measured in JSON, not inferred from the crop.
- `conformance-v3-evidence.json`: exact geometry, image sources/parent positions and empty warning/error log list.

No broad interaction matrix or command tests were rerun. Swipe, wraparound, strips, modal rotation/focus, reduced motion, remaining responsive sizes, physical touch and zoom belong to the fresh QA gate or are source-only checks here. Earlier mobile QA remains historical pre-repair evidence, not this review's execution.

The browser viewport was reset and the dedicated tab closed. Browser ownership was released to the parent immediately after the rendered pass, before preparing this report.
