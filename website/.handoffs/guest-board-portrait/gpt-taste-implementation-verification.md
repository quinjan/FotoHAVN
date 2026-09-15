# Guest board portrait: independent plan conformance

Result: blocked

## Scope and authority

Fresh independent review against `gpt-taste-design-plan.md`, `review-context.md`, and `selected-option-1.png`. Read the gpt-taste skill and website AGENTS instructions. The approved section plan and original photographs take precedence over generic whole-page skill suggestions. No production edits were made.

## Blocking finding

**Neighbor photographs are not visible in the portrait peeks.** At 390 x 844, both edge cues appear as blank white wedges. The target and plan require recognizable slivers of adjacent photographs. `GuestAlbum.module.css` centers contained images inside mostly offscreen `.neighbor` frames, leaving the visible section occupied by the empty mat. Keep complete/contained images for the active photograph and strips; adjust only the decorative neighbor crop, image position, and/or edge reveal so both edges include original photographic content. Preserve inert/aria-hidden behavior and the stationary metal surface.

Evidence: `conformance-comparison.png` shows the selected mock at left and the live 390px view at right. `conformance-390-photo.png` is the unaltered browser capture. Browser navigation is included in the implementation capture; it is existing page chrome, not a design discrepancy.

## Verified conformance

- Main composition follows the approved image: editorial heading, large tilted original guest-trio photograph, silver clip, edge-to-edge cropped metal with upper/lower rails, circular arrows, pill note control, and 12 / 18 counter.
- Inline note action was exercised in the browser: it reveals the title, existing editorial note and explicit FOTOHAVN credit; the front is removed from the accessibility tree and no dialog opens. Section height remains exactly 943.375px before and after flipping.
- Exact encouragement is rendered below the controls: “Better in landscape.” and “Turn your phone to see the whole board.” `conformance-390-note.png` captures the lower note, controls and encouragement after scrolling.
- Source confirms a single CSS portrait condition `(max-width: 767px) and (orientation: portrait)`; there are no divergent JavaScript rendering conditions. Both layouts stay mounted, preserving reducer selection and side across orientation changes.
- Full board uses original scatter anchors and `aspect-ratio: 1.5`; the vertical scatter overrides are removed. Portrait material is a stationary wider-than-viewport surface using original `metal-board.webp`, not a vertical framed board.
- Source preserves all 18 original photograph/strip records and editorial-versus-testimonial branching. Original assets, contain sizing, failure-message note access, and narrow strip presentation are present.
- Source has Phosphor native icons, real named buttons, aria-pressed note state, a named region, polite position announcements, inert decorative neighbors/hidden face, scoped keyboard arrows, 48px portrait controls, and 2px/3px-offset focus styling.
- Source has retained selection independent from modal state, wraparound and reset-on-navigation reducer behavior, horizontal-dominant 40px swipe threshold with pan-y scrolling, 240ms inline flip, and immediate reduced-motion transitions.
- Source checks modal close destinations for positive geometry and restores focus to visible fallback controls. Short-landscape dialogs scroll with the sheet minimum removed.

## Evidence limits and next gate

This was the plan-conformance gate, not broad responsive QA. Only 390 x 844 portrait and the inline flip/lower content were exercised live. Swipe, keyboard/focus traversal, strips, wraparound, rotation/modal focus behavior, reduced motion, and the full breakpoint matrix still need the fresh responsive reviewer after repair and a fresh conformance pass. No broad console claim is made.

The primary photograph loaded successfully; DOM reported naturalWidth 296 at the 390px portrait viewport with nominal active width 296.4px. The fallback src was the original asset's 1400w variant. CurrentSrc/DPR were not captured, so the apparent softness relative to the generated reference is not classified as a defect; a fresh reviewer should record those values before claiming an undersized asset. Original source photography remains authoritative.

Parent reported scoped guest-board ESLint and TypeScript passing; whole-project lint has an unrelated existing hook-naming error in `scripts/online-photobooth-preview.test.mjs:39`. Build was still running separately. This review does not substitute for those command outputs.

Browser viewport was reset and the review tab closed on completion.
