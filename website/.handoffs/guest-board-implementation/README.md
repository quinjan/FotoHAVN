# On the Other Side — completed implementation

Current collection: a subsequent user request expands this to 18 smaller photographs and six keepsake placements. See `../guest-board-expanded/README.md` and the root QA report for the current layout, source map and checks; earlier counts below are historical.

Latest update: the user subsequently requested a busy overlapping collage. The original grid description below is historical; current layout and verification are recorded in `../../design-qa.md` with screenshots in `../guest-board-collage/`.

A further update adds two plush charms, a ribbon, and a flower-ticket keepsake without increasing the ten-photo collection. Sources and prompts are in `../guest-board-keepsakes/assets.md`; the root QA report describes the latest checks.

2026-09-10. Preview: http://localhost:3000/FOTOHAVN#guest-album

The user selected displayed option 2 and explicitly approved editorial captions instead of guest testimonials for now. The approved image is `approved-design.png`; the scoped implementation contract is `plan.md`. Selection history is preserved in `../guest-board-ideation/brief.md`.

## Delivered

- One responsive grey metal board with a silver frame and small raster clips, retaining FOTOHAVN's warm editorial typography and palette.
- Ten native, keyboard-accessible photo buttons: six existing Evia photographs and four complete published 2x6 strips from the user-supplied temporary website.
- A selected print lifts from its board position into a readable viewer. Turn it over for an explicitly labeled editorial note; browse previous/next; animate back to the board.
- Full-window, scrollable board mode using an edge-to-edge modal. Every print remains clickable; browser chrome stays available.
- Two-column phone, four-column tablet and five-column desktop boards. Complete strips remain uncropped. No mandatory dragging or tiny scaled desktop canvas.
- Native modal containment, nested Escape, focus restoration after React clears board inertness, scroll-lock cleanup and reduced-motion behavior.

## Code map

- `src/components/GuestAlbum.tsx` and `GuestAlbum.module.css`: board, viewer and fullscreen interaction.
- `guestBoardData.ts`: ten photographs, editorial text, pure navigation state and an empty-by-default approved-testimonial extension point.
- `guestBoardMotion.ts`: finite lift geometry and reduced-motion handling.
- `GuestBoardPhoto.tsx`: existing responsive image integration and readable load-failure fallback.
- `src/app/globals.css`: stable scrollbar gutter to reduce layout shifting during modal scroll locking.
- `scripts/prepare-board-strips.mjs`: reproducible strip derivatives.
- `scripts/guest-board.test.mjs`: data, navigation, motion, assets and server-markup checks.
- `scripts/compare-guest-board.mjs`: actual source/render comparison artifacts.

## Assets and provenance

See `source-assets.md` for the four published source URLs and actual dimensions; originals are retained in this folder. `board-asset.md` and `clip-asset-note.md` retain exact built-in ImageGen prompts and output details. Generated assets are only the empty board and clip, not the guests. The small clip is an opaque grey-backed fallback; genuine alpha generation failed and is not claimed. Runtime uses `silver-clip-small.webp`, not the large archival clip.

All notes are editorial descriptions. No names or customer quotations were invented or published. Add a testimonial only with approved text, attribution and appropriate guest permission.

## Verification and limits

See `../../design-qa.md` for visual comparisons, responsive evidence, repaired findings and limits. The previous root hero report is archived as `previous-design-qa.md`.

- Board tests 8/8, existing hero 12/12 and booth 4/4: 24/24 passed.
- Full lint, standalone TypeScript and final production build passed.
- Live checks include all ten fullscreen selections; phone 320x720; tablet 820x1180; desktop; full strips; keyboard Enter/Escape; exact focus restoration; scroll cleanup; final production console with no warnings/errors.
- Physical-device touch, OS reduced motion, screen readers and browser text zoom were not exercised. Reduced motion is fixture/source verified. Fullscreen is a verified full-window board view, not an OS-level fullscreen takeover.
- No independent review pass is claimed. Asset-generation agents produced only their bounded raster assets; main agent integrated and verified the result.

## Workspace handoff

Branch remains `codex/website-real-booth-redesign`. Work is uncommitted; no push, deployment, external message or form submission was performed. The repository already contained a broad dirty redesign and unrelated changes; these remain preserved. No hero, Editorial Lift or 3D redesign was undertaken in this board pass. The user's development server remains on port 3000. The task-owned port-3002 production smoke server was stopped after verification; its exact process identity was checked before stopping.
