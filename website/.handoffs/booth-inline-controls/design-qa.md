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
