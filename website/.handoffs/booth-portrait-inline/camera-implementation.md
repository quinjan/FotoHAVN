# Portrait camera and input implementation

Parent owns `src/components/booth/createBoothScene.ts` and `scripts/booth-scene.test.mjs`; separate UI implementation owns BoothExplorer TSX/CSS.

- Portrait exterior distance fits all eight corners of the actual cabinet bounds into 88% of the current camera frame. It responds to canvas aspect ratio and yaw/pitch. Existing zoom distance acts as a multiplier, so zoom controls retain intentional close-up behavior. Desktop/landscape use their original distance logic.
- Inside/Bench preserve their physical camera positions. Portrait vertical field of view expands to retain the useful horizontal framing of the original camera-wall and bench presets, rather than backing the camera through a wall.
- One shared portrait media condition matches the CSS. Resize/media changes reframe the existing renderer without rebuilding geometry or resetting selected view/curtain/zoom state.
- Touch gestures wait for horizontal intent before marking a custom view or changing yaw. Predominantly vertical touch movement stays with page scrolling; pointer cancellation and lost capture stop the drag. Desktop drag and existing keyboard controls remain supported.
- No model geometry, texture assets, reflection permissions, or resource lifecycle changes.

Twelve real-geometry tests pass using `node --test --test-isolation=none scripts/booth-scene.test.mjs`. Added coverage projects complete cabinet bounds at 305/375/415px across four exterior presets, checks portrait interior subjects and orientation without camera translation/model rebuild, and checks vertical/horizontal touch intent and lost capture. Existing nine scene/model tests still pass. Focused scene/tests ESLint passes.

Live route: http://localhost:3000/fotohavn#the-booth. Use existing in-app CUA, fresh hidden tab for independent review. Browser viewport is shared; one reviewer at a time. Read viewport capability documentation before setting it. Screenshot saving through `node:fs/promises` and `tab.screenshot({fullPage:false})` works; screenshot clip can include sticky chrome unexpectedly, so favor original full viewport evidence. Review selected `../booth-portrait-ideation/option-1-inline.png` alongside the actual browser capture.

Pending: fresh rendered plan conformance, responsive interactions, and final production build. Capture current real geometry faithfully; generated reference model polish is illustrative.
