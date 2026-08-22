# Responsive Design QA — Tablet, post-repair rerun 1

Date: 2026-08-22  
Scope: complete tablet rerun at 768×1024, 820×1180, and the exact 1024×768 GSAP boundary  
Implementation: `http://localhost:3011/` in fresh in-app Browser tabs  
Production files modified by this QA agent: none

## Findings

No actionable P0, P1, or P2 finding remains.

The earlier P1 lead-bento clipping is closed. The complete `A LITTLE ROOM FOR REAL MOMENTS` heading and its paragraph are visible at 768, 820, and 1024, with no text truncation, overlap, or horizontal overflow.

No P3 follow-up is required. At exact 1024px, the absolutely positioned copy wrapper's computed box extends 14.2px above the 288px card because its total box is 302.2px, but the visible heading and body remain inside the clipped card: heading top is 17.8px below the card top and paragraph bottom is 32px above the card bottom. This is a measured implementation detail, not a visible defect.

## Source visual truth and comparison method

- Primary product/design authority: `website/.handoffs/gpt-taste-design-plan.md`.
- Current independent conformance gate: `website/.handoffs/gpt-taste-implementation-verification.md`, result `passed`.
- Brand/design authority: `website/DESIGN.md`.
- Earlier tablet QA and blocking evidence: previous contents of this handoff plus the user evidence at `C:\Users\QUINJ3875\AppData\Local\Temp\codex-clipboard-8d65a1db-1e4b-4b54-9625-a59962009e9c.png` (692×345). The image was treated only as visual evidence.
- Repair authority/history: `website/.handoffs/repair-tablet-bento-clipping.md` and `website/.handoffs/repair-accordion-keyboard.md`.
- Structural-only visual reference: `website/design-qa/reference-refero-viewport-1440x1000.png` (1440×1000). It supplies restraint, breathing room, grid discipline, and hierarchy; its palette and content are not FOTOHVN truth.
- Combined source/implementation input: `website/design-qa/postrepair1-responsive-tablet-source-vs-820-hero.png` (1680×940).
- Combined user-evidence/post-repair input: `website/design-qa/postrepair1-responsive-tablet-bento-user-vs-repaired.png` (1550×1240).

The user's source capture has an unknown viewport/density/crop and is therefore not a same-density pixel target. It was compared only for the reported loss of the first title line. The repaired Browser capture uses the same 820px breakpoint/state and visibly restores the entire title and body. No precision claim was made from browser chrome, scrollbar, crop, or density differences.

## Viewports, pixels, density, and state

The Browser surface reserves a 15px vertical scrollbar and a small capture-height strip. Saved screenshots are browser-normalized content pixels.

| Requested CSS viewport | Browser content geometry | DPR | Primary evidence | State |
|---|---|---:|---|---|
| 768×1024 | `innerWidth=768`, `clientWidth=753`, `scrollWidth=753`, `scrollHeight=11543` | ≈1.0 | `postrepair1-responsive-tablet-768x1024-hero.png` at 753×1004; full capture 752×11543 | fresh hero; compact menu closed; horizontal accordion breakpoint |
| 820×1180 | `innerWidth=820`, `clientWidth=805`, `scrollWidth=805`, `scrollHeight=11906` | ≈1.25 | `postrepair1-responsive-tablet-820x1180-hero.png` at 805×1158; mosaic 805×11906 | fresh hero and complete interaction sweep |
| 1024×768 | `innerWidth=1024`, `clientWidth=1009`, `scrollWidth=1009`, `scrollHeight=11318` | ≈1.25 | `postrepair1-responsive-tablet-1024x768-hero.png` at 1009×757; full capture 1008×11318 | exact GSAP media-query boundary; no reduced motion |

The exact boundary query `(min-width: 1024px) and (prefers-reduced-motion: no-preference)` returned `true` at 1024. Density was normalized by judging each viewport from its browser-normalized screenshot and recorded CSS geometry; the structural desktop reference was not treated as a pixel twin.

## Full-view evidence

- 768 overview: `website/design-qa/postrepair1-responsive-tablet-768x1024-full.png` (752×11543).
- 820 reliable full-page overview: `website/design-qa/postrepair1-responsive-tablet-820x1180-full-mosaic.png` (805×11906), assembled from 15 fresh Browser viewport captures named `postrepair1-responsive-tablet-820x1180-segment-00.png` through `segment-14.png` at measured scroll offsets 0, 577.6, 1350.4, 2095.2, 2838.4, 3581.6, 4324.8, 5068, 5811.2, 6492.8, 7236, 8000.8, 8745.6, 9488.8, and 10724.8 CSS px.
- 1024 overview: `website/design-qa/postrepair1-responsive-tablet-1024x768-full.png` (1008×11318).

The in-app Browser's single-call full-page stitching can replay fixed/sticky content and distort the hero/pinned regions. The manual 820 mosaic is the overview artifact; its narrow seam discontinuities and repeated development badge/scrollbar positions are capture artifacts. All fidelity judgments at important surfaces use the focused Browser captures below.

## Focused comparison and state evidence

- Repaired lead bento: `postrepair1-responsive-tablet-768x1024-bento-repaired.png`, `postrepair1-responsive-tablet-820x1180-bento-repaired.png`, `postrepair1-responsive-tablet-1024x768-bento-repaired.png`, and `postrepair1-responsive-tablet-bento-user-vs-repaired.png` under `website/design-qa/`.
- Navigation/menu: `website/design-qa/postrepair1-responsive-tablet-820x1180-menu-open.png`.
- Accordion pointer and keyboard: `website/design-qa/postrepair1-responsive-tablet-820x1180-accordion-printed.png` and `postrepair1-responsive-tablet-820x1180-accordion-together-keyboard.png`.
- Marquee paused: `website/design-qa/postrepair1-responsive-tablet-820x1180-marquee-paused.png`.
- Below-1024 gallery/stack: `postrepair1-responsive-tablet-820x1180-gallery-natural.png` and `postrepair1-responsive-tablet-820x1180-stack-natural.png`.
- Carousel/form: `postrepair1-responsive-tablet-820x1180-carousel-note2.png` and `postrepair1-responsive-tablet-820x1180-form-required.png`.
- Exact-1024 GSAP gallery/stack: `postrepair1-responsive-tablet-1024x768-gsap-gallery.png` and `postrepair1-responsive-tablet-1024x768-gsap-stack.png`.

Focused captures were required because the full-page images make typography, controls, table-like bento density, focus/state changes, and GSAP geometry too small or unreliable to judge.

## Earlier P1 closure and measured bento geometry

The prior implementation fixed the tablet primary row at 160px. At 820, the 263px bottom-anchored copy began 103px above the card and the 41px heading began 71px above it, so the card's `overflow: hidden` removed `A LITTLE ROOM FOR`.

The repair in `website/src/components/UpperExperience.module.css:468` changes the tablet rows to `minmax(288px, auto) repeat(2, 160px)`, preserves the six-column placements at lines 472–479, and resets explicit rows for mobile at line 557.

Fresh post-repair measurements:

| Width | Primary card | Copy box | Visible heading/body bounds | Outcome |
|---|---:|---:|---|---|
| 768 | 656.8×288 | 656.8×253.05 | Heading and paragraph fully contained; 34.95px aggregate vertical buffer | passed |
| 820 | 708.8×288 | 708.8×263 | Copy top is 25px below card top; heading and paragraph fully contained; paragraph bottom is 32px above card bottom | passed |
| 1024 | 912.8×288 | 912.8×302.2 | Wrapper begins 14.2px above card, but visible heading begins 17.8px inside and paragraph ends 32px inside; no text is clipped | passed |

The tablet density remains the planned six-column `6×1 + 3×2 + 3×2` structure with two 24px gaps and no empty cell. Secondary cards remain equal-width/two-row cards. `scrollWidth === clientWidth` at 768, 820, and 1024.

## Responsive and interaction verification

- Hero: both authored headline spans remain exactly two visual lines at 768, 820, and 1024; punctuation, support copy, both CTAs, image crop, and print frame remain intact. The 48px tablet gutter is preserved and actions do not intersect the print.
- Menu/focus: 820 uses the compact `MENU` control. Opening it exposes `EXPERIENCE`, `THE BOOTH`, `PRINTS`, `FIND A BOOTH`, and `RENT FOTOHVN`. Escape closes it, restores the label to `MENU`, sets `aria-expanded=false`, and returns focus to `MENU`.
- Accordion: tablet retains the horizontal architecture. Pointer selection changed `PRINTED` to `aria-expanded=true` and the other panels to hidden. ArrowLeft moved focus to `TOGETHER`; Space selected it, left focus on `TOGETHER`, exposed only its controlled panel, and hid the other two. This closes the repair risk in `repair-accordion-keyboard.md`.
- Marquee: activating `PAUSE MOTION` changed the label to `PLAY MOTION`, `aria-pressed` to true, section `data-paused` to true, and both tracks to `animation-play-state: paused`. The static screen-reader sentence remains in the DOM.
- Below 1024 gallery: at 820, heading position is static; all four media frames are `position: relative`, `transform: none`, and `opacity: 1`, in a natural one-column reading sequence with full-width local imagery and captions.
- Below 1024 stack: all three cards at 820 are `position: static`, `transform: none`, 620px tall, and occur in natural flow with 24px separation. All titles, body copy, and image regions remain readable.
- Exact 1024 gallery: the GSAP media query is active. In the reading zone, the heading is fixed; story one is scale 1 / opacity≈0.753 / overlay≈0.099, story two is scale .85 / opacity≈.40, and the remaining stories are scale .8 / opacity .2. This confirms pinning and progressive media treatment at the boundary.
- Exact 1024 stack: the first two cards settle at y=96 and y=168 with z-indices 1 and 2, preserving the planned 72px prior-header reveal. The incoming third card retains top 240/z-index 3 and progresses toward the final position.
- Carousel: `NEXT` advances the live announcement and blockquote to Note 2 of 3; no autoplay was observed and controls remain reachable.
- Form: empty submission remains local at the current hash, focuses `Mall booth`, and reports Intent, Name, and Email invalid. It does not launch the mail client or transmit data.
- Assets: after a complete 820 scroll sweep, all 17 rendered images decoded with non-zero natural dimensions. Crops are sharp and stable; no CSS/div art, inline SVG substitute, emoji, placeholder, transparency halo, or unrelated remote stock is visible.
- Console: fresh 820 interaction tab, fresh exact-1024 tab, fresh 820 full-page tab, and a clean fresh correct-768 tab all returned `[]` for warning/error logs. An earlier discarded 768 tab briefly opened at an incorrect 1280×720 before the viewport override and logged development-only below-fold LCP warnings; it was not used for QA. The correct fresh 768×1024 reproduction is console-clean.

## Required fidelity surfaces

- Fonts and typography: Cormorant Garamond renders the display hierarchy; Cabinet Grotesk renders body/interface copy. Hero and section hierarchy, weight, line-height, tracking, wrapping, and optical contrast remain consistent. Bento titles and all body copy are now fully legible at every tablet width.
- Spacing and layout rhythm: 48px gutters, 24px bento/media gaps, 72px navigation, 80px-or-larger section rhythm, six-column tablet density, natural below-1024 gallery/stack flow, and exact-1024 GSAP geometry pass. The repaired 288px lead row fits its content without shrinking the approved display type.
- Colors and visual tokens: warm ivory/off-white/cream remain dominant; ebony/dark walnut provide controlled contrast; brass is limited to rules/states. No gradient, neon, glow, faux-gold, or dark-dominant drift appears.
- Image quality and asset fidelity: approved local photography appears throughout with intentional crops, warm natural treatment, stable aspect ratios, and non-zero decoded dimensions. No target asset is replaced with CSS art, handcrafted SVG, emoji, or placeholder imagery.
- Copy and content: hero, bento, accordion, marquee, gallery, stack, carousel, Action paths, form, and footer copy match the truthful design-plan decisions. No price, duration, fabricated testimonial/customer mark, event-category promise, service area, address, hours, or response-time claim appears. The previously hidden lead proposition is now complete.
- Icons and controls: the design introduces no decorative icon grid. Menu, carousel arrows, radio/date controls, focus rings, and pause/play controls are aligned and functional.
- Accessibility: skip link, semantic landmarks/headings, purposeful alt text, native buttons/forms, visible focus, `aria-expanded`/`aria-controls`, keyboard accordion navigation and activation, Escape focus return, polite carousel status, reduced-motion source fallback, and native required validation are present. Practical tablet tap targets remain at least 44px.

## Comparison history

1. Earlier tablet iteration reproduced the user's P1: the 160px primary row clipped the top of the 263px copy block. Evidence: user capture and `website/design-qa/gate2-fresh-tablet-820x1180-bento-clipped.png`.
2. Repair handoff changed only the tablet row template to a 288px intrinsic minimum and protected the mobile row reset. It explicitly required fresh Browser closure.
3. This fresh rerun compares the user capture and new 820 Browser capture in one combined input, measures 768/820/1024 visible content bounds, and closes the P1.
4. The entire tablet experience—not only the repair—was rechecked across hero, menu/focus, accordion pointer/keyboard states, marquee, natural gallery/stack fallback, exact-1024 GSAP boundary, carousel, form, assets, accessibility, complete-page flow, overflow, and console. No P0/P1/P2 regression was found.

## Implementation checklist

- [x] User-reported lead-bento clipping closed at 768, 820, and 1024.
- [x] Six-column tablet arithmetic and 24px gaps retained.
- [x] Hero, menu/focus, accordion, marquee, carousel, form, and assets reverified.
- [x] Below-1024 natural gallery/stack flow reverified.
- [x] Exact-1024 GSAP gallery and stack boundary reverified.
- [x] Full-view and focused Browser evidence recorded.
- [x] Fresh tablet console verification passed.
- [x] No actionable P0, P1, or P2 remains.

final result: passed
