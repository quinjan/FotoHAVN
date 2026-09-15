# Independent portrait plan conformance

**Result: PASS — no P0/P1/P2 conformance findings.** Fresh review on 2026-09-15, before broad responsive QA. No production edits.

## Authority and evidence

Reviewed `plan.md`, `ui-implementation.md`, `camera-implementation.md`, the selected `../booth-portrait-ideation/option-1-inline.png`, BoothExplorer TSX/CSS, scene framing/input source, and global focus styles.

- `conformance-reference-comparison.png`: selected reference on left, actual browser capture on right, combined with Sharp and visually reviewed together.
- `conformance-390-overview.png`: real localhost WebGL overview.
- `conformance-390-inside.png`: camera wall preset.
- `conformance-390-bench.png`: bench preset.

Actual browser viewport was **390 x 1000**, while the reference scaled to 390px is approximately 844px tall. The browser reserves 15px for its desktop scrollbar, leaving a measured **375 x 480 CSS px canvas**. Extra capture height is for page-flow inspection; this does not claim the whole section fits a typical 390 x 844 screen. Normal page scrolling is explicitly part of the approved plan.

## Passed

- Complete overview silhouette is clearly visible: sign/roof, floor/base, and both exterior edges. The model is prominent and the camera fits real geometry. It has somewhat more breathing room than the generated reference; this is consistent with the selected 480px canvas and 88% bounds fit, and preserves the existing physical model rather than treating generated geometry as authority.
- Compact serif heading is two lines at the tested width, followed by the approved short supporting copy. Warm full-width stage has no inset card border or stage-label overlay.
- Swipe instruction and controls remain entirely below the model. Four distinct 48 x 48px targets follow left / minus / plus / right order. The contiguous segmented group differs from the reference's separated rotation buttons, but keeps the approved interaction hierarchy and legibility. This is an acceptable implementation detail, not a redesign blocker.
- Native labeled viewpoint select and Ebony curtain action are adjacent, each measured 159.5 x 48px. Labels are readable and unobstructed at this width.
- All six presets are present in the select and scene map: Overview, Front, Side, Back, Inside, Bench. Inside and Bench were exercised live, show their intended subject clearly, automatically open the curtain, and update descriptions/status. Rotation displays the disabled Custom view placeholder; Reset restores Overview and the closed curtain.
- Source accessibility: canvas host is named, keyboard focusable when ready, and associated with instructions; decorative canvas/icons are hidden from accessibility; buttons have explicit names; chooser has a real label; inactive alternative controls use display:none; live status, fallback photograph alt text, loading/failure text, retry, and disclaimer remain. Global focus-visible outline applies, with an inset outline for the canvas host. Portrait DOM order matches the control row's visual order.
- Camera and CSS use the same narrow-portrait condition. Source retains resize/media reframing, reduced-motion handling, bounded zoom, keyboard commands, touch-action:pan-y, horizontal touch intent, and cancellation/lost-capture cleanup.

## Limits / next gate

This is the conformance gate, not the full interaction/responsive matrix. Fresh responsive QA still needs 320px and 390px captures, every preset, zoom/curtain/reset and focus inspection, and representative landscape/desktop checks. Physical touch scrolling and unavailable-WebGL behavior were not exercised live here; source support was reviewed. Parent-reported scene tests/build/lint are not independently rerun by this review.

Browser ownership was released immediately after the live checks: temporary viewport reset and the reviewer's hidden tab closed. The fresh responsive reviewer may proceed.
