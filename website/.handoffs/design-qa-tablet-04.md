# Responsive design QA — tablet pass 4

Date: 2026-08-22  
QA scope: 768x1024, 820x1180, and 1024x768 requested tablet viewports  
Implementation: `http://localhost:3011/`  
Production files modified: none  
`website/design-qa.md` modified: no  
result: passed

## Independence disclosure

Thread-limit fallback assigned this pass to an agent that previously authored implementation 02, mobile responsive QA pass 3, and gpt-taste verification pass 9. It has not served as a tablet responsive-QA agent and did not author repair 05. A new named in-app Browser session and new tab produced only new `responsive-qa-pass4-tablet-*` evidence. Prior measurements and screenshots were treated as history, not acceptance evidence.

## Comparison truth and normalization

- Source design truth: `website/.handoffs/gpt-taste-design-plan.md` version 1.1, with `website/DESIGN.md` applying only where the plan does not override it.
- Source bitmap pixels/CSS size/density: `n/a`. The source authority is a written design contract, not a fixed-pixel mock, so this report makes no pixel-perfect bitmap or same-frame density-normalization claim.
- Rendered implementation: fresh in-app Browser captures from the shared local implementation.

| Requested viewport | Focused implementation pixels | Full-page pixels | Primary states |
|---:|---:|---:|---|
| 768x1024 | 753x1004 | 752x11415 | initial header/hero, static stack and final image |
| 820x1180 | 805x1158 | 804x11777 | menu, accordion, marquee, stack, carousel, form |
| 1024x768 | 1009x757 | 1008x11190 | GSAP activation boundary |

Browser chrome and reserved scrollbar space account for the difference between requested outer viewport and saved implementation pixels. Live CSS geometry and saved capture dimensions are recorded separately rather than treated as identical.

Full-view captures establish overall AIDA order, proportions, density, and reach. Focused evidence is required because the 11,000px full-page stitches make typography, controls, and image state too small to judge precisely.

## Findings

No actionable P0, P1, or P2 finding remains. Repair 05 closes the prior tablet H3/media overlap, and the earlier header/image repairs remain effective.

One accepted P3/tool-state note remains: full-page/deep-state development rendering can log a Next.js LCP suggestion for a lazy `candid-guests.png` instance. The fresh 768 top load was clean; the retained tab log later contained one such warning but no error or hydration warning. The affected images decoded when positioned, and this does not reproduce the earlier blank third-image defect.

## Critical repair-05 verification

Every stack-card H3 ends before the media column and no text range intersects image paint.

| Viewport | H3 font | Media left | STEP INSIDE right | BE YOURSELVES right | KEEP THE PHOTOGRAPH right | Tightest clearance |
|---:|---:|---:|---:|---:|---:|---:|
| 768x1024 | 35.328px | 331.663px | 201.525px | 297.725px | 329.225px | 2.438px |
| 820x1180 | 37.72px | 353.325px | 208.675px | 311.388px | 345.013px | 8.313px |

At both widths:

- cards are `position: static`, `transform: none`, and have zero card overflow;
- copy and media retain the planned two-column regions;
- all approved heading copy is complete and visually unobscured;
- the third image is `loading=eager`, has a non-empty optimizer URL, and is `complete=true` at natural 672x839 and 724x904 respectively;
- the other two lazy images also decode after natural positioning.

At 1024, the repair rule correctly stops applying. Card H3 returns to 51.2px, all glyph ranges remain before media left x=448.703, cards are sticky, and the three initial transforms are scale .94 with y≈99.53.

The chapter H2 is also in-bounds throughout: maximum text right/content right is 573.088/704.8 at 768, 595.487/772 at 820, and 712.275/976 at 1024.

## Prior repair regression verification

- Header contrast: passed. After transition settlement at 768, `matchMedia('(min-width: 768px)')` is active and the initial header is `rgba(251,248,242,.97)` with the hairline, 72px height, Ebony brand/MENU, and 44px targets. The same surface persists at 820/1024 and when scrolled/menu-open.
- Third image loading: passed. The final print image is eager, decoded, non-empty, and visibly rendered at every tablet width.
- Hero: passed. 768 uses 57.6px/-0.04em and exactly two lines; 820 uses 61.5px and exactly two lines; 1024 uses 76.8px and exactly two lines. Gutters are 48px, punctuation is intact, actions remain 48px high, and no CTA/print overlap appears.
- Static/active boundary: passed. Gallery and stack are static, fully opaque, and untransformed at 768/820. At 1024, GSAP is active exactly at the planned boundary.

## Required fidelity surfaces

- Fonts and typography: passed. Cabinet Grotesk and Cormorant Garamond report loaded; family, weight, line height, tracking, hierarchy, wrap, and truncation conform. Repair-05 H3 and chapter-H2 measures pass.
- Spacing and layout rhythm: passed. Tablet 48px gutters, six-column/24px grid, 80–144px section rhythm, 24px component gaps, restrained radii/borders, and editorial slab spacing remain coherent without collision.
- Colors and visual tokens: passed. Off-white/Ivory/Paper surfaces, Ebony text/actions, restrained brass rules, hero veil, and high-contrast header/action states match the written design authority.
- Image quality and asset fidelity: passed. Correct local booth, guest, detail, and print imagery is sharp, decoded in focused states, stably cropped, and free of placeholders, fake CSS art, inline-SVG substitutes, or unrelated stock.
- Copy and content: passed. Fixed hero, component, carousel, action, form, and footer language remains coherent, truthful, and complete.
- Icons and surfaces: passed. The design uses its intended minimal text controls and restrained arrows; no mismatched icon family, generic card factory, decorative blob, fake illustration, or inappropriate shadow appears. The development-only Next.js tool is excluded from production fidelity.
- Accessibility: passed. Skip link, landmarks, native links/buttons/form controls, ARIA states, live carousel status, labels, alt text, focus return, and validation remain. Fresh target sweeps found no visible application link/button below 44x44.
- Responsiveness: passed. Document `scrollWidth === clientWidth` at 768/820/1024. Critical descendant text/card/media checks pass and no clipping, collapse, or hidden persistent control was observed.
- AI-shortcut artifacts: passed.

## Interaction and breakpoint evidence

- Navigation: at 820, MENU opened a contrast-safe panel. Navigation rows are 60px high; the two intent actions are 48px high and equally reachable. Escape closes the panel and returns focus to MENU.
- Dense bento: all widths compute six columns, three rows, 24px gaps, and `grid-auto-flow:dense`; spans are 6x1 + 3x2 + 3x2, filling all 18 cells.
- Horizontal accordion: at 820, PRINTED expands to 449.35px while siblings remain 105.725px; native `aria-expanded` and hidden-panel state match. ArrowLeft moves focus to TOGETHER.
- Marquee: PAUSE sets `aria-pressed=true` and pauses both tracks; re-enabling motion preserves the truthful term set.
- Carousel: manual NEXT moves note 1→2 and ArrowRight moves 2→3 with updated `aria-live` text; no autoplay was observed.
- Form: empty submit stays locally blocked, focuses the required Mall booth radio, and reports `Please select one of these options.` No external mail application is opened.
- 1024 GSAP boundary: the gallery heading becomes fixed at top 198.375; first media is scale 1/opacity .7133 with exit overlay .1147, second is scale .8589/opacity .4357, later media remain .8/.2. Stack cards are sticky with planned top offsets 96/168/240 and z-index 1/2/3.
- Reload/console: a top-position 1024 reload returns scrollY 0, fonts loaded, and document width equality. No runtime error or hydration warning occurred.

## Evidence inventory

- `website/design-qa/responsive-qa-pass4-tablet-768x1024-hero.png`
- `website/design-qa/responsive-qa-pass4-tablet-768x1024-full.png`
- `website/design-qa/responsive-qa-pass4-tablet-768x1024-header-initial.png`
- `website/design-qa/responsive-qa-pass4-tablet-768x1024-stack-third.png`
- `website/design-qa/responsive-qa-pass4-tablet-820x1180-hero.png`
- `website/design-qa/responsive-qa-pass4-tablet-820x1180-full.png`
- `website/design-qa/responsive-qa-pass4-tablet-820x1180-menu-open.png`
- `website/design-qa/responsive-qa-pass4-tablet-820x1180-accordion-printed.png`
- `website/design-qa/responsive-qa-pass4-tablet-820x1180-marquee-paused.png`
- `website/design-qa/responsive-qa-pass4-tablet-820x1180-stack-third.png`
- `website/design-qa/responsive-qa-pass4-tablet-820x1180-carousel.png`
- `website/design-qa/responsive-qa-pass4-tablet-820x1180-form-required.png`
- `website/design-qa/responsive-qa-pass4-tablet-1024x768-hero.png`
- `website/design-qa/responsive-qa-pass4-tablet-1024x768-full.png`
- `website/design-qa/responsive-qa-pass4-tablet-1024x768-gsap-boundary.png`

## Comparison history

| Iteration | Finding | Fix/post-fix evidence | Result |
|---|---|---|---|
| Responsive QA pass 2 | Initial tablet header contrast failed; 820 final stack image could remain blank. | Repair 04 added the contrast-safe min-768 header surface and eager final image; fresh pass-4 header/image evidence passes. | closed |
| Responsive QA pass 3 | Tablet card H3 ranges entered the media column at 768/820. | Repair 05 added a bounded 768–1023 H3 scale; fresh text-range/media measurements and focused captures pass. | closed |
| Responsive QA pass 4 | No new actionable P0/P1/P2. | Full-view, focused, interaction, overflow, console, reload, and 1024-boundary evidence recorded above. | passed |

## Implementation checklist

No repair is required from tablet pass 4. Preserve the passing repair-05 typography seam, repair-04 header/image behavior, and the exact 1024 GSAP boundary while the remaining viewport agents complete their independent checks.

## Follow-up polish and residual limits

The restored/full-page development-only LCP suggestion remains P3 and non-blocking. Direct reduced-motion emulation, browser zoom/text scaling, cross-browser coverage, external mail-app launch, and physical touch-device testing remain explicit tool/environment limits; source contracts and tablet static/active breakpoint behavior were directly checked.

final result: passed
