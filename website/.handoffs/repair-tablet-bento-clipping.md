# Tablet lead-bento clipping repair

Date: 2026-08-22  
Scope: bounded CSS-only repair in `website/src/components/UpperExperience.module.css`

## Change

- At `@media (max-width: 1024px)`, added an explicit tablet row template: `minmax(288px, auto) repeat(2, 160px)`.
- The 288px primary track is 24px taller than the QA-measured 264px minimum, so the absolutely positioned 263px lead-card copy block has buffer without shrinking the approved heading.
- Kept the six-column placement unchanged: primary `6x1`; secondary and tertiary each `3x2`. This retains the planned `6 + 6 + 6 = 18` occupied cells, 24px gap, inherited `grid-auto-flow: dense`, and clipped media frames.
- At `@media (max-width: 767px)`, added `grid-template-rows: none` so the tablet explicit tracks do not leak into the mobile layout. Mobile continues to use four columns, three `4x1` cards, 16px gaps, and intrinsic `minmax(340px, auto)` auto rows.
- No title, copy, asset, heading size, component structure, or unrelated style was changed.

## Verification

- `npm run lint` — passed (exit code 0). npm emitted only its existing warning about the deprecated `email` user config.
- `npm run build` — passed (exit code 0). Next.js 16.3.2 compiled, type-checked, generated static pages, and finalized successfully.
- Focused source inspection confirmed the tablet and mobile declarations above are present and the occupancy/span declarations remain unchanged.

## Residual risks

- This repair task did not perform or claim browser closure. Fresh gpt-taste and responsive tablet gate agents must render 768x1024, 820x1180, and 1024x768 to confirm the complete heading and paragraph remain visible with comfortable padding and that no void, overlap, or horizontal overflow was introduced.
- Existing uncommitted work outside this bounded CSS seam was preserved.

result: passed
