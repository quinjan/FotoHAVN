# Tablet responsive design-QA handoff 02

Date: 2026-08-22  
Scope: fresh tablet-only Product Design QA after design plan v1.1; production files were not modified  
Implementation: `http://localhost:3011/`  
Source truth: `website/.handoffs/gpt-taste-design-plan.md` version 1.1, with `website/DESIGN.md` where the plan does not override it  
Prior gate: `website/.handoffs/gpt-taste-implementation-verification.md` (`passed`)  
result: blocked

## Gate conclusion

The earlier tablet blockers are closed: the hero is exactly two lines at 768, 820, and 1024px while retaining the plan's 35.2px floor, and the header brand/footer Email targets now measure at least 44px in both dimensions.

This fresh pass found two actionable issues. The static `<1024px` card-stack fallback does not request or render the third card's image at 820px, leaving half of a major editorial slab blank (P1). The transparent top header also places the Ebony FOTOHVN wordmark over the hero's dark left edge, making the persistent brand/navigation target effectively disappear at all three tablet widths (P2). The tablet responsive gate remains blocked.

## Comparison basis and required metadata

The source truth is a written design/implementation contract rather than a fixed-pixel mock. Source bitmap dimensions, source CSS size, source density, and same-frame source/implementation bitmap composite are therefore `n/a`; this report makes no pixel-perfect bitmap claim. Browser-rendered implementation states were compared against plan v1.1's explicit tablet typography, 6-column grid, spacing, palette, imagery, copy, interactions, accessibility, static/GSAP breakpoint, bans, and preflight commitments.

| Requested CSS viewport | Browser-reported layout/density | Implementation pixels | Representative states |
|---|---|---|---|
| 768 x 1024 | `innerWidth=768`, `innerHeight=1024`, `devicePixelRatio~=1`, client width `753` | `753 x 1004` | lower tablet hero, grid, static motion fallback metrics, target geometry |
| 820 x 1180 | `innerWidth=820`, `innerHeight=1180`, `devicePixelRatio~=1`, client width `805` | focused captures `805 x 1158`; full page `804 x 11777` | hero, menu, accordion, marquee, static gallery/stack, carousel, form, footer, missing-image reproduction |
| 1024 x 768 | `innerWidth=1024`, `innerHeight=768`, `devicePixelRatio~=1`, client width `1009` | `1009 x 757` | hero/grid/nav boundary plus active GSAP pin/scrub/stack |

The in-app Browser reserves scrollbar/capture chrome, hence requested viewport and PNG differences. Layout assertions use `innerWidth` for media-query activation and `documentElement.clientWidth/scrollWidth` for geometry/overflow.

## Findings

- [P1] The final static stack slab renders without its image below 1024px.
  - Fidelity surfaces: image quality/asset fidelity, major-region layout, responsiveness.
  - Location: `KEEP THE PHOTOGRAPH` card in `website/src/components/MiddleExperience.tsx`; `<1024px` static stack rules in `MiddleExperience.module.css`.
  - Evidence: at 820 x 1180, the third card was brought into the viewport, centered, and left visible for five seconds. Its image remained `complete=false`, `currentSrc=''`, and `naturalWidth=0`; the CSS-sized media half was visibly blank in `website/design-qa/responsive-qa-pass2-tablet-820x1180-stack-final-image.png`. A second physical-style scrolling reproduction left the card visible at `top=5.85px`, `height=620px` with the same empty source/natural size; see `website/design-qa/responsive-qa-pass2-tablet-820x1180-stack-natural-scroll-clean.png`. The source asset exists at `1122 x 1402`, and the same image loads at the 1024px GSAP boundary (`naturalWidth=593`, non-empty `currentSrc`), so this is not a missing file.
  - Impact: half of a prominent Desire-section composition is empty at a canonical tablet width, materially reducing imagery, hierarchy, and perceived completeness.
  - Fix: repair the static-stack image-loading seam so the third card receives and decodes a responsive source before it becomes visible below 1024px. A narrowly targeted eager-load for this card is acceptable if the lazy-loading cause cannot be made reliable, but retest request sizing and page-load cost at 768/820/1024 rather than eagerly loading unrelated below-fold assets.

- [P2] The transparent tablet header loses the FOTOHVN wordmark against the dark hero edge.
  - Fidelity surfaces: accessibility/contrast, navigation affordance, above-the-fold polish.
  - Location: transparent `.header`/`.brand` state in `website/src/components/SiteChrome.module.css` over the hero crop.
  - Evidence: `website/design-qa/responsive-qa-pass2-tablet-768x1024-top.png`, `...820x1180-top.png`, and `...1024x768-top.png` show the Ebony brand placed on the hero's darkest left area; it is effectively absent at 768/820 and only faintly visible at 1024, while MENU remains readable on the bright right. Computed brand geometry is correct (`121.26 x 44px`) but the initial header background is fully transparent. Once scrolled, the Off-white header surface restores clear readability.
  - Impact: the persistent brand/back-to-top navigation target is difficult to discover at initial load, and the plan permits transparency only where contrast permits.
  - Fix: give the initial tablet header a restrained Off-white surface/hairline (or another brand-aligned contrast treatment) under the mobile-nav breakpoint while preserving the 72px height and current scrolled/menu states. Verify both brand and MENU contrast over 768, 820, and 1024 hero crops.

No other actionable P0/P1/P2 finding was found.

## Browser evidence

Full-view evidence:

- `website/design-qa/responsive-qa-pass2-tablet-820x1180-full.png` — complete-page reach only; sticky/pinned stitching is not used for spatial judgments.
- `website/design-qa/responsive-qa-pass2-tablet-768x1024-top.png`
- `website/design-qa/responsive-qa-pass2-tablet-820x1180-top.png`
- `website/design-qa/responsive-qa-pass2-tablet-1024x768-top.png`

Focused evidence:

- `website/design-qa/responsive-qa-pass2-tablet-820x1180-menu-open.png`
- `website/design-qa/responsive-qa-pass2-tablet-820x1180-accordion-printed.png`
- `website/design-qa/responsive-qa-pass2-tablet-820x1180-marquee-paused.png`
- `website/design-qa/responsive-qa-pass2-tablet-820x1180-gallery-static.png`
- `website/design-qa/responsive-qa-pass2-tablet-820x1180-stack-static.png`
- `website/design-qa/responsive-qa-pass2-tablet-820x1180-stack-final-image.png`
- `website/design-qa/responsive-qa-pass2-tablet-820x1180-stack-natural-scroll-clean.png`
- `website/design-qa/responsive-qa-pass2-tablet-820x1180-carousel-note3.png`
- `website/design-qa/responsive-qa-pass2-tablet-820x1180-form-validation.png`
- `website/design-qa/responsive-qa-pass2-tablet-1024x768-gsap-gallery.png`
- `website/design-qa/responsive-qa-pass2-tablet-1024x768-card-stack.png`
- `website/design-qa/responsive-qa-pass2-tablet-1024x768-card-stack-overlap.png`

Focused regions were required because hero/header contrast, accordion state, the blank stack-media surface, form validation, and GSAP computed states are not readable in the full-page stitch.

## Required fidelity surfaces

- Fonts and typography — passed. Cabinet Grotesk resolved at 16px/26.4px for body copy and Cormorant Garamond 500 for editorial display. The hero resolved to 57.6px at 768, 61.5px at 820, and 76.8px at 1024, exactly two visual lines at all three widths, with intact punctuation and plan-approved tracking. No clipping, truncation, or fallback substitution was observed.
- Spacing and layout rhythm — passed apart from the blank P1 media region. Tablet gutters remain 48px. The bento closed as exact 6-column dense grids with 24px gaps and `6x1 + 3x2 + 3x2` spans: tracks measured `89.4625px` at 768, `98.125px` at 820, and `132.125px` at 1024. Major sections retained at least the required 80px rhythm, with flat 2px surfaces and no void or card collision.
- Colors and visual tokens — passed apart from the header contrast finding. The page used Off-white `#FBF8F2`, Ebony `#1E1A17`, warm ivory/paper regions, warm natural imagery, and restrained brass rules. No neon, fake gold, multicolor gradient, glow, black-dominant treatment, or brass small body copy appeared.
- Image quality and asset fidelity — blocked by the third static stack image. Other required local booth, guest, print, and detail assets loaded with purposeful alt text, stable ratios, coherent warm crops, and no visible compression/halo defect. No CSS/div art, handcrafted SVG substitute, emoji, unrelated party stock, fake avatar, or placeholder illustration was used.
- Copy and content — passed. Fixed hero, accordion, marquee, editorial-note, mall-booth, event-rental, inquiry, and footer copy matched the plan. Literal rendered sweeps found no banned price/duration/package, offered look, event-category, customer testimonial/rating, location, response-time, service-area, generic section label, or prompt leakage.
- Icons — passed. Icon use remained minimal; labelled carousel controls did not rely on arrows alone, and no generic icon-card system or fake illustration appeared.
- Shapes and surfaces — passed. Rectangular controls, hairlines, flat editorial slabs, restrained 2–4px radii, and low elevation matched the design authority; no bubbly card factory, decorative blob, badge, pill taxonomy, or fake metallic treatment appeared.
- Accessibility — blocked by the header contrast finding. Semantics otherwise passed: one `main`/`h1`, skip link, labelled navigation, meaningful image alt text, native labelled buttons/links/form controls, accordion `aria-expanded`/`aria-controls`, carousel live region, 2px focus treatment in source, and truthful required/optional form states. Header brand measured `121.26 x 44px`; footer Email measured `44 x 44px`; the only raw controls under 44px were 18px radio inputs inside 48px labelled targets.
- Responsiveness and overflow — blocked by the missing static-stack image. Document-level overflow passed: `753=753`, `805=805`, and `1009=1009`. No clipped copy, hidden persistent control, or horizontal document scroll appeared. Below 1024, gallery media and stack cards resolved to static natural flow with `transform:none` and full opacity; at 1024, the intended GSAP branch activated.
- AI-shortcut artifacts — passed. No generic metric, raw plan text, decorative numeric marker, fake proof, unrelated stock, CSS-art imagery, novelty filter language, confetti, or neon appeared.

## Interaction, motion, accessibility, reload, and console checks

- Navigation: MENU opened a stable tablet panel with all three anchors and both visitor paths; Escape closed it and returned the trigger to `aria-expanded=false`. Hero contact anchors and menu architecture remained present at all widths.
- Accordion: pointer activation expanded PRINTED; `aria-expanded=true` and the expected panel ID were present. ArrowLeft moved focus to TOGETHER without changing authoritative selection. The expanded panel stayed readable inside 48px gutters.
- Marquee: PAUSE changed to PLAY and both opposing 42s tracks reported `animation-play-state: paused`.
- Static motion fallback: at 768/820, `(min-width:1024px)` was false; gallery heading/cards were static, media were `transform:none`/opacity 1, overlays 0, and all three stack cards were gap-separated `position:static` slabs. The missing third image is the sole failure in this branch.
- 1024 activation: `(min-width:1024px)` was true. The gallery heading entered a fixed pin state; the active media reached scale 1/near-full opacity while later media remained scale .8/opacity .2. Stack cards used sticky tops 96/168/240px, z-indices 1/2/3, scale .94/positive entry offset, and the 72px retained-header relationship; all three images loaded in this branch.
- Carousel: pointer NEXT moved to note 2 and ArrowRight moved to note 3; the polite live string updated, controls remained at least 44px high, and no autoplay occurred.
- Form: Event rental intent selection worked. Empty submission was stopped by native validation and focused Name; intent/name/email remained required, event date/city/notes optional, and no mail application was launched.
- Reduced motion: direct media-preference emulation is unavailable in the selected in-app Browser. Source inspection confirmed GSAP registration is confined to `(min-width:1024px) and (prefers-reduced-motion:no-preference)`, matchMedia/context cleanup exists, static gallery/stack states remain visible, marquee tracks/control are hidden, static terms are shown, and carousel feedback is near-immediate. This is a P3 evidence limit, not an observed defect.
- Reload/hydration/console: reload completed with fonts loaded, no hydration-failure text, equal scroll/client widths, and no console error. Two Next.js development-only LCP suggestions for `candid-guests.png` appeared when the browser restored a deep carousel/stack scroll position; normal top loads were clean in earlier same-pass captures. This advisory is non-blocking and distinct from the reproducible empty third image.

## Comparison history

| Iteration | Earlier tablet findings | Repair evidence in this fresh pass | Current result |
|---|---|---|---|
| Responsive QA 01 | 768px hero wrapped to three lines; brand and footer Email targets missed 44px | Hero now has `1 + 1` span lines at 768/820/1024 with 57.6/61.5/76.8px type; brand is `121.26 x 44px`, footer Email `44 x 44px` | closed |
| Responsive QA 02 | Fresh full Product Design/browser comparison | P1 missing third static-stack image and P2 transparent-header brand contrast found; no repair made by this read-only agent | blocked |

## Repair checklist

1. Repair the third static stack image-loading seam and confirm a non-empty responsive `currentSrc`, non-zero natural dimensions, and visible image at 768/820 after natural scrolling.
2. Give the initial tablet header sufficient FOTOHVN/MENU contrast over the hero at 768/820/1024 without altering its 72px architecture or touch targets.
3. Rerun a fresh gpt-taste conformance gate, then fresh desktop/tablet/mobile responsive QA and synthesis as the orchestration contract requires.

## Follow-up polish and residual gaps

- [P3] Direct reduced-motion rendering, browser zoom/text scaling, and a physical-keyboard native Enter/Space smoke check remain unavailable in this Browser surface; source/semantic checks cover those paths.
- [P3] Recheck the deep-scroll development LCP advisory during the post-repair reload sequence; escalate only if it reproduces on a normal top load.

result: blocked
