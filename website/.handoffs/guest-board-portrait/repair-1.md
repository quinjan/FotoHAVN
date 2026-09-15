# Portrait neighbor photograph repair

Changed only the decorative neighbor rules in `src/components/GuestAlbum.module.css`.

- Increased each neighbor's nominal edge peek from 20px to 28px, within the approved 16–28px range.
- Aligned the left neighbor's photograph to its right edge and the right neighbor's photograph to its left edge. This places the photograph against the viewport-facing white border instead of hiding it behind a centered letterbox margin.
- Retained `object-fit: contain` for both the active photograph and decorative neighbors, preserving original strip proportions. White borders, tilt, stage dimensions, active print, interactions, and state remain unchanged.

Reviewed the selected mock and CSS. No browser checks were run in this repair pass; fresh plan-conformance review and responsive QA remain the next gates.
