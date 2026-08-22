# Implementation result handoff 02

Date: 2026-08-22  
result: passed

## Scope and files

Production file changed:

- `website/src/components/UpperExperience.module.css`

Handoff written:

- `website/.handoffs/implementation-result-02.md`

No other production file, design authority, dependency, asset, copy, component structure, gutter, image crop, hero height, CTA geometry, print geometry, or interaction was modified by this implementation pass.

## Exact implementation diff

```diff
 @media (max-width: 767px) {
   .heroHeading {
     max-inline-size: 100%;
-    font-size: clamp(1.7rem, 7.2vw, 3.45rem);
   }
 }

 @media (max-width: 360px) {
-  .heroHeading {
-    font-size: 2.2rem;
-  }
-
   .cardCopy h2,
   .cardCopy h3 {
     font-size: 2.35rem;
```

The base `.heroHeading` remains `font-size: clamp(2.2rem, 7.5vw, 6rem)`, `width: 100%`, and `max-inline-size: 72rem`. The phone rule still applies `max-inline-size: 100%`. The 768–819px rule still applies `letter-spacing: -0.04em`; fresh rendering confirmed that it produces the required two-line tablet heading.

## Browser verification

The shared preview at `http://localhost:3011` was rendered and inspected with the requested in-app Browser after `document.fonts.ready` completed. The browser reserved 15px for its vertical scrollbar, so recorded client widths are 15px below the requested outer viewport widths. That matches the design plan's scrollbar allowance.

| Viewport | Client width | H1 size | Span lines | Total | Tracking | H1 width | Gutters L/R | CTA-to-print clearance |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 320×720 | 305px | 35.2px | 1 + 2 | 3 | -1.232px | 256.8px | 24 / 24.2px | 12px |
| 375×812 | 360px | 35.2px | 1 + 2 | 3 | -1.232px | 312px | 24 / 24px | 12px |
| 390×844 | 375px | 35.2px | 1 + 2 | 3 | -1.232px | 327.2px | 24 / 23.8px | 12px |
| 768×1024 | 753px | 57.6px | 1 + 1 | 2 | -2.304px (`-0.04em`) | 656.8px | 48 / 48.2px | 184.61px |
| 820×1180 | 805px | 61.5px | 1 + 1 | 2 | -2.1525px (`-0.035em`) | 708.8px | 48 / 48.2px | 236.61px |
| 1024×768 | 1009px | 76.8px | 1 + 1 | 2 | -2.688px | 912.8px | 48 / 48.2px | 440.61px |
| 1280×800 | 1265px | 96px | 1 + 1 | 2 | -3.36px | 1136.8px | 64 / 64.2px | 638.21px |
| 1440×1000 | 1425px | 96px | 1 + 1 | 2 | -3.36px | 1152px cap | 72.4 / 72.6px | 756.21px |

At every viewport:

- font status was `loaded` and the rendered family was Cormorant Garamond with the recorded fallbacks;
- the two source spans remained exactly `PHOTOGRAPHS,` and `DEVELOPED DIFFERENTLY.`, with punctuation intact;
- the H1 stayed inside the viewport, used visible overflow rather than clipping, and did not exceed three visual lines;
- `document.documentElement.scrollWidth === document.documentElement.clientWidth`;
- the print stayed horizontally inside the client viewport;
- neither CTA intersected the print;
- both hero CTA targets remained 48px high, above the 44px minimum.

The 320px and 768px hero renders were also visually inspected. The 320px heading retained dominant editorial hierarchy across the approved three lines, while the 768px heading rendered as exactly two lines. Neither render showed clipped punctuation, hidden text, CTA/print overlap, or damaged booth/print composition. Browser-console inspection returned no errors or warnings.

## Static verification

- `npm run lint` — passed.
- `npx tsc --noEmit` — passed.
- `npm run build` — passed; Next.js 16.3.2 produced the static `/` and `/_not-found` routes.
- Literal source sweep for `SECTION 0`, `QUESTION 0`, `ABOUT US`, `OUR STORY`, `₱8,500`, `3 HOURS`, `FOTOHVN SIGNATURE`, `trusted by`, `five-star`, and `5-star` — no matches under `website/src`.
- Obsolete responsive declarations — absent.

The only command noise was npm's existing warning about the user-level `email` config; it did not affect any gate.

## Deviations and conflicts

None. The revision was implemented exactly in its authorized CSS seam. No unsafe implementation conflict was found and no substitute decision was introduced.

## Fresh gate instructions

Run a fresh gpt-taste conformance verification agent next. It must treat plan version 1.1 as current authority, independently repeat the required browser measurements, and replace `website/.handoffs/gpt-taste-implementation-verification.md` with an exact `passed` or `blocked` result before responsive design QA resumes. Do not reuse the earlier verification result or its agent.
