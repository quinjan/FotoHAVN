# Mobile responsive Product Design QA — pass 4

Date: 2026-08-22  
Scope: fresh mobile QA at 320 x 720, 375 x 812, and 390 x 844  
Implementation: `http://localhost:3011/`  
Production files modified: none  
`website/design-qa.md` modified: no  
result: passed

## Findings

No actionable P0, P1, or P2 remains in the fresh mobile renders.

Repair 05 restores the full stack chapter H2 to the required 24px content measure at all three widths. The separate card-H3 repair remains contained, the third image is decoded and visible, and the hero/header/target contracts remain intact.

## Independence disclosure

Thread-limit fallback assigned this pass to an agent that previously:

- implemented the bounded desktop Card Stacking scroll-context repair 01;
- performed read-only mobile responsive QA pass 2, which found the earlier card-H3 overflow;
- performed read-only desktop responsive QA pass 3.

This agent did not author repair 05. No prior screenshot or measurement was accepted as pass-4 proof. All evidence and live geometry below came from a newly named in-app Browser session and new tab after fonts loaded.

## Source and comparison metadata

- Source design truth: `website/.handoffs/gpt-taste-design-plan.md` version 1.1, with `website/DESIGN.md` applying only where not overridden.
- Passed conformance authority: fresh `website/.handoffs/gpt-taste-implementation-verification.md`, verifier pass 9.
- Responsive history: `website/design-qa.md`, `website/.handoffs/design-qa-synthesis-03.md`, and `website/.handoffs/design-qa-mobile-03.md`.
- Repair history: `website/.handoffs/repair-responsive-stack-typography-05.md`.
- Implementation truth: new browser-rendered captures from the shared local implementation.

The source authority is a written design/implementation contract, not a fixed bitmap mock. Source pixels, source CSS size, source density, density normalization, and a same-frame source-image composite are therefore `n/a`. The implementation was compared against the plan's explicit responsive typography, gutters, grids, imagery, colors, component states, accessibility, motion fallback, copy, and overflow commitments.

| Requested viewport | Browser layout | Representative pixels | Density | States |
|---|---|---|---|---|
| 320 x 720 | `innerWidth=320`, `clientWidth=305`, `clientHeight=720` | focused `305 x 686`; full page `304 x 12,236` | `devicePixelRatio=1.25` | hero, menu, accordion, marquee, gallery, stack typography, carousel, form, anchors, reload |
| 375 x 812 | `innerWidth=375`, captured client surface 360px wide | hero `360 x 779`; full page `360 x 12,777` | approximately `1` | hero, full page, chapter/card text ranges, image, document overflow |
| 390 x 844 | `innerWidth=390`, captured client surface 375px wide | hero `375 x 811`; full page `375 x 12,882` | approximately `1` | hero, full page, chapter/card text ranges, image, document overflow |

The requested/client/PNG differences are in-app Browser scrollbar and capture-surface effects. Findings use live CSS geometry; no issue was filed from browser chrome or density.

## Critical repair-05 closure

### Stack chapter H2

Every text-range line remains inside the 24px content boundary:

| Requested viewport | H2 font | Content right | Maximum text right | Internal clearance | Visual lines |
|---:|---:|---:|---:|---:|---:|
| 320 x 720 | 37.6px | 280.8px | 278.8px | 2px | 4 |
| 375 x 812 | 43.148px | 351.2px | 316.363px | 34.837px | 4 |
| 390 x 844 | 44.896px | 366.4px | 328.238px | 38.162px | 4 |

At 320 the focused Browser capture visibly shows the complete `PHOTOGRAPH.` line with the repaired right gutter. No nowrap, transform scaling, clipping, copy change, or gutter reduction is present.

### Mobile card H3 regression

| Viewport | Card H3 font | STEP INSIDE max right | BE YOURSELVES max right | KEEP THE PHOTOGRAPH max right | Card right |
|---:|---:|---:|---:|---:|---:|
| 320 | 31.2px | 213.988px | 226.163px | 253.963px | 280.8px |
| 375 | 36.582px | 242.588px | 303.337px | 289.450px | 351.2px |
| 390 | 38.064px | 250.512px | 313.725px | 299.275px | 366.4px |

All three cards report equal scroll/client widths, static positioning, `transform:none`, and no heading or copy/media overlap.

### Third image, header, targets, and hero

- Third `KEEP THE PHOTOGRAPH` image is eager with a non-empty optimizer URL, `complete=true`, opacity 1, and decoded natural sizes `224 x 280`, `279 x 349`, and `294 x 368` at 320/375/390.
- The intended mobile header remains transparent, exactly 72px high, with a clearly readable Ebony brand over the light warm hero top. Brand is `121.26 x 44px`; MENU is `64 x 44px`.
- Fresh visible-target sweep at 320 finds no application link/button smaller than 44 x 44px. Both mobile intent actions are centered, full content width, and 48px high; footer Email remains 44 x 44px.
- Hero remains 35.2px Cormorant Garamond 500 at all three widths, with authored spans rendering 1 + 2 visual lines, intact punctuation, approximately 24px gutters, exactly two 48px actions, and 20.8px secondary-action-to-print clearance. This exceeds the required 12px minimum.

## Full-view evidence

- `website/design-qa/responsive-qa-pass4-mobile-320x720-full.png`
- `website/design-qa/responsive-qa-pass4-mobile-375x812-full.png`
- `website/design-qa/responsive-qa-pass4-mobile-390x844-full.png`
- `website/design-qa/responsive-qa-pass4-mobile-320x720-hero.png`
- `website/design-qa/responsive-qa-pass4-mobile-375x812-hero.png`
- `website/design-qa/responsive-qa-pass4-mobile-390x844-hero.png`

Full-page captures prove page reach. Focused captures are authoritative for typography and interaction detail.

## Focused evidence

- `website/design-qa/responsive-qa-pass4-mobile-320x720-stack-typography.png`
- `website/design-qa/responsive-qa-pass4-mobile-320x720-menu-open.png`
- `website/design-qa/responsive-qa-pass4-mobile-320x720-accordion-printed.png`
- `website/design-qa/responsive-qa-pass4-mobile-320x720-marquee-paused.png`
- `website/design-qa/responsive-qa-pass4-mobile-320x720-gallery-static.png`
- `website/design-qa/responsive-qa-pass4-mobile-320x720-carousel-note3.png`
- `website/design-qa/responsive-qa-pass4-mobile-320x720-form-required.png`

## Required fidelity surfaces

- Fonts and typography: passed. Cabinet Grotesk and Cormorant Garamond report loaded. Hero, repaired chapter H2, mobile card H3s, body, labels, and controls retain their intended optical hierarchy, wrapping, weight, line height, and tracking without clipping or truncation.
- Spacing and layout rhythm: passed. Mobile content uses 24px gutters, 16px card/grid gaps, full-width static slabs, and at least the planned section spacing. The exact four-column bento at 320 uses four 52.2px tracks, 16px gaps, `grid-auto-flow:dense`, and three complete four-span rows with no void.
- Colors and tokens: passed. Warm Off-white/Ivory/Paper surfaces, Ebony text/actions, restrained brass rules, directional hero veil, transparent mobile header, and dark footer remain coherent and legible. No neon, fake gold, glow, or multicolor gradient appears.
- Imagery and assets: passed. Local booth, guest, strip, detail, and print assets render through `next/image` with meaningful alt text, stable aspect ratios, warm crops, and no blank image, placeholder, CSS/div art, handcrafted SVG substitute, emoji, or unrelated stock.
- Copy and content: passed. Fixed hero, stack, carousel, contact, form, and footer copy remains complete and truthful. Literal/rendered sweep finds none of the banned public price/duration/package, offered-look, event-category, testimonial, rating, or client-proof strings.
- Icons/shapes/shortcuts: passed. Minimal arrows and native date affordance remain consistent; slabs and controls retain restrained rectangular surfaces. No generic card factory, decorative blob, fake avatar, or prompt leakage appears.

## Interaction, accessibility, motion fallback, and overflow

- Menu/focus: MENU opens the mobile panel; CLOSE has `aria-expanded=true`; both intent actions are 256.8 x 48px at 320 and centered. Escape closes the panel, resets `aria-expanded=false`, and returns focus to MENU.
- Anchors: mobile menu FIND/RENT actions close the panel and place their targets at top 87.7px, below the 72px sticky header.
- Accordion: pointer selection expands PRINTED to 420px with a readable 76.8px panel; the two siblings remain 184px. Label/panel regions do not collide, ARIA state updates, and ArrowLeft moves focus to the TOGETHER native button.
- Marquee: PAUSE becomes PLAY MOTION, sets `aria-pressed=true`, and pauses both opposing tracks. The truthful term set remains contained by the marquee viewport.
- Static mobile GSAP: gallery heading is static; all four gallery media are opacity 1 and `transform:none`. All three stack cards are static, opacity 1, `transform:none`, and gap-separated.
- Carousel: NEXT then ArrowRight advances to Note 3 and updates the polite live sentence. Manual controls remain in-bounds and there is no autoplay.
- Form: empty submission is blocked locally by native validation and focuses Intent. Invalid controls are only intent, name, and email; the truthful mailto action remains. No valid form was submitted and no mail app opened.
- Accessibility: skip navigation, landmarks, native controls, labels, alt text, accordion ARIA, carousel live state, visible focus rules, 16px form inputs, AA action contrast, and 44px targets pass.
- Document and descendant overflow: `documentElement.scrollWidth === clientWidth` at 320/375/390. Fresh general descendant sweep finds no unexpected off-viewport element outside intentional clipped marquee travel and screen-reader text. Repair-specific H2/H3/card/media text-range checks independently pass, so page clipping is not masking a sizing defect.
- Reduced motion: source confines GSAP to desktop/no-preference and exposes static gallery/stack/marquee alternatives. Direct preference emulation is unavailable in the selected Browser; this remains a P3 evidence limit rather than an observed defect.

## Console, hydration, reload, and command authority

- Fresh 320 top load and the complete interaction sequence produce zero warning/error entries.
- Fresh 375 and 390 top loads are clean.
- Final normal 320 top reload produces no new warning/error entry: `readyState=complete`, fonts loaded, H1 present, `scrollY=0`, no hydration/application/server-error text, document width `305=305`, and no failed loaded image.
- Integrated lint, TypeScript, and production build are recorded green for the settled repair-05 source state in the passed independent gpt-taste pass-9 report. This read-only viewport agent did not rerun shared build commands.

## Comparison history

1. Mobile pass 2 found card-H3 clipping; repair 04 fixed the card-only mobile type seam.
2. Mobile pass 3 verified card-H3/image/header repairs but found the stack chapter H2 breaking the 24px mobile content measure.
3. Repair 05 changed only the max-767 chapter-H2 scale and the separate tablet H3 seam. Independent gpt-taste pass 9 passed the complete conformance matrix.
4. This fresh pass independently confirms the chapter H2, all card H3s, third image, hero, menu, target, interaction, overflow, and console contracts at all required phone widths. No actionable P0/P1/P2 remains.

## Residual P3 evidence limits

- Direct rendered `prefers-reduced-motion` and browser zoom/text-scaling emulation are unavailable in the selected in-app Browser.
- Physical-keyboard native Enter/Space activation remains outside this automation surface; pointer activation, native semantics, directional keyboard focus, and visible focus are verified.

## Implementation checklist

- No mobile repair task is warranted.
- Preserve repair 05 while the fresh responsive synthesis reconciles desktop, tablet, and mobile pass-4 evidence.

final result: passed
