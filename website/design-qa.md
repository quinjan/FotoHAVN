# FOTOHVN Website Intro Experience two-variant design QA

Date: 2026-08-27
Scope: Wayfinder throwaway prototype for GitHub issue 98
Prototype branch: `codex/prototype-website-intro-98`
Implementation URLs: `http://localhost:4173/fotohvn?variant=A` and `http://localhost:4173/fotohvn?variant=B`

## Comparison target

- Variant A source visual truth: `prototype-qa/issue-98/references/real-photobooth-reference.png` (1276 x 1574 px), plus the user's correction that only the upper framed half of the right-hand column is a mirror and the lower half remains walnut cabinetry.
- Variant B source visual truth: `prototype-qa/issue-98/references/drawn-photobooth-reference.png` (1440 x 1440 px), interpreted through the corrected real-booth anatomy.
- Desktop implementation evidence: `prototype-qa/issue-98/variant-a-idle-1440x900.png`, `variant-a-fold-zoom-1440x900.png`, `variant-a-fold-live-reveal-mid-1440x900.png`, `variant-a-fold-final-1440x900.png`, and `variant-b-idle-1440x900.png` (1440 x 900 CSS px and image px).
- Tablet implementation evidence: `prototype-qa/issue-98/variant-a-idle-768x1024.png` and `prototype-qa/issue-98/variant-b-idle-768x1024.png` (768 x 1024 CSS px and image px).
- Small-mobile implementation evidence: `prototype-qa/issue-98/variant-a-fold-live-reveal-mid-390x844.png`, `variant-a-idle-320x720.png`, `variant-b-idle-320x720.png`, and `variant-b-mid-320x720.png` (390 x 844 and 320 x 720 CSS px and image px).
- Additional interaction viewport: 390 x 844 CSS px.
- Density normalization: browser captures used device pixel ratio 1. Comparison boards fit each source and implementation proportionally into labelled equal-width cells without cropping; detail boards use labelled booth crops.
- States: idle closed curtain, controlled mid-transition, completed live-homepage handoff, Skip, Escape, pointer activation, keyboard activation, URL-selected variants, arrow-key switching, and switcher-button switching.

## Combined visual evidence

- Variant A full comparison: `prototype-qa/issue-98/variant-a-full-comparison.png` (1600 x 980 px).
- Variant A focused booth comparison: `prototype-qa/issue-98/variant-a-detail-comparison.png` (1600 x 980 px).
- Variant B full comparison: `prototype-qa/issue-98/variant-b-full-comparison.png` (1600 x 980 px).
- Variant B focused booth comparison: `prototype-qa/issue-98/variant-b-detail-comparison.png` (1600 x 980 px).
- Variant A motion-contract comparison: `prototype-qa/issue-98/variant-a-fold-motion-comparison.png` (1800 x 1200 px), combining the previously approved zoom/curtain/live-hero source sequence with the revised browser-rendered implementation states.

Focused comparisons were required because the mirror/cabinet divider, curtain rail, left utility panel, and material treatment were too small to judge reliably in the full boards. The mid-transition mobile captures verify that the open asset is revealed from left to right and ends with the curtain gathered at the right edge.

## Findings

- P0: none.
- P1: none.
- P2: none after repair.
- [P3] Generated booth details are not documentary reproductions.
  - Surface: image quality and asset fidelity.
  - Evidence: Variant A retains the real booth's illuminated sign, dark walnut structure, cream ring-hung curtain, left display/delivery area, and corrected upper-right mirror/lower-right wood split, but its mall reflections and small printed details are generated approximations. Variant B intentionally remixes those cues in pencil, ink, warm paper, and restrained walnut color.
  - Impact: acceptable for a throwaway composition and motion decision; these assets are not production-authority exports.
  - Follow-up: issue 99 owns the production asset and hero contract.

## Required fidelity surfaces

- Fonts and typography: passed. Existing FOTOHVN display and sans typography are reused. Wordmark and controls retain the site's uppercase hierarchy, optical weight, tracking, and readable contrast at all checked widths.
- Spacing and layout rhythm: passed. Both variants begin zoomed out, keep the booth centered as the dominant object, and preserve the fixed wordmark, skip action, primary entry action, and development-only variant switcher without collisions at 1440, 768, 390, or 320 CSS px.
- Colors and visual tokens: passed. Variant A uses warm mall light, cream, and walnut. Variant B uses warm ivory paper, dark ink, and restrained walnut tint. Controls reuse existing off-white, ebony, and walnut tokens; no gradients or decorative code-drawn assets were introduced.
- Image quality and asset fidelity: passed for prototype scope. Variant A retains its corrected realistic closed-booth desktop/mobile assets, then crossfades before magnification artifacts appear to dedicated high-resolution curtain close-ups with smooth fine linen, a real raster rail, rings, and clips. Variant B retains its matched closed/open drawing pair and paper/ink treatment. No curtain, clip, rail, or illustration was reconstructed as CSS or vector art.
- Copy and content: passed. Visible intro copy is limited to `FOTOHVN`, `SKIP INTRO`, `PRESS TO ENTER FOTOHVN`, and the development-only A/B switcher. The source image's `developing...` caption and carousel arrows were correctly excluded.
- Icons: not applicable. No icon is needed for the selected entry flow.
- Responsiveness: passed. No horizontal overflow was present at 768, 390, or 320 CSS px. Controls remain visible and usable, with the main action at least 56 px high.
- Accessibility and interaction: passed. The homepage remains inert and body scrolling is locked while the modal is active. Tab reaches Skip then Enter; actual browser keyboard Enter activates the sequence; Escape and Skip dismiss it; completed focus moves to `hero-heading`; scrolling is restored; reduced-motion CSS collapses the animation duration.

## Motion and browser checks

- Variant A: passed. The realistic scene now uses a 2800 ms sequence: it zooms tightly to the curtain, invisibly hands off to a high-resolution curtain close-up before the source booth raster would pixelate, then folds the curtain smoothly toward the right over 1650 ms while the live homepage expands behind it. The rail remains in place and the raster clips gather with the folded fabric before the final handoff.
- Variant B: passed. The canvas scene uses a slower 2200 ms zoom-and-reveal sequence; the curtain wipe begins after 760 ms, runs for 1040 ms, and is paired with a restrained stepped ink-trace animation.
- Final handoff: passed. Variant A reveals the live server-rendered homepage directly behind the folding curtain with no generated open-booth intermediate; Variant B retains its illustrated open-booth transition. No Photo Strip remains in the intro.
- Direct URLs: passed. `?variant=A` and `?variant=B` server-render their requested variant without an initial wrong-variant flash.
- Variant controls: passed. PREV/NEXT buttons and ArrowLeft/ArrowRight update the query parameter and reset the selected intro for replay.
- Browser console warnings/errors: `[]`.

## Comparison history

### Pass 1 - blocked

- [P2] The generated realistic right-hand column used the mirror across the entire column instead of only its upper framed section.
- [P2] Completion focus was attempted before the homepage's `inert` state had been removed, leaving focus on `body` instead of the hero heading.

Fixes: regenerated all four realistic desktop/mobile closed/open frames with an upper mirror and lower walnut panel; moved focus transfer to a post-completion effect that runs after the modal cleanup.

### Pass 2 - passed

- Combined full and focused boards confirm the corrected booth anatomy and both selected art directions.
- Fresh browser evidence confirms desktop, tablet, 390 px, and 320 px layouts; controlled transition frames; actual keyboard activation; pointer activation; Skip; Escape; focus handoff; scroll restoration; URL switching; and a clean console.
- No actionable P0, P1, or P2 findings remain.

### Pass 3 - blocked after motion refinement

- [P2] Zooming the full realistic booth raster until the curtain filled the viewport made the fabric visibly pixelated.
- [P2] A straight clip-path wipe revealed the page but did not visibly fold the curtain or show the top hardware moving with it.

Fixes: added purpose-generated desktop and mobile close-range curtain assets with subtle linen texture and visible rail/clips; crossfaded to them before the booth raster exceeded useful detail; replaced Variant A's wipe with a right-anchored folding transform; kept the real raster rail in place while the clips and fabric gather to the right.

### Pass 4 - passed

- The combined motion board shows the retained initial booth, tight curtain zoom, visible top rail/clips, smooth rightward fold over the live homepage, and direct final handoff.
- Fresh browser checks passed at 1440 x 900, 390 x 844, and 320 x 720 with no horizontal overflow or console warnings/errors.
- Actual browser keyboard Enter, pointer activation, Skip, Escape, focus handoff, scroll restoration, and Variant B regression checks passed.
- No actionable P0, P1, or P2 findings remain.

## Follow-up polish

- Production-ready photographic continuity, exact print/fixture details, rights, compression, and final hero matching remain issue 99 work and are not approved by this prototype pass.

final result: passed
