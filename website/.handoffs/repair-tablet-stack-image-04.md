# Tablet third-stack image loading repair 04

result: passed

## Scope

- Production scope honored: `website/src/components/MiddleExperience.tsx` only.
- No CSS, copy, asset, layout, interaction, or other production file was changed by this repair.
- This is a bounded repair result, not a gpt-taste or responsive-design gate pass.

## Exact change

Added a conditional `loading="eager"` contract to only the final `stackCards` image rendered by the existing map:

```tsx
loading={index === stackCards.length - 1 ? "eager" : undefined}
```

The final card is the existing `KEEP THE PHOTOGRAPH` card using `/images/experience-printed.png` and alt text `A physical FOTOHVN print arranged on warm paper.` The first two stack images continue to omit `loading` and render as `loading="lazy"`.

Preserved: the exact asset and alt text, `next/image`, `fill`, responsive `sizes`, media wrapper/CSS/aspect ratio, all stack copy, card order, GSAP behavior, tablet static fallback, desktop sticky behavior, and mobile static flow.

## Browser diagnosis and measurements

Shared implementation: `http://localhost:3011/` in a fresh in-app Browser session.

Pre-fix direct 820x1180 top load:

- Target image was about 8,210px below the viewport.
- `loading="lazy"`, `currentSrc=''`, `complete=false`, `naturalWidth=0`, `naturalHeight=0`.
- A successful natural-scroll attempt loaded the same optimizer candidate at `724x905`, isolating the defect to lazy request scheduling rather than the asset, `sizes`, CSS, or decode capability.

Post-fix direct 820x1180 top load:

- `loading="eager"`.
- `currentSrc=http://localhost:3011/_next/image?url=%2Fimages%2Fexperience-printed.png&w=1080&q=75`.
- `complete=true`, natural dimensions `724x905` while still about 8,210px below the viewport.
- After natural scrolling: image rectangle `top=184.25`, `bottom=803.45`; `visible=true`, `display=block`, `visibility=visible`, `opacity=1`.
- Centered reload restored `scrollY=8025.6` and the same `724x905`, complete, non-empty, visible image state.

Adjacent/responsive checks:

- Fresh direct 768x1024: `currentSrc` non-empty, `complete=true`, natural dimensions `672x840` from the top of the page.
- Fresh direct 1440x1000: `currentSrc` non-empty, `complete=true`, natural dimensions `835x1044`; stack card remains `position: sticky`.
- Fresh direct 320x720: `currentSrc` non-empty, `complete=true`, natural dimensions `224x280`; stack card remains `position: static`.
- Loading-contract isolation: first stack image `lazy`; second stack image `lazy`; third stack image `eager`.

Cross-repair 820px header evidence, captured read-only:

- Initial at `scrollY=0`: header `804.8x72`, Off-white `rgba(251, 248, 242, 0.97)` background, Ebony `rgb(30, 26, 23)` brand/navigation text, Ebony bottom rule.
- Scrolled at `scrollY=500`: header remains `804.8x72` with the same background, text colors, and rule; the scrolled class is added.
- No header production file was edited by this repair.

## Checks

- `node_modules/.bin/eslint.cmd src/components/MiddleExperience.tsx`: passed, exit 0.
- Full build and TypeScript were intentionally not run concurrently against the shared integration server/worktree.
- integrated-build pending: the orchestrator/fresh verifier must run the integrated lint/type/build sequence after all parallel repairs settle.

## Files changed

- `website/src/components/MiddleExperience.tsx`
- `website/.handoffs/repair-tablet-stack-image-04.md`

## Fresh verifier instructions

1. Open a brand-new direct 820x1180 Browser load at `http://localhost:3011/`.
2. Before scrolling, confirm the third stack image is `loading="eager"`, has a non-empty `currentSrc`, is `complete=true`, and has non-zero natural dimensions.
3. Naturally scroll until the third card's media is centered; require visible decoded pixels and visible/display/opacity states consistent with the measurements above.
4. Reload while centered and repeat the request, decode, natural-size, and visibility checks.
5. Repeat a fresh direct 768 tablet check plus desktop and 320 mobile regression checks.
6. Confirm the first two stack images remain lazy and the GSAP/static breakpoint behaviors are unchanged.
7. Run the integrated lint, TypeScript, and production build only after the parallel repairs are settled.
