# FOTOHVN Website Intro Experience approved desktop-flow design QA

Date: 2026-08-27  
Scope: Wayfinder throwaway prototype for GitHub issue 98; Variant A desktop at 1440 x 900 only  
Prototype branch: `codex/prototype-website-intro-98`  
Implementation URLs: `http://localhost:4173/fotohvn?variant=A` and `http://localhost:4173/fotohvn?variant=B`

## Comparison target

- Current decision authority: `.handoffs/issue-98-approved-desktop-intro-direction.md`.
- Selected visual target: `prototype-qa/issue-98/references/approved-left-wall-welcome-screen-storyboard.png` (3072 x 2048 px).
- Physical-anatomy authority: `prototype-qa/issue-98/references/real-booth-open-doorway-reference.png` and `prototype-qa/issue-98/references/real-booth-left-wall-screen-reference.png`.
- Retained idle exterior: `public/prototype/issue-98/variant-a-real-booth-closed.png`.
- New desktop assets: `public/prototype/issue-98/variant-a-threshold-left-wall-desktop.png` and the user-corrected `public/prototype/issue-98/variant-a-left-wall-screen-paired-lights-desktop.png` (1586 x 992 px each).
- Faithful physical-screen content: `public/prototype/issue-98/variant-a-homepage-live-desktop.png`, captured from the real server-rendered homepage at 1440 x 900 CSS/image px.
- Browser implementation evidence: `prototype-qa/issue-98/approved-flow-implementation-idle-1440x900.png`, `approved-flow-repair1-threshold-1440x900.png`, `approved-flow-paired-lights-screen-hold-1440x900.png`, `approved-flow-paired-lights-screen-push-1440x900.png`, and `approved-flow-paired-lights-final-live-1440x900.png`.
- Viewport and density: 1440 x 900 CSS px at DPR 1; browser captures are 1440 x 900 image px. No density normalization was required.
- States: idle exterior, left-biased threshold, inside left-wall assembly, physical-screen hold, screen push, completed live homepage, Skip, Escape, pointer entry, CUA keyboard entry, URL-selected variants, and A/B switching.
- Mobile adaptation and responsive tuning are explicitly deferred. No mobile viewport was reviewed or reported as passed in this checkpoint.

## Combined visual evidence

- Full sequence comparison: `prototype-qa/issue-98/approved-flow-storyboard-browser-comparison.png` (2400 x 1760 px). This board combines the cropped four-frame approved storyboard and five browser-rendered implementation states in one comparison input.
- Focused anatomy comparison: `prototype-qa/issue-98/approved-flow-left-wall-detail-comparison.png` (2400 x 1500 px). This board combines the real interior authority, generated assembly, and browser-rendered homepage-inside-screen state.
- gpt-taste conformance: `prototype-qa/issue-98/approved-flow-gpt-taste-conformance.md`.

Focused comparison was required because the camera label, physical screen bounds, paired-light ownership, control placement, curtain edges, and absence of the rear guest background are too small to judge from the full sequence alone.

## Findings

- P0: none.
- P1: none.
- P2: none after repair.
- [P3] Generated intermediate photography is prototype evidence rather than documentary asset authority.
  - Surface: image quality and production continuity.
  - Evidence: the purpose-made desktop stages preserve the approved warm walnut/cream material language and physical anatomy, including the user's paired-light correction, but fine joinery and lighting continuity are generated approximations rather than exact matched photographs of the production booth.
  - Impact: acceptable for the issue-98 composition/motion decision; it does not authorize production asset use.
  - Follow-up: issue 99 owns production asset continuity, rights, compression, and exact exports.

## Required fidelity surfaces

- Fonts and typography: passed. Intro wordmark, Skip, entry action, and development switcher retain the existing FOTOHVN type hierarchy. The physical screen uses a real homepage capture, so its typography matches the live page rather than a generated imitation.
- Spacing and layout rhythm: passed after repair. Every stage is full-bleed at 1440 x 900, the exterior remains the dominant idle object, the threshold turns left without exposing the guest background, and the interior holds enough surrounding wall for the physical assembly to register before the push.
- Colors and visual tokens: passed. The flow stays within warm mall light, cream linen, dark walnut, ebony, and off-white. Existing token-backed controls remain unchanged.
- Image quality and asset fidelity: passed for throwaway-prototype scope. Both new stage assets exceed the decision viewport's useful density, use the approved physical order, and avoid enlarged low-resolution crops. The homepage inside the touchscreen is a browser capture of the actual site. No booth, curtain, camera, light, screen, or control is reconstructed as CSS, SVG, canvas, icon, or placeholder art.
- Copy and content: passed. Visible intro copy remains `FOTOHVN`, `SKIP INTRO`, `PRESS TO ENTER FOTOHVN`, and the development-only A/B switcher. No Brand Strip, Photo Strip, welcome-copy hold, audio, or development hold was added.
- Interaction affordances: passed. Entry and Skip remain semantic buttons with pointer, Enter, and Space support; focus treatment and button contrast remain visible.

## Motion and browser checks

- Variant A sequence: passed. The 5600 ms desktop journey moves from the retained closed exterior into a right-gathered curtain/left-biased threshold, crossfades into the left-wall assembly, holds the real homepage inside its physical screen, then expands that same capture until it matches the live page.
- Spatial continuity: passed. The right/rear standing or sitting background never appears. The physical order remains `LOOK HERE` lens, landscape screen, matching vertical lights immediately left and right, and metal control below.
- Final handoff: passed. The intro unmounts, `#site-content` loses `inert`, body overflow restores, scroll remains at 0, and focus moves to `#hero-heading` with no visible page jump.
- Pointer activation: passed in the in-app browser.
- Actual browser CUA keyboard activation: passed. Tab reached Skip and then Enter; CUA Enter ran the full sequence and completed with focus on `hero-heading`.
- Escape and Skip: passed. DOM-CUA Escape dismissed both idle and in-motion states after a visible control received focus; pointer Skip dismissed the idle state.
- Variant regression: passed. Direct `?variant=B` rendered Variant B without an A flash; ArrowRight switched B to A; PREV switched A back to B; Variant B completed in its unchanged 2200 ms path.
- Scroll and containment: passed. During idle and motion, body overflow was `hidden`, `#site-content` was inert, and `scrollWidth === clientWidth`. After completion, normal scrolling returned with no horizontal overflow.
- Console warnings/errors: `[]` after the final repair.
- Reduced-motion fast path: source-verified and unchanged. The component still completes in 40 ms when `prefers-reduced-motion: reduce` matches, and the media rule collapses the new stage animations to 1 ms. Fresh runtime media emulation was skipped because the in-app browser surface does not expose a reduced-motion override; this gate is recorded as source verification, not a fresh runtime pass.

## Engineering gates

- `npm run lint`: passed with no errors or warnings.
- `npx tsc --noEmit --pretty false`: passed.
- `npm run build`: passed on Next.js 16.3.2.
- `git diff --check`: passed; only the repository's existing LF-to-CRLF working-copy notices were emitted.
- gpt-taste scoped plan-conformance review: passed after the full-bleed repair. Randomized redesign, new GSAP dependencies, AIDA restructuring, and bento rules were correctly treated as outside this already-selected intro-overlay scope.

## Comparison history

### Pass 1 - blocked

- [P2] The threshold and interior stages briefly exposed a dark matte around their edges because their opening keyframes scaled the full-bleed rasters below 1.
- [P2] A scaled interior hold risked drifting the live homepage overlay away from the physical screen bezel.

Fixes: kept the threshold above full-bleed scale throughout; held the interior raster at scale 1 while the physical-screen idea registers; reserved the final enlargement for the screen portal itself.

### Pass 2 - passed

- Fresh same-viewport browser captures show full-bleed threshold and interior states with no matte edges.
- The focused comparison confirms the homepage stays inside the bounded landscape touchscreen, the lens/paired lights/control retain their physical ownership, and the rear guest background is absent.
- Pointer, actual CUA keyboard entry, Escape, Skip, focus handoff, scroll restoration, inert cleanup, URL-selected variants, A/B switching, Variant B completion, overflow, and console checks all passed.
- No actionable P0, P1, or P2 findings remain.

### Pass 3 - passed after paired-light correction

- Added a purpose-edited end-frame master with a matching vertical light immediately to the screen's left while preserving the screen, lens, right light, control, curtain edges, crop, and 1586 x 992 geometry.
- Fresh 1440 x 900 browser evidence confirms the real homepage remains registered inside the physical bezel, both lights flank the display cleanly, the final live-page match completes, normal focus/scroll/inert cleanup remains intact, horizontal overflow is absent, and the console is clean.
- Rebuilt the full-sequence and focused anatomy comparison boards with the paired-light end frame. No actionable P0, P1, or P2 findings were introduced.

### Pass 4 - corrected external generator-pack framing

- Replaced the previously mixed `3:2` and `16:10` generator references with three dedicated `1920 x 1080` (`16:9`) keyframes under `public/prototype/issue-98/video-generator-pack-16x9/`.
- Used source-guided horizontal outpainting so the animation inputs gain side environment instead of losing booth, sign, curtain, floor, screen, lights, or control to a vertical crop.
- Verified all three exported files report exactly `1920 x 1080`; `prototype-qa/issue-98/video-generator-pack-16x9-contact-sheet.png` shows the complete sequence on matching canvases.
- The browser prototype's `16:10` masters and live homepage composition remain unchanged. This pass affects only assets handed to the external video generator.

## Follow-up polish

- Production-matched booth photography, final camera-path exports, rights, compression, and mobile choreography remain separate follow-up work. Mobile is deferred until the user approves this desktop animation.

final result: passed
