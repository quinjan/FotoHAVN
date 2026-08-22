# Tablet responsive design-QA handoff

Date: 2026-08-22  
Scope: tablet-only responsive QA; production files were not modified  
Implementation: `http://localhost:3011/`  
Source truth: `website/.handoffs/gpt-taste-design-plan.md`, with `website/DESIGN.md` where the plan does not override it  
Result: `blocked`

## Comparison basis and metadata

The source truth is a written implementation/design contract rather than a fixed-pixel mock. Source pixel dimensions, source CSS viewport, density normalization, and a same-frame source/implementation bitmap composite are therefore `n/a`. The rendered website was compared against the plan's explicit typography, six-column tablet math, spacing, palette, imagery, content, component, interaction, accessibility, motion, and breakpoint commitments.

Fresh browser-rendered evidence was captured in the explicitly requested Codex in-app Browser.

| Requested CSS viewport | Browser-reported CSS viewport and density | Implementation capture pixels | State |
|---|---|---|---|
| 820 x 1180 | `innerWidth=820`, `innerHeight=1180`, initial `devicePixelRatio=1.25`; layout content width `805` after scrollbar allocation | viewport PNGs `805 x 1158`; full-page PNG `804 x 11771` | representative tablet hero, bento, accordion, marquee, static GSAP fallbacks, carousel, menu, form, console |
| 768 x 1024 | `innerWidth=768`, `innerHeight=1024`, `devicePixelRatio~=1`; layout content width `753` | hero PNG `753 x 1004` | lower tablet/breakpoint resilience |
| 1024 x 768 | `innerWidth=1024`, `innerHeight=768`, `devicePixelRatio~=1`; layout content width `1009` | viewport PNGs `1009 x 757` | upper tablet/desktop-motion boundary, six-column bento, GSAP pin/scrub/stack |

The differing PNG widths/heights are the in-app Browser capture surface excluding scrollbar/browser chrome. Layout assertions use the browser-reported CSS viewport and `documentElement.clientWidth/scrollWidth`.

## Findings

- [P2] The hero reaches three visual lines at the 768px tablet boundary.
  - Fidelity surface: fonts/typography, above-the-fold layout, responsiveness.
  - Location: hero `h1`, implemented by `UpperExperience.module.css` `.heroHeading`.
  - Evidence: at 768 x 1024, `PHOTOGRAPHS,` occupies the first line while the authored `DEVELOPED DIFFERENTLY.` span wraps into `DEVELOPED` and `DIFFERENTLY.`, producing three visible lines. The heading measured `656.8px` wide at `57.6px/51.84px`; see `website/design-qa/responsive-qa-tablet-768x1024-top.png`. At 820 x 1180 and 1024 x 768 it remains two lines.
  - Expected: the design plan requires exactly two visible authored lines on desktop/tablet and permits three only at 320px.
  - Impact: the wrap materially changes the selected ultra-wide hero composition and above-the-fold hierarchy at a canonical tablet width.
  - Fix: add a narrowly scoped lower-tablet treatment that preserves the approved type floor and 48px tablet gutters while fitting the second authored line, for example by reducing the 768px hero size within the existing fluid range and/or tightening tracking within the plan-approved editorial treatment. Retest 768, 820, and 320 so 320 remains no more than three lines.

- [P2] Two persistent navigation/contact targets do not meet the 44 x 44 tablet touch-target contract.
  - Fidelity surface: accessibility, interactions, touch ergonomics.
  - Location: sticky header brand link and footer `Email` link.
  - Evidence: at 820px, the browser measured `FOTOHVN, back to the top` at `121.26 x 26` CSS px and the footer `Email` link at `36.38 x 44` CSS px. Other tested menu actions, CTAs, carousel controls, form fields, and buttons met or exceeded 44px in both dimensions.
  - Expected: the design plan requires 44px minimum touch targets for interactive elements.
  - Impact: the shallow header hit area and narrow footer email hit area reduce reliable touch use on tablets.
  - Fix: make both links `inline-flex` with `min-width: 44px; min-height: 44px` and restrained padding/alignment that preserves the current editorial rhythm. This is the same shared production seam observed by mobile QA and should be repaired once.

## Browser evidence

Full-view evidence:

- `website/design-qa/responsive-qa-tablet-820x1180-full.png` — complete-page reach. The in-app Browser's full-page stitch stretches sticky/GSAP regions, so this image is not used for spatial fidelity judgments.
- `website/design-qa/responsive-qa-tablet-820x1180-top.png` — canonical tablet hero.
- `website/design-qa/responsive-qa-tablet-768x1024-top.png` — lower tablet boundary and hero-wrap finding.
- `website/design-qa/responsive-qa-tablet-1024x768-top.png` — upper tablet boundary, two-line hero, six-column bento math, and desktop-motion media query.

Focused evidence:

- `website/design-qa/responsive-qa-tablet-820x1180-bento-accordion.png` — exact tablet bento and horizontal accordion boundary.
- `website/design-qa/responsive-qa-tablet-820x1180-accordion-printed.png` — pointer-selected `PRINTED` accordion state.
- `website/design-qa/responsive-qa-tablet-820x1180-marquee-paused.png` — explicit paused marquee state.
- `website/design-qa/responsive-qa-tablet-1024x768-gsap-boundary.png` — pinned split gallery at the 1024px activation boundary.
- `website/design-qa/responsive-qa-tablet-1024x768-card-stack.png` and `website/design-qa/responsive-qa-tablet-1024x768-card-stack-overlap.png` — stack entry and 72px retained-header overlap.
- `website/design-qa/responsive-qa-tablet-820x1180-carousel-next.png` — manual second carousel note and controls.
- `website/design-qa/responsive-qa-tablet-820x1180-menu-open.png` — open tablet navigation with both visitor paths.
- `website/design-qa/responsive-qa-tablet-820x1180-form-validation.png` — selected event-rental intent, required-field validation, form layout, and footer.

## Required fidelity surfaces

- Fonts and typography — blocked only by the recorded 768px hero wrap. Cabinet Grotesk 400/700 and Cormorant Garamond 500 reported loaded; the body resolves to Cabinet Grotesk and major headings to Cormorant. At 820 and 1024 the hero remains exactly two visible lines with punctuation intact. No clipping, truncation, cramped body copy, or fallback-font substitution was observed elsewhere.
- Spacing and layout rhythm — passed apart from the target-size finding. At 820 the content uses 48px gutters. The bento resolves to six `98.125px` tracks, 24px gaps, rows `160/160/160`, and exact spans `6x1 + 3x2 + 3x2`; `grid-auto-flow: dense` is active and no void appears. Sections retain generous editorial rhythm, restrained 2–8px geometry, flat surfaces, and stable natural-flow spacing.
- Colors and tokens — passed. Body canvas resolved to Off-white `rgb(251, 248, 242)` with Ebony `rgb(30, 26, 23)` text, warm ivory/cream photography-led regions, restrained brass rules, and one intentional dark split. No neon, fake gold, multicolor gradient, glow, black-dominant treatment, or brass small body copy was observed.
- Image quality and asset fidelity — passed. Required local booth, guest, print, and detail images loaded with non-zero dimensions, stable aspect ratios, warm editorial crops, purposeful alt text, and no visible compression/halo defect. No placeholder art, CSS/div drawing, handcrafted SVG substitute, emoji, unrelated party stock, fake avatar, or social-feed treatment appears.
- Copy and content — passed. Fixed hero copy and the truthful mall-booth/event-rental paths match the plan. No unconfirmed price, duration, inclusion list, event-category claim, customer testimonial, rating, location, hours, response time, photographic-look offer, or service-area promise is published. The visible word `vintage` appears only in the approved booth/hero description.
- Icons — passed. The interface intentionally uses minimal iconography. Carousel arrows remain secondary to explicit `PREVIOUS`/`NEXT` labels; the browser-native date glyph is coherent. No generic icon-card system or fake illustrative iconography is present.
- Shapes and surfaces — passed. Media frames, buttons, inputs, editorial slabs, borders, and radii remain restrained; there are no oversized pills, bubbly cards, decorative blobs, badges, fake metallic effects, or stacked shadows.
- Accessibility — blocked only by the two measured touch targets. Skip navigation and semantic landmarks are present; all images have meaningful alt text; accordion buttons expose `aria-expanded`/`aria-controls`; the carousel is a labelled focusable region with `aria-live=polite`; form controls are labelled and required/optional states are truthful; visible focus treatment is present on tested controls.
- Viewport resilience — blocked only by the 768px hero wrap. `documentElement.scrollWidth === clientWidth` passed at 820 (`805 === 805`), 768 (`753 === 753`), and 1024 (`1009 === 1009`). No unintended horizontal document scroll, clipping, card collision, or hidden persistent control was found.
- AI-shortcut artifacts — passed. No prompt leakage, raw plan text, generic rounded-card factory, fake metric, decorative numeric marker, unsupported claim, unrelated stock, CSS-art imagery, placeholder surface, confetti, neon, or novelty-filter language was observed.

## Interaction, motion, and browser-console verification

- Navigation — passed aside from target geometry. At 820 the `MENU` opens a stable panel with `EXPERIENCE`, `THE BOOTH`, `PRINTS`, and equally reachable `FIND A BOOTH`/`RENT FOTOHVN` actions. Escape closes it and restores focus to `MENU`. The nav remains stable at 768 and 1024. Both hero contact anchors updated the hash and aligned their targets immediately under the sticky header (`targetTop ~= 87.85px`).
- Accordion — passed. Pointer selection moved the expanded state to `PRINTED`; ArrowLeft moved focus to `TOGETHER` without changing authoritative selection. Expanded content remains readable and all three media slices remain inside the 48px tablet gutters. The in-app Browser's synthesized Enter/Space did not produce native activation, but the same synthetic Enter also failed on an ordinary native link. This is classified as a Browser-control limitation, not an implementation defect; source inspection confirms native `button` controls with ordinary `onClick` selection and only directional/Home/End keys prevented.
- Marquee — passed. The visible control changed from `PAUSE MOTION` to `PLAY MOTION`; both tracks changed from `animation-play-state: running` to `paused`. The rows remain clipped within the marquee section without document overflow.
- GSAP tablet fallback and 1024 boundary — passed. At 820, `(min-width: 1024px)` is false; all four gallery figures and three stack cards resolve to natural flow, `position: static`, `transform: none`, and opacity `1`. At 1024 the pinned heading parent becomes fixed after the trigger, gallery media interpolate through scale `0.8`/opacity `0.2` to the reading zone and back to opacity `0.2`, and stack cards enter from scale `0.94`/positive vertical offset with z-indices `1/2/3`. In the overlap state the first two cards sit at `top=96` and `top=168`, retaining exactly 72px of the prior header.
- Carousel — passed. Pointer `NEXT` moved to Note 2; ArrowRight moved to Note 3; the polite live region updated each time. Controls are at least 44px high, no autoplay was observed, and the composition remains within the viewport.
- Contact form — passed. Event-rental intent selection works. Intent, Name, and Email are required; event date, city/venue, and notes remain optional. An empty submit was safely stopped by native validation and focused Name; no mail app or external action was launched. Text inputs measure 52px high and the submit action 69.2px high.
- Reduced motion — source contract verified; browser emulation unavailable. The selected in-app Browser exposes viewport and visibility only, not media-preference emulation. Source inspection confirms GSAP is registered only for `(min-width: 1024px) and (prefers-reduced-motion: no-preference)`, cleanup reverts `matchMedia` and context, CSS forces gallery/stack final static states, hides animated marquee rows/control, and exposes the static wrapped row. This is a residual test-surface gap, not an observed defect.
- Browser console/hydration — passed. No `error`, `warn`, or `warning` entry appeared after viewport changes, interactions, scrolling through both GSAP sections, menu state changes, carousel changes, and form validation. No hydration warning was observed.

## Comparison history

- Initial tablet iteration: fresh 820, 768, and 1024 evidence was captured. No production fix was made by this QA agent. Two actionable P2 findings remain: the 768px hero wrap and undersized persistent links.

## Implementation checklist

1. Restore the two-line hero at 768px without weakening the approved 48px tablet gutters or allowing more than three lines at 320px.
2. Expand the header brand and footer Email targets to at least 44 x 44 CSS px.
3. Rerun fresh tablet QA at 768 x 1024, 820 x 1180, and 1024 x 768, including the same focused interaction, GSAP/static-fallback, overflow, and console checks.

result: blocked
