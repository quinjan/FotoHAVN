# Inline booth controls — design QA, 2026-09-17

final result: passed

## Visual authority and scope

Selected reference: `.handoffs/booth-inline-controls/selected-design.png` (1715 x 917).
Fullscreen direction: `.handoffs/booth-inline-controls/fullscreen-direction.png` (1748 x 899).
The user's final instruction removes the orange curtain marker; only text remains. The description belongs to the model figure, before its controls. Existing physical booth geometry, photographs, page chrome, typography families and palette remain authoritative over generative variations in the reference.

Implementation: `http://localhost:3000/#the-booth`. Source and browser captures were opened together, followed by normalized side-by-side comparisons. The source is a presentation board with three differently scaled crops, not a literal pixel baseline at the printed viewport dimensions. Comparisons normalize each device crop and browser screenshot to equal widths, preserving aspect ratio. Page navigation and the existing illustration disclaimer are retained and excluded from strict section-content matching.

## Evidence

All paths below are relative to `.handoffs/booth-inline-controls/`.

| Evidence | CSS viewport | Saved image pixels |
| --- | --- | --- |
| `desktop-inline.png` | 1440 x 1500 | 1425 x 1484 |
| `ipad-inline.png` | 834 x 1450 | 819 x 1424 |
| `mobile-inline.png` | 390 x 1200 | 375 x 1154 |
| `ipad-fullscreen.png` | 834 x 1194 | 834 x 1194 |
| `mobile-320-fullscreen.png` | 320 x 900 | 320 x 900 |

Browser devicePixelRatio was 1. The capture provider scales some inline images after excluding the scrollbar; `comparison-mobile.png`, `comparison-ipad.png`, and `comparison-desktop.png` normalize widths to 390/650/650 pixels. Taller inline captures document the complete scrolling section, not a claim that every control fits above the fold on a normal phone. Additional live checks used 320 x 740 and 390 x 1000. The original reference and implementation were also inspected at native capture sizes for control and caption readability.

## Comparison history

- Initial comparison identified excess mobile vertical spacing and an undersized tablet stage relative to the selected composition. Reduced mobile section top padding and stage height; increased tablet stage to 620px and desktop maximum to 660px; made primary control labels 16px.
- Added a deliberate caption break on mobile/tablet to avoid a stranded 'A' and preserve the reference's two-phrase reading. Desktop keeps one continuous line where space allows.
- Re-captured and inspected the three comparison images after these repairs. No remaining actionable P0/P1/P2 finding within the approved booth-section scope. Full-page screenshot experiments omitted offscreen WebGL content and were discarded; the retained images are viewport captures with the scene visibly rendered.

## Required fidelity surfaces

- Typography: existing Cormorant Garamond display/caption and Manrope controls retained. Italic second heading line, quiet curtain instruction, legible 16px primary labels and intentional caption wrapping checked. Site navigation was not redesigned to imitate generated chrome.
- Layout: view selection above the model, fullscreen in the stage corner, text-only curtain instruction and attached caption on the same ivory figure, followed by Rotate/Move, zoom and Reset. Phone switches to a selector and two control rows. Larger screens retain the view row and one toolbar row.
- Colors: existing ivory, off-white, ebony and hairline tokens; no orange indicator, overlay pill, gradients or new decorative palette.
- Assets: actual interactive Three.js booth, its supplied photo textures, mirror and interior retained. Generated model proportions/shading were not substituted for the working scene. Phosphor icons used for controls.
- Content: approved overview description and view-specific descriptions retained. Panning does not discard the last preset's description. Curtain copy changes between open and close; pointer capability selects click versus tap wording.

## Trackpad panning correction

The previous wheel handler explicitly ignored unmodified scrolling inline. Removed that fullscreen-only guard; wheel input on the canvas now pans in the camera plane without rotation in either presentation. Pinch zoom is unchanged. Updated the inline hint and help text. The regression test failed before the fix and passed afterward. All 19 booth tests, TypeScript and lint passed. Browser wheel checks verified vertical and horizontal inline panning: scrollY stayed at 2187px over the canvas; scrolling outside it changed scrollY from 2187px to 2290px. Physical trackpad hardware was not directly exercised.

## Interaction verification

- Browser: clicked the actual curtain to open it; fullscreen retained the curtain state; Escape returned to the inline section. Exact scroll restoration checked (2328px before and after) and focus returned to Full screen.
- Browser: preset selection, automatic opening for Inside, Move + arrow-key navigation, help expansion/collapse, reset, responsive toolbar and exit control checked. Native dialog provides modal focus isolation. The same DOM and WebGL scene move into/out of the dialog, preserving camera and mode.
- At 320px, inline section/document width was 305px within the 320px viewport; visible buttons/select were at least 48px high and 61px wide. Fullscreen controls and help remained accessible at 320 x 740.
- No browser console errors in the focused checks. Development logged an existing image-priority recommendation for the photo fallback.
- 19 scene/model tests passed, including raycast curtain opening/closing, ignored drags/cancellations, inline pinch/pan, scroll intent, bounded interior movement, disposal and fullscreen state preservation. Production build, TypeScript and full-project lint passed.

## Accepted limits and follow-up

- Fullscreen is an immersive browser-viewport dialog; it does not force the operating system to hide browser chrome on mobile.
- Inline canvas owns touch gestures: a vertical one-finger gesture scrolls the page; horizontal movement manipulates the model; two fingers pan/pinch. Trackpad two-finger scrolling over the canvas pans both inline and in fullscreen; scrolling outside the canvas moves the page. Zoom buttons and keyboard controls remain available.
- Physical iPhone/iPad/Android and macOS/Windows trackpad gesture feel, Safari's native gesture dispatch, and assistive-technology behavior have not been verified on hardware. Synthetic scene tests and desktop-browser interaction are not substitutes for those checks.
- P3: the real booth has different proportions and lighting from the generated reference; actual site heading/toolbar scale follows the existing design system. Further subjective sizing changes can be handled as a design iteration.
- No deployment performed.


---

# Experience lasting-memory image — QA, 2026-09-14

final result: passed

## Comparison target

- Source visual truth: `.handoffs/experience-refinement-20260914/keepsake-options/option-1-cafe-revision.png` (1456×1090, landscape 4:3).
- Rendered implementation: `http://localhost:3000/FOTOHAVN#experience`, captured in the Codex in-app Browser at 1440×1000 and 320×720. The browser capture is attached to the task transcript; this browser surface does not expose a local screenshot path.
- State: Experience section at rest after page load.
- Delivered sources: desktop loaded `keepsake-reunion-640w.webp`; 320px loaded `keepsake-reunion-320w.webp`.

## Findings

No actionable P0/P1/P2 differences remain. The implementation renders the approved café image directly. At desktop, the 594×489 story frame keeps all three friends, their hands, the café context, and the secondary prints readable. At 320px, the source-matched 4:3 frame shows the complete group and gesture without horizontal overflow.

## Required fidelity surfaces

- **Fonts and typography:** Existing Cormorant Garamond and Manrope story typography is unchanged.
- **Spacing and layout rhythm:** The desktop 5/2/5 band remains unchanged. The third mobile image now uses the source's 4:3 ratio instead of the superseded square crop.
- **Colors and visual tokens:** No site tokens changed. The image retains its approved cream, walnut, espresso, and muted-brass café palette.
- **Image quality and asset fidelity:** The 1456×1090 approved PNG produced a WebP master and responsive derivatives from 128px through 1400px. Both tested browser variants decoded successfully with the three faces and hands intact.
- **Copy and content:** The lasting-memory heading and support line are unchanged. Alt text describes the friends, café, and old FOTOHAVN strips.

## Focused comparison and verification

The full Experience band was inspected at desktop, followed by a focused 320px view of the third story. No additional crop was needed because the approved raster asset is rendered directly and the browser evidence clearly shows every critical subject and gesture. Both viewports had `scrollWidth` equal to `clientWidth`; console warning/error reads were empty. `npm run lint` passed, and the live route returned HTTP 200 with the new asset and alt text.

## Comparison history

The approved café visual passed on the first implementation comparison with no P0/P1/P2 repair required.

---

# Experience print-treatment image — QA, 2026-09-14

final result: passed

## Comparison target

- Source visual truth: `.handoffs/experience-refinement-20260914/look-options/four-ways-one-face-overlay-wordmark-revision.png` (1024×1536, 1× density).
- Rendered implementation: `http://localhost:3000/FOTOHAVN#experience`, captured in the Codex in-app Browser at 1440×1000 and 320×720. The browser capture is attached to the task transcript; this browser surface does not expose a local screenshot path.
- State: Experience section at rest after page load. Desktop uses the 12-column 5/2/5 story band; mobile uses the single-column story sequence.
- Delivered sources: desktop loaded `look-prints-256w.webp`; 320px loaded `look-prints-320w.webp`. The master and both derivatives preserve the source's 2:3 ratio.

## Findings

No actionable P0/P1/P2 differences remain. The implementation uses the approved image itself rather than recreating it, so the generated guest identity, four full-bleed strip templates, FOTOHAVN wordmark placement, filters, hands, curtain, and walnut setting are exact asset matches. The centered desktop cover crop keeps all four treatments readable in the intentionally narrow middle column. The 320px portrait frame shows the full composition without horizontal overflow.

## Required fidelity surfaces

- **Fonts and typography:** Existing Cormorant Garamond and Manrope story typography is unchanged and retains its established hierarchy.
- **Spacing and layout rhythm:** The desktop 5/2/5 band is unchanged. Mobile changes only the middle image from a landscape box to the source-matched 2:3 portrait ratio.
- **Colors and visual tokens:** No CSS color or token changed. The asset retains its warm cream, walnut, color, sepia, grayscale, and high-contrast treatments.
- **Image quality and asset fidelity:** A 1024×1536 WebP master plus 128–1400px responsive derivatives were generated. Browser inspection confirmed decoded responsive sources and `object-fit: cover` at both breakpoints.
- **Copy and content:** Heading and supporting copy are unchanged. Alt text now names the four visible FOTOHAVN treatments.

## Focused comparison and verification

The Experience image region was reviewed at both viewports because the narrow desktop crop and mobile reflow are the only affected presentation surfaces. No additional focused region was needed: the implementation renders the approved raster asset directly, and the browser captures clearly show its faces, wordmarks, print edges, and hands. Console warning/error reads were empty. `npm run lint` passed, and the live route returned HTTP 200 with the new asset in its rendered HTML.

## Comparison history

The first visual comparison produced no P0/P1/P2 mismatch. A subsequent clean-console pass exposed a Next.js LCP warning on the third story image when opening the Experience anchor directly at 320px. All three images in the compact Experience band now load eagerly; a fresh-tab desktop/mobile recheck cleared the warning without changing the visual result.

---

# Experience story refinement — QA, 2026-09-14

final result: passed

The selected Option 2 structure remains intact. Following the latest reference correction, `Room to be yourself.` now uses a tighter generated reconstruction of the two women in the user-supplied candid. The 3D model supplies the correct booth orientation: they sit on the right-wall bench, the fixed cream backdrop is directly behind them, and the opposite camera/screen wall is outside the image. `A look that feels like you.` shows a guest choosing restrained photographic treatments on the booth screen. The third story reads `A memory made to last.` / `A FOTOHAVN moment, printed to last.`

Desktop and 320px browser checks passed with the new two-person crop preserving both faces, shoulders, and the cream backdrop while excluding the camera, screen, and doorway. The 320px page had no horizontal overflow and the active candidate decoded. Final code gates are recorded in [.handoffs/experience-refinement-20260914/README.md](.handoffs/experience-refinement-20260914/README.md), together with the prompt, 3D references, responsive assets, and screenshots.

No online-photobooth workflow code changed. The earlier Experience/online QA record below describes the original selected design and remains historical evidence; this entry supersedes its old material-only image descriptions. No deployment is included.

---

# Experience redesign and online photobooth — QA, 2026-09-14

final result: passed

This scoped result covers the user-selected Option 2 Experience section and the new `/FOTOHAVN/online` workflow. Fresh final conformance, independent desktop/tablet/mobile reviews, and fresh final synthesis all passed on the source after both repairs. No unresolved actionable P0/P1/P2 findings remain.

The Experience uses three generated material still lifes, the approved editorial copy, and the dramatic online invitation. The website navigation and Prints section also open the booth. The complete Layout → Photographs → Look → Download flow includes four starter layouts, four authored frames, four photographic looks, individual replacement with cancellation, pointer/tap/keyboard arrangement, retained extra photographs, and complete framed previews. Browser checks verified the final image and download link use the same PNG blob.

Responsive evidence covers 1440/1024px desktop, 820/1180px tablet, and 320/390px mobile, with a 375px spot check. Required content and controls remain readable and reachable without horizontal overflow. Actual keyboard focus and clean warning/error console reads are recorded. Final integrated checks passed: 39/39 tests, full lint, TypeScript, and production build. Both canonical local routes returned HTTP 200 with expected content; the local preview remains running.

Physical camera behavior, physical touch devices, and an actual native saved-file artifact remain unverified. Runtime network/storage instrumentation and reduced-motion preference emulation were unavailable; source inspection is not represented as a runtime pass. These are evidence limits, not observed bugs. No deployment is included.

The earlier blocked conformance report describes the pre-filename-repair baseline; the fresh recheck closes that P2. Pointer placement also has a completed repair and actual browser retests. All final responsive reviewers inspected the repaired source.

Evidence: [final synthesis](.handoffs/online-photobooth-20260914/final-verification.md), [conformance recheck](.handoffs/online-photobooth-20260914/gpt-taste-implementation-verification-recheck.md), [desktop](.handoffs/online-photobooth-20260914/responsive-desktop.md), [tablet](.handoffs/online-photobooth-20260914/responsive-tablet.md), [mobile](.handoffs/online-photobooth-20260914/responsive-mobile.md), and [root verification](.handoffs/online-photobooth-20260914/browser-verification.md).

The expanded guest-board report below is preserved verbatim as earlier, separate QA history.

---

# Expanded guest board — QA

final result: passed

User requested smaller prints and more assets combining real repository photos with board keepsakes. The board now contains 18 clickable photographs: 14 real guest photographs and four complete Photo Strips, plus six decorative keepsake placements.

## Delivered

Desktop portrait widths reduced from approximately 24–26% to 15.5–18% of board width. Phone portraits reduced from 58–61% to 43–47%. A minimum 60px width keeps the slim strips usable. Three irregular groups fill the desktop board; a taller staggered arrangement supports natural phone scrolling.

Eight additional guest photographs were selected from the repository's Evia contact sheet and given honest editorial captions. Seven new image families were prepared with all required responsive derivatives; the eighth reuses existing sisters assets. Source mappings and preparation script are documented in `.handoffs/guest-board-expanded/README.md`.

The teddy, bunny, ribbon and flower-ticket artwork is reused, with an additional ribbon and ticket placement. Decorative objects stay outside keyboard navigation and do not intercept photo clicks. The bunny was moved toward the right edge between photo groups to keep it clear of the new bottom-row faces.

## Browser checks

Evidence directory: `.handoffs/guest-board-expanded/`.

- `desktop.png`: 1440x1100 full-window board; all 18 photographs loaded, six keepsake placements, no horizontal overflow. All 18 prints had exposed 44x44 hit targets.
- All eight new photographs were individually opened via currently exposed areas and their selected IDs matched: guest-sisters, guest-trio, guest-solo, guest-duo, guest-flowers, guest-three, guest-close, guest-together. Each returned to the board.
- `tablet.png`: 820x1180, all images loaded and no horizontal overflow. Hover/focus demonstrates photographs rising above neighbouring items.
- `mobile-0.png`, `mobile-1.png`, `mobile-2.png`: 320x720 at successive positions through the board. After scrolling settled, browser hit testing verified an exposed 44x44 target for all 18 prints.
- `mobile-note.png`: the final new guest photograph opens its own editorial note. Counter shows Photograph 18 of 18; Next wraps to Photograph 1 of 18 and restores the photo face.
- Current positions must be measured after each return because a focused photograph intentionally rises above its neighbours. Static cached coordinates are not a valid overlap test.

## Checks and limits

Existing board tests passed 8/8, updated for 18 records and extended to verify every image-loader candidate exists. Final production build including TypeScript passed. Focused ESLint on the changed component, data and scripts passed. Git diff whitespace check passed.

Browser verification used responsive viewports, not physical touch devices. No new screen-reader or separate production-browser-server pass. Lift/reduced-motion behavior remains in the existing tested implementation. The complete images remain available in the viewer even where board overlaps conceal a paper edge or caption.

The warm typography, palette, existing guests and earlier source assets are preserved. No new customer quotations, identities, dependency installation, commit, push or deployment. Previous QA is archived in the evidence directory.

---

# Print section — Keep the moment close — QA, 2026-09-14

final result: passed

## Comparison target

- Source visual truth: `.handoffs/print-section-option-1/selected-option-1.png`, 1086×1448 pixels at a 3:4 ratio.
- Rendered implementation: `http://localhost:3000/fotohavn#prints`, captured in the Codex in-app browser at 1440×1000 and 320×720 CSS pixels, device scale managed by the browser.
- State: default light presentation, Print section reached through the desktop `PRINTS` navigation anchor.

## Full-view comparison

The implementation uses responsive WebP derivatives of the selected source image directly. The desktop photograph frame measures 599×787 CSS pixels and preserves the selected 3:4 composition without cropping the exchanged strips or hands. At 320px, the browser loaded the 320w derivative, the document `scrollWidth` equalled `clientWidth` at 305px, and the complete copy, links, and photograph remained in natural reading order without horizontal overflow.

The source asset and the rendered section were both opened and visually inspected. A separate focused crop was not needed because the source is rendered directly, the print and hand details remain legible in the full section capture, and the browser-reported current source confirms the intended derivative.

## Required fidelity surfaces

- Fonts and typography: the existing Cormorant/Manrope hierarchy is preserved. `Keep the` / `moment close.` forms the intended two-line desktop and mobile heading, with the italic emphasis retained on `close.`
- Spacing and layout rhythm: the existing two-column editorial structure is preserved on desktop and reflows to one column at 320px. The image frame changed from near-square to 3:4 to match the selected visual rather than crop it.
- Colors and visual tokens: the existing off-white, cream, walnut, soft-brown and photographic-shadow tokens are unchanged and align with the warm source image.
- Image quality and asset fidelity: the 1086×1448 selected PNG was archived in the handoff directory and converted to a base WebP plus 128, 256, 320, 384, 640, 960 and 1400 responsive candidates. Desktop loaded the 640w candidate; mobile loaded the 320w candidate. No placeholder or code-drawn replacement is present.
- Copy and content: the section now reads `Keep the moment close.` / `A few minutes inside. A memory that stays with you.` The supporting phrase reads `Your photographs leave the booth as physical prints and scan the QR code for digital copies, made to be held, shared, and kept.`

## Findings and comparison history

No P0, P1 or P2 differences were found in the first rendered comparison, so no repair iteration was required. The taller frame is intentional and faithful to the selected source image. No P3 follow-up is required for this focused change.

## Browser and code checks

- `PRINTS` navigation anchor visibly reached the updated section.
- Both existing section actions remained present with their original destinations.
- Browser console warning/error read: empty.
- Route verification: HTTP 200 with the new copy and asset reference in the rendered HTML.
- ESLint: passed.

The responsive checks use browser viewports rather than physical touch devices. No deployment is included.


## Online booth Template step — 2026-09-14

Scope: approved refinement of option 2; combined template selection in the existing online booth.

Source visual truth: `.handoffs/template-step-ideation-20260914/selected-design.png` (1488 x 1058).
Implementation: `http://localhost:3000/fotohavn/online`.
Final screenshot: `.handoffs/template-step-ideation-20260914/implementation-desktop-final.png` (1473 x 1047 browser capture, 1488 x 1058 requested viewport; capture excludes scrollbar and is slightly reduced by browser tooling). Compared at equivalent full-frame scale, not as a pixel-difference claim.
Mobile evidence: `.handoffs/template-step-ideation-20260914/implementation-mobile-selection.png` (305 x 686 captured content at 320 x 720 requested viewport). Also checked 390 x 844.
State: Template step, Butter Gingham selected, empty session. All original source photographs are replaced with uniform #E5E0D7 photo windows in the selector.

### Comparison history

- Initial desktop comparison: P2 heading was too small, progress/header spacing placed content too low, and body descriptions were too small. Increased heading size and description size, reduced top spacing and heading line height. Re-captured and compared the source and implementation together in the same browser tool result.
- Initial 320px comparison: P2 excessive wrapping in template descriptions. Reduced thumbnail column and row gaps to allocate more width to copy. Re-captured mobile rows: complete names, readable descriptions, radio controls contained, no horizontal overflow.
- Final full-view comparison: selected source and final screenshot were emitted together. No remaining actionable P0/P1/P2 findings. Focused mobile row capture checked the most constrained text/control area; desktop source and final full views were readable at native viewing size.

### Fidelity surfaces

- Typography: existing Cormorant Garamond and Manrope; exact approved heading `Make a little Moment` and subtitle `choose your template`; italic Moment. Original app header retained. Readable 16px desktop template descriptions and 14px narrow-screen descriptions.
- Layout: two desktop columns, left preview below heading, four radio rows right, selected summary and action beneath. One-column mobile adaptation. Selected preview and thumbnails use the identical blob URL for each template, verified in browser DOM.
- Colors: existing warm ivory/off-white/ebony/brass tokens. All photo windows use shared #E5E0D7.
- Assets: actual published yellow gingham and polka artwork with original guest windows blanked. Signature and Panorama are edge-to-edge compositions with the existing canvas lettering pipeline. Native 1:3 / 3:1 output proportions intentionally preserved rather than copying ImageGen's slightly distorted thumbnail dimensions. This is an expected source-fidelity correction.
- Copy: four template names/descriptions, Template step label, combined selection, Continue to photographs, and Change template links. No separate frame selector.

### Verification

- 30 focused tests passed: session photo retention, 3/4 slot switching, native template geometry, empty/captured composition, existing pointer and retake behavior, look confinement and downloads.
- TypeScript passed. Scoped ESLint passed. Production build passed. A final CSS-only button typography polish followed the build and was browser-verified.
- Browser: radio selection, Butter Gingham / Panorama Four / Polka Keepsake states, identical thumbnail/large-preview URL, 320px and 390px containment, three local fixture photos imported, Look step, downloadable 600 x 1800 PNG and Download PNG action exercised, return to blank Template selection.
- Browser warning/error log: empty.
- Physical camera permission/hardware not exercised. Download link and finished PNG preview verified; no OS Downloads-file inspection performed.
- No deployment performed. Existing unrelated working-tree changes preserved.

final result: passed


## Template photo-preview restoration — 2026-09-14

User correction supersedes the prior blank-only large preview after capture. TemplateStep now receives the existing live composition whenever the session has photographs. An empty session still shares the blank image between thumbnail and preview; thumbnails stay blank after capture. Pending revisions do not display an old composition under a new template geometry; composition errors retain retry.

Browser regression verified: imported one local fixture into Butter Gingham, returned via Change template, visually confirmed the photograph in the large preview and two empty windows; switched to Panorama Four and visually confirmed the retained photograph in the first portrait slot. Thumbnails remain blank. Browser warning/error log empty. TypeScript and scoped ESLint passed. Existing approved layout is unchanged.

final result: passed


## Seven output-based Filters — 2026-09-14

Step 3 and transition controls now use Filters/filter. Seven filter choices follow the original Output carousel order. Source mapping, direct descriptions, processing scope and approximation limits are recorded in `.handoffs/template-step-ideation-20260914/filter-reference-map.md`. Existing screen layout retained. Browser verified desktop plus 320px, all seven choices, Sepia/Soft Monochrome/Warm Monochrome selection, and the filtered downloadable PNG with unchanged frame colors. Screenshot: `.handoffs/template-step-ideation-20260914/filters-seven-final.png`. No console errors/warnings. 32 focused tests, TypeScript and scoped ESLint passed.

final result: passed

## Photographs redesign — 2026-09-15

Source visual truth: `.handoffs/photographs-ideation-20260915/selected-camera-final.png` (approved round shutter + larger camera) and `refined-arrange.png` (overall Arrange direction). Latest user instructions override prior pause/resume designs and the photo count display.

Implementation evidence in the same handoff directory:
- `implementation-camera-final.png`: desktop synthetic-stream active countdown, round stop, disabled ON Mirror, full-width camera workspace.
- `implementation-arrange-final.png`: production route, actual Panorama Four, selected second slot, contextual editing actions and drag handle.
- `implementation-camera-mobile.png`, `implementation-arrange-mobile.png`: 320px layout and live/completed-camera state.
- `implementation-download.png`: Butter Gingham600x1800PNG ready after import, swap and Sepia.

Viewport: requested desktop1488x1058 CSS, rendered screenshots1473x1047 pixels (in-app capture/scrollbar scaling); sources1488x1058. Compared complete frames at equivalent scale, about1% capture-size difference. Mobile requested320x720, captures305px wide after scrollbar. Density differences were not treated as design defects. Source single-person sample differs from synthetic stream's two-person photo and imported fixtures; compare structure/crops and controls, not identity. Countdown2 in source and3 in capture are equivalent active states. The source's decorative outer corner crop was replaced by the actual selected template crop, which is intentional.

Full-view comparisons opened source and implementation together for both Camera and Arrange. Controls, selection edges and labels were readable in the full-resolution images, so separate crops were unnecessary. Five surfaces reviewed:
- Typography: approved Cormorant Garamond/Manrope;14px action labels with correct casing, italic light, no count sidebar.
- Layout: broad camera stage, shutter bottom-center, Mirror top-right, uncluttered Arrange, no duplicate photo rail or camera inset. Main alignment expands with the camera workspace. Native vertical/horizontal templates retain their true proportions.
- Colors: local off-white/ivory/ebony/brass tokens. Legible dark-backed live controls. Disabled Mirror retains ON state and thumb position.
- Imagery: camera uses actual media stream; desktop letterboxing preserves full source plus accurate output crop. Mobile fills native slot aspect. Photo art/wordmarks are existing compositor assets, not generated approximations.
- Copy: approved headline/subtitle; removed Your captured photographs will stay and Photograph X of Y. No Pause/Resume action. Retake target label remains to identify the photograph being replaced.

Comparison/repair history:
1. Initial oversized mobile camera left large empty bands (P2): changed mobile container to native slot aspect and cover crop; final mobile evidence has no empty bands or horizontal overflow.
2. Initial inherited uppercase tiny edit controls (P2): scoped sentence-case14px controls added; final Arrange comparison confirms readable contextual actions.
3. Missing selected-photo drag affordance (P2): added library grip icon; final Arrange evidence confirms it.
4. Candidate preview initially could report ready at unchanged session revision (P1): readiness now follows exact composition identity. New regression test confirms candidate and restored original wait for their actual render.
5. Camera pending permission could be cancelled by opening the file picker and stay idle after picker cancellation (P1): opening picker no longer stops camera; moving to Arrange handles stream release.

Validation: production build and TypeScript passed, scoped ESLint passed,35tests passed. Browser checked auto-camera request, synthetic stream capture/stop, mirror state, completion, targeted retake/retry/cancel, import/replacement review, desktop drag, mobile tap Move, Filters and PNG readiness. Final production-route browser errors/warnings []. Temporary synthetic camera route was removed before production build. Physical camera device matrix and native download-file delivery are not claimed; download event was not exposed by the in-app browser.

Findings: no remaining actionable P0/P1/P2 findings within the verified scope. Follow-up P3: contextual action row is centered beneath the print rather than horizontally tracking the selected slot, to keep the same accessible layout across vertical and horizontal templates.

final result: passed

## Horizontal previews rotated counterclockwise — 2026-09-15

User asked to display horizontal templates vertically with a single counterclockwise turn. Implemented preview-only orientation in template thumbnails, blank/populated Template previews, Arrange, Filters, and Download preview. Native composition/export geometry remains original. previewLayout maps each slot (x,y,w,h) to (y,originalWidth-x-w,h,w); artwork gets rotate(-90deg), while selection controls and labels remain upright. The original leftmost photograph is now the bottom slot.

Validated with37tests, scoped ESLint, TypeScript, and production build. New tests assert asymmetric coordinate conversion, native geometry immutability, and drag source identity after rotation. In-app browser checked Panorama Four blank thumbnail and large preview, four imported photographs, top-slot selection identifying photograph4,320px mobile with no horizontal overflow, Filters and Download. Download preview measured160x480 with expected -90deg artwork transform; PNG itself remains1800x600. Browser errors/warnings []. Separate test tab closed and user's original session preserved.

Evidence: `.handoffs/photographs-ideation-20260915/rotation-arrange-desktop.png` and `rotation-arrange-mobile.png`.

final result: passed

## Capture exposure feedback — 2026-09-15

Added a380ms soft exposure flash and650ms Captured check cue after each successful photo, followed by an800ms cancellable beat before the next countdown or review. Camera stream stays live. Stop uses the existing abort signal throughout the feedback interval. Reduced-motion mode omits the flash and animation while retaining a static Captured cue and the same pacing. Feedback never enters the exported image.

Verification:37tests, scoped ESLint and production build/TypeScript passed. Separate synthetic-camera browser session completed a four-shot sequence; targeted retake test observed Captured after countdown and pressed Stop during feedback, returning to Take replacement with video.paused=false. Browser errors/warnings []. Evidence: `.handoffs/photographs-ideation-20260915/capture-feedback.png`. Temporary camera-check route removed before build, test tab closed, user's existing session preserved. Reduced-motion override reviewed in CSS; physical-camera hardware not part of this check.

final result: passed

## Portrait guest keepsake viewer — 2026-09-15

Scope: approved first displayed mock, implemented at `/fotohavn#guest-album`. Source authority is `.handoffs/guest-board-portrait/gpt-taste-design-plan.md` and `selected-option-1.png` (853 x 1844). The selected mock permits immediate portrait browsing with optional landscape encouragement. Existing real photographs, strips, notes and horizontal scatter board are preserved.

### Final visual evidence and normalization

Fresh synthesis inspected `final-portrait-section.png` (375 x 1058 capture from the parent's 390 x 1100 browser viewport, including the whole section and encouragement), `conformance-v3-comparison.png` (1560 x 844), `final-tablet-board.png` (805 x 1158 at requested 820 x 1180), and `final-desktop-fullscreen-top.png`, `final-desktop-fullscreen-bottom.png`, `final-desktop-note.png` (each 1440 x 900). All are in `.handoffs/guest-board-portrait/`.

The comparison presents the selected source, live 390px photo, live 390px note and 320px lower note/controls at common panel width. This is a composition comparison, not a pixel-difference claim: the generated source is a section-only reference, live views include sticky navigation and scrollbars, and the final full-section capture uses a taller viewport. The exact 390 x 844 original captures and geometry remain in the v3 evidence. Screenshots may exclude scrollbar space or be scaled by browser capture; CSS dimensions come from JSON, not image pixel counts. The desktop top and bottom captures together cover the full board. Landscape remains horizontal but requires vertical scrolling on short screens; no single-viewport fit is claimed.

### Five fidelity surfaces

- Typography: Cormorant Garamond editorial heading with italic `you`, existing Manrope controls/body, readable 16px portrait note body. Narrow reverse-action text wraps inside its reserved button.
- Layout: one large modestly tilted clipped print, off-white mat, visible photographic neighbor peeks, wider stationary metal crop and two rails. The centered arrow/pill row, counter, swipe hint and lower encouragement follow the approved hierarchy. Notes replace the photo inline without section movement. Tablet and desktop retain the horizontal scatter composition.
- Colors: warm ivory section, ebony pill, soft brown supporting text, off-white paper and silver material remain coherent with local design authority and the selected reference.
- Imagery: existing guest-trio is the initial 12 / 18 selection; actual customer images and original strips remain contained and proportionate. Real clip/material differences from the generated reference are intentional reuse of production assets. Final tablet and desktop evidence shows the preserved full board and decorations.
- Copy: `Look at you.`, `Every photograph has another side.`, note/photo actions, calculated counter, `Swipe to explore.`, `Better in landscape.` and `Turn your phone to see the whole board.` are present. `Room for three.` retains its editorial credit; no fabricated testimonial was introduced.

### Repair and verification history

1. Decorative neighbor photos were obscured by contained-image letterboxing. Repair 1 aligned each image toward its visible edge and increased the nominal peek to 28px. Fresh conformance confirmed actual photograph peeks.
2. Independent mobile QA found a P2 at 320px: wrapping the reverse-action label increased section height by about 13.19px. Repair 2 reserves 64px for both button faces at widths up to 389px; 390px retains 48px. It also keeps hidden fill-image parents positioned during responsive switching to address Next Image warnings.
3. Fresh conformance v3 passed after repair 2. Photo and note heights match exactly: 964.765625px at 320 x 568 and 943.375px at 390 x 844. Stage heights are 390px and 431.25px respectively. Its fresh browser session recorded zero warnings/errors, including a transition to 844 x 390.
4. Parent post-repair browser checks independently confirmed 320px photo/note section stability, 48px arrows, 64px center button, and document client/scroll widths 305 / 305. Encouragement has positive height in 375px portrait and zero height at 844px landscape. Parent inspected the 820px whole board and exercised 1440px fullscreen -> guest-trio note -> Escape returning focus to the matching print -> Escape returning focus to the fullscreen opener. The main tab contains historical 03:33 warnings, with no warnings from this final session reported.

The earlier fresh mobile QA exercised 320/390/430 portrait, 568/844 landscape, 820 tablet and 1440 desktop with no horizontal page overflow. It verified pointer swipe, vertical scroll without navigation, scoped keyboard navigation, first/last wraparound, strip containment, inline flips, remembered selection/note across orientation, modal survival across rotation and visible portrait focus return. Short landscape modal scrolling brought close/navigation controls into view. These broad interaction checks preceded the final CSS-only repair; they were not all repeated afterward.

Command validation: 11 focused board tests, scoped ESLint and TypeScript passed during implementation. Final production build after repair 2 passed, including TypeScript and five static pages. Full-project lint still has the unrelated existing `react-hooks/rules-of-hooks` error in `scripts/online-photobooth-preview.test.mjs:39`; this task's scoped lint passes. No deployment is claimed.

### Gate ownership and limits

Fresh independent conformance was repeated after repairs. Dedicated fresh per-device responsive/final-synthesis attempts encountered task capacity; the parent, who was not the implementer, performed the final responsive and desktop/tablet checks. This separate fresh synthesis then reviewed the evidence and wrote this conclusion. The originally requested fresh per-device post-repair agent sequence was therefore not completed exactly; it is an orchestration gap, not an observed product defect.

Physical touchscreen inertia, real-device rotation, browser zoom, reduced-motion browser emulation, image-failure injection and all 18 individual notes were not tested. Reduced-motion and image-failure fallback have source/conformance evidence only. The full pre-repair interaction matrix was not rerun after the final CSS-only change. These are explicit validation limits rather than automatic failures.

Findings: no remaining actionable P0/P1/P2 defect in the verified scope. The known narrow-layout defect and observed hidden-image warning cause were repaired and checked. See the handoff README for evidence routing.

final result: passed

## Larger inline 3D booth in phone portrait — 2026-09-15

**Findings:** no remaining actionable P0/P1/P2 finding in the independently verified scope. User selected the first displayed 3D-booth concept, not the earlier guest-board concept.

### Visual authority and comparison

Source: `.handoffs/booth-portrait-ideation/option-1-inline.png` (853 × 1844 pixels, about 390 × 844 logical). Fresh conformance opened that image and the actual WebGL rendering, then inspected them together in `.handoffs/booth-portrait-inline/conformance-reference-comparison.png`. The actual overview used a 390 × 1000 CSS viewport for page-flow inspection, with a stored screenshot of 375 × 962 pixels. Images were normalized to a common 390px width for comparison. The final taller presentation image `qa-390-tall-preview.png` was captured at 390 × 1050 and stored at 375 × 1010 pixels. These presentation captures do not claim everything fits a single ordinary phone screen. Exact 320 × 568 and 390 × 844 browser checks are recorded separately in `responsive-evidence.json`.

Focused 320px control and Inside/Bench captures supplement the full composition comparison. They are readable at their native size and show labels, model subjects, and focus; a second composite crop was unnecessary. Stored screenshots may be scaled by capture transport; live CSS dimensions and image pixel sizes are recorded separately rather than assumed identical.

### Five fidelity surfaces

- Typography: existing Cormorant Garamond headline and Manrope interface are retained. Portrait uses compact heading, short supporting sentence, native select/action text at 16px, and a quiet swipe hint. The long Custom view and Close curtain labels fit the strict 305px effective content width at the 320px viewport.
- Layout: the actual canvas is 480px high, compared with the previous 235px at the tested 390px viewport. It spans the container width without the inset card border or overlay labels. Real booth bounds determine framing. Instructions and four 48px controls sit below the model, followed by one six-view selector, curtain action and Reset. The contiguous control group is an intentional small difference from the separated groups in the generated mock, accepted by independent conformance as preserving the hierarchy and usability.
- Colors: existing off-white/ivory, ebony action and soft-brown copy tokens; no new visual theme. Original walnut, cream fabric and reflective panel materials remain.
- Imagery: actual Three.js geometry/textures are preserved, with a complete exterior silhouette in all four exterior presets. Inside and Bench have portrait field-of-view adaptation while physical camera positions stay inside the cabinet. Generated model polish was not substituted for the real implementation.
- Copy: short portrait Come a little closer., Swipe to turn., six viewpoint labels, Custom view, Open/Close curtain and Reset view are implemented. Wider supporting copy, descriptions, live status, failure/retry messaging and illustrative-model disclaimer remain.

### Independent verification and history

Separate planner and UI implementer worked from the selected image; parent implemented camera/input math and regression tests in disjoint files. A fresh conformance reviewer passed the source and rendered comparison, then a separate fresh responsive reviewer passed the matrix. No post-conformance production repair was required. Parent inspected final portrait, narrow controls and desktop screenshots for synthesis.

Live matrix: 320/390 portrait, 844 × 390 landscape, 820 × 1180 tablet and 1440 × 900 desktop. All six presets were exercised at 320px. Zoom buttons, pointer drag, focused keyboard arrow/minus, curtain/reset, custom status and native selection worked. Page scrolling over the canvas moved by 284px; one canvas and Front/open-curtain state survived portrait → landscape → portrait. No horizontal overflow. All manipulation targets were 48 × 48px; selector and curtain were 124.5 × 48px at 320px. Wider layouts retain their prior presentation. Fresh warning/error console entries were empty.

Command checks: 12 real-geometry scene/model tests, focused ESLint, TypeScript and production build pass. Added tests project complete booth bounds at three phone widths and four exterior angles, check interior framing without camera translation/model rebuild, and verify touch intent/cancellation. Full-project lint still reports the pre-existing unrelated hook-naming error in `scripts/online-photobooth-preview.test.mjs:39`; that file was not changed.

### Verification limits

Live input testing used pointer/wheel automation, not physical touchscreen hardware. Forced unavailable-WebGL and browser reduced-motion overrides were not exercised live; corresponding failure/disposal and reduced-motion boundaries have unit/source coverage. No wider-layout pixel-diff or all-device performance claim. Normal vertical page scrolling is part of the design, including short landscape screens. One fresh responsive reviewer covered the viewport matrix; this was not separate per-device reviewer ownership. No deployment or commit.

Handoff and detailed evidence: `.handoffs/booth-portrait-inline/README.md`, `conformance.md`, `responsive-qa.md`, `responsive-evidence.json`, and `validation.md`.

final result: passed

## Online camera workspace — 2026-09-18

**Findings:** no remaining actionable P0/P1/P2 issue in the implemented camera layout and tested browser flow. Physical-camera verification remains a limit.

### Visual authority and comparison

Source: `.handoffs/camera-capture-ideation/concept-1-centered-countdown.png` (1464 × 1075 board), plus the user's explicit instruction to remove the circle around the countdown. This supersedes the ring in the raster reference. Existing DESIGN.md typography and colors were retained.

Opened the source board and rendered mobile/desktop screenshots together in the same comparison input, and compared the iPad capture separately alongside the same board. This is a paired-image inspection, not a pixel-diff or fabricated composite. Device panels within the board are generated, scaled illustrations; actual CSS viewports and JPEG capture dimensions match 390 × 844, 834 × 1194 and 1440 × 1024. Captures also cover 320 × 568 and 844 × 390. The browser reserves a 15px gutter; measured workspace widths are consequently 375, 819, 1425, 305 and 829px. No horizontal page overflow was observed.

Final evidence in `.handoffs/camera-capture-ideation/implementation/`:
- `mobile-countdown-final.jpg`, `mobile-ready-final.jpg` (390 × 844).
- `phone-320-ready.jpg` (320 × 568).
- `ipad-ready-final.jpg`, `ipad-countdown.jpg` (834 × 1194).
- `desktop-ready-final.jpg`, `desktop-countdown.jpg` (1440 × 1024).
- `landscape-ready.jpg` (844 × 390).
- `signature-mobile-ready.jpg` (390 × 844), `download.jpg` (834 × 1194).
- `responsive-metrics.json` records measured mobile/iPad/desktop geometry.

The portrait comparison uses Panorama Four, matching the mock. Ready screenshots include a selected-photo retake; their status copy intentionally differs from first capture. Countdown screenshots show photograph 1 and, in some cases, a five-second timer instead of the mock's photograph 2/three seconds. These are the same layout states with different session data. No photography fidelity claim: the in-app browser supplies a black stream with a camera-unavailable graphic, rather than a physical camera image. It still provides video frames and permits exercising canvas capture. Final images are JPEG bytes saved with .jpg extensions.

Focused countdown/control review used the readable full-resolution 390px capture and computed styles: countdown text is rgba(251,248,242,0.6), transparent container, zero border. There is no circle or badge. A separate focused composite was unnecessary because those elements are legible in the full mobile frame. Number fade-in means screenshots can capture less than its steady-state opacity.

### Five fidelity surfaces

- **Typography:** Cormorant Garamond for brand/countdown, Manrope for controls; restrained serif numeral, legible labeled camera controls. Small-screen labels no longer wrap awkwardly.
- **Layout:** viewport-owned camera surface, compact header/progress, largest fitted exact-crop preview, persistent separate shutter dock. Secondary controls become invisible/inert during countdown while preserving their space. Short landscape screens use a side dock. Notice space is reserved. iPad preview bounds were exactly x84/y84/651×868 before and during countdown.
- **Colors/tokens:** existing off-white, cream, ebony and muted-brass tokens; translucent ivory number without a full-image veil. Exposure feedback is brief and removed for reduced motion.
- **Imagery/crop:** live video is the image source, not a generated photograph shipped as UI. Preview geometry matches existing 4:3 normalization followed by the selected template crop; portrait, wide and near-square templates have regression coverage. Landmark/face matching against actual phone camera output remains unverified.
- **Copy/content/icons:** Start/Resume, Stop, timer choices, Mirror, Flip camera, Exit and retake/review controls are functional. Phosphor icons are reused. Imported-photo fallback remains available, including a failed-camera retake.

### Comparison and repair history

1. Initial browser inspection found a moderate preview shift when a retake status message disappeared during countdown (P2). Reserved a stable notice area; repeated measurements on iPad show identical photo bounds in ready/capture.
2. At 320px, Flip camera and replacement labels wrapped awkwardly (P2). Kept the support label together, shortened the visible retake caption to Take photo N, and compacted the dock/header on short portrait screens. Post-fix evidence: phone-320-ready.jpg; all controls remain accessible.
3. Camera-switch failure during retake advertised import but only exposed Keep original (P2). Restored Import photos alongside Keep original. Subsequent camera-error/retry flow recovered without discarding accepted photos.
4. Final paired comparison retains the approved composition and the requested numeral-only override. Additional fullscreen, import, status and retake controls are necessary production behavior absent from the simplified mock.

### Functional and command validation

- Browser: automatic four-photo sequences on Panorama Four and Signature Strip; automatic transition to Arrange; select one photograph, capture replacement, accept it; retry/keep original; Stop during countdown; mirror toggle; five-second timer; unsupported rear-camera request/error and retry to previous source; Escape exits a retake while retaining originals; continue through filters to a prepared 1800 × 600 PNG link.
- Fullscreen control was exercised in the in-app browser; accessibility output switched to the workspace-only tree and back. This does not establish iOS/Android fullscreen compatibility.
- Browser warning/error log: [].
- `node --test scripts/online-photobooth.test.mjs scripts/online-photobooth-preview.test.mjs scripts/online-photobooth-gesture.test.mjs`: 39/39 passed, including camera release/stale permission, lens-selection recovery, enumeration failure, crop geometry, cancellation and transactional retake cases.
- Full `npm run lint`: passed. Focused ESLint passed. TypeScript `--noEmit`: passed. Final production `npm run build`: passed.
- No deploy or commit. Existing unrelated booth changes were preserved.

### Remaining limits

Actual mobile front/rear lenses, multiple physical webcams, bright/dark-face readability, browser permission prompts, physical touch input and mobile browser fullscreen behavior require device checks. Reduced-motion handling is implemented but was not browser-emulated. Partial-sequence preservation has reducer/timer tests; the attempted live partial-stop check finished its sequence before the interaction, so that particular browser assertion is not claimed. No independent reviewer was spawned.

final result: passed
