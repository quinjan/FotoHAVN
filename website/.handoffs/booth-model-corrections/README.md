# Booth model — physical layout corrections

Implemented on `codex/website-real-booth-redesign`, 2026-09-10.

Preview: http://localhost:3000/FOTOHAVN#the-booth

## User-authorized corrections

- Front-right panel is an opaque planar mirror with solid timber backing. It reflects the scene and a simple virtual daylight studio; it does not request webcam access. Reflections change with the viewing angle.
- Two identical vertical LED bars flank the monitor.
- Camera aperture and monitor are separate modules set into the existing timber wall. Only the monitor retains its own thin bezel; the shared black console is removed.
- Bench runs along the right wall, facing the left-wall monitor/camera. Seat and legs share the booth's exact wood material and texture. A fixed pleated backdrop hangs on the right wall above/behind the bench.
- Rear wall is replaced by two timber side panels framing a central curtain opening. Front and rear entry curtains share their fabric, pleats, hem, and opening motion. Each gathers to the right when viewed from outside. The backdrop stays closed when the entry curtains open.

## Implementation

- `src/components/booth/createBoothModel.ts`: model geometry, material sharing, mirrored entry-curtain geometry, reflector resource disposal.
- `src/components/booth/createBoothScene.ts`: reflection-only surroundings, Back and Bench presets, wider Bench field of view.
- `src/components/BoothExplorer.tsx`: Back/Bench buttons and accurate descriptions. Existing rotation, keyboard, zoom, reset, curtain animation, and photograph fallback controls retained.
- `scripts/booth-scene.test.mjs`: nine tests using real Three.js geometry/materials, with browser/GPU boundaries substituted.

The redesign skill kept changes targeted to the existing model and explorer. No hero, guest-board, global styling, dependency, or deployment changes in this pass. Proportions remain illustrative, not surveyed dimensions. Rear side panels are timber; no additional rear photo display or mirror was inferred.

The mirror uses the installed Three.js [Reflector addon](https://threejs.org/docs/pages/Reflector.html), with a bounded 256 × 512 reflection target and no MSAA. Reflection scenery is on a separate camera layer, absent from the ordinary exterior/interior view. Reflection target, materials, geometry, listeners, observers, and renderer are disposed with the explorer.

## Verification

- `npm run test:booth`: 9 passed, including opacity/backing, camera-to-monitor wood gap raycast, symmetric LEDs, bench/backdrop orientation and material, matching curtain deformation, clear front-to-rear passage raycast, Bench framing, presets, cleanup, failures, and reduced-motion settling.
- `npm run test:hero`: 12 passed.
- `npm run test:board`: 8 passed.
- `npm run build`: passed, including TypeScript.
- Focused ESLint on the four modified implementation/test files: passed.
- Live in-app browser: desktop front/overview/inside/bench/back, rear open/closed animation endpoints, 820px tablet rear view, 320px capture wall and bench. At 320px, viewport buttons are 44px tall and at least 50px wide; page clientWidth and scrollWidth both 305px (reserved scrollbar gutter). At 820px both are 805px.
- Photograph toggle, return to 3D, keyboard rotation, reset, and console inspected. No runtime errors. Next emits a development-only LCP loading-priority warning when the existing photograph fallback is deliberately shown above the fold; not a 3D failure.
- Temporary viewport override reset. Existing dev server remains running on port 3000. No commit, push, or deployment.

## Browser evidence

- `desktop-overview.png`, `desktop-front-mirror.png`
- `desktop-capture-wall.png`, `desktop-bench.png`
- `desktop-rear-open.png`, `desktop-rear-closed.png`
- `phone-capture-wall.png`, `phone-bench.png`
- `tablet-rear-open.png`

Browser-rendered images verify the visual result; automated scene tests do not pretend to validate GPU rendering. Broader hero/board browser flows were not rerun in this focused model pass.
