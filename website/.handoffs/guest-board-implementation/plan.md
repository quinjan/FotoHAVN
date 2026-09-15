# On the Other Side — implementation contract

User selected displayed option 2, `approved-design.png`, then added: every posted image must be clickable; use 2×6 strips from https://sites.google.com/view/fotohavnph/home; animate the lift; let visitors view the board fullscreen. The user separately approved editorial captions for now. No fake customer quotes will ship.

## Scope

Replace only GuestAlbum, retaining #guest-album, the Look at you heading, warm palette and Cormorant/Manrope. Preserve hero, Editorial Lift, 3D explorer and other sections. Existing Next app and dependencies remain; no scaffold/migration/deployment. Branch remains codex/website-real-booth-redesign, with unrelated dirty work preserved.

## Assets

- Six existing guest photographs, unchanged; four full published Photo Strips downloaded from the supplied temporary site. Keep original strip ratios and all four frames; no cropping into individual faces. Source mapping: source-assets.md.
- Two ImageGen asset agents prepared the empty grey/silver board and silver bulldog clip from the selected visual. No baked-in decorative photos: every print on the board is a real interactive element. Border-image nine-slicing preserves raster metal-frame corners on responsive boards.
- The clip generator did not produce real alpha after two requests. A grey-backed, tightly cropped small raster clip is used only on the grey board. The lifted print leaves its clip behind. The original/fallback and exact prompts are preserved; no fake checkerboard is shipped. Runtime derivative is silver-clip-small.webp, not the large lossless archival file.

## Interaction

Every photo/strip is a named native button. Activation lifts the selected image from the clicked rectangle into a paper viewer. Flip exposes an editorial note on the reverse; front/back semantic visibility is synchronized. Next/previous cycle through all ten records. Closing animates toward the origin and restores focus. Reduced motion skips travel and makes the flip immediate.

Fullscreen opens an edge-to-edge, scrollable modal board filling the browser viewport. Browser chrome remains available; no OS-level fullscreen claim is made. Opening a print from fullscreen retains the board and its scroll position behind the viewer. Escape closes the print first, then fullscreen. Native dialog provides modal focus containment; explicit focus restoration, scroll-lock cleanup and unmount cancellation are required. The initial native-Fullscreen-API enhancement was removed after checking that dialog elements are ineligible; the tested full-window board remains the intended interaction.

## Responsive behavior

Desktop has a five-column, two-row composition of varied strip/portrait sizes with mild rotations. Tablet uses four columns. Phone uses two readable columns and natural vertical board scrolling. No tiny scaled desktop canvas, dragging requirement or offscreen persistent controls. A photo viewer remains readable and its controls stay outside the turning paper; short viewports can scroll the modal.

## Intentional deviations from the concept

Real supplied imagery replaces regenerated miniature mock photos. Editorial notes replace sample testimonials with the user's approval. Fullscreen controls are added by request; all ten records are present, so no fake More photographs button is needed. Controls use the canonical 4px radius rather than the mock's incidental pill. The clip remains on the board as the print lifts.

## Gates

Inspect source/render together at matched state, then desktop/tablet/320 mobile layouts. Test every print in fullscreen, photo/note/next/previous/return, keyboard and nested Escape, focus and scroll restoration, whole strips, zero overflow and console. Run test:board, existing hero/booth tests, lint, typecheck/build. Preserve QA history and report actual limits rather than claim physical-device/native-API coverage not available here.
