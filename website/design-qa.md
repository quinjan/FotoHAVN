# FOTOHVN Website Intro Experience prototype design QA

Date: 2026-08-24
Scope: Wayfinder prototype for `Prototype the Website Intro Experience composition and motion`
Prototype branch: `codex/prototype-website-intro-98`
Implementation URL: `http://localhost:4173/fotohvn`

## Comparison target

- Source visual truth: `C:\Users\QUINJ3875\.codex\generated_images\01a02f32-d41c-7b22-af89-7241abf5291c\exec-5a5edb18-4463-4318-a583-b98737b06289.png`
- Source pixels: 1536 x 1024.
- Desktop implementation evidence: `prototype-qa/issue-98/desktop-idle-pass3-1440x900.png`, `desktop-motion-080ms-1440x900.png`, `desktop-motion-260ms-1440x900.png`, `desktop-motion-510ms-1440x900.png`, and `desktop-final-pass3-1440x900.png`.
- Mobile implementation evidence: `prototype-qa/issue-98/mobile-idle-390x844.png`, `mobile-reveal-390x844.png`, `mobile-final-390x844.png`, and `mobile-idle-320x720.png`.
- CSS viewports: 1440 x 900, 390 x 844, and 320 x 720.
- Implementation pixels matched CSS pixels at `devicePixelRatio: 1`.
- Density normalization: none required for implementation captures. Source crops were resized only inside comparison boards and are labelled as such.
- State: idle, zoom, curtain reveal, completed handoff, Skip, Escape, pointer activation, and keyboard activation.

## Combined visual evidence

- Full design-board comparison: `prototype-qa/issue-98/comparison-full-source-vs-prototype-pass3.png`.
- Focused desktop idle comparison: `prototype-qa/issue-98/comparison-desktop-idle-pass3-focused.png`.
- Focused mobile idle comparison: `prototype-qa/issue-98/comparison-mobile-idle-focused.png`.
- Focused motion comparison: `prototype-qa/issue-98/comparison-motion-source-vs-prototype-pass3.png`.

The focused comparisons were required because the source is a multi-state board. They make the desktop/mobile initial composition, control placement, booth scale, and transition frames readable at the same time. The full comparison verifies that the live homepage reached by the prototype is the intended handoff target.

## Findings

- P0: none.
- P1: none.
- P2: none.
- [P3] Prototype asset details differ subtly from the ImageGen design board.
  - Surface: image quality and asset fidelity.
  - Evidence: the purpose-made closed-booth desktop and mobile assets preserve the selected architecture, palette, material character, and closed curtain but re-render minor room and booth details.
  - Impact: no effect on the composition or interaction decision this prototype answers.
  - Follow-up: ticket `Approve the Website Intro Experience asset and hero contract` owns production asset authorship, rights, and exact matched-state exports.

## Required fidelity surfaces

- Fonts and typography: passed. The prototype reuses FOTOHVN's canonical Cormorant display face and sans system. Wordmark and uppercase controls match the selected hierarchy, weight, spacing, and casing.
- Spacing and layout rhythm: passed. Desktop uses the selected center-weighted, zoomed-out booth with generous room; mobile preserves 24 px gutters. The primary control is 56 px high at 390 and 320 widths.
- Colors and visual tokens: passed. The implementation reuses the canonical off-white, warm ivory, dark walnut, soft brown, and low-elevation treatment with no new palette or decorative effects.
- Image quality and asset fidelity: passed for prototype scope. Desktop and mobile use purpose-made raster assets rather than CSS or vector stand-ins. Both show the complete booth and a fully closed curtain.
- Copy and content: passed. The visible intro copy is limited to `FOTOHVN`, `PRESS TO ENTER FOTOHVN`, and `SKIP INTRO`; no Brand Strip, welcome hold, Photo Strip, or development metaphor remains.
- Icons: not applicable; the selected intro contains no icons.
- Responsiveness: passed. At 390 x 844 and 320 x 720, `scrollWidth === clientWidth`; the booth remains recognizable and controls remain visible and usable.
- Accessibility and interaction: passed. The underlying page is inert while the dialog is active; body scroll is restored on exit; focus begins inside the dialog and transfers to `hero-heading`; Tab cycles through Skip and Enter; Enter activates the primary action; Skip and Escape dismiss; reduced-motion CSS collapses the transition.

## Primary interaction and browser checks

- Pointer press: passed; closed booth zooms in, curtain halves reveal the live homepage, and the intro unmounts after 820 ms.
- Keyboard: passed; Tab reaches `SKIP INTRO` then `PRESS TO ENTER FOTOHVN`; Enter runs the complete sequence.
- Skip button: passed; overlay and scroll lock are removed.
- Escape: passed; overlay is dismissed and focus moves to the hero heading.
- Final handoff: passed; the actual server-rendered homepage is exposed, not a screenshot substitute.
- Desktop overflow: passed.
- Mobile overflow: passed at 390 and 320 CSS px.
- Browser console warnings/errors: `[]`.

## Comparison history

### Pass 1 — blocked

- [P1] The moving curtain exposed an opaque dark fallback instead of the live homepage.
- [P1] Programmatic focus left a visible browser outline around the final hero heading.

Fixes: animated the intro background to transparency at the reveal boundary and removed the non-interactive heading outline while retaining focus transfer.

### Pass 2 — blocked

- [P2] The desktop booth was too large and too far right compared with the selected center-weighted frame.
- [P2] The default initial state displayed a button focus ring that was not present in the selected visual.

Fixes: regenerated the desktop closed-booth asset with more room and a central composition, recalibrated the curtain seam and zoom origin, and moved initial focus to the dialog container so keyboard users retain a valid entry point without changing the default visual state.

### Pass 3 — passed

- The full and focused comparison boards show no actionable P0, P1, or P2 mismatch.
- Browser evidence confirms the zoom, curtain reveal, live hero handoff, responsive frames, focus behavior, input paths, scroll restoration, and clean console.
- The desktop booth is intentionally slightly more distant than the original board because the user's later revision explicitly requested a zoomed-out initial photobooth; this is expected product direction, not design drift.

## Follow-up polish

- Production asset continuity and exact matched exports remain for `Approve the Website Intro Experience asset and hero contract`; they are not accepted as production-ready on the strength of this throwaway prototype.

final result: passed
