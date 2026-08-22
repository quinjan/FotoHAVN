# Responsive design QA — mobile pass 3

Date: 2026-08-22  
QA scope: 320x720, 375x812, and 390x844 requested mobile viewports  
Implementation: `http://localhost:3011/`  
Production files modified: none  
`website/design-qa.md` modified: no  
result: blocked

## Independence disclosure

Thread-limit fallback assigned this pass to the agent that previously implemented revision 02. That earlier role changed only the responsive hero font-size seam in `website/src/components/UpperExperience.module.css`. This QA agent did not author the later repair-04 changes to the mobile stack-card headings, third stack image loading, or header treatment. All evidence below came from a new in-app Browser session and newly created pass-3 captures; implementation-02 measurements and earlier screenshots were not accepted as QA evidence.

## Comparison truth and normalization

- Source design truth: `website/.handoffs/gpt-taste-design-plan.md`, version 1.1, with `website/DESIGN.md` applying only where the plan does not override it.
- Source bitmap pixels, CSS size, and density: `n/a`. The source authority is a written design contract, not a fixed-pixel bitmap, so no pixel-perfect bitmap or same-frame density-normalization claim is made.
- Rendered implementation: fresh in-app Browser captures from the shared local implementation.
- Browser-rendered capture pixels:
  - requested 320x720: focused captures `305x686`; full page `304x12286`; browser DPR `1.25`;
  - requested 375x812: focused capture `360x779`; full page `360x12844`;
  - requested 390x844: focused capture `375x811`; full page `375x12952`.
- The in-app Browser reserves chrome and, where applicable, a scrollbar from the requested outer viewport. Findings use live CSS geometry plus the exact saved capture pixels rather than pretending the outer viewport and screenshot bitmap are identical.
- Full-view comparisons used the three fresh `*-full.png` captures. Focused comparisons were required because important typography and interaction details are too small in a 12,000px-tall stitch; the hero, menu, accordion, marquee, static gallery, stack header, third stack card, carousel, and form were therefore captured separately.

## Findings

- [P2] The stack chapter heading breaks the required 24px mobile gutter.
  - Fidelity surfaces: typography, spacing/layout rhythm, responsiveness.
  - Location: `STEP INSIDE. BE YOURSELVES. KEEP THE PHOTOGRAPH.` in the stack section header; selector `.stackHeader h2` in `website/src/components/MiddleExperience.module.css`.
  - Design evidence: plan v1.1 requires 24px mobile outer gutters and a tested small-phone section-display floor that fits 320px without clipping or damaged hierarchy.
  - Implementation evidence: at requested 320px/client width 305, the text range for the final `PHOTOGRAPH.` line ends at x=300.49 while the 24px content boundary ends at x=280.80. It exceeds the content measure by 19.69px and leaves only 4.51px to the viewport edge. At 375 and 390, the same line reaches x=354.52 and x=367.93; against the saved capture widths of 360 and 375, that leaves only 5.48px and 7.07px. `responsive-qa-pass3-mobile-320x720-stack-header.png` makes the narrowed final-line margin visible.
  - Impact: a major chapter heading abandons the otherwise consistent mobile grid at every required phone width and becomes especially cramped at 320px. Document-level overflow equality masks the descendant drift, so the page can appear overflow-clean while the typography still violates the design contract.
  - Fix: add a bounded max-767 rule for `.stackHeader h2`, keeping `max-width: 100%` and reducing only this heading's mobile scale enough for the longest word to remain within the 24px content boundary. A starting candidate is `font-size: clamp(2.4rem, 12vw, 3.5rem)`, followed by fresh text-range verification at 320/375/390. Do not change the copy, outer gutter, card-heading repair, transforms, or overflow clipping.

No P0 or P1 was found. No additional actionable P2 was found.

## Repair-04 regression verification

- Mobile card headings: passed. `STEP INSIDE`, `BE YOURSELVES`, and `KEEP THE PHOTOGRAPH` remain inside each card's copy measure at all three widths. Fonts were 31.2px, 36.582px, and 38.064px respectively; each card reported zero horizontal overflow.
- Third stack image: passed. The `KEEP THE PHOTOGRAPH` image had a non-empty optimizer URL, `complete=true`, and decoded natural sizes of 224x280 at 320, 279x349 at 375, and 294x368 at 390. The fresh focused 320 capture shows visible decoded pixels.
- Mobile header: passed. The intentional transparent mobile header remains visible in the three hero captures; FOTOHVN and MENU both retain 44px-high targets, and the header remains exactly 72px high.
- Tablet-only stack heading/image overlap: not reproduced on mobile. At 320/375/390, all stack cards use `position: static` and `transform: none`; copy and media remain in separate vertical regions with zero card overflow. Temporary passage beneath the sticky 72px header during natural scrolling is ordinary scroll occlusion, not an intrinsic heading/image overlap.

## Required fidelity surfaces

- Fonts and typography: Cabinet Grotesk and Cormorant Garamond reported loaded. The hero is 35.2px and three lines at all three widths, as plan v1.1 approves. Card-heading wrapping passes. Blocked only by the stack chapter H2 gutter overhang above.
- Spacing and layout rhythm: 24px hero/card gutters, 16px mobile grid gaps, 48px hero actions, section rhythm, and vertical stack-card flow otherwise pass. The H2 overhang is the only actionable spacing drift.
- Colors and visual tokens: warm Off-white/Ivory/Paper surfaces, Ebony type/actions, restrained brass rules, directional hero veil, and transparent mobile-header treatment remain coherent and legible in the focused captures.
- Image quality and asset fidelity: local purpose-made booth, guest, detail, and print imagery renders sharply with stable crops and meaningful alt text. No blank final stack image, placeholder, fake CSS art, inline-SVG substitute, emoji, or unrelated stock asset was observed.
- Copy and content: fixed hero, section, carousel, action, form, and footer copy remains coherent and truthful. No copy was missing or substituted; the finding concerns only the H2's rendered measure.
- Icons and surfaces: restrained borders, square/editorial slabs, media frames, carousel arrows, and control treatments remain consistent. The development-only Next.js tool is excluded from production fidelity and target accounting.
- Accessibility: visible keyboard focus is present; MENU/CLOSE returns focus on Escape; native labels, ARIA states, alt text, live carousel copy, and form validation work. All visible application links/buttons measured at least 44px; radio labels provide the practical target. No contrast regression was observed on mobile.
- Responsiveness: document `scrollWidth === clientWidth` at 320/375/390. Mobile gallery and stack fallbacks are static and fully opaque. The descendant H2 measurement is the only responsive blocker.
- Interaction states: passed except for the unrelated visual finding. Menu, focus return, anchors, accordion, marquee, carousel, and form evidence is detailed below.
- AI-shortcut artifacts: passed.

## Interaction, overflow, console, and reload evidence

- Menu/focus: MENU opened the full-width mobile panel; CLOSE exposed `aria-expanded=true`; the three navigation rows were 60px high and both intent actions were 48px. Escape closed the panel, restored `aria-expanded=false`, and returned focus to MENU. The focused control had a visible outline.
- Primary anchors: the two hero actions retained `href="#find-a-booth"` and `href="#rent-fotohavn"`; activation scrolled the correct target to approximately 87.7px below the sticky header.
- Accordion: selecting PRINTED produced one approximately 420px expanded item, two approximately 184px collapsed items, correct `aria-expanded`/hidden-panel state, and visible copy. ArrowLeft moved focus from PRINTED to TOGETHER.
- Marquee: PAUSE changed the control to PLAY, set `aria-pressed=true`, and paused both tracks. Re-enabling motion set `aria-pressed=false`; after focus moved to the carousel, both tracks reported running. The truthful eight-term set remained intact.
- Static GSAP fallback: all four gallery figures reported `position: static`, `transform: none`, and opacity 1. All three stack cards reported `position: static` and `transform: none` at 320/375/390.
- Carousel: manual NEXT moved note 1 to note 2; ArrowRight moved to note 3; the `aria-live` sentence changed accordingly. It stayed on note 3 after 1.2 seconds, confirming no autoplay.
- Form: empty START THE CONVERSATION remained locally blocked by native validation, focused the required Mall booth radio, and exposed `Please select one of these options.` No mail client or external destination was opened.
- Targets: fresh sweeps at all three widths found no visible application link/button below 44x44; hero actions remained 48px high.
- Overflow: document equality passed at all three widths. Card-heading text ranges and every stack card passed descendant bounds. Intentional marquee tracks were contained by their clipping section. The stack chapter H2 is the sole uncontained design-measure drift.
- Console/hydration/reload: the fresh 320 run had zero warning/error entries after menu, anchors, accordion, marquee, gallery, stack, carousel, and form interaction. A top-position reload remained at scrollY 0 with zero warning/error entries and no hydration warning. Fresh 375 and 390 loads were also clean.

## Evidence inventory

- `website/design-qa/responsive-qa-pass3-mobile-320x720-hero.png`
- `website/design-qa/responsive-qa-pass3-mobile-320x720-full.png`
- `website/design-qa/responsive-qa-pass3-mobile-320x720-menu-open.png`
- `website/design-qa/responsive-qa-pass3-mobile-320x720-accordion-printed.png`
- `website/design-qa/responsive-qa-pass3-mobile-320x720-marquee-paused.png`
- `website/design-qa/responsive-qa-pass3-mobile-320x720-gallery-static.png`
- `website/design-qa/responsive-qa-pass3-mobile-320x720-stack-header.png`
- `website/design-qa/responsive-qa-pass3-mobile-320x720-stack-third.png`
- `website/design-qa/responsive-qa-pass3-mobile-320x720-carousel-next.png`
- `website/design-qa/responsive-qa-pass3-mobile-320x720-form-required.png`
- `website/design-qa/responsive-qa-pass3-mobile-375x812-hero.png`
- `website/design-qa/responsive-qa-pass3-mobile-375x812-full.png`
- `website/design-qa/responsive-qa-pass3-mobile-390x844-hero.png`
- `website/design-qa/responsive-qa-pass3-mobile-390x844-full.png`

## Comparison history

| Iteration | Finding | Fix/post-fix evidence | Result |
|---|---|---|---|
| Responsive QA pass 2 | Mobile stack-card H3 text clipped at 320/375/390. | Repair 04 reduced only `.stackCopy h3`; fresh pass-3 text ranges now remain inside every card at all three widths. | closed |
| Responsive QA pass 2 | Third stack image could remain blank at 820 tablet. | Repair 04 made only the final stack image eager; fresh mobile evidence shows it decoded and visible without mobile overlap. | mobile regression closed |
| Responsive QA pass 3 | Stack chapter H2 breaks the 24px mobile gutter. | No fix was authorized in this read-only pass. | blocked |

## Implementation checklist

1. Repair only the max-767 `.stackHeader h2` scale/measure in `MiddleExperience.module.css`.
2. Recheck every line's text range against the 24px content boundary at 320, 375, and 390.
3. Preserve the passed `.stackCopy h3` repair, third-image loading, static mobile stack/gallery behavior, and all existing copy.
4. Run a fresh gpt-taste conformance verification and then fresh responsive viewport QA after the repair.

## Follow-up polish and residual limits

No separate P3 polish finding is filed. Source pixels remain `n/a` because the design authority is a written contract. Direct reduced-motion preference emulation and external mail-app launch were not available/appropriate in this read-only mobile pass; the mobile breakpoint's static GSAP behavior and native invalid-form path were directly verified instead.

final result: blocked
