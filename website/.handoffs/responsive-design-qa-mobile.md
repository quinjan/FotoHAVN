# Responsive Design QA - Mobile post-repair rerun

Date: 2026-08-22  
Reviewer: fresh post-repair mobile QA agent  
Implementation: `http://localhost:3011/`  
Browser: Codex in-app Browser only  
Production files modified: none

## Findings

- No actionable P0, P1, or P2 mobile finding remains.
- [P3] Native 200% browser zoom was not independently emulated by the in-app Browser.
  - Location: accessibility / text-scaling release check.
  - Evidence: the selected Browser exposes viewport overrides but not a native browser-zoom control. I did not substitute a different browser or mutate the page with CSS zoom.
  - Impact: low residual manual-test gap only. Wrapping resilience was browser-verified at 320, 375, and 390 requested CSS widths with no document overflow, clipping, collision, or unusable control.
  - Follow-up: include one native 200% zoom pass in release accessibility QA when that browser capability is available.

## Comparison target and normalization

- Source visual truth:
  - `website/.handoffs/gpt-taste-design-plan.md` version 1.1: mobile architecture, fixed copy, typography floors, spacing, interaction, and fallback authority.
  - `website/DESIGN.md`: FOTOHVN palette, typography hierarchy, photography, accessibility, and visual restraint.
  - `website/design-qa/reference-refero-viewport-1440x1000.png`: desktop-only structural-restraint reference. It is not mobile-state, palette, type, or imagery authority.
  - User clipping cue: `C:/Users/QUINJ3~1/AppData/Local/Temp/codex-clipboard-8d65a1db-1e4b-4b54-9625-a59962009e9c.png` (692 x 313 px). This is a cropped tablet defect report, not a same-viewport mobile source.
- Implementation source inspected:
  - `website/src/components/UpperExperience.tsx`
  - `website/src/components/UpperExperience.module.css`
  - `website/src/components/MiddleExperience.tsx`
  - `website/src/components/MiddleExperience.module.css`
  - `website/src/components/ClosingExperience.tsx`
  - `website/src/components/ClosingExperience.module.css`
  - `website/src/components/SiteChrome.tsx`
  - `website/src/components/SiteChrome.module.css`
  - `website/src/app/globals.css`
- State: public light page, unauthenticated, normal motion preference, hydrated Next.js development preview. The small circular `N` in captures is the Next.js development overlay and is excluded from product-fidelity judgments.

### Viewport and pixel normalization

| Browser viewport override | Page layout metrics | Focused PNG pixels | Full-page PNG pixels | Density handling |
|---|---|---|---|---|
| 320 x 720 CSS px | `innerWidth=320`, `clientWidth=305`, `scrollWidth=305`; Browser reported DPR 1.25 | 305 x 686 | 304 x 12236 | Browser output is normalized to one output pixel per captured page CSS pixel; reserved in-app Browser scrollbar/chrome is excluded from the PNG. |
| 375 x 812 CSS px | `innerWidth=375`, `clientWidth=360`, `scrollWidth=360`; DPR about 1.0 | 360 x 779 | 360 x 12777 | Same normalized capture behavior. |
| 390 x 844 CSS px | `innerWidth=390`, `clientWidth=375`, `scrollWidth=375`; DPR about 1.0 | 375 x 811 | 375 x 12882 | Same normalized capture behavior. |

The comparison boards preserve each capture's aspect ratio inside labeled cells. No false pixel-level judgment was made between the desktop structural source and the mobile implementation.

## Full-view comparison evidence

- `website/design-qa/postrepair1-responsive-mobile-source-vs-implementation.png` places the opened structural source and the fresh rendered mobile implementation in one combined comparison input.
- `website/design-qa/postrepair1-responsive-mobile-320x720-full.png`
- `website/design-qa/postrepair1-responsive-mobile-375x812-full.png`
- `website/design-qa/postrepair1-responsive-mobile-390x844-full.png`
- `website/design-qa/postrepair1-responsive-mobile-focused-hero-comparison.png` places the three fresh hero breakpoint captures together at normalized density.
- `website/design-qa/postrepair1-responsive-mobile-focused-states-comparison.png` combines fresh menu, bento, accordion, marquee, carousel, and form states for readable focused review.
- `website/design-qa/postrepair1-responsive-mobile-bento-regression-comparison.png` combines the user's clipped tablet cue and the fresh 320px mobile bento. The viewports are intentionally labeled as unmatched; this board is used only to verify that the tablet row repair did not carry clipping into the mobile reset.

## Focused browser evidence

All paths are under `website/design-qa/` and have the unique `postrepair1-responsive-mobile-*` prefix.

- Hero: `320x720-hero.png`, `375x812-hero.png`, `390x844-hero.png`
- Menu: `320x720-menu-open.png`
- Repaired bento regression seam: `320x720-bento.png`
- Accordion keyboard state: `320x720-accordion-keyboard-printed.png`
- Marquee paused: `320x720-marquee-paused.png`
- Natural gallery: `320x720-gallery-natural.png`
- Natural stack: `320x720-stack-natural.png`
- Carousel: `320x720-carousel-note3.png`
- Primary action path: `320x720-action-rent-path.png`
- Inquiry validation: `320x720-form-validation.png`
- Footer: `320x720-footer.png`

## Post-repair regression checks

### Tablet bento row repair did not regress mobile

- At 320px the reset is three intrinsic `340px` rows, four equal `52.2px` columns, and `16px` gaps. Each card is one full-width row (`256.8px` rendered content width).
- The lead card heading and paragraph are fully contained: card y=208..548, heading y=286.85..431.2, paragraph y=447.2..524. The same containment check passed for `FIND A BOOTH` and `RENT FOTOHVN`.
- At 375px the cards are 312px wide and all heading/paragraph boxes remain inside their 340px rows. At 390px they are 327.2px wide and remain contained.
- The complete `A LITTLE ROOM FOR REAL MOMENTS` heading, comma-free fixed copy, and proposition are visible. There is no top clipping, bottom clipping, overlap, hidden void, or horizontal overflow.
- The runtime source correctly resets tablet `grid-template-rows` to `none` below 768px and uses `grid-auto-rows: minmax(340px, auto)`.

### Accordion keyboard repair did not create double activation

- At 320px, ArrowDown moved focus from `ENCLOSED` to `TOGETHER` without changing the active panel. Enter selected only `TOGETHER`, exposed only `accordion-panel-together`, and retained focus.
- A second ArrowDown moved focus to `PRINTED`; Space selected only `PRINTED`, exposed only `accordion-panel-printed`, and retained focus. State was re-read after 700-800ms and remained stable, so no delayed native click reversed or duplicated activation.
- The same ArrowDown -> Enter -> ArrowDown -> Space sequence passed at 375px and 390px with exactly one expanded button and one visible controlled panel.
- Pointer selection remained functional. The mobile layout stayed a vertical disclosure list with 16px gaps, 184px collapsed slices, and one 420px expanded slice.

## Required fidelity surfaces

### Fonts and typography

- Browser font checks passed for Cabinet Grotesk 400/700 and Cormorant Garamond 500. Body computed to Cabinet Grotesk at 16px / 26.4px.
- The H1 computed to Cormorant Garamond 500 at the mandatory 35.2px floor, 31.68px line height, and approximately -0.035em tracking at all three phone widths.
- The authored spans remain intact. `PHOTOGRAPHS,` is one visual line and `DEVELOPED DIFFERENTLY.` wraps to two, producing the plan-approved three visual lines at 320/375/390. The comma and terminal period are intact and unclipped.
- Section headings, card headings, carousel quotation, labels, buttons, field labels, footer copy, and long strings wrap naturally without overlap or truncation. The `STEP INSIDE. BE YOURSELVES. KEEP THE PHOTOGRAPH.` heading takes a readable four-line mobile form rather than clipping.

### Spacing and layout rhythm

- All primary mobile containers retain 24px left/right gutters against the page content width.
- Hero CTAs are 48px high and stacked. At 320, 375, and 390 the editorial print begins exactly 12px below the second CTA.
- `scrollWidth === clientWidth` after the complete interaction sweep at every required viewport: 305/305, 360/360, and 375/375. Overflow clipping is not masking a sizing defect.
- The bento uses one card per row with 16px gaps. Accordion, gallery, and stack use natural vertical flow. Gallery media is full width with parent opacity 1 and transform none. Stack cards are `position: static`, `transform: none`, and separated by 16px.
- The action split becomes one column; the inquiry form becomes one column; footer links wrap cleanly.

### Colors and visual tokens

- Computed body canvas is Off-white `rgb(251, 248, 242)` and text is Ebony `rgb(30, 26, 23)`. The action band uses Warm Ivory; primary actions and footer use the planned restrained Ebony treatment.
- Brass is limited to thin accents/rules. No pastel taxonomy, neon, fake gold, glow, heavy shadow, or black-dominant treatment appears.
- CTA, form, accordion, carousel, and navigation states remain high contrast in the captured mobile views.

### Image quality and asset fidelity

- After scrolling each image-led section into view, all 17 rendered images reported `complete=true` with non-zero intrinsic dimensions and meaningful alt text.
- All visible media uses approved local FOTOHVN imagery. There is no CSS/div art, handcrafted SVG substitute, emoji, placeholder box, Picsum/remote stock, fake customer image, stretch, broken crop, halo, or decoded-image failure.
- Hero, bento, accordion, gallery, stack, and overlapping carousel crops preserve stable frames and the warm photographic direction.

### Copy and content

- Fixed hero, CTA, accordion, marquee, gallery, stack, carousel, action, inquiry, and footer copy is present and coherent at all required phone widths.
- Browser text sweep found none of the banned public claims/labels: `P8,500`, `3 HOURS`, offered look names, `SECTION 01`, `ABOUT US`, or `OUR STORY`.
- Carousel statements remain attributed to FOTOHVN, not customers. The form truthfully says submission opens the visitor's email app.

## Interactions, accessibility, and console

- Mobile menu pointer-open works at all three widths. All five menu targets are at least 48px high. Escape closes the menu and returns focus to the `MENU` button.
- Both hero actions were activated from the rendered hero: `FIND A BOOTH` reached `#find-a-booth`; `RENT FOTOHVN` reached `#rent-fotohavn`. Both target cards entered the viewport with the correct heading.
- Accordion pointer, arrow focus navigation, Enter, and Space passed as recorded above.
- Marquee control changed to `PLAY MOTION`, `aria-pressed=true`, and both animated tracks computed to `animation-play-state: paused`. Document overflow stayed zero. Source retains the reduced-motion static-row fallback; the Browser could not force an OS reduced-motion preference, so that sub-check is source-verified.
- Carousel pointer advanced Note 1 -> 2; ArrowRight advanced Note 2 -> 3; the polite live status and keyboard focus remained on the carousel region.
- Empty inquiry submission stayed local, focused required `Intent`, and left required name/email invalid. Form controls are full width and at least 49.4px high; radio labels are 52px high.
- Footer Instagram/Facebook/Email targets are 44px high; the email link has a 44px width floor.
- Skip link, one H1, main landmark, navigation landmarks, native controls, labels, ARIA expanded/controls state, visible focus, meaningful alt text, and 44px minimum effective touch targets are present.
- Browser console after the complete 320/375/390 interaction and asset sweep: `[]` for warning/error entries. No hydration, runtime, asset, or accessibility console error was observed.

## Comparison history

| Iteration | Earlier issue | Fresh post-repair evidence | Current status |
|---|---|---|---|
| Historical mobile gate | Mobile hero and later heading overflow/clipping | New 320/375/390 hero matrix, full-page captures, exact document widths | Closed |
| User tablet clipping report | Lead bento title/content clipped by fixed tablet row | Fresh mobile containment geometry plus combined regression board; tablet repair resets cleanly below 768px | No mobile regression |
| Responsive desktop gate | Accordion keyboard activation did not select the focused slice | Fresh 320/375/390 Enter/Space sequence with delayed state reads | Closed; no double activation |
| Earlier responsive pass | Zoom evidence unavailable | Still not natively emulatable in selected Browser | P3 manual gap only |

## Implementation checklist

- No mobile production repair is required.
- Preserve the current below-768px bento row reset and intrinsic 340px rows.
- Preserve explicit Enter/Space accordion selection and Arrow/Home/End focus-only navigation.
- Run one native 200% zoom check during release accessibility QA when supported.

final result: passed
