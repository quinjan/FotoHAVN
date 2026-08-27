# FOTOHVN Website Intro Experience approved desktop-flow design QA

Date: 2026-08-28
Scope: Wayfinder throwaway prototype for GitHub issue 98; Variant A desktop/tablet reveal at 1440 x 900, 1280 x 720, and 1024 x 768
Prototype branch: `codex/prototype-website-intro-98`  
Implementation URLs: `http://localhost:4173/fotohvn?variant=A` and `http://localhost:4173/fotohvn?variant=B`

## Comparison target

- Current decision authority: `.handoffs/issue-98-approved-desktop-intro-direction.md`.
- Selected visual target: `prototype-qa/issue-98/references/approved-left-wall-welcome-screen-storyboard.png` (3072 x 2048 px).
- Physical-anatomy authority: `prototype-qa/issue-98/references/real-booth-open-doorway-reference.png` and `prototype-qa/issue-98/references/real-booth-left-wall-screen-reference.png`.
- Retained idle exterior: `public/prototype/issue-98/variant-a-real-booth-closed.png`.
- Selected desktop journey asset: `public/prototype/issue-98/variant-a-generated-journey-desktop.mp4` (1920 x 1080, 5.125 seconds), with `public/prototype/issue-98/video-generator-pack-16x9/01-exterior-closed-1920x1080.png` as the matching desktop poster.
- Faithful post-video content: the actual server-rendered `#site-content`, already mounted beneath the intro at the current responsive viewport size. There is no raster screenshot bridge.
- Browser implementation evidence: `prototype-qa/issue-98/video-flow-video-end-1440x900.png`, `video-flow-black-takeover-1440x900.png`, `video-flow-develop-dark-1440x900.png`, `video-flow-focus-pull-1440x900.png`, `video-flow-sharp-capture-1440x900.png`, and `video-flow-final-live-1440x900.png`.
- Viewport and density: 1440 x 900 CSS px at DPR 1; browser captures are 1440 x 900 image px. No density normalization was required.
- States: idle exterior, left-biased threshold, inside left-wall assembly, physical-screen hold, screen push, completed live homepage, Skip, Escape, pointer entry, CUA keyboard entry, URL-selected variants, and A/B switching.
- Mobile choreography remains explicitly deferred. The live-page reveal was reviewed at the three agreed desktop/tablet resolutions above; no mobile viewport is reported as passed in this checkpoint.

## Combined visual evidence

- Full sequence comparison: `prototype-qa/issue-98/approved-flow-storyboard-browser-comparison.png` (2400 x 1760 px). This board combines the cropped four-frame approved storyboard and five browser-rendered implementation states in one comparison input.
- Selected post-video comparison: `prototype-qa/issue-98/video-flow-selected-storyboard-comparison.png` (2600 x 1050 px). This board puts the user-selected six-stage black-development/focus-pull storyboard beside the six same-state browser captures.
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

- Fonts and typography: passed. Intro wordmark, Skip, entry action, and development switcher retain the existing FOTOHVN type hierarchy. The post-black reveal is the real homepage DOM, so its typography is already the final live typography rather than a generated or rasterized imitation.
- Spacing and layout rhythm: passed after repair. Every stage is full-bleed at 1440 x 900, the exterior remains the dominant idle object, the threshold turns left without exposing the guest background, and the interior holds enough surrounding wall for the physical assembly to register before the push.
- Colors and visual tokens: passed. The flow stays within warm mall light, cream linen, dark walnut, ebony, and off-white. Existing token-backed controls remain unchanged.
- Image quality and asset fidelity: passed for throwaway-prototype scope. The selected 1920 x 1080 video exceeds the decision viewport's useful density and the browser uses its own final frame as the physical zoom source, avoiding a geometry-changing still swap. The page-development layer is the live server-rendered page itself. No booth, curtain, camera, light, screen, or control is reconstructed as CSS, SVG, canvas, icon, or placeholder art.
- Copy and content: passed. Visible intro copy remains `FOTOHVN`, `SKIP INTRO`, `PRESS TO ENTER FOTOHVN`, and the development-only A/B switcher. No Brand Strip, Photo Strip, welcome-copy hold, audio, or development hold was added.
- Interaction affordances: passed. Entry and Skip remain semantic buttons with pointer, Enter, and Space support; focus treatment and button contrast remain visible.

## Motion and browser checks

- Variant A sequence: passed. The 5.125-second supplied video carries the closed exterior into the left-wall assembly. Its paused final frame then continues through a 1900 ms centered dolly until the black glass fills the viewport; the already-mounted live page develops from near-black at low exposure and heavy blur into its normal crisp state.
- Spatial continuity: passed for the selected supplied video. The camera lands on the left-hand screen wall with the `LOOK HERE` lens, landscape black screen, paired vertical lights, and metal control. The post-video stage preserves that exact frame until the black screen takes over; it does not introduce a miniature webpage, alternate curtain, portal edge, or geometry-changing still swap.
- Final handoff: passed. The intro unmounts, `#site-content` loses `inert`, body overflow restores, scroll remains at 0, and focus moves to `#hero-heading`. A stable root scrollbar gutter keeps the live page width unchanged when scrolling is restored.
- Pointer activation: passed in the in-app browser.
- Actual browser CUA keyboard activation: passed. Tab reached Skip and then Enter; CUA Enter ran the full sequence and completed with focus on `hero-heading`.
- Escape and Skip: passed. DOM-CUA Escape dismissed both idle and in-motion states after a visible control received focus; pointer Skip dismissed the idle state.
- Variant regression: passed. Direct `?variant=B` rendered Variant B without an A flash; ArrowRight switched B to A; PREV switched A back to B; Variant B completed in its unchanged 2200 ms path.
- Scroll and containment: passed. During idle and motion, body overflow was `hidden`, `#site-content` was inert, and the scrollbar gutter remained reserved. After completion, normal scrolling returned with no horizontal overflow or content-width change.
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

### Pass 5 - passed after selected video and full-black development workflow

- Replaced the still-stage desktop choreography with the user-supplied 1920 x 1080 MP4 and verified its browser metadata: 5.125 seconds, ready state 4, muted inline playback.
- The first integration briefly crossfaded the video to a differently proportioned paired-light still and showed the homepage as a miniature bounded overlay. The user rejected that handoff, and visual QA confirmed the still swap changed the screen geometry.
- The selected repair keeps the video's own final frame, continues a centered 5.6x physical zoom until the black glass fills the viewport, then develops the full-page capture from low exposure and heavy blur into crisp focus.
- The first repaired ending faded the screenshot against the live page and briefly doubled typography. The final repair holds the crisp capture fully opaque until the intro unmounts; `video-flow-sharp-capture-1440x900.png` and `video-flow-final-live-1440x900.png` are pixel-identical.
- Actual browser CUA Tab reached Skip and Enter; CUA Enter completed the full video and post-video transition; Escape dismissed during video playback. Pointer Skip, final focus, scroll restoration, inert cleanup, Variant B completion, zero horizontal overflow, and a clean browser console also passed.
- The selected storyboard/browser comparison contains no remaining actionable P0, P1, or P2 mismatch.

### Pass 6 - passed after native-size reveal correction

- The user observed the homepage adjusting its size while it developed from black. Evidence confirmed two causes: the earlier capture was 1425 x 891 and the reveal keyframes scaled the page from 1.04 to 1.
- Replaced the transition source with an exact 1440 x 900 homepage capture and removed every homepage transform from the development/focus animation. Only opacity, brightness, saturation, and blur now change.
- Fresh browser measurements at both development and focus-pull states report a 1440 x 900 portal, a 1440 x 900 natural/client image, and computed `transform: none`.
- Rebuilt `video-flow-selected-storyboard-comparison.png` from the corrected browser captures. The crisp capture and final live-page screenshots remain pixel-identical, with no new P0, P1, or P2 finding.

### Pass 7 - passed after removing the raster handoff

- The user still observed an apparent scale correction after the screenshot reached focus. Browser measurements confirmed that a raster-to-live-DOM swap could not stay faithful across responsive widths; they also exposed a separate 15 px width change when the locked page released its vertical scrollbar.
- Removed `variant-a-homepage-live-exact-1440x900.png` and the entire screenshot portal. The real server-rendered `#site-content` now stays mounted underneath and receives only opacity, brightness, saturation, and blur animation; computed `transform` remains `none` throughout.
- Reserved the root scrollbar gutter while body scrolling is locked. At 1440 x 900, 1280 x 720, and 1024 x 768, the measured live-page widths remain respectively 1425 px, 1265 px, and 1009 px before, during, and after the reveal.
- Fresh browser evidence confirms no portal image exists, final focus reaches `#hero-heading`, normal scroll/inert cleanup completes, and the comparison board shows the same live DOM from the first visible page frame through completion. No actionable P0, P1, or P2 finding remains.

## Follow-up polish

- [P3] The user-supplied free Vidu render retains its visible bottom-right watermark during the generated-video portion. Acceptable for this throwaway HITL prototype; a licensed clean export is required for production.
- Production-matched booth photography, final camera-path exports, rights, compression, and mobile choreography remain separate follow-up work. Mobile is deferred until the user approves this desktop animation.

final result: passed
