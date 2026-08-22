# Responsive hero-wrap repair 03

Date: 2026-08-22  
Scope: reconciled P2 early hero wrapping only  
result: passed

## File changed

- `website/src/components/UpperExperience.module.css`

No TSX, copy, assets, gutters, hero-print geometry, CTA geometry, or other production files were changed by this repair.

## Exact CSS

The mobile hero keeps the authored spans wrappable and uses a readable responsive display size that fits the long second span once the layout has more room than the planned 320px exception:

```css
@media (max-width: 767px) {
  .heroHeading {
    max-inline-size: 100%;
    font-size: clamp(1.7rem, 7.2vw, 3.45rem);
  }
}
```

The existing later `@media (max-width: 360px)` rule continues to set `font-size: 2.2rem`, preserving the planned natural three-line 320px composition. The 768px edge needed only a small tracking correction:

```css
@media (min-width: 768px) and (max-width: 819px) {
  .heroHeading {
    letter-spacing: -0.04em;
  }
}
```

No `white-space: nowrap`, clipping, transform scaling, or gutter reduction was introduced.

## In-app Browser measurements

Preview: `http://localhost:3011/`. Measurements were taken after `document.fonts.ready` using range rectangles for each authored span. The browser's persistent scrollbar makes `clientWidth` 15px narrower than the requested viewport; line-count acceptance was evaluated against the actual rendered geometry.

| Requested viewport | Visible H1 lines | Computed H1 size / tracking | H1 width | document scroll/client width | Clipped | Nearest CTA-to-print clearance |
|---|---:|---|---:|---:|---|---:|
| 320 x 720 | 3 | 35.2px / -1.232px | 256.8px | 305 / 305 | no | 12px vertical |
| 375 x 812 | 2 | 27.2px / -0.952px | 312px | 360 / 360 | no | 12px vertical |
| 390 x 844 | 2 | 28.1088px / -0.983808px | 327.2px | 375 / 375 | no | 12px vertical |
| 768 x 1024 | 2 | 57.6px / -2.304px | 656.8px | 753 / 753 | no | 184.61px horizontal |
| 820 x 1180 | 2 | 61.5px / -2.1525px | 708.8px | 805 / 805 | no | 236.61px horizontal |
| 1024 x 768 | 2 | 76.8px / -2.688px | 912.8px | 1009 / 1009 | no | 440.61px horizontal |
| 1280 x 800 | 2 | 96px / -3.36px | 1136.8px | 1265 / 1265 | no | 638.21px horizontal |
| 1440 x 1000 | 2 | 96px / -3.36px | 1152px | 1425 / 1425 | no | 756.21px horizontal |

Both authored strings and punctuation remained exact: `PHOTOGRAPHS,` and `DEVELOPED DIFFERENTLY.`. Visual Browser inspection at 320, 375, and 768 confirmed the intended Artistic Asymmetry hierarchy, intact 24px/48px gutters, no CTA/print collision, and no heading clipping.

## Static checks

- `npm run lint` — passed.
- `npx tsc --noEmit` — passed.
- `npm run build` — passed; Next.js 16.3.2 completed its optimized static build. An initial concurrent-build lock cleared on retry and did not recur.
- The only command noise was npm's pre-existing non-fatal warning about the user-level `email` config.

## Regressions and remaining work

- No regression was observed within this bounded seam.
- This repair does not claim either independent orchestration gate passes.
- Fresh verification must re-run gpt-taste conformance first, then responsive Browser QA at all named viewports. It should independently remeasure range-based span line counts, punctuation/clipping, document overflow, and the 12px small-phone CTA/print clearance rather than relying on this handoff as gate evidence.
