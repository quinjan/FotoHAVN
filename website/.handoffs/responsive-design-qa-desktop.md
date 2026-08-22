# Responsive Design QA — Desktop post-repair rerun

Date: 2026-08-22  
Agent: fresh desktop responsive design-QA agent  
Scope: complete desktop rerun after the accordion-keyboard and tablet-bento repairs  
Canonical implementation: `http://localhost:3011/`  
Production files modified by this agent: none

## Result

The earlier P2 is closed. Pointer, Arrow, Enter, and Space behavior now agree: arrow keys move focus without changing selection; Enter or Space selects the focused slice, updates `aria-expanded`, reveals its controlled panel, hides the previous panel, and retains focus. This was reproduced independently at both required desktop widths.

No actionable P0, P1, or P2 remains. No new P0-P2 was found in the complete desktop experience.

## Source visual truth

- Primary implementation authority: `website/.handoffs/gpt-taste-design-plan.md`, version 1.1.
- Current conformance gate: `website/.handoffs/gpt-taste-implementation-verification.md`, `result: passed`.
- FOTOHVN visual system: `website/DESIGN.md`, `website/variables.css`, `website/theme.css`, `website/tokens.json`, and the approved local assets under `website/public/images/`.
- Structural-only reference captures: `website/design-qa/reference-refero-viewport-1440x1000.png` and `website/design-qa/reference-refero-full-1440x1000.png`. Per the plan, these provide restraint, whitespace, flat surfaces, hierarchy, low elevation, and grid discipline; they are not palette, typography, content, imagery, or pixel-match authority.
- Fresh source capture opened in the in-app Browser: `http://localhost:3011/images/hero-booth.png`, saved as `website/design-qa/postrepair1-responsive-desktop-source-hero-1440x1000.png`. The browser reported natural image pixels `1586 × 992`; the source-image tab rendered at `1280 × 720`, DPR `1.25`.

## Implementation capture normalization

| Requested CSS viewport | Browser inner size | Browser content/capture pixels | DPR | State coverage |
|---|---:|---:|---:|---|
| `1440 × 1000` | `1440 × 1000` | viewport captures `1425 × 990`; raw full page `1424 × 13638` | approximately `1.0` | full interaction and motion-state sweep |
| `1280 × 800` | `1280 × 800` | viewport captures `1265 × 791`; raw full page `1264 × 12415` | approximately `1.0` | responsive integrity plus repaired keyboard proof and sampled motion/action states |

The 15px content-width difference is the in-app Browser's reserved scrollbar; the 9-10px capture-height difference is browser capture trim. Comparisons use proportional CSS-sized tiles and make no false pixel-perfect claim. Sticky/pinned elements can repeat in raw full-page capture mode, so the full-flow comparison board uses fresh viewport states for layout judgment.

States tested: initial hero/header, scrolled header, bento default and hover, accordion pointer/Arrow/Enter/Space, marquee manual and focus pause, GSAP pinned-gallery reading and exit, card-stack entry/overlap/final, carousel pointer/keyboard/no-autoplay, contact anchors, required and filled inquiry states, full-page flow, overflow, assets, focus/ARIA, and console.

## Combined comparison evidence

- Full-flow comparison: `website/design-qa/postrepair1-responsive-desktop-full-comparison-board.png` (`1440 × 999`). It combines the structural source with the current hero, bento hover, repaired accordion, paused marquee, pinned gallery, card stack, carousel, and action/form states in one image.
- Focused source comparison: `website/design-qa/postrepair1-responsive-desktop-focused-comparison.png` (`1440 × 333`). It combines the structural source, fresh approved hero-asset capture, and repaired hero implementation.
- Focused repair history: `website/design-qa/postrepair1-responsive-desktop-accordion-repair-comparison.png` (`1440 × 300`). It combines the earlier Space failure, post-repair Enter selection, and post-repair Space selection.
- Focused comparisons were required because keyboard focus, selected accordion panel, marquee control state, GSAP overlap, carousel text, and form controls are not readable enough in a long-page image.

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: none requiring follow-up.

The initial desktop header is intentionally Off-white rather than transparent. This remains accepted under the plan's “where contrast permits” clause because a transparent split nav would cross materially variable hero pixels and lose consistent contrast. It remains a 72px sticky header with a hairline rule after scroll.

The circular Next.js development-tools control visible in local captures is preview chrome, not a production design element, and is excluded from fidelity judgment.

## Closure of the earlier P2

### Prior state

The previous desktop handoff recorded that ArrowRight moved focus correctly, but Enter and Space did not activate the focused accordion slice. Pointer selection worked.

### Repair

`website/.handoffs/repair-accordion-keyboard.md` records the bounded change in `website/src/components/UpperExperience.tsx`: explicit handling for `Enter`, modern Space (`" "`), and legacy `Spacebar`, with `preventDefault()` and selection of the focused accordion item. Arrow/Home/End behavior and native button/ARIA markup were preserved.

### Fresh same-state browser proof

- At `1440 × 1000`, `ENCLOSED` began selected. ArrowRight moved focus to `TOGETHER` while `ENCLOSED` remained expanded. Enter then produced `TOGETHER aria-expanded="true"`, its panel `hidden=false`, the other panels `hidden=true`, and focus remained on `TOGETHER`. A second ArrowRight plus Space produced the equivalent correct state for `PRINTED` with focus retained.
- At `1280 × 800`, the identical sequence and all expanded/hidden/focus assertions passed.
- Pointer activation of `ENCLOSED` still worked.
- Evidence: `postrepair1-responsive-desktop-accordion-pointer-enclosed-1440x1000.png`, `postrepair1-responsive-desktop-accordion-enter-1440x1000.png`, `postrepair1-responsive-desktop-accordion-space-1440x1000.png`, `postrepair1-responsive-desktop-accordion-enter-1280x800.png`, and `postrepair1-responsive-desktop-accordion-space-1280x800.png`.

Earlier P2 status: closed.

## Required fidelity surfaces

### Fonts and typography — passed

- The hero computed as Cormorant Garamond 500 with fallbacks, `96px`, `86.4px` line height, and `-3.36px` tracking at both desktop widths.
- Both authored spans rendered as exactly two unclipped visual lines with punctuation intact. H1 geometry was `1152px` at 1440 and `1136.8px` at 1280; in both cases `scrollWidth === clientWidth` for the heading.
- Cabinet Grotesk remains the body/interface face with Manrope/system fallbacks. Browser font state was `loaded`. Body text remains at least 16px/1.6-ish and controls retain the planned 12-13px tracked uppercase treatment.
- Section headings, accordion labels, marquee, quotation, form labels, and footer remain readable, optically distinct, and untruncated.

### Spacing and layout rhythm — passed

- The hero measured 880px high at 1440 and 720px at 1280, preserving the planned desktop floor and right-weighted booth composition.
- The hero uses 64px-class desktop gutters, exactly two CTAs, and an overlapping print that remains clear of copy and actions.
- At 1440, the bento computed twelve `84.6625px` columns, two `280px` rows, `24px` gaps, `grid-auto-flow: dense`, a `7 × 2` lead card, and two `5 × 1` cards. At 1280, it retained twelve `72.725px` columns, the same rows/gaps/flow, and no void.
- Every bento card had `scrollWidth === clientWidth`; the supplied tablet clipping regression did not reproduce at desktop widths.
- Major sections retain generous 80-144px rhythm, 24px desktop media gaps, flat surfaces, and low/no shadow treatment. No clipped heading, intersecting control, collapsed section, or footer collision was visible.
- Document overflow passed at both widths: `scrollWidth === clientWidth` (`1425 === 1425` in the final clean 1440 session; `1265 === 1265` at 1280).

### Colors and visual tokens — passed

- Off-white, Warm Ivory, Cream Paper, and warm photography dominate. Ebony is limited to high-emphasis actions/footer and the event path; brass remains a restrained rule/selection accent.
- Button text/background contrast remains Off-white-on-Ebony/Dark Walnut or Ebony-on-light. No small brass body text, multicolor gradient, fake gold, glow, neon, or black-dominant cinematic treatment was observed.

### Image quality and asset fidelity — passed

- All 17 rendered images completed with non-zero intrinsic dimensions; no broken image remained in either desktop session.
- Hero, enclosure, guests, Photo Strips, booth detail, and printed-keepsake imagery use approved local assets through Next Image with meaningful alt text and stable proportions.
- Crops remain crisp and warm with no transparency halos, raster stretching, unrelated stock, CSS drawings, emoji, handcrafted SVG replacements, or placeholder art.
- Clickable bento media reached `matrix(1.05)` after `0.7s` and remained clipped by an `overflow: hidden` frame. Passive media did not imply clickability.

### Copy and content — passed

- The AIDA flow and all fixed hero, bento, accordion, marquee, gallery, stack, carousel, action, form, and footer copy match plan v1.1.
- No public price, three-hour duration, seven-item inclusion list, offered photographic looks, fabricated customer testimonial/rating, event-type claim, location, hours, response time, service area, attendee/setup/customization claim, numeric section marker, generic `OUR STORY`, hero badge/stat, or extra CTA was visible.
- Carousel statements remain attributed to FOTOHVN, not customers. The inquiry truthfully states that submitting opens the email app.

## Components, interactions, and accessibility

- Navigation: desktop links/actions are present and functional. `EXPERIENCE`, `THE BOOTH`, `PRINTS`, `FIND A BOOTH`, `RENT FOTOHVN`, and `ASK ABOUT YOUR DATE` reached the intended anchors with the sticky-header offset preserved. Skip navigation and semantic landmarks remain present.
- Bento: default composition and `scale(1.05)` hover passed without layout shift or clipping.
- Accordion: pointer, ArrowLeft/Right/Up/Down, Home/End architecture, repaired Enter/Space activation, visible focus, native buttons, `aria-expanded`, `aria-controls`, and panel `hidden` state passed.
- Marquee: manual pause changed the label to `PLAY MOTION`, `aria-pressed` to `true`, and both rows to `animation-play-state: paused`. With manual pause off, keyboard focus on the control still kept both rows paused, confirming focus-pause behavior. Source retains the static screen-reader sentence and reduced-motion branch.
- Pinned gallery: the chapter heading became `position: fixed` in the reading zone; media progressed from `.8/.2` toward `1/1`, then exited at opacity `.2` with overlay opacity `.32`. No horizontal translation was observed.
- Card stack: cards entered from approximately `.94` scale/18% Y, settled toward 1/0, retained increasing z-index, and used sticky tops `96px`, `168px`, and `240px`, preserving 72px header reveals. No rotation or fake shadow appeared.
- Carousel: pointer advanced Note 1→2; ArrowRight advanced Note 2→3; the live region remained `aria-live="polite"`; Note 3 remained stable after waiting, confirming no autoplay. Native Previous/Next controls and visible focus passed.
- Inquiry: empty activation stayed at `#inquiry`, focused the first invalid intent control, and left Intent/Name/Email invalid without opening mail. Synthetic non-submitted data produced a valid form. The final action remains `START THE CONVERSATION`.
- Focus rings, native controls, labels, alt text, semantic headings, 44px targets, and contrast passed desktop inspection. Reduced-motion behavior is source-backed by `prefers-reduced-motion` CSS and the GSAP `no-preference` matchMedia gate; the in-app Browser does not expose OS preference emulation.

## Browser console and runtime

- Fresh normal-load `1440 × 1000` tab: `[]` for warnings/errors after 2.5 seconds.
- The same fresh 1440 tab after anchor navigation and repaired accordion keyboard interaction: `[]`.
- Fresh `1280 × 800` tab after the responsive and interaction sweep: `[]`.
- One heavily exercised earlier 1440 tab logged a single Next development-only LCP advisory for `experience-enclosed.png` after rapid anchor navigation before the initial LCP settled. It did not reproduce in either clean normal-load session or the post-interaction clean session and is classified as a QA sequencing artifact, not an implementation finding. The final console verdict is based on the clean fresh sessions above.
- No hydration warning or browser runtime error was observed. Fonts loaded and all inspected assets decoded.

## Evidence index

- Source/hero: `postrepair1-responsive-desktop-source-hero-1440x1000.png`, `postrepair1-responsive-desktop-hero-1440x1000.png`, `postrepair1-responsive-desktop-hero-1280x800.png`.
- Full page: `postrepair1-responsive-desktop-full-1440x1000.png`, `postrepair1-responsive-desktop-full-1280x800.png`.
- Navigation/bento: `postrepair1-responsive-desktop-nav-scrolled-1440x1000.png`, `postrepair1-responsive-desktop-bento-1440x1000.png`, `postrepair1-responsive-desktop-bento-hover-1440x1000.png`, `postrepair1-responsive-desktop-bento-1280x800.png`.
- Accordion: the five closure images listed above plus `postrepair1-responsive-desktop-accordion-repair-comparison.png`.
- Marquee/GSAP: `postrepair1-responsive-desktop-marquee-paused-1440x1000.png`, `postrepair1-responsive-desktop-gsap-gallery-reading-1440x1000.png`, `postrepair1-responsive-desktop-gsap-gallery-exit-1440x1000.png`, `postrepair1-responsive-desktop-card-stack-entry-1440x1000.png`, `postrepair1-responsive-desktop-card-stack-overlap-1440x1000.png`, `postrepair1-responsive-desktop-card-stack-final-1440x1000.png`, `postrepair1-responsive-desktop-gsap-gallery-1280x800.png`, and `postrepair1-responsive-desktop-card-stack-1280x800.png`.
- Carousel/action/form: `postrepair1-responsive-desktop-carousel-note2-1440x1000.png`, `postrepair1-responsive-desktop-carousel-note3-keyboard-1440x1000.png`, `postrepair1-responsive-desktop-carousel-note2-1280x800.png`, `postrepair1-responsive-desktop-action-paths-1440x1000.png`, `postrepair1-responsive-desktop-action-1280x800.png`, `postrepair1-responsive-desktop-form-required-1440x1000.png`, and `postrepair1-responsive-desktop-form-filled-1440x1000.png`.
- Combined comparison inputs: `postrepair1-responsive-desktop-full-comparison-board.png`, `postrepair1-responsive-desktop-focused-comparison.png`, and `postrepair1-responsive-desktop-accordion-repair-comparison.png`.

## Comparison history

| Iteration | Finding | Repair | Fresh post-repair evidence | Status |
|---|---|---|---|---|
| Previous desktop gate | P2: Arrow focus worked, but Enter and Space did not activate the focused accordion slice. | Explicit Enter/Space/Spacebar selection handling in `handleAccordionKeyDown`. | Both widths now show correct focus, `aria-expanded`, controlled-panel `hidden`, and visible-state transitions for Enter and Space. | closed |
| Previous tablet gate/user screenshot | P1: lead bento text clipped at tablet width. | Bounded tablet bento row/content repair. | Cross-viewport desktop regression check shows complete lead-card copy, matching card scroll/client widths, dense arithmetic, and no clipping at 1280/1440. Tablet-specific verdict remains owned by the tablet reviewer. | no desktop regression |
| Earlier hero/header/LCP/stack/responsive repairs | Historical issues already repaired. | Recorded in prior repair handoffs. | Fresh hero, nav, complete-page, GSAP, asset, overflow, and clean-console evidence above. | closed/no regression |

## Implementation checklist

- No desktop repair task is required.
- Preserve the repaired `handleAccordionKeyDown` activation branch and native button/ARIA structure.
- Preserve the current desktop grid, typography, motion boundaries, local assets, truthful copy, and overflow behavior.
- Continue to the independent responsive synthesis/final verification gates.

final result: passed
