# Desktop responsive design-QA handoff

Date: 2026-08-22  
Scope: desktop-only responsive QA; production files were not modified  
Implementation: `http://localhost:3011/`  
Source truth: `website/.handoffs/gpt-taste-design-plan.md`, with `website/DESIGN.md` where the plan does not override it

## Comparison basis

The source truth is a written implementation/design contract, not a fixed-pixel visual mock. Source pixel dimensions, CSS viewport, and density are therefore `n/a`; no pixel-perfect claim is made. The rendered implementation was judged against the plan's explicit typography, grid, spacing, palette, imagery, content, component, interaction, accessibility, and responsive commitments.

Browser-rendered evidence was captured in a fresh Codex in-app Browser session. The requested CSS viewports were `1440 x 1000` and `1280 x 800`.

- At requested `1440 x 1000`, the page reported `window.innerWidth = 1440`, `window.innerHeight = 1000`, and `devicePixelRatio = 1.25`. Viewport PNGs are `1425 x 990` pixels because the browser capture surface excludes browser/scrollbar chrome. The full-page PNG is `1424 x 13638` pixels.
- At requested `1280 x 800`, the page reported `window.innerWidth = 1280`, `window.innerHeight = 800`, and `devicePixelRatio ~= 1`. The viewport PNG is `1265 x 791` pixels for the same capture-surface reason.
- Density was recorded, not normalized against a source bitmap because no source bitmap exists.

## Browser evidence

Full-view evidence:

- `website/design-qa/responsive-qa-desktop-full-1440x1000.png` — full-page browser capture. The Browser's full-page stitch stretches/repeats sticky and GSAP-pinned regions, so it proves complete page reach but is not used for spatial fidelity judgments.
- `website/design-qa/responsive-qa-desktop-hero-1440x1000.png` — canonical 1440 desktop hero viewport.
- `website/design-qa/responsive-qa-desktop-hero-1280x800.png` — 1280 desktop resilience viewport.

Focused evidence:

- `website/design-qa/responsive-qa-desktop-action-anchor-sticky-1440x1000.png` — internal hero anchor destination and scrolled sticky navigation.
- `website/design-qa/responsive-qa-desktop-bento-hover-1440x1000.png` — exact dense bento and clickable-media hover.
- `website/design-qa/responsive-qa-desktop-accordion-together-pointer-1440x1000.png` — pointer-selected accordion state.
- `website/design-qa/responsive-qa-desktop-accordion-printed-keyboard-1440x1000.png` — keyboard focus transfer to `PRINTED`; the expanded state remains `TOGETHER` until authoritative activation.
- `website/design-qa/responsive-qa-desktop-marquee-paused-1440x1000.png` — visible pause control and paused rows.
- `website/design-qa/responsive-qa-desktop-gsap-gallery-reading-1440x1000.png` — pinned split-gallery reading zone.
- `website/design-qa/responsive-qa-desktop-gsap-gallery-exit-1440x1000.png` — prior story at opacity `0.2`/overlay `0.32` and next story entering.
- `website/design-qa/responsive-qa-desktop-stack-entry-1440x1000.png` — stack entry state.
- `website/design-qa/responsive-qa-desktop-stack-two-cards-1440x1000.png` — two-card stack state.
- `website/design-qa/responsive-qa-desktop-stack-three-cards-1440x1000.png` — three-card stack state with prior headers retained.
- `website/design-qa/responsive-qa-desktop-carousel-note2-1440x1000.png` — manual carousel, keyboard/live-state result, and focus treatment.
- `website/design-qa/responsive-qa-desktop-inquiry-filled-1440x1000.png` — required intent plus name/email and optional-field layout.

## Findings

No actionable P0, P1, or P2 finding remains.

No objective desktop mismatch was found between the written source truth and the rendered implementation. The page preserves the AIDA sequence, image-first hierarchy, authored two-line desktop hero, exact dense bento, horizontal accordion, opposing marquee, pinned split gallery, card stack, manual editorial-notes carousel, and two truthful contact paths without horizontal document overflow.

## Required fidelity surfaces

- Fonts and typography — passed. Browser font checks returned loaded Cabinet Grotesk 400/700 and Cormorant Garamond 500. Body is 16px/26.4px. The 1440 and 1280 hero is Cormorant Garamond 96px/86.4px with `-3.36px` tracking and exactly two authored lines; neither line clips at 1280. Labels/buttons retain the concise uppercase sans treatment.
- Spacing and layout rhythm — passed. The page keeps generous section breathing room, 72px sticky navigation, restrained 2–8px geometry, flat surfaces, balanced asymmetry, the planned bento closure, and readable editorial measures. The gallery and stack remain legible through their scroll states. No card collision or hidden persistent control was observed.
- Colors and tokens — passed. The rendered canvas is Off-white `rgb(251, 248, 242)` with warm ivory/cream photography-led regions, Ebony high-emphasis controls, restrained brass rules, and one intentional dark split. No neon, multicolor gradient, fake gold, glow, black-dominant treatment, or brass small body copy was observed. Hero buttons rendered Off-white on Ebony/dark veil with visible borders.
- Image quality and asset fidelity — passed. The required local booth, guests, print, and detail imagery renders sharply with stable crops/aspect ratios in the hero, bento, accordion, gallery, stack, and carousel. No placeholder imagery, CSS art, fake illustration, emoji, custom SVG art, party stock, or social-feed treatment was observed.
- Copy and content — passed. The fixed hero copy and truthful mall-booth/event-rental wording match the plan. No unconfirmed public price, duration, event category, package inclusion, customer testimonial, rating, location, hours, service area, or photographic-look offer appears.
- Icons — passed with no blocking drift. The UI intentionally uses almost no iconography. Carousel arrows are secondary typographic cues beside explicit `PREVIOUS`/`NEXT` labels, and the browser-native date glyph is coherent with its input. No fake illustrated icon system is present.
- Shapes and surfaces — passed. Surfaces are flat and editorial, buttons/inputs are restrained rectangles, media frames are quiet, and no generic bubbly cards, decorative blobs, stacked shadows, badges, or pill taxonomy appears.
- Accessibility — passed for browser-observable desktop behavior. Skip link and semantic landmarks are present; buttons/links/regions expose accessible names; accordion triggers expose `aria-expanded`/`aria-controls`; carousel exposes a labelled, focusable region plus `aria-live="polite"`; required intent/name/email fields are labelled; visible focus treatment is present; CTA/input targets are at least 44px high. Image alt text is purposeful.
- Viewport resilience — passed at 1440x1000 and 1280x800. At both widths `document.documentElement.scrollWidth === clientWidth` (`1425 === 1425` at 1440 and `1265 === 1265` at 1280). The hero remains two lines, primary actions remain reachable, and no desktop clipping or overlap regression was observed.
- AI-shortcut artifacts — passed. No prompt leakage, fake metrics, decorative numeric markers, unsupported content, unrelated stock, generic card factory, placeholder asset, or CSS/HTML-drawn image substitute was observed.

## Interactions, anchors, and state verification

- Sticky navigation — passed. It starts transparent at the hero and becomes `rgba(251, 248, 242, 0.97)` with a hairline border after scroll.
- Hero/navigation anchors — passed. `FIND A BOOTH`, `RENT FOTOHVN`, `EXPERIENCE`, `THE BOOTH`, `PRINTS`, and `ASK ABOUT YOUR DATE` reached the intended internal targets without horizontal drift.
- Bento hover — passed. The clickable image moved from `transform: none` to `matrix(1.05, 0, 0, 1.05, 0, 0)` after the specified 700ms transition; it remained clipped inside its frame.
- Accordion pointer and directional keyboard behavior — passed. Pointer selection moved `aria-expanded` to `TOGETHER`; ArrowRight moved focus to `PRINTED` while preserving the authoritative selected state. The in-app Browser's synthesized Enter/Space did not activate the native button, but the same synthesized Enter also failed to activate an ordinary native hero link. This is classified as a Browser-control limitation, not an implementation defect; the source uses native buttons with an ordinary click handler.
- Marquee — passed. Keyboard focus and explicit pause both set the two animated rows to `animation-play-state: paused`; the visible control changed from `PAUSE MOTION` to `PLAY MOTION` with `aria-pressed="true"`. The third measured row is the static reduced-motion row and correctly has no running animation.
- GSAP split gallery — passed. At desktop the left heading remained pinned while the right stories stayed in document flow. A reading-zone story reached scale `1`; an exited story reached opacity `0.2` with overlay `0.32`; the next story entered without horizontal translation.
- GSAP card stack — passed. Cards entered from approximately scale `0.94`/positive vertical offset, reached scale `1`, used increasing z-index values `1/2/3`, and preserved the prior card headers in the two- and three-card states.
- Carousel — passed. ArrowRight changed Note 1 to Note 2, updated the polite live status, and focus remained on the labelled carousel region. Previous/Next pointer controls both worked. The note remained unchanged during an autoplay observation window.
- Contact paths and inputs — passed. The event-rental intent radio, name, and email are required; date, city/venue, and notes are optional. Controls stay aligned and readable. The form truthfully exposes `mailto:hello@fotohavn.ph?subject=FOTOHVN%20inquiry` and the page says submission opens the email app. QA did not submit the mailto form or open an external application.
- Console/hydration — passed. Both desktop viewports returned no browser console errors or warnings; no hydration warning was observed.

## Residual test limitations

- The in-app Browser exposes no reduced-motion emulation switch in this session. The current browser reported the no-preference branch, so the reduced-motion rendering was not recaptured in this desktop QA pass. This is a test-surface limitation, not an observed implementation defect.
- The Browser's synthetic Enter/Space does not dispatch native activation in this session, demonstrated against both an accordion button and an ordinary native link. Directional keyboard handlers, focus movement, semantics, pointer activation, and visible focus were verified.
- The full-page screenshot stitch is visually unreliable around sticky/GSAP-pinned regions. Spatial judgments use the fresh focused viewport captures listed above.

## Implementation checklist

- No P0/P1/P2 desktop repair is required.
- Preserve the current two-line hero, exact dense bento, gallery pin/exit states, stack header retention, and truthful two-path inquiry structure.
- Retain a manual physical-keyboard and reduced-motion smoke check in final verification because the in-app Browser could not synthesize those two native conditions reliably.

result: passed
