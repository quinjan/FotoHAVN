# Card stack repair 01

Date: 2026-08-22
result: passed

## Scope

Repaired only the blocked desktop/no-reduced-motion GSAP Card Stacking behavior. The implementation in `MiddleExperience.tsx` and `MiddleExperience.module.css` was inspected and intentionally left unchanged because it already contains the exact planned `scale: 0.94`, `yPercent: 18`, scrubbed entry, increasing z-index, 96px base top, and 72px retained-header offsets.

## Files changed

- `website/src/app/globals.css`
- `website/.handoffs/repair-card-stack-01.md`

## Exact fix

The mandatory `<main className="overflow-x-hidden w-full max-w-full">` wrapper previously inherited Tailwind's `overflow-x: hidden`. Browser computation promoted its other axis to `overflow-y: auto`, so `<main>` became the nearest sticky scroll container even though the viewport performed the actual scrolling. That prevented every `.stackCard` from sticking to its planned viewport offset.

Added a more-specific `main.overflow-x-hidden` rule with:

```css
overflow-x: clip;
overflow-y: visible;
```

`clip` preserves horizontal spill protection, including at 320px, without creating a scrolling box. `visible` keeps the viewport as the sticky cards' scrolling context. The mandated literal wrapper class string remains unchanged.

The existing desktop stack geometry remains authoritative:

- card 1 top: 96px;
- card 2 top: 168px;
- card 3 top: 240px;
- each following slab therefore leaves exactly 72px of the preceding slab header visible;
- z-index increases from 1 through 3;
- GSAP entry values remain `scale: 0.94`, `yPercent: 18`, ending at `scale: 1`, `yPercent: 0`, with `scrub: true`;
- below 1024px and under reduced motion, cards remain static, untransformed, and gap-separated.

## Checks

- `npm run lint`: passed.
- `npx tsc --noEmit`: passed.
- `npm run build`: passed; Next.js 16.3.2 produced the static `/` route.
- `git diff --check -- website/src/app/globals.css website/src/components/MiddleExperience.tsx website/src/components/MiddleExperience.module.css`: passed (line-ending conversion warnings only).
- No repair-agent browser evidence was substituted for the required fresh independent in-app Browser gate.

## Fresh verifier instructions

Use the in-app Browser at desktop width with reduced motion disabled and inspect at least two scroll positions within the stack:

1. Confirm `<main>` computes to `overflow-x: clip` and `overflow-y: visible`, and that the viewport/document is the nearest scrolling context for the sticky slabs.
2. When card 2 enters, confirm card 1 remains at approximately 96px and card 2 settles at approximately 168px, leaving 72px of card 1's header visible.
3. When card 3 enters, confirm card 1/card 2/card 3 settle at approximately 96px/168px/240px with increasing z-index and readable retained headers.
4. Confirm the next card visibly enters from the bottom while preceding cards remain stacked and that the GSAP transform reaches `scale: 1`, `yPercent: 0` in its reading state.
5. Recheck tablet/mobile and reduced motion for normal gap-separated flow.
6. Recheck 320px with `document.documentElement.scrollWidth === document.documentElement.clientWidth`.

This repair result covers only the bounded implementation task. It does not assert that either independent conformance gate has passed.
