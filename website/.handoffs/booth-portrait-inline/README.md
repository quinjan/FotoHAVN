# Larger inline 3D booth: completed

User selected the first displayed concept at `../booth-portrait-ideation/option-1-inline.png`. Implemented at http://localhost:3000/fotohavn#the-booth.

Phone portrait now has an edge-to-edge 480px canvas (previously 235px at the tested 390px viewport), actual-geometry camera fitting, compact rotation/zoom controls, one native viewpoint chooser, a curtain action and Reset. All six presets remain available. Inside/Bench adapt their field of view without moving the camera outside the cabinet. Resize/orientation changes preserve the scene and its state. Wider layouts retain their prior arrangement.

## Evidence

- `plan.md`: approved composition and interaction requirements.
- `ui-implementation.md` and `camera-implementation.md`: disjoint UI and scene implementation ownership.
- `conformance.md` and `conformance-reference-comparison.png`: fresh independent source/visual conformance, passed.
- `responsive-qa.md` and `responsive-evidence.json`: separate fresh live responsive/interaction review, passed. Includes all six views at 320px, pointer/keyboard controls, zoom, curtain/reset, state across orientation, desktop/tablet/landscape, no horizontal overflow and clean fresh console.
- `qa-390-tall-preview.png`: presentation screenshot of the model and complete primary control set, using a taller capture. Normal page scrolling remains part of the design.
- `validation.md`: 12 scene/model tests, scoped ESLint, TypeScript and production build pass. Full-project lint still reports the unrelated pre-existing online-preview test hook error.

## Scope and limits

Changed BoothExplorer.tsx, BoothExplorer.module.css, booth/createBoothScene.ts and scripts/booth-scene.test.mjs only, plus task handoff/QA notes. Existing geometry, textures, reflection behavior, fallback, and other sections were preserved. No deployment or commit.

Physical touchscreen behavior, a forced unavailable-WebGL screen, and browser reduced-motion emulation were not tested live. Unit tests cover touch intent, geometry projection, failure/disposal and reduced-motion boundaries. No all-device performance or wider-layout pixel-diff claim is made. The existing larger controls and original rendered model differ in small stylistic details from the generated reference; independent conformance found no actionable P0/P1/P2 mismatch.

final result: passed
