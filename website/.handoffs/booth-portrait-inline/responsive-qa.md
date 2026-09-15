# Fresh responsive QA

**PASS — no P0/P1/P2 findings in the exercised matrix.** Independent live review after conformance PASS, 2026-09-15. No production edits.

## Verified live

- **320 × 568 portrait:** all six presets exercised and captured. Overview, Front, Side and Back retain the complete exterior silhouette; Inside clearly shows camera/screen/light strips; Bench clearly shows seat/back curtain. Actual canvas is **305 × 480 CSS px**, because this browser reserves 15px for its scrollbar.
- Four manipulation targets are **48 × 48px**. Native chooser and curtain action are **124.5 × 48px**, with 16px text, in that stricter 305px content width. **Custom view** and **Close curtain** remain fully readable side by side; Reset is 48px high. Screenshot `qa-320-custom-controls.png` records the longest labels.
- Native presets update status and descriptions; Inside/Bench open the curtain. Rotation produces Custom view. Curtain close/open, Reset, zoom-in/out buttons, focused ArrowRight rotation and keyboard minus zoom were exercised. Reset restores Overview/closed curtain. Keyboard canvas focus outline is visible (`qa-320-keyboard-focus.png`).
- Real pointer drag changed the camera and selected Custom view. Scrolling over the canvas moved the page from scrollY 2582 to 2866 without blocking page scrolling. Live canvas computed `touch-action` is `pan-y` (its parent is `auto`).
- **390 × 844 portrait:** overview plus Front/open-curtain controls inspected and captured. **390 portrait → 844 × 390 landscape → 390 portrait** retained Front and open curtain; Close curtain was still actionable on return and then correctly closed it. One canvas remained throughout all measured states.
- **844 × 390 landscape, 820 × 1180 tablet, 1440 × 900 desktop:** original inset stage, viewpoint buttons and supporting layout return; model and text remain legible. Landscape requires normal vertical scrolling at its short viewport height.
- Every measured viewport had `scrollWidth === clientWidth`: no horizontal page overflow. Fresh tab console had **zero warning/error entries**.

## Evidence and limits

`responsive-evidence.json` contains viewport, canvas, controls, state, scroll and console evidence. `qa-320-{overview,front,side,back,inside,bench}.png` records all narrow presets. Additional captures document controls, zoom, focus and wider layouts. `qa-390-tall-preview.png` is a **390 × 1050 presentation capture** including heading, model, controls and Reset; it is not evidence of fitting a normal phone viewport.

Physical mobile touch scrolling was **not** tested: the live check used pointer/wheel automation. Forced unavailable-WebGL and reduced-motion states were **not** exercised live. Parent-reported 12 real-geometry tests cover input/framing/failure/motion boundaries; this reviewer did not rerun them. No baseline pixel-diff claim is made for the wider layouts.

Browser ownership released immediately after checks: viewport override reset and reviewer-created hidden tab closed.
