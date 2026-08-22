# Desktop responsive Product Design QA — pass 3

Date: 2026-08-22  
Scope: fresh desktop QA at 1440 x 1000 and 1280 x 800  
Implementation: `http://localhost:3011/`  
Production files modified: none  
result: passed

## Findings

No actionable P0, P1, or P2 remains in the fresh desktop render.

The repair-04 initial header surface closes the pass-2 contrast finding at both desktop widths. The eager third stack image renders decoded pixels before and during the desktop stack state. The mobile-only stack-heading type repair remains isolated below 768px and does not change, clip, or collide with desktop typography.

## Source and comparison metadata

- Source visual/design truth: `website/.handoffs/gpt-taste-design-plan.md` version 1.1, supported by `website/DESIGN.md` where not overridden.
- Passed conformance authority: fresh `website/.handoffs/gpt-taste-implementation-verification.md`, verifier pass 8.
- Historical responsive inputs: `website/design-qa.md`, `website/.handoffs/design-qa-synthesis-02.md`, and `website/.handoffs/design-qa-desktop-02.md`.
- Repair history inspected: `repair-initial-header-contrast-04.md`, `repair-tablet-stack-image-04.md`, and `repair-mobile-stack-headings-04.md`.
- Implementation truth: new browser-rendered captures from fresh Codex in-app Browser tabs after fonts loaded.

The source authority is a written design/implementation specification, not a fixed bitmap mock. Source pixels, source CSS dimensions, source density, density normalization, and a same-frame source-image composite are therefore `n/a`. The implementation was compared against the plan's explicit desktop typography, grid, spacing, color, image, component, interaction, accessibility, GSAP, copy, and ban decisions.

| Requested viewport | Browser layout | Representative pixels | Density | States |
|---|---|---|---|---|
| 1440 x 1000 | `innerWidth=1440`, `clientWidth=1425`, `clientHeight=1000` | viewport `1425 x 990`; full page `1424 x 13,638` | `devicePixelRatio=1.25` | initial/scrolled header, hero, bento hover, accordion, marquee, gallery pin, stack/third slab, carousel, form, footer |
| 1280 x 800 | `innerWidth=1280`, direct client width 1280; final reload client width 1265 after scrollbar allocation | hero `1265 x 791`; full page `1264 x 12,415` | approximately `1` | initial/scrolled header, hero, eager third asset, desktop type isolation, overflow, reload |

The small requested/client/PNG differences are in-app Browser scrollbar and capture-surface effects. No fidelity finding was filed from browser chrome or density.

## Full-view evidence

- `website/design-qa/responsive-qa-pass3-desktop-full-1440x1000.png`
- `website/design-qa/responsive-qa-pass3-desktop-full-1280x800.png`
- `website/design-qa/responsive-qa-pass3-desktop-hero-1440x1000.png`
- `website/design-qa/responsive-qa-pass3-desktop-hero-1280x800.png`

Full-page stitching proves page reach only; focused viewport captures are authoritative for sticky and GSAP spatial judgments.

## Focused evidence

- Initial/scrolled header: `responsive-qa-pass3-desktop-initial-header-1440x1000.png`, `responsive-qa-pass3-desktop-header-scrolled-1440x1000.png`.
- Bento/accordion/marquee: `responsive-qa-pass3-desktop-bento-hover-1440x1000.png`, `responsive-qa-pass3-desktop-accordion-1440x1000.png`, `responsive-qa-pass3-desktop-marquee-paused-1440x1000.png`.
- GSAP: `responsive-qa-pass3-desktop-gsap-gallery-1440x1000.png`, `responsive-qa-pass3-desktop-card-stack-1440x1000.png`, `responsive-qa-pass3-desktop-card-stack-third-1440x1000.png`.
- Conversion/footer: `responsive-qa-pass3-desktop-carousel-note3-1440x1000.png`, `responsive-qa-pass3-desktop-form-filled-1440x1000.png`, `responsive-qa-pass3-desktop-footer-1440x1000.png`.

All focused paths above are under `website/design-qa/` and were created with the required `responsive-qa-pass3-desktop-` prefix.

## Repair-04 closure

### Initial and scrolled header surface

- At 1440 initial state: header is sticky at top 0, `1424.8 x 72px`, background `rgba(251,248,242,0.97)`, Ebony text, and an Ebony/hairline lower rule. Brand is `121.26 x 44px`, Cormorant 26px/600.
- At 1440 after scrolling to EXPERIENCE: the same surface, rule, 72px height, sticky geometry, and zero horizontal overflow remain. The target settles at top 88px below the 72px header.
- At 1280 initial and scrollY 632: header remains `1280 x 72px` with the same surface and rule.
- The documented conservative Ebony-on-surface contrast range is 15.27:1 to 16.33:1, exceeding 4.5:1 for every small navigation label and 3:1 for the large brand. The Off-white-on-Ebony primary action remains 16.30:1.

### Desktop third stack image

- Fresh 1440 top load: only the third image is eager, `currentSrc` is the optimizer result for `/images/experience-printed.png`, `complete=true`, and natural size is `835 x 1044` before scrolling.
- Visible third-card state: image is opacity 1, visible, decoded at `835 x 1044`, fills a positive `726.58 x 709.35px` media region, and visually renders the intended printed-experience crop.
- Fresh 1280 top load: eager, complete, non-empty optimizer URL, natural size `742 x 927`.
- Final normal 1280 reload repeats the complete `742 x 927` eager state with no failed image.

### Mobile-heading repair isolation

- Desktop stack headings remain on the original desktop sizing: 72px at 1440 and 64px at 1280, Cormorant Garamond 500, with the original 0.94 line-height and -0.025em tracking.
- In the visible 1440 third card, the second text-range line ends at x=597.30 while the media begins at x=617.05, leaving approximately 19.75px and producing no text/media collision or clipping.
- The responsive mobile clamp did not leak into desktop, and document width equality remains intact at both widths.

## Required fidelity surfaces

- Fonts and typography: passed. Cabinet Grotesk resolves for 16px/26.4px body/interface copy; Cormorant Garamond resolves at 500 for display. The hero remains 96px/86.4px with -3.36px tracking and exactly two one-line authored spans at both desktop widths. No punctuation, heading, label, or body copy clips.
- Spacing and layout: passed. The 1280px content container uses 64px outer gutters at 1280 and approximately 72.4px when capped at 1440. Major sections retain cinematic spacing. Bento is exactly twelve 84.6625px columns at 1440, two 280px rows, 24px gaps, `grid-auto-flow:dense`, and 7x2 + 5x1 + 5x1 cards with no void.
- Colors and tokens: passed. The repaired header, warm Off-white/Ivory/Paper canvas, Ebony/Dark Walnut action surfaces, and restrained brass rules match the plan. No neon, fake gold, multicolor gradient, glow, or black-dominant treatment appears.
- Imagery and assets: passed. Required local booth, guest, strip, detail, and print assets render through `next/image` with meaningful alt text, stable frames, warm coherent crops, and no compression/masking blocker. No CSS/div art, emoji, handcrafted SVG substitute, remote stock, or placeholder is visible.
- Copy and content: passed. Fixed hero copy and truthful mall/event paths match plan v1.1. No public price, duration, package list, selectable-look claim, event-category claim, customer testimonial, rating, location, hours, response time, or service-area promise is rendered.
- Icons/shapes/shortcuts: passed. Icon use stays minimal; rectangles retain restrained 4px radii; slabs have no rotation or fake shadow; there are no bubbly cards, pills, badges, decorative blobs, fake avatars, or prompt leakage.

## Interaction, GSAP, accessibility, and resilience

- Navigation/anchors: desktop header actions reach their sections. EXPERIENCE settles at top 88px, clear of the 72px header. Initial and scrolled surfaces retain contrast, geometry, focus styles, and 44px+ target contracts.
- Clickable media: bento hover reaches `matrix(1.05,0,0,1.05,0,0)` after the specified `transform 0.7s ease-out` inside `overflow:hidden`.
- Horizontal accordion: pointer selection expands TOGETHER to 837.75px while ENCLOSED/PRINTED remain 197.125px. `aria-expanded`/`aria-controls` update, and ArrowRight moves focus to the PRINTED native button.
- Infinite marquee: PAUSE becomes PLAY MOTION, `aria-pressed=true`, and both opposing 42s tracks compute `animation-play-state:paused`.
- GSAP split gallery: the left heading enters a fixed pin. In the recorded reading state the first story is at scale 1 with interpolated exit opacity/overlay, the second is at scale 0.8911/opacity 0.5642, and later stories remain at the planned scale 0.8/opacity 0.2.
- GSAP Card Stacking: cards are sticky with z-index 1/2/3, no shadow/rotation, settled first top 96, and later slabs retain planned 0.94 entry scale plus 18%/129.6px offset. The focused third state visibly contains the eager image and keeps heading copy separate from media.
- Carousel: NEXT then ArrowRight advances to Note 3, updates the polite live region, and preserves manual labelled controls without autoplay or fabricated attribution.
- Form: empty submission triggers native validation and focuses Intent; only intent/name/email are invalid. Selecting Event rental and entering valid test name/email leaves no invalid control. Date, venue, and notes remain optional. The truthful mailto action/disclosure remains; no valid form was submitted and no email app was opened.
- Footer: Instagram/Facebook/Email labels and destinations remain intact; each target is 44px high, Email is exactly 44 x 44px, and the footer stays in-bounds.
- Semantics/accessibility: one main, one H1, one footer, skip link, landmarks, native controls, labelled form fields, alt text, accordion ARIA, carousel live state, visible focus rules, and AA header/action contrast pass.
- Viewport resilience: document `scrollWidth === clientWidth` at 1440 and 1280. Fresh descendant sweep finds no unexpected off-viewport element beyond the intentionally clipped marquee/screen-reader patterns. No failed loaded image, collision, or hidden persistent control remains.
- Reduced motion: current source confines GSAP to desktop/no-preference and provides static/reverted reduced-motion states. The selected Browser exposes no media-preference emulator, so direct reduced-motion rendering remains a P3 evidence limit rather than an observed defect.

## Console, hydration, reload, and build authority

- Fresh 1440 initial load: zero warning/error entries.
- Full interaction sweep through header, bento, accordion, marquee, gallery, stack, carousel, form, and footer: zero warning/error entries.
- Fresh 1280 load and scrolled state: zero warning/error entries.
- Final normal 1280 top reload: no new or accumulated warning/error entries; `readyState=complete`, fonts loaded, H1 present, `scrollY=0`, no hydration/application/server-error text, document width `1265=1265`, no unexpected descendant overflow, and no failed loaded image.
- Integrated lint, TypeScript, and production build are recorded green for the settled repair-04 source state in the passed independent gpt-taste pass-8 report. This read-only viewport agent did not rerun shared build commands.

## Comparison history

1. Desktop responsive QA pass 2 blocked on the initial transparent-header contrast failure; all other desktop surfaces passed.
2. Repair 04 added the existing Off-white/hairline surface only from 768px upward. Separate repair-04 tasks made the third stack image eager and fixed only the below-768 stack-heading clamp.
3. Independent gpt-taste verifier pass 8 passed the full skill/plan/browser/command matrix and authorized fresh responsive QA.
4. This new desktop pass independently remeasures initial/scrolled header contrast and geometry, eager third-image rendering, and desktop type isolation at both required widths. No actionable P0/P1/P2 remains.

## Residual P3 evidence limits

- Direct rendered `prefers-reduced-motion` and browser zoom/text-scaling emulation are unavailable in the selected in-app Browser.
- Synthetic Enter/Space native default activation is an established Browser limitation; pointer activation, native semantics, directional keyboard focus, and visible focus are verified.
- Full-page stitching is retained only as reach evidence, not spatial evidence for pinned/sticky regions.

## Implementation checklist

- No desktop repair task is warranted.
- Preserve all three repair-04 seams while tablet/mobile pass-3 agents complete their independent checks.

final result: passed
