# Narrow portrait control and hidden image-parent repair

Changed only `src/components/GuestAlbum.module.css` in production.

- At portrait widths up to 389px, reserve a 64px minimum height for the note button. The 14px label retains its existing 1.4 line height and padding, leaving enough space for both `Read the note` and the naturally wrapping `See the photograph`. This addresses the measured 320px section-height increase of 13.19px when flipping. The selected 390px composition keeps its existing 48px control height.
- Keep `.portraitImage` and `.neighbor`, the parents of the portrait viewer's fill images, positioned outside the portrait media query. Both layouts remain mounted during rotation, so delayed image loading should still find a positioned parent when the viewer is hidden. The visible portrait rules retain the existing relative active-image parent and absolute neighbor positioning.

Inspected the actual image parents in `GuestAlbum.tsx`, the approved plan, implementation notes, previous repair, and local Next.js CSS guidance. No browser checks were run in this repair pass. The parent must verify stable 320px photo/note section height, unchanged 390px appearance, and the reported Next Image parent-position warnings after rotation. Fresh plan-conformance review precedes the repaired browser checks.
