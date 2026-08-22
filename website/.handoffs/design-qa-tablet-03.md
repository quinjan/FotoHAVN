# FOTOHVN tablet responsive Product Design QA — pass 3

Date: 2026-08-22  
QA role: fresh tablet Product Design QA under thread-limit fallback  
Implementation: `http://localhost:3011/`  
Production files modified: none  
`website/design-qa.md` modified: no  
result: blocked

## Independence disclosure

This QA agent previously authored the bounded tablet-image repair and served as gpt-taste verifier 08. Neither repair-04 measurements nor pass-8 screenshots were reused as tablet pass-3 evidence. A new in-app Browser session and new tabs produced the `responsive-qa-pass3-tablet-*` captures and every measurement in this report.

## Comparison truth and normalization

- Source design truth: `website/.handoffs/gpt-taste-design-plan.md`, version 1.1; `website/DESIGN.md` applies where the plan does not override it.
- Source bitmap pixels/CSS size/density: `n/a`. The source authority is a written design contract, not a fixed-pixel mock. Pixel-perfect bitmap comparison and same-frame source/implementation compositing are therefore not meaningful.
- Implementation CSS viewports: 768x1024, 820x1180, and 1024x768 at Browser DPR 1.25.
- Focused implementation captures:
  - 768: 753x1004 pixels (`responsive-qa-pass3-tablet-768x1024-*.png`).
  - 820: 805x1158 pixels (`responsive-qa-pass3-tablet-820x1180-*.png`).
  - 1024: 1009x757 pixels (`responsive-qa-pass3-tablet-1024x768-*.png`).
- Full-page reach captures: 752x11415 at requested 768 and 804x11777 at requested 820. Browser full-page stitching distorts sticky/image regions and is not used for spatial fidelity judgments; focused viewport captures and live CSS/text-range geometry are authoritative.
- Browser chrome and the Next development badge are capture artifacts, not product UI.

## Findings

- [P2] Tablet stack headings extend beneath the image column and lose visible letters.
  - Location: `KEEP THE PHOTOGRAPH`, and at 768 also `BE YOURSELVES`, in `website/src/components/MiddleExperience.module.css` / the three `data-stack-card` slabs.
  - Design evidence: plan v1.1 requires normal gap-separated tablet slabs, complete approved copy, large-image priority, and no responsive clipping or overlap.
  - Implementation evidence:
    - At 768, copy right is 307.66px and media starts at 331.66px. `BE YOURSELVES` reaches 347.25px, overlapping media by 15.59px; `KEEP THE PHOTOGRAPH` reaches 386.46px, overlapping media by 54.8px.
    - At 820, copy right is 329.33px and media starts at 353.33px. `KEEP THE PHOTOGRAPH` reaches 386.46px, overlapping media by 33.14px.
    - Focused captures visibly render only `KEEP THE PHOTOGRA...`; the media paints over the last letters.
    - At 1024, the final text reaches 436.22px and media begins at 440.41px, so no media overlap occurs. Scope is the narrower tablet range.
  - Evidence: `responsive-qa-pass3-tablet-768x1024-stack-heading-overlap.png` and `responsive-qa-pass3-tablet-820x1180-stack-third.png`.
  - Impact: approved chapter copy is materially incomplete in a major Desire section at required tablet widths. The repaired image now loads reliably, but its restored foreground exposes the pre-existing long-word collision.
  - Fix: add a tablet-only heading scale in `MiddleExperience.module.css`, scoped to `min-width: 768px` and `max-width: 1023px`, without changing copy, grid, padding, imagery, or motion. A suitable starting contract is `.stackCopy h3 { font-size: clamp(2.2rem, 4.6vw, 2.75rem); }`; remeasure every text range against `stackMedia.left` at 768 and 820 before acceptance.

No other actionable P0/P1/P2 was found.

## Repaired pass-2 findings

### Initial header contrast — passed

- 768 initial/scrolled: 752.8x72px; 820 initial/scrolled: 804.8x72px; 1024 initial/scrolled: 1008.8x72px.
- Every state uses `rgba(251, 248, 242, 0.97)`, the Hairline divider, and Ebony text without control displacement.
- Conservative contrast remains 15.27:1–16.33:1 for Ebony text on the composited surface; the Ebony action remains 16.30:1 with Off-white text.
- Brand and MENU targets are at least 44px; mobile-navigation intent actions measure 48px high. MENU opens, Escape closes and restores focus, and the two intent actions remain equal-reach.

### Third stack image loading — passed

- Fresh direct 820 top load: `loading=eager`, non-empty w=1080 optimizer URL, `complete=true`, natural 724x905 while the image is about 8,210px below the viewport.
- Centered 820: top 256.25, bottom 875.45, visible, complete, opacity 1, static/untransformed card.
- Centered reload restores the exact same scroll position, image rectangle, URL, and 724x905 decode.
- 768 fresh load resolves 672x840; 1024 fresh load resolves 593x742.
- First two stack images remain lazy; only the third is eager.

## Required fidelity surfaces

- Fonts and typography: blocked only by the tablet stack-heading collision above. Cabinet Grotesk and Cormorant Garamond computed stacks load; hero sizes/line heights/tracking are correct. Hero is exactly two lines at 57.6px (768), 61.5px (820), and 76.8px (1024).
- Spacing and layout rhythm: passed outside the heading collision. Tablet gutters are 48px, bento/accordion/gallery/stack gaps are 24px, major sections retain 80px-or-greater rhythm, cards use restrained 2px edges, and no generic rounded-card/elevation drift appears.
- Colors and tokens: passed. Off-white, Warm Ivory, Cream Paper, Ebony, Hairline, and restrained brass map to the design system. Header and action contrast pass; no loud gradients, neon, fake gold, or color taxonomy appears.
- Image quality and asset fidelity: passed. Required local imagery is sharp, decoded, correctly cropped, and uses stable `next/image` frames. No placeholders, CSS drawings, custom SVG substitutes, halos, stretching, or remote stock imagery were found.
- Copy and content: blocked by visible truncation of approved stack copy at 768/820. All underlying strings otherwise match the truthful plan; no fabricated package, location, testimonial, event-category, or photographic-look claim appears.
- Icons: passed. The implementation does not introduce a generic icon grid or substitute visible target assets with handcrafted icons; carousel arrows are restrained labelled controls.
- Shapes and surfaces: passed. Flat editorial slabs, hairlines, 2px media edges, 4px controls, one restrained print shadow, and the Warm Ivory/Ebony action split match the source direction.
- Accessibility: blocked by the visually hidden heading letters. Header/menu contrast, native controls, ARIA, alt text, focus rules, form labels/validation, and practical touch targets otherwise pass. The 18px radio itself sits inside a 48px labelled hit target.
- Responsiveness: blocked only at the 768–820 stack-heading/media seam. `scrollWidth === clientWidth` at all three tablet widths; hero, bento, accordion, carousel, form, and footer do not create document overflow.
- AI-shortcut artifacts: passed. No fake assets, decorative blobs, placeholder avatars, pill factory, dense generic card grid, or approximate CSS art was found.

## Layout and component evidence

- Hero: exact fixed copy, two lines at all three tablet widths, zero CTA/print intersection, decoded booth image, 48px actions, and preserved artistic print overlap.
- Bento: six tracks, 24px gap, `grid-auto-flow:dense`, exact 6x1 + 3x2 + 3x2 layout, and no void.
- Accordion: horizontal at tablet, native buttons with ARIA, 600px height, pointer selection, and ArrowRight focus movement. At 820 the active slice is 449.35px and inactive slices are 105.725px.
- Marquee: two opposing 42s tracks; PAUSE stops both and PLAY plus focus release resumes both.
- 768/820 GSAP fallback: gallery heading and all media are static/untransformed with opacity 1 and overlay 0; stack cards are static/untransformed with 24px gaps.
- 1024 activation boundary: gallery media begin at scale .8/opacity .2; at scrollY 3188.8 the heading is fixed and media interpolate through scale/fade/overlay. Stack cards activate sticky 96/168/240 targets with z-index 1/2/3 and scrubbed transforms.
- Carousel: NEXT advances note 1→2 and ArrowRight advances 2→3; `aria-live` tracks the current FOTOHVN-attributed statement.
- Primary paths/form: `FIND A BOOTH` scrolls to its target; empty form submission remains natively blocked and focuses required Intent; mailto action and disclosure remain truthful.

## Console and interaction evidence

- Fresh 768, 820, and 1024 top loads: no warning/error entries.
- 768 header/menu interactions: no error or hydration issue.
- 820 hover, accordion, marquee, carousel, anchor, form, gallery, and stack interactions: no error or hydration issue.
- 1024 header, gallery, and stack GSAP scroll states: no warning/error entries.
- A deep restored-scroll 820 reload produced one Next development LCP suggestion for a lazy `candid-guests.png` instance. Responsive synthesis 02 already classifies restored-scroll-only LCP suggestions as non-actionable P3; the normal top-load console is clean and the repaired target image remains decoded.
- Direct reduced-motion, browser zoom/text-scaling, physical-keyboard native Enter/Space synthesis, external mail-app launch, and cross-browser rendering remain tool/evidence gaps. Source contracts and live breakpoint fallbacks were inspected, but these gaps are not represented as verified passes.

## Evidence inventory

- `website/design-qa/responsive-qa-pass3-tablet-768x1024-top.png`
- `website/design-qa/responsive-qa-pass3-tablet-768x1024-full.png`
- `website/design-qa/responsive-qa-pass3-tablet-768x1024-scrolled-header.png`
- `website/design-qa/responsive-qa-pass3-tablet-768x1024-stack-heading-overlap.png`
- `website/design-qa/responsive-qa-pass3-tablet-820x1180-top.png`
- `website/design-qa/responsive-qa-pass3-tablet-820x1180-full.png`
- `website/design-qa/responsive-qa-pass3-tablet-820x1180-bento-hover.png`
- `website/design-qa/responsive-qa-pass3-tablet-820x1180-accordion.png`
- `website/design-qa/responsive-qa-pass3-tablet-820x1180-gallery-static.png`
- `website/design-qa/responsive-qa-pass3-tablet-820x1180-stack-third.png`
- `website/design-qa/responsive-qa-pass3-tablet-820x1180-stack-third-reload.png`
- `website/design-qa/responsive-qa-pass3-tablet-820x1180-carousel.png`
- `website/design-qa/responsive-qa-pass3-tablet-1024x768-top.png`
- `website/design-qa/responsive-qa-pass3-tablet-1024x768-gsap-gallery.png`
- `website/design-qa/responsive-qa-pass3-tablet-1024x768-gsap-stack.png`

## Comparison history

| Iteration | Finding/fix/evidence | Result |
|---|---|---|
| Responsive QA pass 2 | P1 blank 820 third image; P2 initial header contrast; unrelated mobile P2 heading clipping. | tablet blocked |
| Repair 04 | Header gained a 768+ Off-white/hairline surface; only the third stack image became eager; mobile-only stack type was reduced. | fresh gates required |
| gpt-taste pass 8 | Independently closed the three repair findings across the complete conformance matrix. | gpt gate passed |
| Tablet QA pass 3 | Header and image repairs pass; focused Product Design review finds the 768–820 stack-heading/media collision and records new post-repair evidence. | tablet blocked |

## Open Questions

- None requiring a product decision. The heading collision is an implementation defect, not an ambiguous design choice.
- The browser full-page stitch artifacts are evidence-tool limitations and should not be treated as product defects.

## Implementation Checklist

1. Add a tablet-only `.stackCopy h3` size rule in `MiddleExperience.module.css` without changing copy, grid, padding, media, or motion.
2. Reverify text ranges at 768 and 820: every heading must end before `stackMedia.left`; no clipping or copy/media overlap.
3. Confirm the 1024 GSAP boundary and 320/375/390 mobile heading repair remain unchanged.
4. Rerun the fresh gpt-taste gate, then fresh desktop/tablet/mobile responsive QA and synthesis.

## Follow-up Polish

- P3: retain the documented deep restored-scroll Next development LCP suggestion as a development-only limitation unless it appears on a normal top load.
- P3 test gaps: direct reduced-motion rendering, zoom/text scaling, cross-browser checks, and external mail-app launch.

final result: blocked
