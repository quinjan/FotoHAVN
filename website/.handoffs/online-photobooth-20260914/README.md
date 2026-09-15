# Experience redesign and online photobooth

The user selected Option 2, “A Study in Light,” and authorized its implementation on 2026-09-14. The selected Experience design and complete four-stage online booth are implemented locally. Fresh final conformance, desktop/tablet/mobile reviews, and final QA synthesis all passed with no unresolved actionable P0/P1/P2 findings. See [final-verification.md](final-verification.md) for that implementation's consolidated evidence and verification limits. A later user-directed image/copy refinement is recorded separately in [../experience-refinement-20260914/README.md](../experience-refinement-20260914/README.md) and supersedes the original first-two image descriptions and third caption below.

## Preview

- Experience: http://localhost:3000/fotohvn#experience
- Online booth: http://localhost:3000/fotohvn/online

The preview runs from `website/`. Its current hidden development server binds to IPv4 loopback on port 3000; browser links intentionally use `localhost`. See `browser-verification.md` for runtime evidence. No deployment or commit was requested.

## Delivered behavior

The Experience section uses three original generated still-life images: curtain and walnut, optical glass, and an abstract printed keepsake. The selected editorial composition leads into “Your next photograph starts here” and “EXPERIENCE FOTOHVN ONLINE.” The shared navigation and the physical-print section provide additional entry links.

The online booth follows Layout → Photographs → Look → Download. It offers four layouts, four designed frames, and four original photographic looks. All photo slots can be retaken individually with a keep-original choice, and photographs can be rearranged by pointer, tap placement, or keyboard. Switching to fewer slots retains extra photographs for later placement. The complete selected frame is visible during filter selection. The final image and download use the same full-resolution PNG blob.

Camera capture uses an explicit video-only request, a three-second countdown, a stop control, and consistent mirroring. Device-photo import also supports the complete flow. Source photographs and the generated composition remain in browser memory; the implementation adds no photograph upload or storage service. Motion includes section reveals, stage transitions, countdown feedback, placement feedback, and a short print reveal, with reduced-motion handling.

## Starter presets

`src/components/onlinePhotobooth/presets.ts` owns the versioned geometry and original online starter choices. These are not represented as copies of the physical booth's final presets.

| Category | Choices |
| --- | --- |
| Layout | Long Strip (4), Story Strip (3), Pair (2), Contact Sheet (4) |
| Frame | Ivory, Archive, Walnut, Gallery |
| Look | Original, Warm Paper, Silver, Soft Fade |

`compositor.ts` applies the selected look to photograph windows and renders the authored paper, borders, and lettering into the PNG. Original source pixels remain available for changing looks and layouts. `session.ts` owns transactional replacement, ordering, and retained photographs; `useCamera.ts` owns camera lifecycle. Future physical-booth presets should be mapped into these versioned definitions and checked against the real print geometry and color output.

## Evidence and authority

- `selected-design.md`: user selection, exact copy, resolved scope and acceptance requirements.
- `visual-2.png`: selected design reference.
- `interaction-plan.md`: detailed workflow and state design.
- `references-and-visuals.md`: web references and generated-image direction.
- `experience-implementation-result.md` and `online-implementation-result.md`: implementation ownership and details.
- `repair-1-result.md`: pointer placement repair and result-specific filenames.
- `repair-2-result.md`: filename compatibility fallback and regression evidence.
- `browser-verification.md`: root browser checks and explicit test limits.
- `gpt-taste-implementation-verification-recheck.md`: passed fresh conformance against the final repaired source. The earlier report preserves its pre-repair blocked baseline.
- [responsive-desktop.md](responsive-desktop.md), [responsive-tablet.md](responsive-tablet.md), and [responsive-mobile.md](responsive-mobile.md): passed fresh viewport reviews, including 320px.
- [final-verification.md](final-verification.md): passed fresh synthesis, repair closure, integrated checks, and remaining evidence limits.
- [website/design-qa.md](../../design-qa.md): current scoped QA result followed by the preserved earlier guest-board report.

Post-repair code checks: 39/39 combined hero, photobooth, gesture, and filename tests passed. Repository lint and TypeScript passed. The final production build passed and generated the homepage and online route. Fresh independent conformance passed with no actionable P0/P1/P2 findings, including an independent 13/13 gesture and filename regression run and the full Contact Sheet/Gallery/Silver flow through a matching 1800 × 1500 image and download blob. Fresh responsive reviews and final synthesis passed against the same final source.

The browser tool did not expose a saved download artifact. The final image's dimensions, PNG composition contract, and identity with the download link were verified; an actual saved file is not claimed as verified. Physical camera hardware was not exercised. These two manual checks remain explicit test limits, not known implementation failures.
