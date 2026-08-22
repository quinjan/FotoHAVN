# Design-revision result handoff 01

Date: 2026-08-22  
result: passed

## Files written

- `website/.handoffs/gpt-taste-design-plan.md`
- `website/.handoffs/design-revision-result-01.md`

No production source, CSS, TSX, package, asset, runtime configuration, or `website/DESIGN.md` file was modified.

## Conflict resolved

Plan version 1.1 deliberately rejects the earlier responsive-QA requirement for exactly two visible hero lines at 375px and 390px. The attempted implementation met that count only by shrinking the H1 to 27.2px and 28.1088px, below the recorded 35.2px floor and nearly equal to the 26px wordmark. That collapsed the selected Artistic Asymmetry hierarchy.

The revised authority preserves `clamp(2.2rem, 7.5vw, 6rem)`, Cormorant Garamond 500, line-height `0.88–0.94`, approximately `-0.035em` tracking, fixed copy, 24px phone gutters, two CTAs, the print overlap, and the 72rem H1 maximum. Below 768px, the two authored spans may naturally resolve to two or three visual lines; three at 320px, 375px, and 390px are expected and approved. At 768px and above, exactly two visual lines remain required. This stays inside gpt-taste's mandatory two-to-three-line rule while preserving editorial scale, asymmetry, CTA clearance, and zero overflow.

## Precise next implementation instructions

Modify only `website/src/components/UpperExperience.module.css`:

1. Remove `font-size: clamp(1.7rem, 7.2vw, 3.45rem)` from the `max-width: 767px` `.heroHeading` rule so the base `clamp(2.2rem, 7.5vw, 6rem)` applies.
2. Remove the redundant `max-width: 360px` `.heroHeading { font-size: 2.2rem; }` override.
3. Preserve the phone `max-inline-size: 100%` rule.
4. Retain the 768–819px `letter-spacing: -0.04em` adjustment if fresh rendering confirms exactly two lines at 768px; otherwise document a conflict rather than shrinking type or gutters.
5. Do not change the fixed copy, hero image/crop/height, 24px/48px gutters, CTA count or geometry, print geometry, or any unrelated production seam. Do not introduce no-wrap, transform scaling, clipping, smaller type, altered copy, or damaging tracking.

## Verification expectations

Fresh browser verification must cover 320, 375, 390, 768, 820, 1024, 1280, and 1440 after fonts are ready. Confirm the clamp-computed font size and 35.2px minimum, no more than three visual lines below 768px, exactly two from 768px upward, intact punctuation, dominant H1 hierarchy, legible veil, preserved booth focus, no CTA/print intersection, at least 12px small-phone CTA-to-print clearance, approved gutters, and `document.documentElement.scrollWidth === document.documentElement.clientWidth`.

After implementation, run a fresh gpt-taste conformance gate before any responsive QA. The historical responsive-QA and repair handoffs do not override plan version 1.1.
