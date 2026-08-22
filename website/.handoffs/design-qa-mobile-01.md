# Mobile responsive design-QA — initial iteration

Date: 2026-08-22  
QA scope: mobile only; production files were not modified  
Implementation: `http://localhost:3011/`  
Source truth: `website/.handoffs/gpt-taste-design-plan.md`, then `website/DESIGN.md` where not overridden  
Result: `blocked`

## Comparison metadata

The authority is a written implementation/design specification, not a pixel-based mock. Source pixels and a same-frame source/implementation composite are therefore not applicable. The implementation was rendered and inspected in the explicitly requested Codex in-app Browser. No density normalization was applied.

| Requested CSS viewport | Browser-reported CSS viewport | Representative implementation pixels | State |
|---|---|---|---|
| 390 x 844 | `innerWidth=390`, `innerHeight=844`, `devicePixelRatio=1.25`; the in-app content/scrollbar capture was 375 x 811 | `website/design-qa/responsive-qa-mobile-390x844-hero.png` (375 x 811); long page 375 x 12,997 | hero, full/long page, menu, accordion, marquee, gallery, stack, carousel, form, footer |
| 375 x 812 | `innerWidth=375`, `innerHeight=812`, `devicePixelRatio=1` | hero 375 x 812; long page 360 x 12,844 after in-app scrollbar allocation | hero and full/long page |
| 320 x 720 | `innerWidth=320`, `innerHeight=720`, `devicePixelRatio=1`; viewport captures were 305 x 686 while browser chrome/scrollbar was present | hero/menu/accordion/form 305 x 686; long page 304 x 12,286 | mandatory narrow view, menu, accordion, form validation, long page |

The differing screenshot pixel widths are an in-app Browser capture artifact. Layout assertions use browser-reported CSS viewport and `documentElement.clientWidth/scrollWidth`; the direct 375 x 812 hero capture independently confirms the wrapping finding below.

## Findings

- [P2] The hero reaches three lines before the 320px exception.
  - Fidelity surface: typography, wrapping, above-the-fold layout, responsiveness.
  - Location: hero `h1`, implemented by `UpperExperience.module.css` `.heroHeading`.
  - Evidence: at 375 x 812, `PHOTOGRAPHS,` occupies one line and the authored `DEVELOPED DIFFERENTLY.` span occupies two lines (three total); see `website/design-qa/responsive-qa-mobile-375x812-hero.png`. The 390 run showed the same composition in its 375px in-app content capture. At 320 x 720 the heading is also three lines, which is allowed; see `website/design-qa/responsive-qa-mobile-320x720-hero.png`.
  - Expected: the plan says the hero is two authored lines where space permits and may naturally become three lines at 320px. The direct 375px result should still preserve the two-line composition.
  - Impact: this materially changes the above-the-fold hierarchy at mainstream phone widths and weakens the selected ultra-wide hero composition.
  - Fix: keep the 24px gutters and 2.2rem small-phone floor, but add a narrowly scoped 361–400px treatment that lets the second authored line fit, such as a slightly tighter plan-approved tracking value around `-0.05em`; retest 390, 375, and 320 and keep 320 at no more than three lines.

- [P2] Two persistent navigation/contact targets do not meet the recorded 44px minimum target size.
  - Fidelity surface: accessibility, interaction targets.
  - Location: header brand link and footer `Email` link.
  - Evidence: browser geometry measured the `FOTOHVN` brand link at 121.26 x 26 CSS px and the footer `Email` link at 36.38 x 44 CSS px at mobile widths. Other tested CTA, menu, carousel, and form label targets met or exceeded 44px in both dimensions.
  - Expected: the design plan requires 44px minimum targets for interactive elements.
  - Impact: the brand link has a shallow hit area and the short footer label has a narrow hit area, reducing reliable touch use.
  - Fix: make both anchors inline-flex and enforce `min-height: 44px; min-width: 44px`; add restrained inline padding to the short footer link without changing the visible footer rhythm.

## Full-view comparison evidence

- 390 long page: `website/design-qa/responsive-qa-mobile-390x844-long-page.png`
- 375 long page: `website/design-qa/responsive-qa-mobile-375x812-long-page.png`
- 320 long page: `website/design-qa/responsive-qa-mobile-320x720-long-page.png`

Across the long views, the AIDA sequence remains intact, the warm/off-white/ebony palette is consistent, photography remains primary, sections retain calm rhythm, and no generic rounded-card, fake SVG/image, emoji, gradient, or placeholder shortcut was visible. All local editorial images loaded with non-zero natural dimensions after progressive scrolling. Crops were stable and sharp at the tested widths.

## Focused browser evidence

- Hero: `responsive-qa-mobile-390x844-hero.png`, `responsive-qa-mobile-375x812-hero.png`, `responsive-qa-mobile-320x720-hero.png`.
- Menu: `responsive-qa-mobile-390x844-menu-open.png`, `responsive-qa-mobile-390x844-menu-action-focus.png`, `responsive-qa-mobile-320x720-menu-open.png`.
- Accordion: `responsive-qa-mobile-390x844-accordion-together.png`, `responsive-qa-mobile-390x844-accordion-printed-focus.png`, `responsive-qa-mobile-320x720-accordion-printed.png`.
- Motion/fallbacks: `responsive-qa-mobile-390x844-marquee-paused.png`, `responsive-qa-mobile-390x844-gallery-natural.png`, `responsive-qa-mobile-390x844-stack-natural.png`.
- Carousel: `responsive-qa-mobile-390x844-carousel-slide-2.png`.
- Form/footer: `responsive-qa-mobile-390x844-form-required.png`, `responsive-qa-mobile-390x844-form-filled.png`, `responsive-qa-mobile-320x720-form-required.png`, `responsive-qa-mobile-390x844-footer.png`.

## Rubric and interaction results

- Fonts/typography: Cabinet Grotesk 400/700 and Cormorant Garamond 500 reported loaded. Body text is 16px/1.65 and the serif/sans hierarchy is coherent. The actionable hero wrap is recorded above; no clipping or truncation was found elsewhere.
- Spacing/gutters/rhythm: content uses 24px mobile gutters. At 320 the bento resolves to four 52.2px tracks with 16px gaps, `grid-auto-flow: dense`, and all three cards span four columns/one row with 16px vertical gaps and no void. Section rhythm remains generous.
- Colors/tokens/contrast: body resolved to Off-white `rgb(251,248,242)` with Ebony text; the primary hero action resolved to Off-white on Ebony. Brass is used as a thin detail, not small body copy. No visually evident contrast blocker was found.
- Images/assets: approved local photography is used throughout with meaningful alt text. No CSS art, handcrafted SVG image substitute, fake avatar, emoji, unrelated stock image, or visible placeholder was found.
- Copy/content: core hero and truthful contact-path copy match the plan. No price, duration, package inclusion, photographic-look claim, testimonial, rating, client mark, event-type promise, or other banned factual fabrication was visible.
- Shapes/surfaces: 4px controls, restrained frames/hairlines, flat editorial slabs, and minimal elevation match the contracts. No bubbly/pill-card drift was found.
- Menu: open/close, outside-click close, Escape close, and focus return passed. Mobile actions are full-width, aligned, 48px high, and equally reachable. Focus is visible.
- Hero: both CTAs are 48px high and separated from the overlapping print by 12px at 320/375; no CTA, print, or heading overlap was found. The heading remains within three lines and does not clip at 320.
- Accordion: all three pointer-selected states render as one 420px expanded item plus two 184px collapsed items with 16px gaps and no overlap. ArrowUp/ArrowDown moved focus among triggers. In this in-app Browser run, synthetic Enter/Space fired keydown handlers but did not synthesize the native button click; this is treated as an automation artifact because the controls are native `button` elements, selection is wired to `onClick`, and the only prevented keys are Arrow/Home/End. It is not filed as a product defect; a physical-keyboard rerun is advisable.
- Marquee/reduced motion: pause/play click toggled `aria-pressed`; both marquee rows resolved to `animation-play-state: paused`. The active browser reported `prefers-reduced-motion: false`. Source inspection confirms the reduced-motion rule hides both animated tracks and the control and shows the static wrapped terms; the in-app Browser does not expose media-preference emulation, so the reduced rendering was not directly captured.
- GSAP mobile fallbacks: gallery heading/media resolve to `position: static`, `transform: none`, full opacity and natural flow. All three stack cards resolve to `position: static`, `transform: none`, zero overlap, and 16px gaps.
- Carousel: manual Next and Previous controls are 44px high and remain within the viewport. Click and ArrowRight/ArrowLeft changed slides; the polite live region updated from note 2 to note 3. No autoplay or horizontal scroll appeared.
- Contact form: missing required intent focuses the Mall booth radio and exposes native validation. Intent, Name, and Email are required; optional fields remain optional. Text inputs are 16px and full-width at 390 and 320. The truthful email-app note is visible. The form was not submitted.
- Viewport resilience: `documentElement.scrollWidth === documentElement.clientWidth` passed at 390, 375, and 320. A descendant-level overflow sweep at 320 found no clipped element outside the intentionally translated, overflow-hidden marquee tracks and screen-reader text.
- Anchors: mobile `FIND A BOOTH` and `RENT FOTOHVN` links closed the menu, updated the hash, and aligned their targets immediately beneath the sticky header.
- Browser console: no error or hydration message was recorded after reloads, resizes, and interactions. One non-blocking Next.js development warning reported `/images/printed-strips.png` as LCP and suggested eager loading; classify this as P3 performance polish, not a responsive design blocker.
- Text/zoom resilience: wrapping and 16px form inputs were checked at the mandatory narrow viewport. Browser zoom was not changed because the in-app Browser exposes viewport size but not a zoom or text-scale control.

## Open questions / residual test gaps

- Direct reduced-motion rendering and browser zoom/text scaling could not be emulated by the selected in-app Browser. The code-level reduced-motion contract is present, but a future run with those environment preferences enabled would close the evidence gap.
- The native Enter/Space activation should be spot-checked with a physical keyboard because the selected Browser's synthetic key path did not produce default button activation even though Arrow-key handlers and carousel keyboard handling did run.

## Comparison history

- Initial iteration: captured fresh 390, 375, and 320 evidence. No production fix was made by this QA agent. Two actionable P2 findings remain: early hero wrapping and undersized persistent link targets.

## Implementation checklist

1. Restore the two-line hero at 390/375 without reducing 24px gutters or letting the 320 heading exceed three lines.
2. Expand the header brand and footer Email hit areas to at least 44 x 44 CSS px.
3. Rerun fresh mobile QA at 390 x 844, 375 x 812, and 320 x 720, including the same focused states and console check.

final result: blocked
