# 3D booth portrait exploration

Status: user selected option 1 (Larger inline studio). Implementation and verification are recorded in ../booth-portrait-inline/.

User asks for a larger, easier-to-manipulate 3D booth in mobile portrait, analogous to the portrait guest-board improvement. Product Design ideate was explicitly requested. All concepts work in portrait without mandatory rotation.

## Displayed image order (selection authority)

1. `option-1-inline.png` — Larger inline studio. Edge-to-edge stage, tighter camera framing, consolidated zoom/rotation controls, a single viewpoint chooser. Keeps normal page browsing; horizontal rotation must preserve vertical page scrolling.
2. `option-2-fullscreen.png` — Fullscreen explorer, recommended for manipulation. Image depicts the viewer after opening it from an Expand action. Large model, close control, zoom controls and compact bottom controls. Proposed full orbit and pinch-to-zoom require gesture implementation; they are not claims about current functionality. Close should preserve camera/curtain state. Landscape encouragement is optional.
3. `option-3-guided.png` — Guided close-ups. Large preset views, next/previous navigation, whole-booth return and access to free exploration. Image depicts Inside (5 of 6 existing presets). Makes details accessible without manual camera positioning.

Source ImageGen results respectively: `exec-96e82eb5-abb7-464d-912d-ce6ec36c0cd9.png`, `exec-7363cc70-8abe-43c1-9593-31dc8735b610.png`, `exec-5deb3702-37a1-40aa-bd53-333708dcd65f.png`.

## Grounding

- Current live `/fotohavn#the-booth` at a 390 x 844 browser viewport has a 325 x 235 CSS-pixel canvas within a 375px content width (reserved scrollbar). `current-portrait.png` was captured and inspected.
- Current mobile stage is 380px tall; the canvas subtracts 145px, leaving 235px for the model. Under 350px the stage is 350px. Header, labels and separate controls consume substantial additional vertical space.
- `BoothExplorer.tsx`, its CSS and `booth/createBoothScene.ts` inspected. Current touch dragging changes yaw, while pitch changes only for non-touch pointers. Existing zoom buttons, preset views, curtain and reset are available. Multi-touch pinch is a proposal in the fullscreen concept.
- Current model overview and inside screenshots under `.handoffs/booth-model-corrections/` were visually inspected and attached to ImageGen, alongside the current mobile screenshot. Use existing actual 3D geometry/materials when implementing; generated render polish is illustrative.
- Preserve walnut construction, cream curtains, photo display, print hatch, backed mirror, separate camera and monitor, two LED bars and existing bench arrangement. Preserve fallback, keyboard support and reduced motion.
- Brand: local DESIGN.md, warm ivory/off-white/ebony/soft brown, Cormorant Garamond + Manrope. Target concept frames are 390 x 844 logical pixels.

The user selected the first displayed result. The source image and ordering above remain the visual authority.

