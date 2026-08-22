# Mobile responsive design-QA — pass 2

Date: 2026-08-22  
QA scope: fresh mobile-only pass at 320 x 720, 375 x 812, and 390 x 844  
Implementation: `http://localhost:3011/`  
Production files modified: none  
result: blocked

## Findings

- [P2] Card Stacking slab headings escape their mobile measure and are visibly clipped.
  - Fidelity surfaces: fonts/typography, responsive layout, descendant overflow, content legibility.
  - Location: `website/src/components/MiddleExperience.module.css`, `.stackCopy h3` and its mobile rules; rendered slabs `BE YOURSELVES` and `KEEP THE PHOTOGRAPH`.
  - Evidence: the H3 element remains constrained to `9ch`/`10ch`, but the unbroken words extend beyond both that element and, in key cases, the mobile client viewport. At requested 320px (`clientWidth=305`), `BE YOURSELVES` reaches `right=280.98` for its second line but the full heading's scroll width reaches 233px from `left=48`, and `KEEP THE PHOTOGRAPH` reaches `right=317.34`, 12.34px beyond the client viewport. At requested 375px (`clientWidth=360`), `KEEP THE PHOTOGRAPH` reaches `right=369.99`; at requested 390px (`clientWidth=375`), it reaches `right=383.04`. The 320 and 390 screenshots visibly cut the end of `PHOTOGRAPH`. Evidence: `website/design-qa/responsive-qa-pass2-mobile-320x720-stack-third.png` and `website/design-qa/responsive-qa-pass2-mobile-390x844-stack-third.png`.
  - Impact: a primary editorial chapter heading loses visible characters at every required phone width. The document-level equality check still reports `scrollWidth === clientWidth` only because the page wrapper clips horizontal spill; this is the exact masked descendant-sizing defect the plan says must be detected independently.
  - Fix: revise the mobile `.stackCopy h3` measure and width-aware type sizing so every complete word stays inside the slab's 24px-inset content box. Do not insert discretionary breaks into the approved copy or rely on `overflow-x: clip`. Reverify each heading's text-range rectangles, not only the H3 border box, at 320/375/390.

No other actionable P0, P1, or P2 was found in this fresh mobile pass.

## Source and comparison metadata

- Source visual/design truth: `website/.handoffs/gpt-taste-design-plan.md` version 1.1, supported by `website/DESIGN.md` where not overridden.
- Passed conformance input: `website/.handoffs/gpt-taste-implementation-verification.md` from independent verifier 05.
- Historical comparison input only: `website/design-qa.md` and `website/.handoffs/design-qa-mobile-01.md`. Their old two-line phone interpretation is superseded by plan v1.1.
- Implementation truth: fresh browser renders from `http://localhost:3011/` using a new Codex in-app Browser tab/session.
- The source authority is a written design contract, not a bitmap mock. Source pixels, source CSS dimensions, density normalization, and a same-frame bitmap composite are therefore `n/a`. The implementation was compared against the plan's exact responsive geometry, typography, component, interaction, asset, copy, accessibility, and motion decisions.

| Requested CSS viewport | Browser layout | Representative implementation pixels | Density | States |
|---|---|---|---|---|
| 320 x 720 | `innerWidth=320`, `clientWidth=305`, `clientHeight=720` before browser chrome allocation | viewport captures `305 x 686`; full page `304 x 12,286` | `devicePixelRatio=1.25` | hero, menu, all accordion states, marquee, gallery, stack, third slab, carousel, required form, top header |
| 375 x 812 | `innerWidth=375`, initial `clientWidth=375`; captured scrollbar surface 360px wide | hero `360 x 779`; full page `360 x 12,844` | approximately `1` | hero, dense bento, full page, stack text geometry |
| 390 x 844 | `innerWidth=390`, initial `clientWidth=390`; captured scrollbar surface 375px wide | viewport captures `375 x 811`; full page `375 x 12,952` | approximately `1` | hero, focus/menu, dense bento, third slab, footer, full page, stack text geometry |

The differing requested, client, and PNG widths are in-app Browser scrollbar/capture-surface behavior. Layout findings use live CSS geometry. No visual issue was filed from capture density or browser chrome.

## Full-view evidence

- `website/design-qa/responsive-qa-pass2-mobile-320x720-full.png`
- `website/design-qa/responsive-qa-pass2-mobile-375x812-full.png`
- `website/design-qa/responsive-qa-pass2-mobile-390x844-full.png`

These full-page images prove page reach and overall AIDA order. Focused viewport images are the authority for typography, sticky chrome, interactions, and the clipped stack-heading finding.

## Focused evidence

- Hero/header: `responsive-qa-pass2-mobile-320x720-hero.png`, `responsive-qa-pass2-mobile-320x720-top-header.png`, `responsive-qa-pass2-mobile-375x812-hero.png`, `responsive-qa-pass2-mobile-390x844-hero.png`, `responsive-qa-pass2-mobile-390x844-top-header.png`.
- Menu/focus: `responsive-qa-pass2-mobile-320x720-menu-open.png`, `responsive-qa-pass2-mobile-390x844-menu-focus.png`.
- Accordion: `responsive-qa-pass2-mobile-320x720-accordion-enclosed.png`, `responsive-qa-pass2-mobile-320x720-accordion-together.png`, `responsive-qa-pass2-mobile-320x720-accordion-printed.png`.
- Motion/fallbacks: `responsive-qa-pass2-mobile-320x720-marquee-paused.png`, `responsive-qa-pass2-mobile-320x720-gallery-static.png`, `responsive-qa-pass2-mobile-320x720-stack-static.png`.
- Blocking text and third-slab asset: `responsive-qa-pass2-mobile-320x720-stack-third.png`, `responsive-qa-pass2-mobile-390x844-stack-third.png`.
- Carousel/form/footer: `responsive-qa-pass2-mobile-320x720-carousel-note3.png`, `responsive-qa-pass2-mobile-320x720-form-required.png`, `responsive-qa-pass2-mobile-390x844-footer.png`.

All paths above are under `website/design-qa/` and were created by this fresh pass with the mandated `responsive-qa-pass2-mobile-` prefix.

## Required fidelity surfaces

- Fonts and typography: Cabinet Grotesk resolves for body/interface text at 16px/26.4px; Cormorant Garamond resolves at weight 500 for display text. The hero is correct under plan v1.1: 35.2px at every phone width, authored spans render 1 + 2 lines, punctuation is intact, and the editorial hierarchy remains dominant. The stack-heading clipping is the sole typography blocker.
- Spacing and layout rhythm: hero and content gutters resolve to approximately 24px; major sections retain the intended breathing room. The hero print remains inside the viewport. Last CTA-to-print clearance is 20.8px at all three widths, exceeding the required 12px. No component collision was observed outside the filed heading overflow.
- Colors and visual tokens: the body resolves to Off-white `rgb(251,248,242)` with Ebony `rgb(30,26,23)` text; primary hero actions are Off-white on Ebony. The warm Ivory/Paper photography-led treatment, restrained brass, flat slabs, and minimal elevation remain aligned with the plan.
- Image quality and asset fidelity: local approved photography renders with stable frames, meaningful alt text, and no CSS/SVG/emoji substitute. The specifically questioned third stack image passes mobile: at 320 it is complete, visible, opacity 1, `currentSrc` is the Next image for `/images/experience-printed.png` at `w=384`, and natural size is `224 x 280`; at 390 it is complete/visible with natural size `294 x 368`. Both are visibly populated in focused Browser captures.
- Copy and content: the approved hero, slab, carousel, and contact copy is present. The rendered/literal sweep found none of the banned price, duration, look, event-category, testimonial, rating, or client-proof strings.
- Icons and shortcuts: icon use remains intentionally minimal. No generic icon-card system, fake image asset, decorative blob, rounded-card factory, prompt leakage, or placeholder surface was found.

## Responsive, interaction, and accessibility checks

- Hero: passes v1.1 at 320/375/390 with Cormorant Garamond 35.2px, one-line first span plus two-line second span, no punctuation loss, no H1 clipping, 24px gutters, exactly two 48px CTAs, and no print intersection.
- Header visibility and targets: the transparent top header's Ebony brand is visually readable over the light warm hero image at 320, 375, and 390. It resolves to 26px/600 and `121.26 x 44px`; MENU resolves to `64 x 44px`. The repaired footer Email target resolves to `44 x 44px`. Instagram/Facebook are also at least 44px high.
- Menu: open/close, Escape close, and focus return to MENU pass. Mobile FIND/RENT actions render centered at full content width and 48px high. The menu actions close after selection, update the correct hash, and place both target sections below the 72px sticky header (`targetTop=87.7px`).
- Bento: mobile uses four equal columns with 16px gaps and `grid-auto-flow:dense`; measured tracks are 69.8px at 375 and 73.6px at 390, with all three cards closing the single-column 4-span arrangement and no void.
- Accordion: pointer activation passed for ENCLOSED, TOGETHER, and PRINTED. Each selected 320 item is 420px high, unselected items are 184px, labels and panels do not overlap, ARIA expanded state updates, and ArrowDown moves focus from ENCLOSED to the TOGETHER native button.
- Marquee and reduced motion: pause changes the control to PLAY MOTION with `aria-pressed=true`; both opposing tracks compute `animation-play-state:paused`. The active Browser reports no reduced-motion preference. Current source confines GSAP to `min-width:1024px` plus no-preference, statically resets gallery/stack below 1024px, and under reduced motion hides moving marquee tracks/control while showing wrapped static terms. Direct preference emulation is unavailable in the selected Browser.
- Mobile GSAP fallback: gallery heading is static; all four media stories are visible at opacity 1 and transform none. Stack slabs are static, transform none, opacity 1, and 16px gap-separated. The text sizing defect is independent of this correct motion fallback.
- Carousel: NEXT changes note 1 to 2; ArrowRight changes note 2 to 3; the polite live region updates; controls remain in viewport; there is no autoplay.
- Form: an empty START THE CONVERSATION attempt invokes native required validation and focuses Intent. Intent, Name, and Email are required; date, city/venue, and notes remain optional. Test values were entered but the mailto form was not submitted.
- Document and descendant overflow: document equality passes at all viewports. The general descendant sweep found only intentionally translated tracks inside the clipped marquee and screen-reader-only content. A text-range sweep then correctly found the masked slab-heading overflow filed above.
- Assets: the hero/print and all reached images report non-zero natural dimensions. The third stack asset was separately forced into view and visually verified at both 320 and 390.
- Accessibility: skip navigation, landmarks, native controls, ARIA disclosure relationships, visible focus, live carousel status, alt text, 16px inputs, and 44px persistent targets pass. Browser zoom/text scaling and physical-keyboard native Enter/Space activation remain untested environment limits.

## Console, hydration, load/resize/interact/reload

- Initial load, viewport resize sequence, and verified interactions produced zero console errors and zero warnings.
- A deep restored-scroll reload produced one Next.js development-only LCP suggestion for `/images/candid-guests.png`, matching the known deep-scroll advisory class recorded by the independent gpt gate. There was no error or hydration warning.
- A subsequent normal top reload at 320 produced no new console entries: `readyState=complete`, fonts loaded, H1 present, `scrollY=0`, no hydration/application/server-error text, and document width `305=305`.
- The deep-scroll LCP advisory is [P3] performance evidence only; it is not the responsive gate blocker.

## Comparison history

| Iteration | Authority/evidence | Result |
|---|---|---|
| Responsive QA 01 | historical desktop/tablet/mobile evidence | blocked on an old hero-wrap interpretation plus two undersized targets |
| Design revision + implementation 02 | plan v1.1 and its focused CSS implementation | three phone lines explicitly approved; 35.2px floor restored |
| Fresh gpt-taste gate 05 | independent source and Browser verification | passed; touch targets and revised hero accepted |
| Mobile responsive QA pass 2 | this fresh in-app Browser run and `responsive-qa-pass2-mobile-*` evidence | blocked by one newly observed P2 stack-heading clipping defect |

No production fix was made by this read-only QA agent, so no post-fix comparison exists yet.

## Implementation checklist

1. Repair only the mobile `.stackCopy h3` measure/type seam so all slab headings remain fully inside the 24px-inset slab content at 320/375/390.
2. Recheck text-range right edges for STEP INSIDE, BE YOURSELVES, and KEEP THE PHOTOGRAPH; do not rely only on document `scrollWidth`.
3. Run a fresh gpt-taste verification, then a fresh mobile responsive QA pass with same-width focused captures.

## Residual P3 test gaps

- Direct rendered `prefers-reduced-motion`, browser zoom/text scaling, and physical-keyboard native Enter/Space activation are unavailable in the selected in-app Browser.
- The deep restored-scroll development LCP advisory should remain classified separately from normal top-load console health unless it becomes reproducible on an ordinary top load.

final result: blocked
